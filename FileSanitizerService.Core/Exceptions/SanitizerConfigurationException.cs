namespace FileSanitizerService.Core.Exceptions;

public sealed class SanitizerConfigurationException : Exception
{
    public SanitizerConfigurationException(string message)
        : base(message)
    {
    }
}
