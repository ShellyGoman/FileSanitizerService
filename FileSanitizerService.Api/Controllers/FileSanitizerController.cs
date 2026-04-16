using FileSanitizerService.Core.Services;
using FileSanitizerService.Api.Filters;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

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
        if (!TryGetMultipartContentType(Request.ContentType, out var contentType))
            return BadRequest("Expected a multipart/form-data request.");

        string boundary;
        try
        {
            boundary = GetBoundary(contentType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        var reader = new MultipartReader(boundary, Request.Body);

        FileMultipartSection? fileSection;
        try
        {
            fileSection = await FindFileSectionAsync(reader, ct);
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

        var fileName = GetFileName(fileSection) ?? "sanitized.bin";

        var sanitizedStream = await _service.SanitizeToTempFileAsync(fileStream, ct);
        HttpContext.Response.RegisterForDispose(sanitizedStream);

        return File(sanitizedStream, "application/octet-stream", fileName);
    }

    private static bool TryGetMultipartContentType(string? contentType, out MediaTypeHeaderValue mediaType)
    {
        mediaType = default!;
        if (!MediaTypeHeaderValue.TryParse(contentType, out var parsed))
            return false;

        mediaType = parsed;
        return mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetBoundary(MediaTypeHeaderValue contentType)
    {
        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).ToString().Trim();
        if (string.IsNullOrWhiteSpace(boundary))
            throw new ArgumentException("Missing multipart boundary.");

        return boundary;
    }

    private static async Task<FileMultipartSection?> FindFileSectionAsync(
        MultipartReader reader,
        CancellationToken ct)
    {
        MultipartSection? section;
        try
        {
            while ((section = await reader.ReadNextSectionAsync(ct)) is not null)
            {
                var fileSection = section.AsFileSection();
                if (fileSection is not null)
                    return fileSection;

                await section.Body.CopyToAsync(Stream.Null, ct);
            }
        }
        catch (IOException ex)
        {
            throw new ArgumentException("Invalid multipart body.", ex);
        }

        return null;
    }

    private static string? GetFileName(FileMultipartSection section)
    {
        return string.IsNullOrWhiteSpace(section.FileName) ? null : section.FileName;
    }
}

