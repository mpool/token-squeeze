using System.ComponentModel;
using Spectre.Console.Cli;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Commands;

internal sealed class PurgeCommand(IndexStore store) : Command<PurgeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The name of the index to delete")]
        public string Name { get; init; } = string.Empty;
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        try
        {
            var manifest = store.LoadManifest(settings.Name);

            if (manifest is null && !store.IsLegacyFormat(settings.Name))
            {
                JsonOutput.WriteError($"Project not found: {settings.Name}");
                return 1;
            }

            store.Delete(settings.Name);

            JsonOutput.Write(new
            {
                purged = settings.Name,
                status = "deleted"
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
