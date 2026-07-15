using System.IO;

namespace DeployKit.Gui.Helpers;

public class ProgressStream : Stream
{
    private readonly Stream _inner;
    private readonly long _length;
    private long _position;
    private int _lastReportedPct;
    private readonly Action<double> _onProgress;

    public ProgressStream(Stream inner, Action<double> onProgress)
    {
        _inner = inner;
        _length = inner.Length;
        _onProgress = onProgress;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    private void Report()
    {
        if (_length <= 0) return;
        var pct = (int)((double)_position / _length * 100);
        if (pct == _lastReportedPct) return;
        _lastReportedPct = pct;
        _onProgress(pct);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        _position += read;
        Report();
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = _inner.Read(buffer);
        _position += read;
        Report();
        return read;
    }

    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
