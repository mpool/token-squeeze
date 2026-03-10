using System.ComponentModel;
using Spectre.Console.Cli;
using TokenSqueeze.Indexing;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Commands;

internal sealed class IndexCommand(LanguageRegistry registry) : Command<IndexCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("The directory path to index")]
        public string Path { get; init; } = string.Empty;
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        try
        {
            var path = System.IO.Path.GetFullPath(settings.Path);

            if (!Directory.Exists(path))
            {
                JsonOutput.WriteError($"Directory not found: {settings.Path}");
                return 1;
            }

            var cacheDir = System.IO.Path.Combine(path, ".cache");
            var store = new IndexStore(cacheDir);
            var indexer = new ProjectIndexer(store, registry);

            var result = indexer.Index(path);

            JsonOutput.Write(new
            {
                sourcePath = result.Index.SourcePath,
                filesIndexed = result.Index.Files.Count,
                symbolsExtracted = result.Index.Symbols.Count,
                cacheDir,
                errorsEncountered = result.ErrorCount
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
