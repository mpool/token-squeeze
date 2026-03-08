using Spectre.Console.Cli;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Commands;

internal sealed class ListCommand : Command<ListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        try
        {
            var store = new IndexStore();
            var projectNames = store.ListProjects();

            var projects = new List<object>();
            foreach (var name in projectNames)
            {
                var index = store.Load(name);
                if (index is null) continue;

                var languages = index.Symbols
                    .Select(s => s.Language)
                    .Distinct()
                    .OrderBy(l => l)
                    .ToList();

                projects.Add(new
                {
                    name = index.ProjectName,
                    sourcePath = index.SourcePath,
                    fileCount = index.Files.Count,
                    symbolCount = index.Symbols.Count,
                    languages,
                    indexedAt = index.IndexedAt
                });
            }

            JsonOutput.Write(new { projects });
            return 0;
        }
        catch (Exception ex)
        {
            JsonOutput.WriteError(ex.Message);
            return 1;
        }
    }
}
