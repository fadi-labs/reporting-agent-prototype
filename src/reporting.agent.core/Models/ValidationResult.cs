namespace reporting.agent.core.Models;

public sealed record ValidationError(string Code, string Message, string? Hint = null);

public sealed class ValidationResult
{
    public bool Valid { get; init; }
    public IReadOnlyList<ValidationError> Errors { get; init; } = Array.Empty<ValidationError>();

    public static ValidationResult Ok() => new() { Valid = true };
    public static ValidationResult Fail(params ValidationError[] errors) =>
        new() { Valid = false, Errors = errors };
}

