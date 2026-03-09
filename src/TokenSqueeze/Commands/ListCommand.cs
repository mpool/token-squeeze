using Spectre.Console.Cli;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Commands;

internal sealed class ListCommand(IndexStore store) : Command<ListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        try
        {
            var json = store.LoadCatalogJson();
            if (json is not null)
            {
                Console.Out.Write(json);
                return 0;
            }

            // No catalog yet — return empty list
            JsonOutput.Write(new { projects = Array.Empty<object>() });
            return 0;
        }
        catch (Exception ex)
        {
            JsonOutput.WriteError(ex.Message);
            return 1;
        }
    }
}
