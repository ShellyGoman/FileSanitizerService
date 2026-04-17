using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace FileSanitizerService.Api.Utils;

internal static class MultipartRequestHelper
{
    // Parses and validates that the content-type is multipart/form-data.
    internal static bool TryGetMultipartContentType(string? contentType, out MediaTypeHeaderValue mediaType)
    {
        mediaType = null!;
        if (!MediaTypeHeaderValue.TryParse(contentType, out var parsed))
            return false;

        mediaType = parsed;
        return mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase);
    }

    // Extracts and returns the boundary string from a multipart content-type header.
    internal static string GetBoundary(MediaTypeHeaderValue contentType)
    {
        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).ToString().Trim();
        return string.IsNullOrWhiteSpace(boundary)
            ? throw new ArgumentException("Missing multipart boundary.")
            : boundary;
    }

    // Goes through multipart sections and returns only the first file section found.
    // Note: one multipart "file section" contains the full uploaded file stream.
    internal static async Task<FileMultipartSection?> FindFileSectionAsync(
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

                // This section is not a file (for example a text form field).
                // Drain and discard it so MultipartReader can advance to the next section.
                await section.Body.CopyToAsync(Stream.Null, ct);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            throw new ArgumentException("Invalid multipart body.", ex);
        }

        return null;
    }

    // Returns the file name from the section, or null if it is absent or whitespace.
    internal static string? GetFileName(FileMultipartSection section)
    {
        return string.IsNullOrWhiteSpace(section.FileName) ? null : section.FileName;
    }
}
