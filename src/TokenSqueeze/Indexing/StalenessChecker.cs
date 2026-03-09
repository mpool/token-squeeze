using System.Security.Cryptography;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;
using TokenSqueeze.Security;

namespace TokenSqueeze.Indexing;

internal sealed record StalenessResult(
    List<string> StaleFiles,
    List<string> DeletedFiles,
    List<string> NewFiles,
    bool MtimeUpdated);

internal static class StalenessChecker
{
    public static StalenessResult DetectStaleFiles(Manifest manifest, LanguageRegistry registry, CancellationToken cancellation = default)
    {
        var staleFiles = new List<string>();
        var deletedFiles = new List<string>();
        var mtimeUpdates = new Dictionary<string, ManifestFileEntry>();

        foreach (var (relativePath, entry) in manifest.Files)
        {
            cancellation.ThrowIfCancellationRequested();
            var fullPath = Path.Combine(manifest.SourcePath, relativePath);

            // BUG-01: Deleted files go to deletedFiles list
            if (!File.Exists(fullPath))
            {
                deletedFiles.Add(relativePath);
                continue;
            }

            var currentMtime = File.GetLastWriteTimeUtc(fullPath);

            // If LastModifiedUtc is null (old index), fall back to hash check
            if (entry.LastModifiedUtc is not null)
            {
                // Mtime match means not stale
                if (currentMtime == entry.LastModifiedUtc.Value)
                    continue;
            }

            // Already have enough stale files -- skip expensive hash but keep scanning for deletions
            if (staleFiles.Count >= IncrementalReindexer.MaxReindexPerQuery)
                continue;

            // Mtime differs (or null) -- check hash
            var fileBytes = File.ReadAllBytes(fullPath);
            var hashBytes = SHA256.HashData(fileBytes);
            var currentHash = Convert.ToHexStringLower(hashBytes);

            if (currentHash == entry.Hash)
            {
                // Content unchanged despite mtime change -- defer mtime update to avoid dictionary mutation during enumeration
                mtimeUpdates[relativePath] = entry with { LastModifiedUtc = currentMtime };
                continue;
            }

            // Hash differs -- file is stale
            staleFiles.Add(relativePath);
        }

        // Apply deferred mtime updates now that enumeration is complete
        foreach (var (k, v) in mtimeUpdates)
            manifest.Files[k] = v;

        // Detect new files on disk that aren't in the manifest
        var newFiles = DetectNewFiles(manifest, registry);

        return new StalenessResult(staleFiles, deletedFiles, newFiles, mtimeUpdates.Count > 0);
    }

    private static List<string> DetectNewFiles(Manifest manifest, LanguageRegistry registry)
    {
        var newFiles = new List<string>();
        var sourcePath = manifest.SourcePath;

        if (!Directory.Exists(sourcePath))
            return newFiles;

        EnumerateNewFilesRecursive(sourcePath, sourcePath, manifest, registry, newFiles, new List<GitignoreRule>());

        return newFiles;
    }

    private static void EnumerateNewFilesRecursive(
        string dir,
        string rootPath,
        Manifest manifest,
        LanguageRegistry registry,
        List<string> newFiles,
        List<GitignoreRule> gitignoreStack)
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

        // Enumerate files in this directory
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(dir);
        }
        catch (UnauthorizedAccessException)
        {
            if (addedGitignore)
                gitignoreStack.RemoveAt(gitignoreStack.Count - 1);
            return;
        }

        foreach (var file in files)
        {
            // 1. Gitignore check
            if (IsIgnoredByStack(file, gitignoreStack))
                continue;

            // 2. Extension check (supported language?)
            var ext = Path.GetExtension(file);
            if (string.IsNullOrEmpty(ext) || registry.GetSpecForExtension(ext) is null)
                continue;

            // 3. Secret file check
            if (SecretDetector.IsSecretFile(file))
                continue;

            // 4. File size check
            if (new FileInfo(file).Length > DirectoryWalker.MaxFileSize)
                continue;

            // 5. Symlink escape check
            if (PathValidator.IsSymlinkEscape(file, rootPath))
                continue;

            var relativePath = Path.GetRelativePath(rootPath, file).Replace('\\', '/');

            if (!manifest.Files.ContainsKey(relativePath))
                newFiles.Add(relativePath);
        }

        // Enumerate subdirectories
        IEnumerable<string> subdirs;
        try
        {
            subdirs = Directory.EnumerateDirectories(dir);
        }
        catch (UnauthorizedAccessException)
        {
            if (addedGitignore)
                gitignoreStack.RemoveAt(gitignoreStack.Count - 1);
            return;
        }

        foreach (var subDir in subdirs)
        {
            var dirName = Path.GetFileName(subDir);
            if (DirectoryWalker.SkippedDirectories.Contains(dirName))
                continue;

            // Check if directory is ignored by gitignore stack
            if (IsIgnoredByStack(subDir + Path.DirectorySeparatorChar, gitignoreStack))
                continue;

            EnumerateNewFilesRecursive(subDir, rootPath, manifest, registry, newFiles, gitignoreStack);
        }

        // On exit: remove gitignore if we added one
        if (addedGitignore)
            gitignoreStack.RemoveAt(gitignoreStack.Count - 1);
    }

    private static bool IsIgnoredByStack(string fullPath, List<GitignoreRule> gitignoreStack)
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

    private sealed record GitignoreRule(string BaseDir, Ignore.Ignore Ignore);
}
