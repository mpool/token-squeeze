using System.ComponentModel;
using System.Text.RegularExpressions;
using Spectre.Console.Cli;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Models;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Commands;

internal sealed class FindCommand : Command<FindCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The name of the indexed folder")]
        public string Name { get; init; } = string.Empty;

        [CommandArgument(1, "<query>")]
        [Description("The search query")]
        public string Query { get; init; } = string.Empty;

        [CommandOption("--kind|-k <KIND>")]
        [Description("Filter by symbol kind (function, class, method, constant, type)")]
        public string? Kind { get; init; }

        [CommandOption("--path|-p <PATH>")]
        [Description("Glob pattern to scope search to matching files")]
        public string? PathFilter { get; init; }

        [CommandOption("--limit|-l <LIMIT>")]
        [Description("Maximum number of results")]
        [DefaultValue(50)]
        public int Limit { get; init; } = 50;
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

        // Parse kind filter
        SymbolKind? kindFilter = null;
        if (!string.IsNullOrWhiteSpace(settings.Kind))
        {
            if (!Enum.TryParse<SymbolKind>(settings.Kind, ignoreCase: true, out var parsed))
            {
                var validKinds = string.Join(", ", Enum.GetNames<SymbolKind>().Select(n => n.ToLowerInvariant()));
                JsonOutput.WriteError($"Invalid kind: {settings.Kind}. Valid options: {validKinds}");
                return 1;
            }
            kindFilter = parsed;
        }

        // Build path filter regex
        Regex? pathRegex = null;
        if (!string.IsNullOrWhiteSpace(settings.PathFilter))
        {
            pathRegex = GlobToRegex(settings.PathFilter);
        }

        var query = settings.Query;
        var scored = new List<(Symbol symbol, int score)>();

        foreach (var symbol in index.Symbols)
        {
            // Apply kind filter
            if (kindFilter.HasValue && symbol.Kind != kindFilter.Value)
                continue;

            // Apply path filter
            if (pathRegex is not null)
            {
                var normalizedFile = symbol.File.Replace('\\', '/');
                if (!pathRegex.IsMatch(normalizedFile))
                    continue;
            }

            // Score against query
            int score = 0;

            if (string.Equals(symbol.Name, query, StringComparison.OrdinalIgnoreCase))
                score += 200;
            else if (symbol.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                score += 100;

            if (symbol.QualifiedName.Contains(query, StringComparison.OrdinalIgnoreCase))
                score += 75;

            if (symbol.Signature.Contains(query, StringComparison.OrdinalIgnoreCase))
                score += 50;

            if (!string.IsNullOrEmpty(symbol.Docstring) &&
                symbol.Docstring.Contains(query, StringComparison.OrdinalIgnoreCase))
                score += 25;

            if (score > 0)
                scored.Add((symbol, score));
        }

        var results = scored
            .OrderByDescending(x => x.score)
            .Take(settings.Limit)
            .Select(x => new
            {
                id = x.symbol.Id,
                name = x.symbol.Name,
                kind = x.symbol.Kind.ToString(),
                file = x.symbol.File,
                line = x.symbol.Line,
                signature = x.symbol.Signature,
                score = x.score
            })
            .ToArray();

        JsonOutput.Write(new
        {
            query,
            resultCount = results.Length,
            results
        });

        return 0;
    }

    private static Regex GlobToRegex(string glob)
    {
        var normalized = glob.Replace('\\', '/');
        // Escape regex special chars except * and ?
        var pattern = Regex.Escape(normalized)
            .Replace("\\*\\*", "<<GLOBSTAR>>")
            .Replace("\\*", "[^/]*")
            .Replace("\\?", "[^/]")
            .Replace("<<GLOBSTAR>>", ".*");

        return new Regex("^" + pattern + "$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
