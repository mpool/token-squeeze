using Spectre.Console.Cli;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Commands;

internal sealed class ListCommand(IndexStore store, LanguageRegistry registry) : Command<ListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        try
        {
            var projectNames = store.ListProjects();

            var projects = new List<object>();
            foreach (var name in projectNames)
            {
                // Try new manifest format first
                var manifest = store.LoadManifest(name);
                if (manifest is not null)
                {
                    projects.Add(ProjectToJson(manifest));
                    continue;
                }

                // Legacy format: try migration
                if (store.IsLegacyFormat(name))
                {
                    if (LegacyMigration.TryMigrateIfNeeded(name, store, registry, out var error))
                    {
                        if (error is not null)
                        {
                            Console.Error.WriteLine($"Warning: could not migrate '{name}': {error}");
                            continue;
                        }

                        // Retry with manifest after successful migration
                        manifest = store.LoadManifest(name);
                        if (manifest is not null)
                        {
                            projects.Add(ProjectToJson(manifest));
                        }
                    }
                }
            }

            JsonOutput.Write(new { projects });
            return 0;
        }
        catch (Exception ex)
        {
            JsonOutput.WriteError(ex.Message);
            return 1;
        }

        object ProjectToJson(Models.Manifest m)
        {
            var totalSymbols = m.Files.Values.Sum(f => f.SymbolCount);
            var languages = m.Files.Values
                .Select(f => f.Language)
                .Distinct()
                .OrderBy(l => l)
                .ToList();

            return new
            {
                name = m.ProjectName,
                sourcePath = m.SourcePath,
                fileCount = m.Files.Count,
                symbolCount = totalSymbols,
                languages,
                indexedAt = m.IndexedAt
            };
        }
    }
}
