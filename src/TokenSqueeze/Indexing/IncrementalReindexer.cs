using System.Security.Cryptography;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Indexing;

internal sealed class IncrementalReindexer
{
    public const int MaxReindexPerQuery = 50;

    private readonly IndexStore _store;
    private readonly LanguageRegistry _registry;

    public IncrementalReindexer(IndexStore store, LanguageRegistry registry)
    {
        _store = store;
        _registry = registry;
    }

    public Manifest ReindexFiles(Manifest manifest, StalenessResult result, CancellationToken cancellation = default)
    {
        var staleRelativePaths = result.StaleFiles;

        // Count total work items
        var totalWork = staleRelativePaths.Count + result.DeletedFiles.Count + result.NewFiles.Count;
        if (totalWork > MaxReindexPerQuery)
        {
            Console.Error.WriteLine(
                $"{totalWork} files to process, reindexing first {MaxReindexPerQuery}. Run 'token-squeeze index <path>' for full reindex.");
        }

        // Track all affected files for incremental search index update
        var affectedFiles = new HashSet<string>(StringComparer.Ordinal);

        // Handle deleted files first — cheap operations, share the global budget
        var deletedCount = Math.Min(result.DeletedFiles.Count, MaxReindexPerQuery);

        foreach (var relativePath in result.DeletedFiles.Take(deletedCount))
        {
            cancellation.ThrowIfCancellationRequested();
            affectedFiles.Add(relativePath);
            if (manifest.Files.TryGetValue(relativePath, out var entry))
            {
                _store.DeleteFileFragment(manifest.ProjectName, entry.StorageKey);
                manifest.Files.Remove(relativePath);
            }
        }

        // Stale + new share the remaining budget after deletions
        var remainingAfterDeletes = Math.Max(0, MaxReindexPerQuery - deletedCount);
        var staleCount = Math.Min(staleRelativePaths.Count, remainingAfterDeletes);
        var remainingBudget = remainingAfterDeletes - staleCount;
        var newCount = Math.Min(result.NewFiles.Count, remainingBudget);

        var usedKeys = new HashSet<string>(
            manifest.Files.Values.Select(e => e.StorageKey),
            StringComparer.Ordinal);

        var extractor = new SymbolExtractor(_registry);

        // Handle stale files (existing logic)
        foreach (var relativePath in staleRelativePaths.Take(staleCount))
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                var fullPath = Path.Combine(manifest.SourcePath, relativePath);

                if (!File.Exists(fullPath))
                    continue;

                var fileLength = new FileInfo(fullPath).Length;
                if (fileLength > DirectoryWalker.MaxFileSize)
                {
                    Console.Error.WriteLine($"Skipping oversized file ({fileLength:N0} bytes): {relativePath}");
                    continue;
                }

                var fileBytes = File.ReadAllBytes(fullPath);
                var hashBytes = SHA256.HashData(fileBytes);
                var newHash = Convert.ToHexStringLower(hashBytes);
                var mtime = File.GetLastWriteTimeUtc(fullPath);

                var ext = Path.GetExtension(fullPath);
                var spec = _registry.GetSpecForExtension(ext);
                if (spec is null)
                    continue;

                var symbols = extractor.ExtractSymbols(relativePath, fileBytes, spec);

                var storageKey = manifest.Files[relativePath].StorageKey;
                var fragment = new FileSymbolData
                {
                    File = relativePath,
                    Symbols = symbols
                };

                _store.SaveFileFragment(manifest.ProjectName, storageKey, fragment);
                affectedFiles.Add(relativePath);

                manifest.Files[relativePath] = new ManifestFileEntry
                {
                    Path = relativePath,
                    Hash = newHash,
                    Language = spec.DisplayName,
                    SymbolCount = symbols.Count,
                    StorageKey = storageKey,
                    LastModifiedUtc = mtime
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reindexing {relativePath}: {ex.Message}");
            }
        }

        // Handle new files (BUG-02)
        foreach (var relativePath in result.NewFiles.Take(newCount))
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                var fullPath = Path.Combine(manifest.SourcePath, relativePath);

                if (!File.Exists(fullPath))
                    continue;

                var newFileLength = new FileInfo(fullPath).Length;
                if (newFileLength > DirectoryWalker.MaxFileSize)
                {
                    Console.Error.WriteLine($"Skipping oversized file ({newFileLength:N0} bytes): {relativePath}");
                    continue;
                }

                var fileBytes = File.ReadAllBytes(fullPath);
                var hashBytes = SHA256.HashData(fileBytes);
                var hash = Convert.ToHexStringLower(hashBytes);
                var mtime = File.GetLastWriteTimeUtc(fullPath);

                var ext = Path.GetExtension(fullPath);
                var spec = _registry.GetSpecForExtension(ext);
                if (spec is null)
                    continue;

                var symbols = extractor.ExtractSymbols(relativePath, fileBytes, spec);

                var baseKey = StoragePaths.PathToStorageKey(relativePath);
                var storageKey = baseKey;
                var suffix = 2;
                while (!usedKeys.Add(storageKey))
                {
                    storageKey = $"{baseKey}-{suffix}";
                    suffix++;
                }
                var fragment = new FileSymbolData
                {
                    File = relativePath,
                    Symbols = symbols
                };

                _store.SaveFileFragment(manifest.ProjectName, storageKey, fragment);
                affectedFiles.Add(relativePath);

                manifest.Files[relativePath] = new ManifestFileEntry
                {
                    Path = relativePath,
                    Hash = hash,
                    Language = spec.DisplayName,
                    SymbolCount = symbols.Count,
                    StorageKey = storageKey,
                    LastModifiedUtc = mtime
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error indexing new file {relativePath}: {ex.Message}");
            }
        }

        _store.UpdateSearchIndex(manifest.ProjectName, manifest, affectedFiles);

        return manifest;
    }
}
