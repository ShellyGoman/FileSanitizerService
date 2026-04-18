namespace FileSanitizerService.Core.Interfaces;

public interface ISanitizationService
{
    /// <summary>
    /// Detects the format of the incoming file, sanitizes it,
    /// and returns a stream of the sanitized result.
    /// </summary>
    Task<Stream> SanitizeToTempFileAsync(
        Stream inputStream,
        string? fileName = null,
        CancellationToken ct = default);
}
