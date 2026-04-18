namespace FileSanitizerService.Api.Exceptions;

public sealed class FileTooLargeException : Exception
{
    public FileTooLargeException()
        : base("File exceeds the maximum allowed upload size.")
    {
    }
}
