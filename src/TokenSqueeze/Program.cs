using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using TokenSqueeze.Commands;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Parser;

var services = new ServiceCollection();
services.AddSingleton<LanguageRegistry>();
var registrar = new TypeRegistrar(services);

var app = new CommandApp(registrar);
app.Configure(config =>
{
    config.SetApplicationName("token-squeeze");

    config.AddCommand<IndexCommand>("index")
        .WithDescription("Index a local folder");
    config.AddCommand<OutlineCommand>("outline")
        .WithDescription("Show symbols in a file");
    config.AddCommand<ExtractCommand>("extract")
        .WithDescription("Get full source of a symbol");
    config.AddCommand<FindCommand>("find")
        .WithDescription("Search symbols by query");
    config.AddCommand<ParseTestCommand>("parse-test")
        .IsHidden()
        .WithDescription("Parse a file and dump extracted symbols as JSON");

#if DEBUG
    config.PropagateExceptions();
    config.ValidateExamples();
#endif
});

var exitCode = app.Run(args);

// Dispose the service provider to release native tree-sitter handles (DEBT-02)
if (registrar.ServiceProvider is IDisposable disposable)
    disposable.Dispose();

return exitCode;
