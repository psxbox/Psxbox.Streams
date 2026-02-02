using Psxbox.Utils;
using System.Buffers;
using System.Text;
using System.Threading.Channels;

namespace Psxbox.Streams
{
    public abstract class BaseStream(int operationTimeout, bool use7E1 = false) : IStream
    {
        public int OperationTimeout { get => OperationTimeoutMs; set => OperationTimeoutMs = value; }

        protected readonly Channel<byte> _channel = Channel.CreateUnbounded<byte>();

        protected int OperationTimeoutMs = operationTimeout;
        protected bool Use7E1 = use7E1;

        public abstract string Name { get; }
        public abstract bool IsConnected { get; }
        public bool Available => _channel.Reader.TryPeek(out _);

        public virtual void Flush()
        {
            while (_channel.Reader.TryRead(out _)) { }
        }

        // ====================== SINXRON READ ======================
        public virtual int Read(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            return ReadAsync(data, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        public virtual byte Read()
            => ReadAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();

        // ====================== ASINXRON READ ======================
        public virtual async ValueTask<int> ReadAsync(byte[] data, CancellationToken ct = default)
        {
            var response = await ReadFromDataAsync(data.Length, TimeSpan.FromMilliseconds(OperationTimeoutMs), ct)
                .ConfigureAwait(false);

            if (Use7E1)
            {
                // response = response.ConvertTo7Bit();
                Converters.ConvertTo7Bit(response.AsSpan());
            }

            Array.Copy(response, 0, data, 0, response.Length);
            return response.Length;
        }

        public virtual async ValueTask<byte> ReadAsync(CancellationToken ct = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                cts.CancelAfter(OperationTimeoutMs);

                var b = await _channel.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
                return Use7E1 ? Converters.ConvertTo7Bit(b) : b;
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Timeout while reading a byte from stream.");
            }
        }

        // ====================== ICHKI O'QISH (ValueTask<byte[]>) ======================
        private async ValueTask<byte[]> ReadFromDataAsync(int length, TimeSpan timeout, CancellationToken ct = default)
        {
            if (length <= 0)
                return [];

            // Optimallashtirish: bitta bayt uchun to'g'ridan-to'g'ri ReadAsync
            if (length == 1)
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linkedCts.CancelAfter(timeout);
                var b = await _channel.Reader.ReadAsync(linkedCts.Token).ConfigureAwait(false);
                return [b];
            }

            var buffer = ArrayPool<byte>.Shared.Rent(length);
            var received = 0;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            try
            {
                while (received < length)
                {
                    await _channel.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false);

                    while (received < length && _channel.Reader.TryRead(out var b))
                    {
                        buffer[received++] = b;
                    }
                }

                return buffer.AsSpan(0, received).ToArray();
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Timeout: {received}/{length} bayt o'qildi.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // ====================== READ UNTIL ======================
        public virtual byte[] ReadUntil(string str) => ReadUntilAsync(str).GetAwaiter().GetResult();
        public virtual Task<byte[]> ReadUntilAsync(string str)
            => ReadUntilAsync(Encoding.ASCII.GetBytes(str));

        public virtual byte[] ReadUntil(char ch) => ReadUntilAsync(ch).GetAwaiter().GetResult();
        public virtual Task<byte[]> ReadUntilAsync(char ch)
            => ReadUntilAsync(Encoding.ASCII.GetBytes([ch]));

        public virtual byte[] ReadUntil(byte searchByte) => ReadUntilAsync([searchByte]).GetAwaiter().GetResult();
        public virtual Task<byte[]> ReadUntilAsync(byte searchByte)
            => ReadUntilAsync([searchByte]);

        public virtual byte[] ReadUntil(byte[] searchBytes) => ReadUntilAsync(searchBytes).GetAwaiter().GetResult();

        public virtual async Task<byte[]> ReadUntilAsync(byte[] searchBytes, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(searchBytes);
            if (searchBytes.Length == 0)
                throw new ArgumentException("searchBytes cannot be empty", nameof(searchBytes));

            const int InitialBufferSize = 256;
            var buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
            var position = 0;
            var matchIndex = 0;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(OperationTimeoutMs));

            try
            {
                while (true)
                {
                    var b = await ReadAsync(cts.Token).ConfigureAwait(false);

                    // Buffer hajmini kengaytirish kerak bo'lsa
                    if (position >= buffer.Length)
                    {
                        var newBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                        Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);
                        ArrayPool<byte>.Shared.Return(buffer);
                        buffer = newBuffer;
                    }

                    buffer[position++] = b;

                    // Pattern matching
                    if (b == searchBytes[matchIndex])
                    {
                        matchIndex++;
                        if (matchIndex == searchBytes.Length)
                        {
                            return buffer.AsSpan(0, position).ToArray();
                        }
                    }
                    else
                    {
                        matchIndex = b == searchBytes[0] ? 1 : 0;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // ====================== YOZISH ======================
        public abstract void Write(byte[] data);
        public abstract Task WriteAsync(byte[] data);

        // ====================== ULANISH ======================
        public abstract void Connect();
        public abstract Task ConnectAsync();
        public abstract void Close();
        public abstract Task CloseAsync();

        // ====================== DISPOSE ======================
        public virtual ValueTask DisposeAsync()
        {
            _channel.Writer.TryComplete();
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}