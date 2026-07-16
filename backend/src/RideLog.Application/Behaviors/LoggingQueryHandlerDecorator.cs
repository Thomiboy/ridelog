using Microsoft.Extensions.Logging;
using RideLog.Application.Messaging;

namespace RideLog.Application.Behaviors;

internal sealed class LoggingQueryHandlerDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    ILogger<TQuery> logger) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public async Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Handling {Query}", typeof(TQuery).Name);
        var result = await inner.HandleAsync(query, cancellationToken);
        logger.LogInformation("Handled {Query}", typeof(TQuery).Name);
        return result;
    }
}
