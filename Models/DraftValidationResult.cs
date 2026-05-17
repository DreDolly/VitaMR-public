namespace VitaMR.Models;

public sealed class DraftValidationResult
{
    public DraftValidationResult(bool isValid, IReadOnlyList<string> messages)
    {
        IsValid = isValid;
        Messages = messages;
    }

    public bool IsValid { get; }

    public IReadOnlyList<string> Messages { get; }
}
