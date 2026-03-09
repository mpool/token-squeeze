using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Indexing;

internal sealed record IndexResult(CodeIndex Index, int ErrorCount);

internal sealed class ProjectIndexer
{
    private readonly IndexStore _store;
    private readonly LanguageRegistry _registry;

    public ProjectIndexer(IndexStore store, LanguageRegistry registry)
    {
        _store = store;
        _registry = registry;
    }

    public IndexResult Index(string directoryPath, string? projectName = null)
    {
        var fullPath = Path.GetFullPath(directoryPath);
        var name = ResolveProjectName(fullPath, projectName);

        // Load existing index for incremental comparison
        var existing = _store.Load(name);
        var existingFiles = existing?.Files ?? new Dictionary<string, IndexedFile>();
        var existingSymbolsByFile = existing?.Symbols
            .GroupBy(s => s.File)
            .ToDictionary(g => g.Key, g => g.ToList())
            ?? new Dictionary<string, List<Symbol>>();

        // DirectoryWalker uses _registry for extension filtering (before parallel loop)
        var walker = new DirectoryWalker(_registry, fullPath);

        // Materialize walker output before parallel loop (walker yields lazily)
        var walkedFiles = walker.Walk().ToList();

        // Thread-safe collections for parallel results
        var results = new ConcurrentBag<(string RelativePath, IndexedFile FileInfo, List<Symbol> Symbols)>();

        int filesParsed = 0;
        int filesSkipped = 0;
        int errorCount = 0;

        Parallel.ForEach(
            walkedFiles,
            // localInit: create per-thread LanguageRegistry + SymbolExtractor
            () => {
                var threadRegistry = new LanguageRegistry();
                var threadExtractor = new SymbolExtractor(threadRegistry);
                return (Registry: threadRegistry, Extractor: threadExtractor);
            },
            // body: process each file
            (walkedFile, loopState, threadLocal) =>
            {
                var relativePath = Path.GetRelativePath(fullPath, walkedFile.Path).Replace('\\', '/');
                var hash = ComputeFileHash(walkedFile.Bytes);

                var ext = Path.GetExtension(walkedFile.Path);
                var spec = threadLocal.Registry.GetSpecForExtension(ext)!;

                // Incremental: skip unchanged files (existingFiles is read-only, safe to read concurrently)
                if (existingFiles.TryGetValue(relativePath, out var existingFile)
                    && existingFile.Hash == hash)
                {
                    Interlocked.Increment(ref filesSkipped);
                    var reusedSymbols = existingSymbolsByFile.TryGetValue(relativePath, out var cached)
                        ? cached
                        : [];
                    // Capture fresh mtime even for unchanged files (old index may lack it)
                    var reusedFile = existingFile with { LastModifiedUtc = File.GetLastWriteTimeUtc(walkedFile.Path) };
                    results.Add((relativePath, reusedFile, reusedSymbols));
                    return threadLocal;
                }

                // Parse file
                try
                {
                    Interlocked.Increment(ref filesParsed);
                    var symbols = threadLocal.Extractor.ExtractSymbols(relativePath, walkedFile.Bytes, spec);
                    var fileInfo = new IndexedFile
                    {
                        Path = relativePath,
                        Hash = hash,
                        Language = spec.DisplayName,
                        SymbolCount = symbols.Count,
                        LastModifiedUtc = File.GetLastWriteTimeUtc(walkedFile.Path)
                    };
                    results.Add((relativePath, fileInfo, symbols));
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref errorCount);
                    Console.Error.WriteLine($"Error parsing {relativePath}: {ex.Message}");
                }

                return threadLocal;
            },
            // localFinally: dispose per-thread LanguageRegistry
            threadLocal => threadLocal.Registry.Dispose()
        );

        // Deterministic ordering after parallel collection
        var ordered = results.OrderBy(r => r.RelativePath, StringComparer.Ordinal);

        var allFiles = new Dictionary<string, IndexedFile>();
        var allSymbols = new List<Symbol>();

        foreach (var (relativePath, fileInfo, symbols) in ordered)
        {
            allFiles[relativePath] = fileInfo;
            allSymbols.AddRange(symbols);
        }

        var index = new CodeIndex
        {
            ProjectName = name,
            SourcePath = fullPath,
            IndexedAt = DateTime.UtcNow,
            Files = allFiles,
            Symbols = allSymbols
        };

        _store.Save(index);

        var errorSuffix = errorCount > 0 ? $", {errorCount} errors" : "";
        Console.Error.WriteLine($"Indexed {name}: {walkedFiles.Count} files scanned, {filesParsed} parsed, {filesSkipped} unchanged, {allSymbols.Count} symbols{errorSuffix}");

        return new IndexResult(index, errorCount);
    }

    internal static string SanitizeName(string rawName)
    {
        // Replace any character that isn't alphanumeric, hyphen, underscore, or dot
        var sanitized = Regex.Replace(rawName, @"[^a-zA-Z0-9\-_.]", "-");
        // Collapse repeated hyphens
        sanitized = Regex.Replace(sanitized, @"-{2,}", "-");
        // Trim leading/trailing hyphens and dots (prevents .. traversal and leading-dot hiding)
        sanitized = sanitized.Trim('-', '.');
        // Return "unnamed" if result is empty
        return string.IsNullOrEmpty(sanitized) ? "unnamed" : sanitized;
    }

    private string ResolveProjectName(string fullPath, string? explicitName)
    {
        var baseName = SanitizeName(explicitName ?? Path.GetFileName(fullPath));
        if (string.IsNullOrEmpty(baseName))
            baseName = "unnamed";

        var candidate = baseName;
        var suffix = 2;

        while (true)
        {
            var manifest = _store.LoadManifest(candidate);
            if (manifest is null)
                return candidate;

            // Same source path means same project -- reuse name
            if (string.Equals(manifest.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase))
                return candidate;

            candidate = $"{baseName}-{suffix}";
            suffix++;
        }
    }

    private static string ComputeFileHash(byte[] fileBytes)
    {
        var hashBytes = SHA256.HashData(fileBytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
