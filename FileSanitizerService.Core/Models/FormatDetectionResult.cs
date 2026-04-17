namespace FileSanitizerService.Core.Models;

public readonly record struct FormatDetectionResult(
    FileFormat Format,
    byte[] PrefetchedBytes);
