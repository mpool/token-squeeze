using System.ComponentModel;
using System.Text.RegularExpressions;
using Spectre.Console.Cli;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Commands;

internal sealed class FindCommand(IndexStore store, LanguageRegistry registry) : Command<FindCommand.Settings>
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
        var symbols = store.LoadAllSymbols(settings.Name);
        if (manifest is null || symbols is null)
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
        var scored = new List<(SearchSymbol symbol, int score)>();

        // Pre-filter by kind and path before scoring
        IEnumerable<SearchSymbol> candidates = symbols;

        if (kindFilter.HasValue)
            candidates = candidates.Where(s => s.Kind == kindFilter.Value);

        if (pathRegex is not null)
        {
            var loggedTimeout = false;
            candidates = candidates.Where(s =>
            {
                try
                {
                    return pathRegex.IsMatch(s.File.Replace('\\', '/'));
                }
                catch (RegexMatchTimeoutException)
                {
                    if (!loggedTimeout)
                    {
                        Console.Error.WriteLine("Warning: path filter regex timed out; some results may be excluded.");
                        loggedTimeout = true;
                    }
                    return false;
                }
            });
        }

        foreach (var symbol in candidates)
        {
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

        var topResults = scored
            .OrderByDescending(x => x.score)
            .Take(settings.Limit)
            .ToList();

        var results = topResults
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
        catch (Exception ex)
        {
            JsonOutput.WriteError(ex.Message);
            return 1;
        }
    }

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    internal static Regex GlobToRegex(string glob)
    {
        // SEC-04: Reject overly long globs to prevent ReDoS
        if (glob.Length > 200)
            throw new ArgumentException($"Glob pattern exceeds maximum length of 200 characters (was {glob.Length}).");

        var normalized = glob.Replace('\\', '/');

        // Handle rooted patterns: strip leading / and anchor to start
        var isRooted = normalized.StartsWith('/');
        if (isRooted)
            normalized = normalized.TrimStart('/');

        // Escape regex special chars except * and ?
        // Handle **/ (globstar + separator) as optional directory prefix
        var pattern = Regex.Escape(normalized)
            .Replace("\\*\\*/", "<<GLOBSTAR_SEP>>")
            .Replace("\\*\\*", "<<GLOBSTAR>>")
            .Replace("\\*", "[^/]*")
            .Replace("\\?", "[^/]")
            .Replace("<<GLOBSTAR_SEP>>", "(.*/)?")
            .Replace("<<GLOBSTAR>>", ".*");

        var options = RegexOptions.IgnoreCase | RegexOptions.Compiled;

        // Rooted patterns or globstar patterns anchor to start
        if (isRooted || normalized.StartsWith("**/"))
            return new Regex("^" + pattern + "$", options, RegexTimeout);

        // Non-rooted patterns allow partial path matching (match after any /)
        return new Regex("(^|/)" + pattern + "$", options, RegexTimeout);
    }
}
