using System.ComponentModel;
using System.Text;
using Spectre.Console.Cli;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;
using TokenSqueeze.Security;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Commands;

internal sealed class ExtractCommand(LanguageRegistry registry) : Command<ExtractCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[id]")]
        [Description("The symbol ID to extract")]
        public string? Id { get; init; }

        [CommandOption("--batch <IDS>")]
        [Description("Extract multiple symbols by ID")]
        public string[]? Batch { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        try
        {
        var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), ".cache");
        if (!Directory.Exists(cacheDir))
        {
            JsonOutput.WriteError("No index found. Run /token-squeeze:index");
            return 1;
        }

        var store = new IndexStore(cacheDir);

        var manifest = QueryReindexer.EnsureFresh(store, registry, cancellation);
        if (manifest is null)
        {
            JsonOutput.WriteError("No index found. Run /token-squeeze:index");
            return 1;
        }

        // Collect target IDs
        var targetIds = new List<string>();
        if (settings.Batch is { Length: > 0 })
        {
            targetIds.AddRange(settings.Batch);
        }
        else if (!string.IsNullOrWhiteSpace(settings.Id))
        {
            targetIds.Add(settings.Id);
        }
        else
        {
            JsonOutput.WriteError("No symbol ID provided. Use <id> argument or --batch option.");
            return 1;
        }

        bool isBatch = settings.Batch is { Length: > 0 };

        // Group target IDs by file path using centralized ParseId
        var idsByFile = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var id in targetIds)
        {
            var parsed = Symbol.ParseId(id);
            var filePath = parsed?.FilePath ?? "";

            if (!idsByFile.TryGetValue(filePath, out var ids))
            {
                ids = [];
                idsByFile[filePath] = ids;
            }
            ids.Add(id);
        }

        // Load symbols per file and build lookup dictionary
        var symbolById = new Dictionary<string, Symbol>(StringComparer.Ordinal);
        foreach (var filePath in idsByFile.Keys)
        {
            var fileSymbols = store.LoadFileSymbols(filePath, manifest);
            if (fileSymbols is not null)
            {
                foreach (var sym in fileSymbols)
                    symbolById[sym.Id] = sym;
            }
        }

        // Cache file bytes to avoid re-reading the same file
        var fileBytesCache = new Dictionary<string, byte[]>();

        var results = new List<object>();

        foreach (var id in targetIds)
        {
            symbolById.TryGetValue(id, out var symbol);

            if (symbol is null)
            {
                results.Add(new { id, error = "Symbol not found" });
                continue;
            }

            var sourceFilePath = Path.Combine(manifest.SourcePath, symbol.File);

            try
            {
                PathValidator.ValidateWithinRoot(sourceFilePath, manifest.SourcePath);
            }
            catch (System.Security.SecurityException)
            {
                results.Add(new { id, error = "Source file path escapes project root" });
                continue;
            }

            if (!File.Exists(sourceFilePath))
            {
                results.Add(new
                {
                    id,
                    name = symbol.Name,
                    kind = symbol.Kind.ToString(),
                    file = symbol.File,
                    error = "Source file not found"
                });
                continue;
            }

            // Read file bytes (cached per file)
            if (!fileBytesCache.TryGetValue(symbol.File, out var cachedBytes))
            {
                cachedBytes = File.ReadAllBytes(sourceFilePath);
                fileBytesCache[symbol.File] = cachedBytes;
            }

            // Byte-offset extraction
            string source;
            try
            {
                if (symbol.ByteOffset + symbol.ByteLength > cachedBytes.Length)
                {
                    results.Add(new
                    {
                        id,
                        name = symbol.Name,
                        kind = symbol.Kind.ToString(),
                        file = symbol.File,
                        error = "Byte offset out of range (file may have changed)"
                    });
                    continue;
                }

                source = Encoding.UTF8.GetString(cachedBytes, symbol.ByteOffset, symbol.ByteLength);
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    id,
                    name = symbol.Name,
                    kind = symbol.Kind.ToString(),
                    file = symbol.File,
                    error = $"Extraction failed: {ex.Message}"
                });
                continue;
            }

            results.Add(new
            {
                id,
                name = symbol.Name,
                kind = symbol.Kind.ToString(),
                signature = symbol.Signature,
                source,
                file = symbol.File,
                line = symbol.Line,
                endLine = symbol.EndLine
            });
        }

        if (isBatch)
        {
            JsonOutput.Write(new { results });
        }
        else
        {
            JsonOutput.Write(results[0]);
        }

        return 0;
        }
        catch (Exception ex)
        {
            JsonOutput.WriteError(ex.Message);
            return 1;
        }
    }
}
