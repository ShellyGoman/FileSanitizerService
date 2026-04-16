using FileSanitizerService.Core.Models;

namespace FileSanitizerService.Core.Interfaces;

public interface IFileSanitizerResolver
{
    /// <summary>
    /// Returns the sanitizer for the given format, or null if unsupported.
    /// </summary>
    IFileSanitizer? GetSanitizerByFormat(FileFormat format);
}
