using RideLog.Application.Messaging;
using RideLog.Application.Validation;

namespace RideLog.Application.Behaviors;

internal sealed class ValidationCommandHandlerDecorator<TCommand>(
    ICommandHandler<TCommand> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    public async Task HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        foreach (var validator in validators)
        {
            errors.AddRange(await validator.ValidateAsync(command, cancellationToken));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        await inner.HandleAsync(command, cancellationToken);
    }
}
