namespace RideLog.Application.Validation;

public sealed class ValidationException(IReadOnlyCollection<string> errors)
    : Exception($"Validation failed: {string.Join("; ", errors)}")
{
    public IReadOnlyCollection<string> Errors { get; } = errors;
}
