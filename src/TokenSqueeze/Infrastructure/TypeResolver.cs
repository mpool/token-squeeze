using Spectre.Console.Cli;

namespace TokenSqueeze.Infrastructure;

internal sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
{
    public object? Resolve(Type? type) => type == null ? null : provider.GetService(type);

    public void Dispose() => (provider as IDisposable)?.Dispose();
}
