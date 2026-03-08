using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using Spectre.Console.Cli;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Models;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Commands;

internal sealed class ExtractCommand : Command<ExtractCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The name of the indexed folder")]
        public string Name { get; init; } = string.Empty;

        [CommandArgument(1, "[id]")]
        [Description("The symbol ID to extract")]
        public string? Id { get; init; }

        [CommandOption("--batch <IDS>")]
        [Description("Extract multiple symbols by ID")]
        public string[]? Batch { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var store = new IndexStore();
        var index = store.Load(settings.Name);
        if (index is null)
        {
            JsonOutput.WriteError($"Project not found: {settings.Name}");
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

        // Cache file hashes to avoid re-reading the same file
        var fileHashCache = new Dictionary<string, (byte[] bytes, bool stale)>();

        var results = new List<object>();

        foreach (var id in targetIds)
        {
            var symbol = index.Symbols.FirstOrDefault(s =>
                string.Equals(s.Id, id, StringComparison.Ordinal));

            if (symbol is null)
            {
                results.Add(new { id, error = "Symbol not found" });
                continue;
            }

            var sourceFilePath = Path.Combine(index.SourcePath, symbol.File);

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
            if (!fileHashCache.TryGetValue(symbol.File, out var cached))
            {
                var fileBytes = File.ReadAllBytes(sourceFilePath);
                bool isStale = false;

                // Hash validation against indexed file hash
                if (index.Files.TryGetValue(symbol.File, out var indexedFile))
                {
                    var currentHash = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();
                    isStale = !string.Equals(currentHash, indexedFile.Hash, StringComparison.OrdinalIgnoreCase);
                }

                cached = (fileBytes, isStale);
                fileHashCache[symbol.File] = cached;
            }

            // Byte-offset extraction
            string source;
            try
            {
                if (symbol.ByteOffset + symbol.ByteLength > cached.bytes.Length)
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

                source = Encoding.UTF8.GetString(cached.bytes, symbol.ByteOffset, symbol.ByteLength);
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

            if (cached.stale)
            {
                results.Add(new
                {
                    id,
                    name = symbol.Name,
                    kind = symbol.Kind.ToString(),
                    signature = symbol.Signature,
                    source,
                    file = symbol.File,
                    line = symbol.Line,
                    endLine = symbol.EndLine,
                    stale = true,
                    warning = "File has changed since last index"
                });
            }
            else
            {
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
}
