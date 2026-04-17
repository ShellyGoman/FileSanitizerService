namespace FileSanitizerService.Core.Exceptions;

public sealed class InvalidFileStructureException : SanitizationException
{
    public InvalidFileStructureException(string message)
        : base(message)
    {
    }
}
