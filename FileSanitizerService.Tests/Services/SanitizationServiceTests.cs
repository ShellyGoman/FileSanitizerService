using System.Text;
using FileSanitizerService.Core.Exceptions;
using FileSanitizerService.Core.Interfaces;
using FileSanitizerService.Core.Models;
using FileSanitizerService.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FileSanitizerService.Tests.Services;

public class SanitizationServiceTests
{
    private readonly Mock<IFormatDetector> _detectorMock;
    private readonly Mock<IFileSanitizer> _sanitizerMock;
    private readonly Mock<IFileSanitizerResolver> _resolverMock;
    private readonly Mock<ITempFileProvider> _tempProviderMock;
    
    private readonly SanitizationService _sut;

    public SanitizationServiceTests()
    {
        _detectorMock = new Mock<IFormatDetector>();
        _sanitizerMock = new Mock<IFileSanitizer>();
        _resolverMock = new Mock<IFileSanitizerResolver>();
        _tempProviderMock = new Mock<ITempFileProvider>();

        _detectorMock
            .Setup(d => d.DetectAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FormatDetectionResult(FileFormat.Abc));

        _sanitizerMock
            .Setup(s => s.SanitizeAsync(It.IsAny<Stream>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _resolverMock
            .Setup(r => r.GetSanitizerByFormat(It.IsAny<FileFormat>()))
            .Returns(_sanitizerMock.Object);

        _tempProviderMock.Setup(t => t.CreatePath()).Returns("fake-path");
        _tempProviderMock.Setup(t => t.OpenWrite(It.IsAny<string>())).Returns(new MemoryStream());
        _tempProviderMock.Setup(t => t.OpenReadTemporary(It.IsAny<string>())).Returns(new MemoryStream());

        _sut = new SanitizationService(
            _detectorMock.Object,
            _resolverMock.Object,
            _tempProviderMock.Object,
            NullLogger<SanitizationService>.Instance);
    }
    
    [Fact]
    public async Task SanitizeToTempFileAsync_KnownFormat_SanitizerFound_ReturnsReadableStream()
    {
        var result = await _sut.SanitizeToTempFileAsync(StreamFrom("123\nA1C\n789"), "test.abc");

        Assert.True(result.CanRead);
    }

    [Fact]
    public async Task SanitizeToTempFileAsync_UnknownFormat_ThrowsUnsupportedFormatException()
    {
        _detectorMock
            .Setup(d => d.DetectAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FormatDetectionResult(FileFormat.Unknown));

        var ex = await Assert.ThrowsAsync<UnsupportedFormatException>(
            () => _sut.SanitizeToTempFileAsync(StreamFrom("XYZ\nA1C\n789")));

        Assert.Equal("Unsupported or unrecognized file format.", ex.Message);
    }

    [Fact]
    public async Task SanitizeToTempFileAsync_NoSanitizerRegistered_ThrowsSanitizerConfigurationException()
    {
        _resolverMock
            .Setup(r => r.GetSanitizerByFormat(It.IsAny<FileFormat>()))
            .Returns((IFileSanitizer?)null);

        var ex = await Assert.ThrowsAsync<SanitizerConfigurationException>(
            () => _sut.SanitizeToTempFileAsync(StreamFrom("123\nA1C\n789")));

        Assert.Contains("No sanitizer registered for detected format", ex.Message);
    }

    [Fact]
    public async Task SanitizeToTempFileAsync_SanitizerThrows_DeletesTempFile()
    {
        _sanitizerMock
            .Setup(s => s.SanitizeAsync(It.IsAny<Stream>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sanitizer boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SanitizeToTempFileAsync(StreamFrom("123\nA1C\n789")));

        _tempProviderMock.Verify(t => t.TryDelete("fake-path"), Times.Once());
    }

    [Fact]
    public async Task SanitizeToTempFileAsync_DetectorThrows_DeletesTempFile()
    {
        _detectorMock
            .Setup(d => d.DetectAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("detector boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SanitizeToTempFileAsync(StreamFrom("123\nA1C\n789")));

        _tempProviderMock.Verify(t => t.TryDelete("fake-path"), Times.Once());
    }

    private static MemoryStream StreamFrom(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }
}
