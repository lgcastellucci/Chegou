namespace Chegou.Api.Middleware
{
    internal sealed class ResponseCaptureStream : Stream
    {
        private readonly Stream _response;
        private readonly MemoryStream _capture = new();

        public ResponseCaptureStream(Stream response) => _response = response;

        public string GetBody()
        {
            _capture.Position = 0;
            using var reader = new StreamReader(_capture, leaveOpen: true);
            return reader.ReadToEnd();
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => _response.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _response.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _response.FlushAsync(cancellationToken);

        public override void Write(byte[] buffer, int offset, int count)
        {
            _response.Write(buffer, offset, count);
            _capture.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _response.WriteAsync(buffer, offset, count, cancellationToken);
            await _capture.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _response.WriteAsync(buffer, cancellationToken);
            await _capture.WriteAsync(buffer, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _capture.Dispose();
            base.Dispose(disposing);
        }
    }
}
