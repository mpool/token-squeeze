using TokenSqueeze.Parser;
using TokenSqueeze.Security;

namespace TokenSqueeze.Indexing;

internal sealed class DirectoryWalker
{
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", "bin", "obj", ".vs", ".idea",
        "__pycache__", ".mypy_cache", ".pytest_cache",
        "dist", "build", ".next", ".nuxt"
    };

    private readonly LanguageRegistry _registry;
    private readonly string _rootPath;
    private readonly Ignore.Ignore? _gitignore;

    public DirectoryWalker(LanguageRegistry registry, string rootPath)
    {
        _registry = registry;
        _rootPath = Path.GetFullPath(rootPath);

        var gitignorePath = Path.Combine(_rootPath, ".gitignore");
        if (File.Exists(gitignorePath))
        {
            _gitignore = new Ignore.Ignore();
            foreach (var line in File.ReadAllLines(gitignorePath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                    _gitignore.Add(trimmed);
            }
        }
    }

    public IEnumerable<string> Walk()
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System
        };

        foreach (var file in Directory.EnumerateFiles(_rootPath, "*", options))
        {
            var relativePath = Path.GetRelativePath(_rootPath, file)
                .Replace('\\', '/');

            // 1. Skip built-in directories
            if (HasSkippedSegment(relativePath))
                continue;

            // 2. .gitignore check
            if (_gitignore != null && _gitignore.IsIgnored(relativePath))
                continue;

            // 3. Secret file check
            if (SecretDetector.IsSecretFile(file))
                continue;

            // 4. Extension check (supported language?)
            var ext = Path.GetExtension(file);
            if (string.IsNullOrEmpty(ext) || _registry.GetSpecForExtension(ext) is null)
                continue;

            // 5. Binary file check
            if (IsBinaryFile(file))
                continue;

            // 6. Symlink escape check
            if (PathValidator.IsSymlinkEscape(file, _rootPath))
                continue;

            yield return file;
        }
    }

    private static bool HasSkippedSegment(string relativePath)
    {
        var segments = relativePath.Split('/');
        for (int i = 0; i < segments.Length - 1; i++) // exclude filename
        {
            if (SkippedDirectories.Contains(segments[i]))
                return true;
        }
        return false;
    }

    internal static bool IsBinaryFile(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> buffer = stackalloc byte[8192];
            var bytesRead = stream.Read(buffer);
            if (bytesRead == 0)
                return false;

            return buffer[..bytesRead].Contains((byte)0);
        }
        catch
        {
            return true; // Fail safe: treat unreadable as binary
        }
    }
}
