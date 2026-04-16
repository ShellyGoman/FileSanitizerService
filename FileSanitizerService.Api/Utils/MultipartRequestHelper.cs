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

    // Advances through multipart sections and returns the first file section found.
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

                await section.Body.CopyToAsync(Stream.Null, ct);
            }
        }
        catch (IOException ex)
        {
            throw new ArgumentException("Invalid multipart body.", ex);
        }

        return null;
    }

    // Returns the file name from the section, or null if it is absent or whitespace.
    internal static string? GetFileName(FileMultipartSection section) =>
        string.IsNullOrWhiteSpace(section.FileName) ? null : section.FileName;
}
