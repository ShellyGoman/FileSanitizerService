using System.Text;
using FileSanitizerService.Core.Detection;
using FileSanitizerService.Core.Models;

namespace FileSanitizerService.Tests.Detection;

public class HeaderDetectorTests
{
    private readonly HeaderDetector _sut = new();

    [Theory]
    [InlineData("123\n")]
    [InlineData("123\r\n")]
    public async Task DetectAsync_KnownAbcHeader_ReturnsAbcFormat(string header)
    {
        using var stream = StreamFrom(header + "A1C\n789");

        var result = await _sut.DetectAsync(stream);

        Assert.Equal(FileFormat.Abc, result.Format);
    }

    [Fact]
    public async Task DetectAsync_UnknownHeader_ReturnsUnknownFormat()
    {
        using var stream = StreamFrom("XYZ\nA1C\n789");

        var result = await _sut.DetectAsync(stream);

        Assert.Equal(FileFormat.Unknown, result.Format);
    }

    [Fact]
    public async Task DetectAsync_EmptyStream_ThrowsArgumentException()
    {
        using var stream = new MemoryStream();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.DetectAsync(stream));

        Assert.Equal("Uploaded file is empty.", ex.Message);
    }

    private static MemoryStream StreamFrom(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new MemoryStream(bytes);
    }
}
