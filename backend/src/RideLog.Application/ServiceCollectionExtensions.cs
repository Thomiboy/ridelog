using System.Reflection;
using RideLog.Application.Behaviors;
using RideLog.Application.Messaging;
using RideLog.Application.Validation;

// Placed in the DI namespace so callers get AddCqrs without an extra using.
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the dispatcher and scans the given assemblies (default: RideLog.Application) for
    /// command/query handlers and validators. Handlers are wrapped in the decorator pipeline:
    /// logging → validation → handler.
    /// </summary>
    public static IServiceCollection AddCqrs(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = [typeof(IDispatcher).Assembly];
        }

        services.AddScoped<IDispatcher, Dispatcher>();

        var candidates = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false });

        foreach (var implementation in candidates)
        {
            foreach (var contract in implementation.GetInterfaces().Where(i => i.IsGenericType))
            {
                var definition = contract.GetGenericTypeDefinition();
                if (definition == typeof(ICommandHandler<>))
                {
                    AddPipeline(services, contract, implementation,
                        typeof(ValidationCommandHandlerDecorator<>), typeof(LoggingCommandHandlerDecorator<>));
                }
                else if (definition == typeof(IQueryHandler<,>))
                {
                    AddPipeline(services, contract, implementation,
                        typeof(ValidationQueryHandlerDecorator<,>), typeof(LoggingQueryHandlerDecorator<,>));
                }
                else if (definition == typeof(IValidator<>))
                {
                    services.AddScoped(contract, implementation);
                }
            }
        }

        return services;
    }

    /// <summary>Registers a handler behind its contract, wrapped innermost-first in the given decorators.</summary>
    private static void AddPipeline(
        IServiceCollection services, Type contract, Type implementation, params Type[] decorators)
    {
        services.AddScoped(implementation);
        services.AddScoped(contract, provider =>
        {
            var handler = provider.GetRequiredService(implementation);
            foreach (var decorator in decorators)
            {
                var closed = decorator.MakeGenericType(contract.GetGenericArguments());
                handler = ActivatorUtilities.CreateInstance(provider, closed, handler);
            }

            return handler;
        });
    }
}
