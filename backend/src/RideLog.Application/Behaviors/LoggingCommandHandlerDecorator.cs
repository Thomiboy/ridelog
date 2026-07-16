using Microsoft.Extensions.Logging;
using RideLog.Application.Messaging;

namespace RideLog.Application.Behaviors;

internal sealed class LoggingCommandHandlerDecorator<TCommand>(
    ICommandHandler<TCommand> inner,
    ILogger<TCommand> logger) : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    public async Task HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Handling {Command}", typeof(TCommand).Name);
        await inner.HandleAsync(command, cancellationToken);
        logger.LogInformation("Handled {Command}", typeof(TCommand).Name);
    }
}
