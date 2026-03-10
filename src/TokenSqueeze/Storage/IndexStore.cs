using System.Text.Json;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Models;
using TokenSqueeze.Security;

namespace TokenSqueeze.Storage;

internal sealed class IndexStore
{
    private readonly string _cacheDir;

    public IndexStore(string cacheDir)
    {
        _cacheDir = Path.GetFullPath(cacheDir);
    }

    public string CacheDir => _cacheDir;

    public void Save(CodeIndex index)
    {
        Directory.CreateDirectory(_cacheDir);
        WriteCacheMarkers();

        var filesDir = StoragePaths.GetFilesDir(_cacheDir);
        Directory.CreateDirectory(filesDir);

        // Group symbols by file
        var symbolsByFile = index.Symbols
            .GroupBy(s => s.File)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Track used storage keys to handle collisions
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);
        var manifestEntries = new Dictionary<string, ManifestFileEntry>();

        // Write per-file fragments
        foreach (var (filePath, fileInfo) in index.Files)
        {
            var baseKey = StoragePaths.PathToStorageKey(filePath);
            var storageKey = baseKey;
            var suffix = 2;
            while (!usedKeys.Add(storageKey))
            {
                storageKey = $"{baseKey}-{suffix}";
                suffix++;
            }

            var symbols = symbolsByFile.GetValueOrDefault(filePath, []);
            var fragment = new FileSymbolData
            {
                File = filePath,
                Symbols = symbols
            };

            var fragmentPath = StoragePaths.GetFileFragmentPath(_cacheDir, storageKey);
            PathValidator.ValidateWithinRoot(fragmentPath, _cacheDir);
            AtomicWrite(fragmentPath, fragment);

            manifestEntries[filePath] = new ManifestFileEntry
            {
                Path = fileInfo.Path,
                Hash = fileInfo.Hash,
                Language = fileInfo.Language,
                SymbolCount = fileInfo.SymbolCount,
                StorageKey = storageKey,
                LastModifiedUtc = fileInfo.LastModifiedUtc
            };
        }

        // Write search-index.json (lightweight symbols without ByteOffset/ByteLength/ContentHash)
        var searchSymbols = ProjectSearchSymbols(index.Symbols);
        var searchIndexPath = StoragePaths.GetSearchIndexPath(_cacheDir);
        AtomicWrite(searchIndexPath, searchSymbols);

        // Write manifest LAST (crash safety)
        var manifest = new Manifest
        {
            FormatVersion = 3,
            SourcePath = index.SourcePath,
            IndexedAt = index.IndexedAt,
            Files = manifestEntries
        };

        var manifestPath = StoragePaths.GetManifestPath(_cacheDir);
        AtomicWrite(manifestPath, manifest);

        // Cleanup orphaned fragments
        if (Directory.Exists(filesDir))
        {
            var validKeys = manifestEntries.Values.Select(e => e.StorageKey).ToHashSet(StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(filesDir, "*.json"))
            {
                var key = Path.GetFileNameWithoutExtension(file);
                if (!validKeys.Contains(key))
                    File.Delete(file);
            }
        }
    }

    public Manifest? LoadManifest()
    {
        var manifestPath = StoragePaths.GetManifestPath(_cacheDir);
        if (!File.Exists(manifestPath))
            return null;

        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<Manifest>(json, JsonDefaults.Options);
    }

    public List<Symbol>? LoadFileSymbols(string filePath)
    {
        var manifest = LoadManifest();
        if (manifest is null)
            return null;

        return LoadFileSymbols(filePath, manifest);
    }

    public List<Symbol>? LoadFileSymbols(string filePath, Manifest manifest)
    {
        if (!manifest.Files.TryGetValue(filePath, out var entry))
            return null;

        var fragmentPath = StoragePaths.GetFileFragmentPath(_cacheDir, entry.StorageKey);
        PathValidator.ValidateWithinRoot(fragmentPath, _cacheDir);
        if (!File.Exists(fragmentPath))
            return null;

        var json = File.ReadAllText(fragmentPath);
        var fragment = JsonSerializer.Deserialize<FileSymbolData>(json, JsonDefaults.Options);
        return fragment?.Symbols;
    }

    public List<SearchSymbol>? LoadAllSymbols()
    {
        var searchIndexPath = StoragePaths.GetSearchIndexPath(_cacheDir);
        if (!File.Exists(searchIndexPath))
            return null;

        var json = File.ReadAllText(searchIndexPath);
        return JsonSerializer.Deserialize<List<SearchSymbol>>(json, JsonDefaults.Options);
    }

    public CodeIndex? Load()
    {
        var manifest = LoadManifest();
        if (manifest is null)
            return null;

        var allSymbols = new List<Symbol>();
        var allFiles = new Dictionary<string, IndexedFile>();

        foreach (var (filePath, entry) in manifest.Files)
        {
            allFiles[filePath] = new IndexedFile
            {
                Path = entry.Path,
                Hash = entry.Hash,
                Language = entry.Language,
                SymbolCount = entry.SymbolCount,
                LastModifiedUtc = entry.LastModifiedUtc
            };

            var fragmentPath = StoragePaths.GetFileFragmentPath(_cacheDir, entry.StorageKey);
            PathValidator.ValidateWithinRoot(fragmentPath, _cacheDir);
            if (File.Exists(fragmentPath))
            {
                var fragJson = File.ReadAllText(fragmentPath);
                var fragment = JsonSerializer.Deserialize<FileSymbolData>(fragJson, JsonDefaults.Options);
                if (fragment?.Symbols is not null)
                    allSymbols.AddRange(fragment.Symbols);
            }
        }

        return new CodeIndex
        {
            SourcePath = manifest.SourcePath,
            IndexedAt = manifest.IndexedAt,
            Files = allFiles,
            Symbols = allSymbols
        };
    }

    public void DeleteFileFragment(string storageKey)
    {
        var fragmentPath = StoragePaths.GetFileFragmentPath(_cacheDir, storageKey);
        PathValidator.ValidateWithinRoot(fragmentPath, _cacheDir);

        if (File.Exists(fragmentPath))
            File.Delete(fragmentPath);
    }

    public void SaveFileFragment(string storageKey, FileSymbolData fragment)
    {
        var fragmentPath = StoragePaths.GetFileFragmentPath(_cacheDir, storageKey);
        PathValidator.ValidateWithinRoot(fragmentPath, _cacheDir);

        AtomicWrite(fragmentPath, fragment);
    }

    public void SaveManifest(Manifest manifest)
    {
        var manifestPath = StoragePaths.GetManifestPath(_cacheDir);
        AtomicWrite(manifestPath, manifest);
    }

    public void RebuildSearchIndex(Manifest manifest)
    {
        var allSymbols = new List<Symbol>();

        foreach (var (_, entry) in manifest.Files)
        {
            var fragmentPath = StoragePaths.GetFileFragmentPath(_cacheDir, entry.StorageKey);
            PathValidator.ValidateWithinRoot(fragmentPath, _cacheDir);
            if (!File.Exists(fragmentPath))
                continue;

            var json = File.ReadAllText(fragmentPath);
            var fragment = JsonSerializer.Deserialize<FileSymbolData>(json, JsonDefaults.Options);
            if (fragment?.Symbols is not null)
                allSymbols.AddRange(fragment.Symbols);
        }

        var searchSymbols = ProjectSearchSymbols(allSymbols);
        var searchIndexPath = StoragePaths.GetSearchIndexPath(_cacheDir);
        AtomicWrite(searchIndexPath, searchSymbols);

        // Write updated manifest
        var manifestPath = StoragePaths.GetManifestPath(_cacheDir);
        AtomicWrite(manifestPath, manifest);
    }

    /// <summary>
    /// Incrementally update the search index: remove symbols for affected files,
    /// then append fresh symbols from their fragments. Falls back to full rebuild
    /// if the existing search index is missing.
    /// </summary>
    public void UpdateSearchIndex(Manifest manifest, HashSet<string> affectedFiles)
    {
        var searchIndexPath = StoragePaths.GetSearchIndexPath(_cacheDir);

        // If no existing search index, fall back to full rebuild
        if (!File.Exists(searchIndexPath))
        {
            RebuildSearchIndex(manifest);
            return;
        }

        // Load existing search symbols and remove entries for affected files
        var json = File.ReadAllText(searchIndexPath);
        var existing = JsonSerializer.Deserialize<List<SearchSymbol>>(json, JsonDefaults.Options) ?? [];
        var kept = existing.Where(s => !affectedFiles.Contains(s.File)).ToList();

        // Load fresh symbols from fragments of affected files still in the manifest
        foreach (var file in affectedFiles)
        {
            if (!manifest.Files.TryGetValue(file, out var entry))
                continue; // deleted file -- no fragment to add

            var fragmentPath = StoragePaths.GetFileFragmentPath(_cacheDir, entry.StorageKey);
            PathValidator.ValidateWithinRoot(fragmentPath, _cacheDir);
            if (!File.Exists(fragmentPath))
                continue;

            var fragJson = File.ReadAllText(fragmentPath);
            var fragment = JsonSerializer.Deserialize<FileSymbolData>(fragJson, JsonDefaults.Options);
            if (fragment?.Symbols is not null)
            {
                foreach (var symbol in fragment.Symbols)
                    kept.Add(symbol.ToSearchSymbol());
            }
        }

        AtomicWrite(searchIndexPath, kept.ToArray());

        // Write updated manifest
        var manifestPath = StoragePaths.GetManifestPath(_cacheDir);
        AtomicWrite(manifestPath, manifest);
    }

    private void WriteCacheMarkers()
    {
        var tagPath = Path.Combine(_cacheDir, "CACHEDIR.TAG");
        if (!File.Exists(tagPath))
        {
            File.WriteAllText(tagPath,
                "Signature: 8a477f597d28d172789f06886806bc55\n" +
                "# This file is a cache directory tag created by TokenSqueeze.\n" +
                "# For information see https://bford.info/cachedir/\n");
        }

        var gitignorePath = Path.Combine(_cacheDir, ".gitignore");
        if (!File.Exists(gitignorePath))
        {
            File.WriteAllText(gitignorePath, "*\n");
        }
    }

    private static SearchSymbol[] ProjectSearchSymbols(IEnumerable<Symbol> symbols)
    {
        return symbols.Select(s => s.ToSearchSymbol()).ToArray();
    }

    internal static void AtomicWriteRaw(string targetPath, string json)
    {
        var tempPath = targetPath + $".tmp-{Guid.NewGuid():N}";
        File.WriteAllText(tempPath, json);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(tempPath, targetPath, overwrite: true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                Thread.Sleep(attempt * 10);
            }
            catch
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
                throw;
            }
        }
    }

    internal static void AtomicWrite<T>(string targetPath, T value)
    {
        var tempPath = targetPath + $".tmp-{Guid.NewGuid():N}";
        var json = JsonSerializer.Serialize(value, JsonDefaults.Options);
        File.WriteAllText(tempPath, json);

        // Retry File.Move to handle concurrent write contention (Windows file locking)
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(tempPath, targetPath, overwrite: true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                Thread.Sleep(attempt * 10);
            }
            catch
            {
                // Clean up orphaned temp file on unrecoverable failure
                try { File.Delete(tempPath); } catch { /* best effort */ }
                throw;
            }
        }
    }
}
