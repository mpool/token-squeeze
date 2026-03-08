using System.Security.Cryptography;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Indexing;

internal sealed class ProjectIndexer
{
    private readonly IndexStore _store;
    private readonly LanguageRegistry _registry;

    public ProjectIndexer(IndexStore store, LanguageRegistry registry)
    {
        _store = store;
        _registry = registry;
    }

    public CodeIndex Index(string directoryPath, string? projectName = null)
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

        var walker = new DirectoryWalker(_registry, fullPath);
        var extractor = new SymbolExtractor(_registry);

        var allSymbols = new List<Symbol>();
        var allFiles = new Dictionary<string, IndexedFile>();

        int filesScanned = 0;
        int filesParsed = 0;
        int filesSkipped = 0;

        foreach (var file in walker.Walk())
        {
            filesScanned++;
            var relativePath = Path.GetRelativePath(fullPath, file).Replace('\\', '/');
            var fileBytes = File.ReadAllBytes(file);
            var hash = ComputeFileHash(fileBytes);

            var ext = Path.GetExtension(file);
            var spec = _registry.GetSpecForExtension(ext)!;

            // Incremental: skip unchanged files
            if (existingFiles.TryGetValue(relativePath, out var existingFile)
                && existingFile.Hash == hash)
            {
                filesSkipped++;
                allFiles[relativePath] = existingFile;
                if (existingSymbolsByFile.TryGetValue(relativePath, out var reusedSymbols))
                    allSymbols.AddRange(reusedSymbols);
                continue;
            }

            // Parse file
            filesParsed++;
            var symbols = extractor.ExtractSymbols(relativePath, fileBytes, spec);
            allSymbols.AddRange(symbols);
            allFiles[relativePath] = new IndexedFile
            {
                Path = relativePath,
                Hash = hash,
                Language = spec.DisplayName,
                SymbolCount = symbols.Count
            };
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

        Console.Error.WriteLine($"Indexed {name}: {filesScanned} files scanned, {filesParsed} parsed, {filesSkipped} unchanged, {allSymbols.Count} symbols");

        return index;
    }

    private string ResolveProjectName(string fullPath, string? explicitName)
    {
        var baseName = explicitName ?? Path.GetFileName(fullPath);
        if (string.IsNullOrEmpty(baseName))
            baseName = "unnamed";

        var candidate = baseName;
        var suffix = 2;

        while (true)
        {
            var existing = _store.Load(candidate);
            if (existing is null)
                return candidate;

            // Same source path means same project -- reuse name
            if (string.Equals(existing.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase))
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
