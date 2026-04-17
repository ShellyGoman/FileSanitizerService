using FileSanitizerService.Core.Interfaces;
using FileSanitizerService.Core.Models;

namespace FileSanitizerService.Core.Services;

public sealed class SanitizationService
{
    private readonly IFormatDetector _fileFormatDetector;
    private readonly IFileSanitizerResolver _fileSanitizerResolver;
    private readonly ITempFileProvider _tempFileProvider;

    public SanitizationService(
        IFormatDetector fileFormatDetector,
        IFileSanitizerResolver fileSanitizerResolver,
        ITempFileProvider tempFileProvider)
    {
        _fileFormatDetector = fileFormatDetector;
        _fileSanitizerResolver = fileSanitizerResolver;
        _tempFileProvider = tempFileProvider;
    }
    
    public async Task<Stream> SanitizeToTempFileAsync(
        Stream inputStream,
        CancellationToken ct = default)
    {
        var tempPath = _tempFileProvider.CreatePath();
        try
        {
            await using (var tempWriteStream = _tempFileProvider.OpenWrite(tempPath))
            {
                await DetectFileTypeAndSanitizeAsync(inputStream, tempWriteStream, ct);
                await tempWriteStream.FlushAsync(ct);
            }

            return _tempFileProvider.OpenReadTemporary(tempPath);
        }
        catch
        {
            _tempFileProvider.TryDelete(tempPath);
            throw;
        }
    }

    private async Task DetectFileTypeAndSanitizeAsync(
        Stream input,
        Stream output,
        CancellationToken ct = default)
    {
        if (!output.CanWrite)
            throw new ArgumentException("Output stream must be writable.", nameof(output));

        var detection = await _fileFormatDetector.DetectAsync(input, ct);
        var format = detection.Format;

        if (format == FileFormat.Unknown)
            throw new ArgumentException("Unsupported or unrecognized file format.");

        // get the right sanitizer class according to the file format
        var sanitizer = _fileSanitizerResolver.GetSanitizerByFormat(format);
        if (sanitizer is null)
        {
            throw new ArgumentException($"No sanitizer found for format '{format}'.");
        }

        await sanitizer.SanitizeAsync(input, output, ct);
    }
}
