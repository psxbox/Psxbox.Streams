using Psxbox.Utils;
using System.Net.Sockets;

namespace Psxbox.Streams
{
    /// <summary>
    /// TCP mijoz stream'i.
    /// Ulanish va qabul qilish to'liq asinxron (<see cref="TcpClient.ConnectAsync(string, int, CancellationToken)"/>):
    /// avvalgi SuperSimpleTcp asosidagi sinxron <c>Connect()</c> (BeginConnect + WaitOne) yuklama ostida
    /// thread pool'ni band qilib, localhost'ga ulanishda ham "Timeout connecting" xatolarini keltirib chiqarardi.
    /// </summary>
    public class TcpStream : BaseStream
    {
        private readonly string _host;
        private readonly int _port;
        private readonly SemaphoreSlim _connectLock = new(1, 1);

        private TcpClient? _client;
        private NetworkStream? _network;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveLoop;

        /// <summary>Ulanish kutish vaqti (ms).</summary>
        public int ConnectTimeoutMs { get; set; } = 5000;

        public override string Name => "TCP Stream";

        public override bool IsConnected => _client?.Connected == true && _network is not null;

        public TcpStream(string host, int port, int timeOut, bool use7E1 = false) : base(timeOut, use7E1)
        {
            _host = host;
            _port = port;
        }

        public override void Connect() => ConnectAsync().GetAwaiter().GetResult();

        public override Task ConnectAsync() => ConnectAsync(CancellationToken.None);

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (IsConnected) return;

            await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (IsConnected) return;
                await DisconnectCoreAsync().ConfigureAwait(false);

                var client = new TcpClient { NoDelay = true };
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(ConnectTimeoutMs);
                try
                {
                    await client.ConnectAsync(_host, _port, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    client.Dispose();
                    throw new TimeoutException($"Timeout connecting to {_host}:{_port}");
                }
                catch
                {
                    client.Dispose();
                    throw;
                }

                _client = client;
                _network = client.GetStream();
                _receiveCts = new CancellationTokenSource();
                _receiveLoop = ReceiveLoopAsync(_network, _receiveCts.Token);
            }
            finally
            {
                _connectLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(NetworkStream network, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var read = await network.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0) break; // masofaviy tomon ulanishni yopdi
                    _channel.Writer.TryWrite(buffer.AsSpan(0, read).ToArray());
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        }

        public override void Write(byte[] data) => WriteAsync(data).GetAwaiter().GetResult();

        public override async Task WriteAsync(byte[] data)
        {
            await ConnectAsync().ConfigureAwait(false);
            if (Use7E1) Converters.ConvertTo7Bit(data.AsSpan());
            await _network!.WriteAsync(data).ConfigureAwait(false);
        }

        public override void Close()
        {
            DisconnectCoreAsync().GetAwaiter().GetResult();
            Flush();
        }

        public override async Task CloseAsync()
        {
            await _connectLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await DisconnectCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                _connectLock.Release();
            }
            Flush();
        }

        private async Task DisconnectCoreAsync()
        {
            var cts = _receiveCts;
            var loop = _receiveLoop;
            var client = _client;
            _receiveCts = null;
            _receiveLoop = null;
            _network = null;
            _client = null;

            if (cts is not null)
            {
                await cts.CancelAsync().ConfigureAwait(false);
            }
            if (client is not null)
            {
                // Abortive close (linger 0): mijoz tomonda TIME_WAIT qolmaydi. Har bir o'qish yangi
                // ulanish ochadi; oddiy yopishda sekundiga yuzlab ulanish ~120 s TIME_WAIT'da qolib,
                // ephemeral portlar tugaydi (WSAEADDRINUSE). Yopish o'qish tugagach bo'lgani uchun
                // yo'qoladigan ma'lumot yo'q.
                // Socket.Close(0) to'g'ridan-to'g'ri chaqiriladi: TcpClient.Dispose avval NetworkStream'ni
                // yopadi, u esa Shutdown(Both) (FIN) yuboradi va linger sozlamasi e'tiborsiz qoladi.
                try
                {
                    client.Client.LingerState = new LingerOption(true, 0);
                    client.Client.Close(0);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
                client.Dispose(); // kutilayotgan ReadAsync'ni ham bo'shatadi
            }
            if (loop is not null)
            {
                try { await loop.ConfigureAwait(false); } catch { /* loop o'z xatolarini yutadi */ }
            }
            cts?.Dispose();
        }

        public override async ValueTask DisposeAsync()
        {
            await DisconnectCoreAsync().ConfigureAwait(false);
            _connectLock.Dispose();
            GC.SuppressFinalize(this);
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
