using FileSanitizerService.Api.Filters;
using FileSanitizerService.Api.Options;
using FileSanitizerService.Api.Utils;
using FileSanitizerService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace FileSanitizerService.Api.Controllers;

[ApiController]
[Route("api")]
public class FileSanitizerController : ControllerBase
{
    private const int MultipartHeadersLengthLimit = 64 * 1024; // 64 kb

    private readonly ISanitizationService _service;
    private readonly ILogger<FileSanitizerController> _logger;
    private readonly long _maxUploadBytes;

    public FileSanitizerController(
        ISanitizationService service,
        IOptions<UploadLimitsOptions> uploadLimitsOptions,
        ILogger<FileSanitizerController> logger)
    {
        _service = service;
        _logger = logger;
        _maxUploadBytes = uploadLimitsOptions.Value.MaxUploadBytes > 0
            ? uploadLimitsOptions.Value.MaxUploadBytes
            : UploadLimitsOptions.DefaultMaxUploadBytes;
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
            if (fileSection is null)
                return BadRequest("No file provided or file is empty.");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        var fileStream = fileSection.FileStream;
        if (fileStream is null)
            return BadRequest("No file provided or file is empty.");

        var fileName = MultipartRequestHelper.GetFileName(fileSection) ?? "sanitized.bin";

        _logger.LogInformation("Sanitize request received for '{FileName}'", fileName);

        var sanitizedStream = await _service.SanitizeToTempFileAsync(fileStream, fileName, ct);
        HttpContext.Response.RegisterForDispose(sanitizedStream);

        _logger.LogInformation("Sanitization complete for '{FileName}'", fileName);

        return File(sanitizedStream, "application/octet-stream", fileName);
    }
}
