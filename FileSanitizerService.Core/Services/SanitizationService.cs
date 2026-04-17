using FileSanitizerService.Core.Interfaces;
using FileSanitizerService.Core.Models;
using FileSanitizerService.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace FileSanitizerService.Core.Services;

public sealed class SanitizationService : ISanitizationService
{
    private readonly IFormatDetector _fileFormatDetector;
    private readonly IFileSanitizerResolver _fileSanitizerResolver;
    private readonly ITempFileProvider _tempFileProvider;
    private readonly ILogger<SanitizationService> _logger;

    public SanitizationService(
        IFormatDetector fileFormatDetector,
        IFileSanitizerResolver fileSanitizerResolver,
        ITempFileProvider tempFileProvider,
        ILogger<SanitizationService> logger)
    {
        _fileFormatDetector = fileFormatDetector;
        _fileSanitizerResolver = fileSanitizerResolver;
        _tempFileProvider = tempFileProvider;
        _logger = logger;
    }
    
    // Sanitizes the input stream and writes the result to a temp file,
    // returning a read stream to it
    public async Task<Stream> SanitizeToTempFileAsync(
        Stream inputStream,
        string? fileName = null,
        CancellationToken ct = default)
    {
        var tempPath = _tempFileProvider.CreatePath();
        try
        {
            await using (var tempWriteStream = _tempFileProvider.OpenWrite(tempPath))
            {
                await DetectFileTypeAndSanitizeAsync(inputStream, tempWriteStream, fileName, ct);
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

    // Detects the file format from the input stream and forward to the matching sanitizer
    private async Task DetectFileTypeAndSanitizeAsync(
        Stream input,
        Stream output,
        string? fileName = null,
        CancellationToken ct = default)
    {
        if (!output.CanWrite)
            throw new ArgumentException("Output stream must be writable.", nameof(output));

        var detection = await _fileFormatDetector.DetectAsync(input, ct);
        var format = detection.Format;

        if (format == FileFormat.Unknown)
        {
            _logger.LogError("File format could not be detected — rejecting request");
            throw new UnsupportedFormatException();
        }

        _logger.LogInformation("Detected format {Format} — routing to sanitizer", format);

        var sanitizer = _fileSanitizerResolver.GetSanitizerByFormat(format);
        if (sanitizer is null)
        {
            _logger.LogError("No sanitizer registered for format {Format}", format);
            throw new SanitizerConfigurationException($"No sanitizer registered for detected format '{format}'.");
        }

        _logger.LogInformation("Starting sanitization process for file '{FileName}'", fileName ?? "unknown");

        await sanitizer.SanitizeAsync(input, output, ct);
    }
}
