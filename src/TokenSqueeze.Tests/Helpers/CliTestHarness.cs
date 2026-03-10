// CLI integration tests using this harness should be placed in [Collection("CLI")]
// to prevent parallel Console.Out conflicts across test classes.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using TokenSqueeze.Commands;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Parser;

namespace TokenSqueeze.Tests.Helpers;

[CollectionDefinition("CLI")]
public sealed class CliCollection : ICollectionFixture<CliTestHarness> { }

public sealed class CliTestHarness : IDisposable
{
    private readonly string _tempRoot;
    private readonly TextWriter _originalOut;

    public string StorageDir { get; }

    public CliTestHarness()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ts-test-{Guid.NewGuid():N}");
        StorageDir = Path.Combine(_tempRoot, "storage");
        Directory.CreateDirectory(StorageDir);
        _originalOut = Console.Out;
    }

    public (int exitCode, string output) Run(params string[] args)
    {
        return RunInDir(null, args);
    }

    public (int exitCode, string output) RunInDir(string? workingDir, params string[] args)
    {
        var writer = new StringWriter();
        var previousDir = Directory.GetCurrentDirectory();
        try
        {
            Console.SetOut(writer);

            if (workingDir is not null)
                Directory.SetCurrentDirectory(workingDir);

            var services = new ServiceCollection();
            services.AddSingleton<LanguageRegistry>();
            var registrar = new TypeRegistrar(services);

            var app = new CommandApp(registrar);
            app.Configure(config =>
            {
                config.SetApplicationName("token-squeeze");
                config.AddCommand<IndexCommand>("index");
                config.AddCommand<OutlineCommand>("outline");
                config.AddCommand<ExtractCommand>("extract");
                config.AddCommand<FindCommand>("find");
                config.AddCommand<ParseTestCommand>("parse-test");
                config.PropagateExceptions();
            });

            var exitCode = app.Run(args);
            return (exitCode, writer.ToString());
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDir);
            Console.SetOut(_originalOut);
        }
    }

    public string CreateSourceDir(string name, Dictionary<string, string> files)
    {
        var dir = Path.Combine(_tempRoot, "sources", name);
        Directory.CreateDirectory(dir);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        foreach (var (fileName, content) in files)
        {
            var filePath = Path.Combine(dir, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, content, encoding);
        }
        return dir;
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
