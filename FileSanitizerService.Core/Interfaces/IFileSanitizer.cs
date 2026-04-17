using FileSanitizerService.Core.Models;

namespace FileSanitizerService.Core.Interfaces;

public interface IFileSanitizer
{
    /// <summary>
    /// The format this sanitizer handles. Used by the registry for self-registration.
    /// </summary>
    FileFormat SupportedFormat { get; }
    
    Task SanitizeAsync(
        Stream input,
        Stream output,
        CancellationToken ct = default);
}
