using Psxbox.Utils;
using SuperSimpleTcp;

namespace Psxbox.Streams
{
    public class TcpStream : BaseStream
    {
        readonly SimpleTcpClient _client;

        public override string Name => "TCP Stream";

        public override bool IsConnected => _client.IsConnected;

        public TcpStream(string host, int port, int timeOut, bool use7E1 = false) : base(timeOut, use7E1)
        {
            _client = new SimpleTcpClient(host, port);
            _client.Events.DataReceived += Events_DataReceived;
        }

        private void Events_DataReceived(object? sender, DataReceivedEventArgs e)
        {
            foreach (var item in e.Data)
            {
                // _data.Enqueue(item);
                _channel.Writer.TryWrite(item);
            }
        }

        public override void Write(byte[] data)
        {
            if (!_client.IsConnected) _client.Connect();
            if (Use7E1) Converters.ConvertTo7Bit(data.AsSpan());
            // _client.Send(Use7E1 ? data.ConvertTo7Bit('E') : data);
            _client.Send(data);
        }

        public override async Task WriteAsync(byte[] data)
        {
            await ConnectAsync();
            if (Use7E1) Converters.ConvertTo7Bit(data.AsSpan());
            // await _client.SendAsync(Use7E1 ? data.ConvertTo7Bit('E') : data);
            await _client.SendAsync(data);
        }

        public override ValueTask DisposeAsync()
        {
            _client.Dispose();
            _client.Events.DataReceived -= Events_DataReceived;
            GC.SuppressFinalize(this);
            return base.DisposeAsync();
        }

        public override void Connect()
        {
            if (!_client.IsConnected)
                _client.Connect();
        }

        public override void Close()
        {
            _client.Disconnect();
            Flush();
        }

        public override Task ConnectAsync()
        {
            if (!_client.IsConnected)
                _client.Connect();

            return Task.CompletedTask;
        }

        public override async Task CloseAsync()
        {
            await _client.DisconnectAsync();
            Flush();
        }
    }
}
