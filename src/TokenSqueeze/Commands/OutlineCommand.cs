using System.ComponentModel;
using Spectre.Console.Cli;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Commands;

internal sealed class OutlineCommand(IndexStore store, LanguageRegistry registry) : Command<OutlineCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The name of the indexed folder")]
        public string Name { get; init; } = string.Empty;

        [CommandArgument(1, "<file>")]
        [Description("The file to show symbols for")]
        public string File { get; init; } = string.Empty;
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        try
        {
        // Legacy migration
        if (LegacyMigration.TryMigrateIfNeeded(settings.Name, store, registry, out var migrationError))
        {
            if (migrationError is not null)
            {
                JsonOutput.WriteError(migrationError);
                return 1;
            }
        }

        var manifest = QueryReindexer.EnsureFresh(settings.Name, store, registry, cancellation);
        if (manifest is null)
        {
            JsonOutput.WriteError($"Project not found: {settings.Name}");
            return 1;
        }

        var requestedFile = settings.File.Replace('\\', '/');

        // Find the matching file key in the manifest
        string? matchedKey = null;
        foreach (var key in manifest.Files.Keys)
        {
            var normalizedKey = key.Replace('\\', '/');
            if (string.Equals(normalizedKey, requestedFile, StringComparison.OrdinalIgnoreCase)
                || normalizedKey.EndsWith("/" + requestedFile, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedKey, requestedFile.TrimStart('/'), StringComparison.OrdinalIgnoreCase))
            {
                matchedKey = key;
                break;
            }
        }

        if (matchedKey is null)
        {
            JsonOutput.WriteError($"File not found in index: {settings.File}");
            return 1;
        }

        var fileSymbols = store.LoadFileSymbols(settings.Name, matchedKey);
        if (fileSymbols is null || fileSymbols.Count == 0)
        {
            JsonOutput.WriteError($"File not found in index: {settings.File}");
            return 1;
        }

        // Build hierarchy: top-level symbols (no parent) as roots, others nested recursively
        var roots = fileSymbols.Where(s => s.Parent is null).ToList();
        var childrenByParent = fileSymbols
            .Where(s => s.Parent is not null)
            .GroupBy(s => s.Parent!)
            .ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<string, object?> BuildNode(Models.Symbol symbol)
        {
            var node = new Dictionary<string, object?>
            {
                ["name"] = symbol.Name,
                ["kind"] = symbol.Kind.ToString(),
                ["signature"] = symbol.Signature,
                ["line"] = symbol.Line,
                ["endLine"] = symbol.EndLine
            };

            if (childrenByParent.TryGetValue(symbol.QualifiedName, out var kids) && kids.Count > 0)
                node["children"] = kids.Select(BuildNode).ToArray();

            return node;
        }

        var symbolNodes = roots.Select(BuildNode).ToArray();

        JsonOutput.Write(new
        {
            file = matchedKey,
            symbols = symbolNodes
        });

        return 0;
        }
        catch (Exception ex)
        {
            JsonOutput.WriteError(ex.Message);
            return 1;
        }
    }
}
