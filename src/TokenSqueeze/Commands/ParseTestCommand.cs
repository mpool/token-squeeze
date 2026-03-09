using System.ComponentModel;
using System.Text;
using Spectre.Console.Cli;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Parser;

namespace TokenSqueeze.Commands;

internal sealed class ParseTestCommand(LanguageRegistry registry) : Command<ParseTestCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("The file to parse and extract symbols from")]
        public string File { get; init; } = string.Empty;
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var filePath = Path.GetFullPath(settings.File);
        if (!System.IO.File.Exists(filePath))
        {
            JsonOutput.WriteError($"File not found: {filePath}");
            return 1;
        }

        var ext = Path.GetExtension(filePath);
        var spec = registry.GetSpecForExtension(ext);

        if (spec == null)
        {
            JsonOutput.WriteError($"Unsupported file extension: {ext}");
            return 1;
        }

        var sourceBytes = System.IO.File.ReadAllBytes(filePath);
        var extractor = new SymbolExtractor(registry);
        var symbols = extractor.ExtractSymbols(filePath, sourceBytes, spec);

        JsonOutput.Write(new { symbols, count = symbols.Count });
        return 0;
    }
}
