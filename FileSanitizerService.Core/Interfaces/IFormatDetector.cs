using FileSanitizerService.Core.Models;

namespace FileSanitizerService.Core.Interfaces;

public interface IFormatDetector
{
    /// <summary>
    /// Reads just enough bytes from the beginning of <paramref name="stream"/> to identify
    /// its format and returns the result. The consumed bytes are <b>not</b> returned; callers
    /// must account for the fact that the stream position has advanced past the header.
    /// Each <see cref="IFileSanitizer"/> implementation is responsible for re-emitting its
    /// own header bytes to the output stream.
    /// </summary>
    Task<FormatDetectionResult> DetectAsync(Stream stream, CancellationToken ct = default);
}
