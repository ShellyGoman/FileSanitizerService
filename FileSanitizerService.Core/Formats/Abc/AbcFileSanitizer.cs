using FileSanitizerService.Core.Interfaces;
using FileSanitizerService.Core.Models;

namespace FileSanitizerService.Core.Formats.Abc;

public sealed class AbcFileSanitizer : IFileSanitizer
{
    private const int ReadBufferSize = 4096;
    private const int BlockSize = 3;
    private const byte BlockStartByte = (byte)'A';
    private const byte BlockEndByte = (byte)'C';
    private const byte BenignMinByte = (byte)'1';
    private const byte BenignMaxByte = (byte)'9';

    private static readonly byte[] HeaderBytes = "123\n"u8.ToArray();
    private static readonly byte[] ReplacementBlock = "A255C"u8.ToArray();
    private static readonly byte[] FooterBytes = "789"u8.ToArray();
    private static readonly byte[] NewLineByte = [(byte)'\n'];

    public FileFormat SupportedFormat => FileFormat.Abc;

    public async Task SanitizeAsync(
        Stream input, Stream output, CancellationToken ct = default)
    {
        await ValidateAndConsumeHeaderAsync(input, ct);
        await output.WriteAsync(HeaderBytes, ct);

        var state = new ParserState();
        var readBuf = new byte[ReadBufferSize];
        int bytesRead;
        
        while ((bytesRead = await input.ReadAsync(readBuf, ct)) > 0)
        {
            await ProcessChunkAsync(readBuf, bytesRead, state, output, ct);
        }

        EnsureCompletedState(state);
        await output.WriteAsync(FooterBytes, ct);
    }
    
    // Reads and validates the ABC header without relying on stream seek support.
    private static async Task ValidateAndConsumeHeaderAsync(Stream input, CancellationToken ct)
    {
        var headerBuffer = new byte[HeaderBytes.Length];
        var offset = 0;

        while (offset < headerBuffer.Length)
        {
            var bytesRead = await input.ReadAsync(headerBuffer.AsMemory(offset, headerBuffer.Length - offset), ct);
            if (bytesRead == 0)
                throw new InvalidOperationException("Missing header: file must start with '123\\n'.");

            offset += bytesRead;
        }

        if (!headerBuffer.AsSpan().SequenceEqual(HeaderBytes))
            throw new InvalidOperationException("Invalid header: file must start with '123\\n'.");
    }

    // Processes one read buffer chunk and rejects trailing bytes after a completed footer.
    private static async Task ProcessChunkAsync(
        byte[] buffer,
        int bytesRead,
        ParserState state,
        Stream output,
        CancellationToken ct)
    {
        if (state.IsFooterFullyRead)
            throw new InvalidOperationException("Unexpected bytes after footer '789'.");

        for (var index = 0; index < bytesRead; index++)
        {
            await ProcessByteAsync(buffer[index], state, output, ct);

            if (state.IsFooterFullyRead && index + 1 < bytesRead)
                throw new InvalidOperationException("Unexpected bytes after footer '789'.");
        }
    }

    // Routes each byte to footer, newline, or data-block handling.
    private static async Task ProcessByteAsync(
        byte currentByte,
        ParserState state,
        Stream output,
        CancellationToken ct)
    {
        if (ShouldParseFooter(currentByte, state))
        {
            ProcessFooterByte(currentByte, state);
            return;
        }

        if (currentByte == (byte)'\n')
        {
            await ProcessNewLineAsync(state, output, ct);
            return;
        }

        await ProcessDataByteAsync(currentByte, state, output, ct);
    }

    // Decides whether the current byte should be interpreted as part of the footer.
    private static bool ShouldParseFooter(byte currentByte, ParserState state)
    {
        return state.FooterBytesMatchedCount > 0
               || (state.ExpectingFooterStartAfterNewLine && currentByte == FooterBytes[0]);
    }

    // Validates and tracks sequential footer bytes until the footer is complete.
    private static void ProcessFooterByte(byte currentByte, ParserState state)
    {
        state.ExpectingFooterStartAfterNewLine = false;

        byte expected = FooterBytes[state.FooterBytesMatchedCount];
        if (currentByte != expected)
        {
            throw new InvalidOperationException(
                $"Invalid footer: expected '{(char)expected}', got '{(char)currentByte}'.");
        }

        state.FooterBytesMatchedCount++;
        if (state.FooterBytesMatchedCount == FooterBytes.Length)
            state.IsFooterFullyRead = true;
    }

    // Validates block boundaries on newline, writes it through, and enables footer detection.
    private static async Task ProcessNewLineAsync(
        ParserState state,
        Stream output,
        CancellationToken ct)
    {
        if (state.CurrentBlockByteCount != 0)
        {
            throw new InvalidOperationException(
                "Invalid ABC file: newline encountered inside a block.");
        }

        await output.WriteAsync(NewLineByte, ct);
        state.ExpectingFooterStartAfterNewLine = true;
    }

    // Buffers data bytes into 3-byte blocks and emits each block when complete.
    private static async Task ProcessDataByteAsync(
        byte currentByte,
        ParserState state,
        Stream output,
        CancellationToken ct)
    {
        state.ExpectingFooterStartAfterNewLine = false;
        state.CurrentBlockBytes[state.CurrentBlockByteCount++] = currentByte;

        if (state.CurrentBlockByteCount < state.CurrentBlockBytes.Length)
            return;

        await WriteSanitizedBlockAsync(state.CurrentBlockBytes, output, ct);
        state.CurrentBlockByteCount = 0;
    }

    // Validates a full A*C block and writes either the original or replacement block.
    private static async Task WriteSanitizedBlockAsync(
        byte[] blockBuffer,
        Stream output,
        CancellationToken ct)
    {
        if (blockBuffer[0] != BlockStartByte || blockBuffer[2] != BlockEndByte)
        {
            throw new InvalidOperationException(
                $"Invalid block: expected A<byte>C, got " +
                $"0x{blockBuffer[0]:X2} 0x{blockBuffer[1]:X2} 0x{blockBuffer[2]:X2}.");
        }

        byte dataByte = blockBuffer[1];
        if (dataByte is >= BenignMinByte and <= BenignMaxByte)
            await output.WriteAsync(blockBuffer.AsMemory(0, BlockSize), ct);
        else
            await output.WriteAsync(ReplacementBlock, ct);
    }

    // Verifies parsing ended in a valid terminal state and raises detailed structural errors.
    private static void EnsureCompletedState(ParserState state)
    {
        if (state.IsFooterFullyRead)
            return;

        if (state.FooterBytesMatchedCount > 0)
        {
            throw new InvalidOperationException(
                $"Incomplete footer: file ended after partial footer (read {state.FooterBytesMatchedCount}/{FooterBytes.Length} bytes).");
        }

        if (state.CurrentBlockByteCount > 0)
            throw new InvalidOperationException("File ended mid-block: incomplete A*C block.");

        throw new InvalidOperationException("Missing footer: file must end with '\\n789'.");
    }

    // Keeps incremental parser state so streaming works across
    // chunk boundaries without buffering the whole files.
    private sealed class ParserState
    {
        public byte[] CurrentBlockBytes { get; } = new byte[BlockSize];

        public int CurrentBlockByteCount { get; set; }

        public bool ExpectingFooterStartAfterNewLine { get; set; }

        public int FooterBytesMatchedCount { get; set; }

        public bool IsFooterFullyRead { get; set; }
    }
}
