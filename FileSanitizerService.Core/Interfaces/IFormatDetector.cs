using FileSanitizerService.Core.Models;

namespace FileSanitizerService.Core.Interfaces;

public interface IFormatDetector
{
    /// <summary>
    /// Inspects the beginning of <paramref name="stream"/> to identify its format and
    /// returns the prefetched bytes that were consumed while detecting it.
    /// </summary>
    Task<FormatDetectionResult> DetectAsync(Stream stream, CancellationToken ct = default);
}
