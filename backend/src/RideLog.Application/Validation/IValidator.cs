namespace RideLog.Application.Validation;

/// <summary>Validation hook run by the pipeline before a handler; implementations are picked up by assembly scanning.</summary>
public interface IValidator<in T>
{
    Task<IReadOnlyCollection<string>> ValidateAsync(T instance, CancellationToken cancellationToken = default);
}
