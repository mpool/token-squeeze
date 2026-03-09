using TokenSqueeze.Indexing;
using TokenSqueeze.Parser;

namespace TokenSqueeze.Storage;

internal static class LegacyMigration
{
    public static bool TryMigrateIfNeeded(string projectName, IndexStore store, LanguageRegistry registry, out string? error)
    {
        error = null;

        if (!store.IsLegacyFormat(projectName))
            return false;

        var sourcePath = store.GetLegacySourcePath(projectName);
        if (sourcePath is null || !Directory.Exists(sourcePath))
        {
            error = $"Legacy index for '{projectName}' found but source path is missing or inaccessible. Please re-index manually with: token-squeeze index <path>";
            return true;
        }

        Console.Error.WriteLine($"Index format outdated for '{projectName}'. Re-indexing from {sourcePath}...");

        var indexer = new ProjectIndexer(store, registry);
        indexer.Index(sourcePath, projectName);

        error = null;
        return true;
    }
}
