using FileSanitizerService.Api.Filters;
using FileSanitizerService.Api.Utils;
using FileSanitizerService.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace FileSanitizerService.Api.Controllers;

[ApiController]
[Route("api")]
public class FileSanitizerController : ControllerBase
{
    private readonly SanitizationService _service;

    public FileSanitizerController(SanitizationService service)
    {
        _service = service;
    }

    [HttpPost("sanitize")]
    [DisableFormValueModelBinding]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Sanitize(CancellationToken ct)
    {
        if (!MultipartRequestHelper.TryGetMultipartContentType(Request.ContentType, out var contentType))
            return BadRequest("Expected a multipart/form-data request.");

        string boundary;
        try
        {
            boundary = MultipartRequestHelper.GetBoundary(contentType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        var reader = new MultipartReader(boundary, Request.Body);

        FileMultipartSection? fileSection;
        try
        {
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
