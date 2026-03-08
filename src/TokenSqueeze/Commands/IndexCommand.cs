using System.ComponentModel;
using Spectre.Console.Cli;
using TokenSqueeze.Indexing;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Commands;

internal sealed class IndexCommand : Command<IndexCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("The directory path to index")]
        public string Path { get; init; } = string.Empty;

        [CommandOption("--name|-n <NAME>")]
        [Description("Explicit project name alias")]
        public string? Name { get; init; }
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

            using var registry = new LanguageRegistry();
            var store = new IndexStore();
            var indexer = new ProjectIndexer(store, registry);

            var index = indexer.Index(path, settings.Name);

            JsonOutput.Write(new
            {
                projectName = index.ProjectName,
                sourcePath = index.SourcePath,
                filesIndexed = index.Files.Count,
                symbolsExtracted = index.Symbols.Count,
                indexPath = StoragePaths.GetIndexPath(index.ProjectName)
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
