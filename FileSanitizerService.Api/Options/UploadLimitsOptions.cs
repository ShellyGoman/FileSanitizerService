namespace FileSanitizerService.Api.Options;

public class UploadLimitsOptions
{
    public const string SectionName = "UploadLimits";

    public long MaxUploadBytes { get; set; }
}
