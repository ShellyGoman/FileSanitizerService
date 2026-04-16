using FileSanitizerService.Core.Interfaces;

namespace FileSanitizerService.Infrastructure.Services;

public sealed class TempFileProvider : ITempFileProvider
{
    private const string DirectoryName = "FileSanitizerServiceTmp";
    private const int StreamBufferSize = 64 * 1024;

    public string CreatePath()
    {
        var root = Path.Combine(Path.GetTempPath(), DirectoryName);
        Directory.CreateDirectory(root);

        return Path.Combine(root, $"{Guid.NewGuid():N}.tmp");
    }

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
