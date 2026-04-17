using FileSanitizerService.Core.Interfaces;
using FileSanitizerService.Core.Models;

namespace FileSanitizerService.Core.Registry;

public sealed class FileSanitizerResolver : IFileSanitizerResolver
{
    private readonly IReadOnlyDictionary<FileFormat, IFileSanitizer> _map;

    public FileSanitizerResolver(IEnumerable<IFileSanitizer> sanitizers)
    {
        _map = sanitizers.ToDictionary(s => s.SupportedFormat);
    }

    /// <summary>
    /// Returns the sanitizer for the given format, or null if unsupported.
    /// </summary>
    public IFileSanitizer? GetSanitizerByFormat(FileFormat format)
    {
        return _map.GetValueOrDefault(format);   
    }
}
