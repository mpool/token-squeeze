using TokenSqueeze.Indexing;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;

namespace TokenSqueeze.Storage;

internal static class QueryReindexer
{
    public static Manifest? EnsureFresh(IndexStore store, LanguageRegistry registry, CancellationToken cancellation = default)
    {
        var manifest = store.LoadManifest();
        if (manifest is null)
            return null;

        var result = StalenessChecker.DetectStaleFiles(manifest, registry, cancellation);
        if (result.StaleFiles.Count == 0 && result.DeletedFiles.Count == 0 && result.NewFiles.Count == 0)
        {
            // Persist mtime corrections so we don't re-hash these files on the next query
            if (result.MtimeUpdated)
                store.SaveManifest(manifest);

            return manifest;
        }

        var reindexer = new IncrementalReindexer(store, registry);
        return reindexer.ReindexFiles(manifest, result, cancellation);
    }
}
