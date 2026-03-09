using System.Text.Json;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Models;
using TokenSqueeze.Security;

namespace TokenSqueeze.Storage;

internal sealed class IndexStore
{

    public IndexStore()
    {
        StoragePaths.EnsureRootExists();
    }

    public void Save(CodeIndex index)
    {
        var projectDir = StoragePaths.GetProjectDir(index.ProjectName);
        PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);
        Directory.CreateDirectory(projectDir);

        var filesDir = StoragePaths.GetFilesDir(index.ProjectName);
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

            var fragmentPath = StoragePaths.GetFileFragmentPath(index.ProjectName, storageKey);
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
        var searchIndexPath = StoragePaths.GetSearchIndexPath(index.ProjectName);
        AtomicWrite(searchIndexPath, searchSymbols);

        // Write manifest LAST (crash safety)
        var manifest = new Manifest
        {
            FormatVersion = 2,
            ProjectName = index.ProjectName,
            SourcePath = index.SourcePath,
            IndexedAt = index.IndexedAt,
            Files = manifestEntries
        };

        var manifestPath = StoragePaths.GetManifestPath(index.ProjectName);
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

        // Delete legacy files
        var legacyIndexPath = StoragePaths.GetLegacyIndexPath(index.ProjectName);
        if (File.Exists(legacyIndexPath))
            File.Delete(legacyIndexPath);

        var legacyMetaPath = StoragePaths.GetMetadataPath(index.ProjectName);
        if (File.Exists(legacyMetaPath))
            File.Delete(legacyMetaPath);
    }

    public Manifest? LoadManifest(string projectName)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);

        var manifestPath = StoragePaths.GetManifestPath(projectName);
        if (!File.Exists(manifestPath))
            return null;

        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<Manifest>(json, JsonDefaults.Options);
    }

    public List<Symbol>? LoadFileSymbols(string projectName, string filePath)
    {
        var manifest = LoadManifest(projectName);
        if (manifest is null)
            return null;

        return LoadFileSymbols(projectName, filePath, manifest);
    }

    public List<Symbol>? LoadFileSymbols(string projectName, string filePath, Manifest manifest)
    {
        if (!manifest.Files.TryGetValue(filePath, out var entry))
            return null;

        var fragmentPath = StoragePaths.GetFileFragmentPath(projectName, entry.StorageKey);
        if (!File.Exists(fragmentPath))
            return null;

        var json = File.ReadAllText(fragmentPath);
        var fragment = JsonSerializer.Deserialize<FileSymbolData>(json, JsonDefaults.Options);
        return fragment?.Symbols;
    }

    public List<SearchSymbol>? LoadAllSymbols(string projectName)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);

        var searchIndexPath = StoragePaths.GetSearchIndexPath(projectName);
        if (!File.Exists(searchIndexPath))
            return null;

        var json = File.ReadAllText(searchIndexPath);
        return JsonSerializer.Deserialize<List<SearchSymbol>>(json, JsonDefaults.Options);
    }

    public ProjectMetadata? LoadMetadata(string projectName)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);

        var metaPath = StoragePaths.GetMetadataPath(projectName);
        if (!File.Exists(metaPath))
            return null;

        var json = File.ReadAllText(metaPath);
        return JsonSerializer.Deserialize<ProjectMetadata>(json, JsonDefaults.Options);
    }

    public CodeIndex? Load(string projectName)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);

        // Try new split format first
        var manifest = LoadManifest(projectName);
        if (manifest is not null)
        {
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

                var fragmentPath = StoragePaths.GetFileFragmentPath(projectName, entry.StorageKey);
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
                ProjectName = manifest.ProjectName,
                SourcePath = manifest.SourcePath,
                IndexedAt = manifest.IndexedAt,
                Files = allFiles,
                Symbols = allSymbols
            };
        }

        // Legacy fallback: old monolithic index.json
        var indexPath = StoragePaths.GetLegacyIndexPath(projectName);
        if (!File.Exists(indexPath))
            return null;

        var json = File.ReadAllText(indexPath);
        return JsonSerializer.Deserialize<CodeIndex>(json, JsonDefaults.Options);
    }

    public bool IsLegacyFormat(string projectName)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);

        var manifestPath = StoragePaths.GetManifestPath(projectName);
        var indexPath = StoragePaths.GetLegacyIndexPath(projectName);

        return !File.Exists(manifestPath) && File.Exists(indexPath);
    }

    public string? GetLegacySourcePath(string projectName)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);

        var indexPath = StoragePaths.GetLegacyIndexPath(projectName);
        if (!File.Exists(indexPath))
            return null;

        using var stream = File.OpenRead(indexPath);
        using var doc = JsonDocument.Parse(stream);

        if (doc.RootElement.TryGetProperty("sourcePath", out var prop))
            return prop.GetString();

        return null;
    }

    public List<string> ListProjects()
    {
        if (!Directory.Exists(StoragePaths.RootDir))
            return [];

        return Directory.GetDirectories(StoragePaths.RootDir)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToList();
    }

    public void Delete(string projectName)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);
        if (Directory.Exists(projectDir))
            Directory.Delete(projectDir, recursive: true);
    }

    public void DeleteFileFragment(string projectName, string storageKey)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);

        var fragmentPath = StoragePaths.GetFileFragmentPath(projectName, storageKey);
        PathValidator.ValidateWithinRoot(fragmentPath, StoragePaths.GetFilesDir(projectName));

        if (File.Exists(fragmentPath))
            File.Delete(fragmentPath);
    }

    public void SaveFileFragment(string projectName, string storageKey, FileSymbolData fragment)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);

        var fragmentPath = StoragePaths.GetFileFragmentPath(projectName, storageKey);
        PathValidator.ValidateWithinRoot(fragmentPath, StoragePaths.GetFilesDir(projectName));

        AtomicWrite(fragmentPath, fragment);
    }

    public void SaveManifest(string projectName, Manifest manifest)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);

        var manifestPath = StoragePaths.GetManifestPath(projectName);
        AtomicWrite(manifestPath, manifest);
    }

    public void RebuildSearchIndex(string projectName, Manifest manifest)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);

        var allSymbols = new List<Symbol>();

        foreach (var (_, entry) in manifest.Files)
        {
            var fragmentPath = StoragePaths.GetFileFragmentPath(projectName, entry.StorageKey);
            if (!File.Exists(fragmentPath))
                continue;

            var json = File.ReadAllText(fragmentPath);
            var fragment = JsonSerializer.Deserialize<FileSymbolData>(json, JsonDefaults.Options);
            if (fragment?.Symbols is not null)
                allSymbols.AddRange(fragment.Symbols);
        }

        var searchSymbols = ProjectSearchSymbols(allSymbols);
        var searchIndexPath = StoragePaths.GetSearchIndexPath(projectName);
        AtomicWrite(searchIndexPath, searchSymbols);

        // Write updated manifest
        var manifestPath = StoragePaths.GetManifestPath(projectName);
        AtomicWrite(manifestPath, manifest);
    }

    /// <summary>
    /// Incrementally update the search index: remove symbols for affected files,
    /// then append fresh symbols from their fragments. Falls back to full rebuild
    /// if the existing search index is missing.
    /// </summary>
    public void UpdateSearchIndex(string projectName, Manifest manifest, HashSet<string> affectedFiles)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);

        var searchIndexPath = StoragePaths.GetSearchIndexPath(projectName);

        // If no existing search index, fall back to full rebuild
        if (!File.Exists(searchIndexPath))
        {
            RebuildSearchIndex(projectName, manifest);
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
                continue; // deleted file — no fragment to add

            var fragmentPath = StoragePaths.GetFileFragmentPath(projectName, entry.StorageKey);
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
        var manifestPath = StoragePaths.GetManifestPath(projectName);
        AtomicWrite(manifestPath, manifest);
    }

    private static SearchSymbol[] ProjectSearchSymbols(IEnumerable<Symbol> symbols)
    {
        return symbols.Select(s => s.ToSearchSymbol()).ToArray();
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
