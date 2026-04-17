using FileSanitizerService.Core.Interfaces;

namespace FileSanitizerService.Infrastructure.Services;

public sealed class TempFileProvider : ITempFileProvider
{
    private const string DirectoryName = "FileSanitizerServiceTmp";
    private const int StreamBufferSize = 64 * 1024; // 64 kb

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

    // Opens a read-only buffered async stream that automatically deletes the file on close.
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

    // Attempts to delete the temp file at the given path, if failed logs the error
    public void TryDelete(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // TODO: Add logging for temp file cleanup failures.
        }
        catch (UnauthorizedAccessException)
        {
            // TODO: Add logging for temp file cleanup failures.
        }
    }
}
