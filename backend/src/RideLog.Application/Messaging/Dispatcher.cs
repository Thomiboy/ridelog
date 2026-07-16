using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace RideLog.Application.Messaging;

internal sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    public Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand
        => services.GetRequiredService<ICommandHandler<TCommand>>().HandleAsync(command, cancellationToken);

    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        // The concrete query type is only known at runtime, so the handler contract is closed here.
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        var handler = services.GetRequiredService(handlerType);
        var handleAsync = handlerType.GetMethod(nameof(IQueryHandler<IQuery<TResult>, TResult>.HandleAsync))!;

        try
        {
            return await (Task<TResult>)handleAsync.Invoke(handler, [query, cancellationToken])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}
