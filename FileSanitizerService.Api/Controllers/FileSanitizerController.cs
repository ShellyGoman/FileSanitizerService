using FileSanitizerService.Api.Filters;
using FileSanitizerService.Api.Options;
using FileSanitizerService.Api.Utils;
using FileSanitizerService.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace FileSanitizerService.Api.Controllers;

[ApiController]
[Route("api")]
public class FileSanitizerController : ControllerBase
{
    private const long DefaultMaxUploadBytes = 500L * 1024 * 1024;
    private const int MultipartHeadersLengthLimit = 64 * 1024;

    private readonly SanitizationService _service;
    private readonly long _maxUploadBytes;

    public FileSanitizerController(SanitizationService service, IOptions<UploadLimitsOptions> uploadLimitsOptions)
    {
        _service = service;
        _maxUploadBytes = uploadLimitsOptions.Value.MaxUploadBytes > 0
            ? uploadLimitsOptions.Value.MaxUploadBytes
            : DefaultMaxUploadBytes;
    }

    [HttpPost("sanitize")]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> Sanitize(CancellationToken ct)
    {
        if (!MultipartRequestHelper.TryGetMultipartContentType(Request.ContentType, out var contentType))
            return BadRequest("Expected a multipart/form-data request.");

        FileMultipartSection? fileSection;
        
        try
        {
            var boundary = MultipartRequestHelper.GetBoundary(contentType);
            var reader = new MultipartReader(boundary, Request.Body)
            {
                BodyLengthLimit = _maxUploadBytes,
                HeadersLengthLimit = MultipartHeadersLengthLimit
            };
            fileSection = await MultipartRequestHelper.FindFileSectionAsync(reader, ct);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        if (fileSection is null)
            return BadRequest("No file provided or file is empty.");

        var fileStream = fileSection.FileStream;
        if (fileStream is null)
            return BadRequest("No file provided or file is empty.");

        var fileName = MultipartRequestHelper.GetFileName(fileSection) ?? "sanitized.bin";

        var sanitizedStream = await _service.SanitizeToTempFileAsync(fileStream, ct);
        HttpContext.Response.RegisterForDispose(sanitizedStream);

        return File(sanitizedStream, "application/octet-stream", fileName);
    }
}
