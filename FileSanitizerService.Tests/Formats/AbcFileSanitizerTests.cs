using System.Text;
using FileSanitizerService.Core.Formats.Abc;
using FileSanitizerService.Core.Interfaces;

namespace FileSanitizerService.Tests.Formats;

public class AbcFileSanitizerTests
{
    private readonly IFileSanitizer _sut;

    public AbcFileSanitizerTests()
    {
        _sut = new AbcFileSanitizer();
    }

    // -------------------------------------------------------------------------
    // Happy path — benign passthrough
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SanitizeAsync_SingleBenignBlock_OutputMatchesExpected()
    {
        var result = await SanitizeToStringAsync("A1C\n789");

        Assert.Equal("123\nA1C\n789", result);
    }

    [Theory]
    [InlineData('1')]
    [InlineData('2')]
    [InlineData('3')]
    public async Task SanitizeAsync_BenignDataBytes_PassThrough(char dataByte)
    {
        var input = $"A{dataByte}C\n789";

        var result = await SanitizeToStringAsync(input);

        Assert.Equal($"123\nA{dataByte}C\n789", result);
    }

    [Fact]
    public async Task SanitizeAsync_MultipleBlocksOnSameLine_OutputMatchesExpected()
    {
        var result = await SanitizeToStringAsync("A1CA3CA7C\n789");

        Assert.Equal("123\nA1CA3CA7C\n789", result);
    }

    [Fact]
    public async Task SanitizeAsync_MultipleDataLines_OutputMatchesExpected()
    {
        var result = await SanitizeToStringAsync("A1CA3CA7C\nA2C\nA5C\n789");

        Assert.Equal("123\nA1CA3CA7C\nA2C\nA5C\n789", result);
    }

    [Fact]
    public async Task SanitizeAsync_EmptyDataSection_OutputIsHeaderAndFooterOnly()
    {
        var result = await SanitizeToStringAsync("\n789");

        Assert.Equal("123\n\n789", result);
    }

    // -------------------------------------------------------------------------
    // Sanitization — malicious block replacement
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SanitizeAsync_SingleMaliciousBlock_IsReplacedWithA255C()
    {
        var result = await SanitizeToStringAsync("AFC\n789");

        Assert.Equal("123\nA255C\n789", result);
    }

    [Fact]
    public async Task SanitizeAsync_MultipleMaliciousBlocks_AllAreReplaced()
    {
        var result = await SanitizeToStringAsync("AFCAXC\n789");

        Assert.Equal("123\nA255CA255C\n789", result);
    }

    [Fact]
    public async Task SanitizeAsync_MixOfBenignAndMalicious_OnlyMaliciousIsReplaced()
    {
        var result = await SanitizeToStringAsync("A1CAFC\n789");

        Assert.Equal("123\nA1CA255C\n789", result);
    }

    [Theory]
    [InlineData((byte)'0')]
    [InlineData((byte)'A')]
    public async Task SanitizeAsync_VariousMaliciousDataBytes_AreAllReplaced(byte maliciousByte)
    {
        var input = new byte[] { (byte)'A', maliciousByte, (byte)'C', (byte)'\n', (byte)'7', (byte)'8', (byte)'9' };

        var result = await SanitizeBytesToStringAsync(input);

        Assert.Equal("123\nA255C\n789", result);
    }

    // -------------------------------------------------------------------------
    // Line ending handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SanitizeAsync_WindowsCrLfLineEndings_OutputUsesLfOnly()
    {
        var result = await SanitizeToStringAsync("A1C\r\nA3C\r\n789");

        Assert.Equal("123\nA1C\nA3C\n789", result);
    }

    [Fact]
    public async Task SanitizeAsync_BlockDoesNotStartWithA_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SanitizeToStringAsync("B1C\n789"));

        Assert.Contains("Invalid block", ex.Message);
    }

    [Fact]
    public async Task SanitizeAsync_BlockDoesNotEndWithC_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SanitizeToStringAsync("A1B\n789"));

        Assert.Contains("Invalid block", ex.Message);
    }

    [Fact]
    public async Task SanitizeAsync_NewlineEncounteredMidBlock_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SanitizeToStringAsync("A1\nC\n789"));

        Assert.Contains("incomplete A*C block", ex.Message);
    }

    [Fact]
    public async Task SanitizeAsync_MissingFooterEntirely_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SanitizeToStringAsync("A1C\n"));

        Assert.Contains("Missing footer", ex.Message);
    }

    [Theory]
    [InlineData("7")]
    [InlineData("78")]
    public async Task SanitizeAsync_PartialFooter_ThrowsInvalidOperationException(string partialFooter)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SanitizeToStringAsync($"A1C\n{partialFooter}"));

        Assert.Contains("Incomplete footer", ex.Message);
    }

    [Fact]
    public async Task SanitizeAsync_TrailingBytesAfterFooter_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SanitizeToStringAsync("A1C\n789extra"));

        Assert.Contains("Unexpected bytes after footer", ex.Message);
    }

    [Fact]
    public async Task SanitizeAsync_FileTruncatedMidBlock_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SanitizeToStringAsync("A1"));

        Assert.Contains("File ended mid-block", ex.Message);
    }

    [Fact]
    public async Task SanitizeAsync_CarriageReturnNotFollowedByLineFeed_ThrowsInvalidOperationException()
    {
        var input = "A1C\rA3C\n789"u8.ToArray();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SanitizeBytesToStringAsync(input));

        Assert.Contains(@"'\r' must be immediately followed by '\n'", ex.Message);
    }

    [Fact]
    public async Task SanitizeAsync_FileEndsWithBareCarriageReturn_ThrowsInvalidOperationException()
    {
        var input = "A1C\r"u8.ToArray();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SanitizeBytesToStringAsync(input));

        Assert.Contains(@"'\r'", ex.Message);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // Converts a string to UTF-8 bytes and passes it to SanitizeBytesToStringAsync.
    private async Task<string> SanitizeToStringAsync(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        return await SanitizeBytesToStringAsync(inputBytes);
    }

    // Sends raw bytes through the sanitizer and returns the sanitized output as a UTF-8 string.
    private async Task<string> SanitizeBytesToStringAsync(byte[] input)
    {
        await using var inputStream = new MemoryStream(input);
        await using var outputStream = new MemoryStream();
        await _sut.SanitizeAsync(inputStream, outputStream);
        return Encoding.UTF8.GetString(outputStream.ToArray());
    }
}
