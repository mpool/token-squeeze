using Spectre.Console.Cli;
using TokenSqueeze.Commands;
using TokenSqueeze.Infrastructure;

// Tree-sitter native library smoke test -- validates that native libs loaded correctly.
// Runs on every startup; fast (single parse of a tiny string), logs only to stderr.
try
{
    using var language = new TreeSitter.Language("Python");
    using var parser = new TreeSitter.Parser(language);
    using var tree = parser.Parse("def hello(): pass");
    var rootType = tree?.RootNode?.Type ?? "(null)";
    Console.Error.WriteLine($"TreeSitter loaded: root node type = {rootType}");
}
catch (DllNotFoundException ex)
{
    JsonOutput.WriteError($"TreeSitter native library failed to load: {ex.Message}");
    return 1;
}

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("token-squeeze");

    config.AddCommand<IndexCommand>("index")
        .WithDescription("Index a local folder");
    config.AddCommand<ListCommand>("list")
        .WithDescription("List indexed folders");
    config.AddCommand<PurgeCommand>("purge")
        .WithDescription("Delete index for a folder");
    config.AddCommand<OutlineCommand>("outline")
        .WithDescription("Show symbols in a file");
    config.AddCommand<ExtractCommand>("extract")
        .WithDescription("Get full source of a symbol");
    config.AddCommand<FindCommand>("find")
        .WithDescription("Search symbols by query");
    config.AddCommand<ParseTestCommand>("parse-test")
        .WithDescription("(hidden) Parse a file and dump extracted symbols as JSON");

#if DEBUG
    config.PropagateExceptions();
    config.ValidateExamples();
#endif
});

return app.Run(args);
