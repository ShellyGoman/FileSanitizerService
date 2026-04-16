using FileSanitizerService.Core.Interfaces;
using FileSanitizerService.Core.Models;

namespace FileSanitizerService.Core.Detection;

public sealed class HeaderDetector : IFormatDetector
{
    private static readonly IReadOnlyDictionary<FileFormat, byte[]> Headers =
        new Dictionary<FileFormat, byte[]>
        {
            [FileFormat.Abc] = "123\n"u8.ToArray()
        };

    private static readonly int MaxHeaderLength =
        Headers.Values.Max(s => s.Length);

    public async Task<FormatDetectionResult> DetectAsync(Stream stream, CancellationToken ct = default)
    {
        var buffer = new byte[MaxHeaderLength];
        var bytesRead = 
            await stream.ReadAsync(buffer.AsMemory(0, MaxHeaderLength), ct);
        var prefetchedBytes = buffer.AsSpan(0, bytesRead).ToArray();
        
        // go through all headers formats exists in the system and check if one matches
        foreach (var (format, header) in Headers.OrderByDescending(pair => pair.Value.Length))
        {
            if (bytesRead >= header.Length &&
                buffer.AsSpan(0, header.Length).SequenceEqual(header))
            {
                return new FormatDetectionResult(format, prefetchedBytes);
            }
        }

        return new FormatDetectionResult(FileFormat.Unknown, prefetchedBytes);
    }
}
