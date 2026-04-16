namespace FileSanitizerService.Core.Interfaces;

public interface ITempFileProvider
{
    string CreatePath();
    Stream OpenWrite(string path);
    Stream OpenReadTemporary(string path);
    void TryDelete(string path);
}
