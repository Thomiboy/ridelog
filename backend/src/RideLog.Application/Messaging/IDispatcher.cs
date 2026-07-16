namespace RideLog.Application.Messaging;

/// <summary>Routes commands and queries to their single registered handler, through the decorator pipeline.</summary>
public interface IDispatcher
{
    Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand;

    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
