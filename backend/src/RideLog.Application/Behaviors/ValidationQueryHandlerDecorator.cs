using RideLog.Application.Messaging;
using RideLog.Application.Validation;

namespace RideLog.Application.Behaviors;

internal sealed class ValidationQueryHandlerDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    IEnumerable<IValidator<TQuery>> validators) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public async Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        foreach (var validator in validators)
        {
            errors.AddRange(await validator.ValidateAsync(query, cancellationToken));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return await inner.HandleAsync(query, cancellationToken);
    }
}
