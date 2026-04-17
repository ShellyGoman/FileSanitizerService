using FileSanitizerService.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FileSanitizerService.Infrastructure.Services;

public sealed class TempFileProvider : ITempFileProvider
{
    private const string DirectoryName = "FileSanitizerServiceTmp";
    private const int StreamBufferSize = 64 * 1024; // 64 kb
    
    private readonly ILogger<TempFileProvider> _logger;

    public TempFileProvider(ILogger<TempFileProvider> logger)
    {
        _logger = logger;
    }

    // Generates a unique temp file path inside the service's dedicated temp directory.
    public string CreatePath()
    {
        var root = Path.Combine(Path.GetTempPath(), DirectoryName);
        Directory.CreateDirectory(root);

        return Path.Combine(root, $"{Guid.NewGuid():N}.tmp");
    }

    // Opens a write-only buffered async stream to the specified path, creating a new file.
    public Stream OpenWrite(string path)
    {
        return new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            StreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    // FileOptions.DeleteOnClose ensures the temp file is removed when the response stream is disposed.
    public Stream OpenReadTemporary(string path)
    {
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            StreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
    }

    public void TryDelete(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, 
                "Failed to delete temp file '{Path}' due to an I/O error.", path);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, 
                "Failed to delete temp file '{Path}' due to insufficient permissions.", path);
        }
    }
}
