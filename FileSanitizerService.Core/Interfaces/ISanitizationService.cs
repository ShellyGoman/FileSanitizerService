namespace FileSanitizerService.Core.Interfaces;

public interface ISanitizationService
{
    Task<Stream> SanitizeToTempFileAsync(
        Stream inputStream,
        string? fileName = null,
        CancellationToken ct = default);
}
