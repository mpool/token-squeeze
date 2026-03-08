using System.ComponentModel;
using Spectre.Console.Cli;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Commands;

internal sealed class OutlineCommand : Command<OutlineCommand.Settings>
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
        var store = new IndexStore();
        var index = store.Load(settings.Name);
        if (index is null)
        {
            JsonOutput.WriteError($"Project not found: {settings.Name}");
            return 1;
        }

        var requestedFile = settings.File.Replace('\\', '/');

        var fileSymbols = index.Symbols
            .Where(s =>
            {
                var symbolFile = s.File.Replace('\\', '/');
                return string.Equals(symbolFile, requestedFile, StringComparison.OrdinalIgnoreCase)
                    || symbolFile.EndsWith("/" + requestedFile, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(symbolFile, requestedFile.TrimStart('/'), StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        if (fileSymbols.Count == 0)
        {
            JsonOutput.WriteError($"File not found in index: {settings.File}");
            return 1;
        }

        // Build hierarchy: top-level symbols (no parent) as roots, others nested
        var roots = fileSymbols.Where(s => s.Parent is null).ToList();
        var childrenByParent = fileSymbols
            .Where(s => s.Parent is not null)
            .GroupBy(s => s.Parent!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var symbolNodes = roots.Select(root =>
        {
            var children = childrenByParent.TryGetValue(root.Name, out var kids)
                ? kids.Select(c => new
                {
                    name = c.Name,
                    kind = c.Kind.ToString(),
                    signature = c.Signature,
                    line = c.Line,
                    endLine = c.EndLine
                }).ToArray()
                : null;

            return new
            {
                name = root.Name,
                kind = root.Kind.ToString(),
                signature = root.Signature,
                line = root.Line,
                endLine = root.EndLine,
                children = children?.Length > 0 ? children : null
            };
        }).ToArray();

        // Resolve the actual matched file path for output
        var matchedFile = fileSymbols.First().File;

        JsonOutput.Write(new
        {
            file = matchedFile,
            symbols = symbolNodes
        });

        return 0;
    }
}
