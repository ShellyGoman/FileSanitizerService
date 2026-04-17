namespace FileSanitizerService.Core.Services;

internal sealed class PrefixReplayStream : Stream
{
    private readonly Stream _inner;
    private readonly byte[] _prefix;
    private int _prefixOffset;

    public PrefixReplayStream(byte[] prefix, Stream inner)
    {
        _prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset + count > buffer.Length)
            throw new ArgumentException("Offset and count exceed buffer size.");

        var totalRead = CopyFromPrefix(buffer.AsSpan(offset, count));
        if (totalRead == count)
            return totalRead;

        var innerRead = _inner.Read(buffer, offset + totalRead, count - totalRead);
        return totalRead + innerRead;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var totalRead = CopyFromPrefix(buffer.Span);
        if (totalRead == buffer.Length)
            return totalRead;

        var innerRead = await _inner.ReadAsync(buffer[totalRead..], cancellationToken);
        return totalRead + innerRead;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    private int CopyFromPrefix(Span<byte> destination)
    {
        var remainingPrefix = _prefix.Length - _prefixOffset;
        if (remainingPrefix <= 0 || destination.Length == 0)
            return 0;

        var bytesToCopy = Math.Min(remainingPrefix, destination.Length);
        _prefix.AsSpan(_prefixOffset, bytesToCopy).CopyTo(destination);
        _prefixOffset += bytesToCopy;
        return bytesToCopy;
    }
}
