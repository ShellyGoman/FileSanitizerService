using FileSanitizerService.Core.Interfaces;
using FileSanitizerService.Core.Models;

namespace FileSanitizerService.Core.Detection;

public sealed class HeaderDetector : IFormatDetector
{
    private const byte LineFeed = (byte)'\n';
    
    private static readonly IReadOnlyList<(FileFormat Format, byte[] Header)> Headers =
    [
        // Keep longest signatures first so CRLF is matched before LF.
        (FileFormat.Abc, "123\r\n"u8.ToArray()),
        (FileFormat.Abc, "123\n"u8.ToArray())
    ];

    private static readonly int MaxHeaderLength =
        Headers.Max(signature => signature.Header.Length);

    public async Task<FormatDetectionResult> DetectAsync(Stream stream, CancellationToken ct = default)
    {
        var headerBytes = new List<byte>(MaxHeaderLength);
        var oneByte = new byte[1];

        while (headerBytes.Count < MaxHeaderLength && await stream.ReadAsync(oneByte, ct) > 0)
        {
            headerBytes.Add(oneByte[0]);
            // assuming all headers last byte is '\n'
            if (oneByte[0] == LineFeed)
                break;
        }

        if (headerBytes.Count == 0)
            throw new ArgumentException("Uploaded file is empty.");

        var headerArray = headerBytes.ToArray();

        foreach (var (format, header) in Headers)
        {
            if (headerArray.Length >= header.Length &&
                headerArray.AsSpan(0, header.Length).SequenceEqual(header))
            {
                return new FormatDetectionResult(format);
            }
        }

        return new FormatDetectionResult(FileFormat.Unknown);
    }
}
