using FileSanitizerService.Core.Models;

namespace FileSanitizerService.Core.Interfaces;

public interface IFormatDetector
{
    /// <summary>
    /// Peeks at the start of <paramref name="stream"/> to figure out what file format it is.
    /// Keep in mind that the header bytes get consumed during detection, so the stream won't
    /// be at position 0 anymore when this returns.
    /// </summary>
    Task<FormatDetectionResult> DetectAsync(Stream stream, CancellationToken ct = default);
}
