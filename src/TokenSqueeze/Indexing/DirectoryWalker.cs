using TokenSqueeze.Parser;
using TokenSqueeze.Security;

namespace TokenSqueeze.Indexing;

internal sealed class DirectoryWalker
{
    public const long MaxFileSize = 1_048_576;

    internal static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", "bin", "obj", ".vs", ".idea",
        "__pycache__", ".mypy_cache", ".pytest_cache",
        "dist", "build", ".next", ".nuxt", ".cache"
    };

    private readonly LanguageRegistry _registry;
    private readonly string _rootPath;
    private readonly long _maxFileSize;

    public DirectoryWalker(LanguageRegistry registry, string rootPath, long maxFileSize = MaxFileSize)
    {
        _registry = registry;
        _rootPath = Path.GetFullPath(rootPath);
        _maxFileSize = maxFileSize;
    }

    public IEnumerable<WalkedFile> Walk()
    {
        return WalkDirectory(_rootPath, new List<GitignoreRule>());
    }

    private IEnumerable<WalkedFile> WalkDirectory(string dir, List<GitignoreRule> gitignoreStack)
    {
        // On entry: check for .gitignore in this directory
        bool addedGitignore = false;
        var gitignorePath = Path.Combine(dir, ".gitignore");
        if (File.Exists(gitignorePath))
        {
            var ignore = new Ignore.Ignore();
            foreach (var line in File.ReadAllLines(gitignorePath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                    ignore.Add(trimmed);
            }
            gitignoreStack.Add(new GitignoreRule(dir, ignore));
            addedGitignore = true;
        }

        // Enumerate files in this directory (non-recursive)
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(dir);
        }
        catch (UnauthorizedAccessException)
        {
            files = [];
        }

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(_rootPath, file)
                .Replace('\\', '/');

            // 1. Gitignore stack check
            if (IsIgnoredByStack(file, gitignoreStack))
                continue;

            // 2. Secret file check
            if (SecretDetector.IsSecretFile(file))
                continue;

            // 3. Extension check (supported language?)
            var ext = Path.GetExtension(file);
            if (string.IsNullOrEmpty(ext) || _registry.GetSpecForExtension(ext) is null)
                continue;

            // 4. File size pre-check
            var fileInfo = new FileInfo(file);
            if (fileInfo.Length > _maxFileSize)
            {
                Console.Error.WriteLine($"Skipping oversized file ({fileInfo.Length:N0} bytes): {relativePath}");
                continue;
            }

            // 5. Symlink escape check (before reading bytes — SEC-03)
            if (PathValidator.IsSymlinkEscape(file, _rootPath))
                continue;

            // 6. Read file and binary content check
            byte[] fileBytes;
            try
            {
                fileBytes = File.ReadAllBytes(file);
            }
            catch
            {
                continue; // Fail safe: skip unreadable files
            }

            if (fileBytes.Length > 0 && IsBinaryContent(fileBytes))
                continue;

            yield return new WalkedFile(file, fileBytes);
        }

        // Enumerate subdirectories
        IEnumerable<string> subdirs;
        try
        {
            subdirs = Directory.EnumerateDirectories(dir);
        }
        catch (UnauthorizedAccessException)
        {
            subdirs = [];
        }

        foreach (var subDir in subdirs)
        {
            var dirName = Path.GetFileName(subDir);

            // Skip built-in directories
            if (SkippedDirectories.Contains(dirName))
                continue;

            // Check if directory is ignored by gitignore stack
            if (IsIgnoredByStack(subDir + Path.DirectorySeparatorChar, gitignoreStack))
                continue;

            foreach (var walkedFile in WalkDirectory(subDir, gitignoreStack))
                yield return walkedFile;
        }

        // On exit: remove gitignore if we added one
        if (addedGitignore)
            gitignoreStack.RemoveAt(gitignoreStack.Count - 1);
    }

    private bool IsIgnoredByStack(string fullPath, List<GitignoreRule> gitignoreStack)
    {
        foreach (var rule in gitignoreStack)
        {
            var relativePath = Path.GetRelativePath(rule.BaseDir, fullPath)
                .Replace('\\', '/');
            if (rule.Ignore.IsIgnored(relativePath))
                return true;
        }
        return false;
    }

    internal static bool IsBinaryContent(ReadOnlySpan<byte> content)
    {
        var checkLength = Math.Min(content.Length, 8192);
        return content[..checkLength].Contains((byte)0);
    }

    private sealed record GitignoreRule(string BaseDir, Ignore.Ignore Ignore);
}

internal sealed record WalkedFile(string Path, byte[] Bytes);
