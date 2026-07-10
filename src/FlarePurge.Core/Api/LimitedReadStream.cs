using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FlarePurge.Core.Api;

/// <summary>
/// Read-only pass-through stream that throws <see cref="IOException"/> once more
/// than <c>maxBytes</c> have been read. Guards the JSON deserialiser against an
/// unbounded response body (audit N3): the API is streamed with
/// <see cref="System.Net.Http.HttpCompletionOption.ResponseHeadersRead"/>, so
/// without this a hostile or defective payload could stream indefinitely into the
/// parser. The cap is generous (see <see cref="ApiClient"/>); real Cloudflare
/// responses are orders of magnitude smaller.
/// </summary>
internal sealed class LimitedReadStream(Stream inner, long maxBytes) : Stream
{
    private readonly Stream _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly long _maxBytes = maxBytes;
    private long _read;

    private void Account(int justRead)
    {
        _read += justRead;
        if (_read > _maxBytes)
            throw new IOException($"Response exceeded the {_maxBytes}-byte limit.");
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = _inner.Read(buffer, offset, count);
        Account(n);
        return n;
    }

    public override int Read(Span<byte> buffer)
    {
        var n = _inner.Read(buffer);
        Account(n);
        return n;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var n = await _inner.ReadAsync(buffer, ct).ConfigureAwait(false);
        Account(n);
        return n;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        var n = await _inner.ReadAsync(buffer.AsMemory(offset, count), ct).ConfigureAwait(false);
        Account(n);
        return n;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;
    public override long Position { get => _read; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}
