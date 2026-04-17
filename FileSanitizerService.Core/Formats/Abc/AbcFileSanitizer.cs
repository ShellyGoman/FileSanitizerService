using System.Buffers;
using FileSanitizerService.Core.Interfaces;
using FileSanitizerService.Core.Models;

namespace FileSanitizerService.Core.Formats.Abc;

public sealed class AbcFileSanitizer : IFileSanitizer
{
    private const int ReadBufferSize = 4096; // 4kb
    private const int BlockSize = 3;
    private const byte BlockStartByte = (byte)'A';
    private const byte BlockEndByte = (byte)'C';
    private const byte BenignMinByte = (byte)'1';
    private const byte BenignMaxByte = (byte)'9';
    private const byte LineFeed = (byte)'\n';
    private const byte CarriageReturn = (byte)'\r';

    private static readonly byte[] HeaderBytes = "123\n"u8.ToArray();
    private static readonly byte[] ReplacementBlock = "A255C"u8.ToArray();
    private static readonly byte[] FooterBytes = "789"u8.ToArray();
    private static readonly byte[] NewLineByte = [LineFeed];

    public AbcFileSanitizer() { }

    public FileFormat SupportedFormat => FileFormat.Abc;

    // Streams the ABC file, sanitizes each data block,
    // and writes the result to a temp output
    public async Task SanitizeAsync(
        Stream input, Stream output, CancellationToken ct = default)
    {
        await output.WriteAsync(HeaderBytes, ct);

        var state = new ParserState();
        var currentChunkBytes = new byte[ReadBufferSize];
        var outputWriter = new ArrayBufferWriter<byte>(ReadBufferSize * 2);
        int currentChunkSize;

        while ((currentChunkSize = await input.ReadAsync(currentChunkBytes, ct)) > 0)
        {
            ProcessChunk(currentChunkBytes, currentChunkSize, state, outputWriter);
            
            await output.WriteAsync(outputWriter.WrittenMemory, ct);
            outputWriter.ResetWrittenCount();
        }

        EnsureCompletedState(state);
        await output.WriteAsync(FooterBytes, ct);
    }

    // Processes one read buffer chunk and rejects trailing bytes after a completed footer.
    private static void ProcessChunk(
        byte[] chunkBytes,
        int currentChunkSize,
        ParserState state,
        ArrayBufferWriter<byte> outputWriter)
    {
        if (state.IsFooterFullyRead)
            throw new InvalidOperationException("Unexpected bytes after footer '789'.");

        for (var index = 0; index < currentChunkSize; index++)
        {
            ProcessByte(chunkBytes[index], state, outputWriter);

            if (state.IsFooterFullyRead && index + 1 < currentChunkSize)
                throw new InvalidOperationException("Unexpected bytes after footer '789'.");
        }
    }

    // Routes each byte to footer, newline, or data-block handling.
    private static void ProcessByte(
        byte currentByte,
        ParserState state,
        ArrayBufferWriter<byte> outputWriter)
    {
        if (currentByte == CarriageReturn)
        {
            state.SeenCarriageReturn = true;
            return;
        }

        if (state.SeenCarriageReturn)
        {
            state.SeenCarriageReturn = false;

            // after \r we expect to see \n only
            if (currentByte != LineFeed)
                throw new InvalidOperationException(@"Invalid line ending: '\r' must be immediately followed by '\n'.");

            ProcessNewLine(state, outputWriter);
            return;
        }

        if (ShouldParseFooter(currentByte, state))
        {
            ProcessFooterByte(currentByte, state);
            return;
        }

        if (currentByte == LineFeed)
        {
            ProcessNewLine(state, outputWriter);
            return;
        }

        ProcessDataByte(currentByte, state, outputWriter);
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
    private static void ProcessNewLine(
        ParserState state,
        ArrayBufferWriter<byte> outputWriter)
    {
        if (state.CurrentBlockByteCount != 0)
        {
            throw new InvalidOperationException(
                $"Invalid ABC file: newline encountered after {state.CurrentBlockByteCount} of {BlockSize} bytes in a block (incomplete A*C block).");
        }

        outputWriter.Write(NewLineByte);
        state.ExpectingFooterStartAfterNewLine = true;
    }

    // Buffers data bytes into 3-byte blocks and emits each block when complete.
    private static void ProcessDataByte(
        byte currentByte,
        ParserState state,
        ArrayBufferWriter<byte> outputWriter)
    {
        state.ExpectingFooterStartAfterNewLine = false;
        state.CurrentBlockBytes[state.CurrentBlockByteCount++] = currentByte;

        // did not create yet the full block with the full length (3)
        // need to wait for the rest of the data
        if (state.CurrentBlockByteCount < state.CurrentBlockBytes.Length)
            return;

        WriteSanitizedBlock(state.CurrentBlockBytes, outputWriter);
        state.CurrentBlockByteCount = 0;
    }

    // Validates a full A*C block and writes either the original or replacement block.
    private static void WriteSanitizedBlock(
        byte[] blockBuffer,
        ArrayBufferWriter<byte> outputWriter)
    {
        if (blockBuffer[0] != BlockStartByte || blockBuffer[2] != BlockEndByte)
        {
            throw new InvalidOperationException(
                $"Invalid block: expected A<byte>C, got " +
                $"'{(char)blockBuffer[0]}' '{(char)blockBuffer[1]}' '{(char)blockBuffer[2]}'.");
        }

        byte dataByte = blockBuffer[1];
        outputWriter.Write(dataByte is >= BenignMinByte and <= BenignMaxByte
            ? blockBuffer.AsSpan(0, BlockSize)
            : ReplacementBlock);
    }

    // Verifies parsing ended in a valid terminal state and raises detailed structural errors.
    private static void EnsureCompletedState(ParserState state)
    {
        if (state.IsFooterFullyRead)
            return;

        if (state.SeenCarriageReturn)
            throw new InvalidOperationException(@"Invalid line ending: file ended with '\r' without trailing '\n'.");

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

        public bool ExpectingFooterStartAfterNewLine { get; set; } = true;

        public int FooterBytesMatchedCount { get; set; }

        public bool IsFooterFullyRead { get; set; }

        public bool SeenCarriageReturn { get; set; }
    }
}
