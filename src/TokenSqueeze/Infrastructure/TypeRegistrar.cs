using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace TokenSqueeze.Infrastructure;

internal sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    public IServiceProvider? ServiceProvider { get; private set; }

    public ITypeResolver Build()
    {
        ServiceProvider = services.BuildServiceProvider();
        return new TypeResolver(ServiceProvider);
    }

    public void Register(Type service, Type implementation) =>
        services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) =>
        services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory) =>
        services.AddSingleton(service, _ => factory());
}
