namespace FileSanitizerService.Api.Options;

public class UploadLimitsOptions
{
    public const string SectionName = "UploadLimits";
    public const long DefaultMaxUploadBytes = 500L * 1024 * 1024; // 500 MB

    public long MaxUploadBytes { get; init; }
}
