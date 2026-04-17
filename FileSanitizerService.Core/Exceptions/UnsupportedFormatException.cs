namespace FileSanitizerService.Core.Exceptions;

public sealed class UnsupportedFormatException : SanitizationException
{
    public UnsupportedFormatException(string message = "Unsupported or unrecognized file format.")
        : base(message)
    {
    }
}
