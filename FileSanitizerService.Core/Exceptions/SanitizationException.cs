namespace FileSanitizerService.Core.Exceptions;

public abstract class SanitizationException : Exception
{
    protected SanitizationException(string message)
        : base(message)
    {
    }
}
