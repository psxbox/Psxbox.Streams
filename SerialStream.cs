using Psxbox.Utils;
using System.IO.Ports;

namespace Psxbox.Streams;

public class SerialStream(string portName,
                          int baudRate = 9600,
                          Parity parity = Parity.None,
                          int dataBits = 8,
                          StopBits stopBits = StopBits.One,
                          int timeOut = 1000,
                          bool use7E1 = false) : BaseStream(timeOut, use7E1)
{
    private SerialPort? _serialPort;
    public override string Name => "Serial Stream";
    public override bool IsConnected => _serialPort?.IsOpen ?? false;
    private readonly byte[] _buffer = new byte[4096];

    public override void Close()
    {
        CloseAsync().GetAwaiter().GetResult();
    }

    public override async Task CloseAsync() => await Task.Run(() =>
    {
        if (_serialPort is not null && _serialPort.IsOpen)
            _serialPort?.Close();
    });

    public override void Connect()
    {
        ConnectAsync().GetAwaiter().GetResult();
    }

    public override async Task ConnectAsync() => await Task.Run(() =>
    {
        if (_serialPort is not null && _serialPort.IsOpen)
        {
            if (_serialPort.BaudRate == baudRate
                && _serialPort.Parity == parity
                && _serialPort.DataBits == dataBits
                && _serialPort.StopBits == stopBits)
            {
                return;
            }

            _serialPort.Close();
            _serialPort.Dispose();
        }

        _serialPort = new SerialPort
        {
            PortName = portName,
            BaudRate = baudRate,
            Parity = parity,
            DataBits = dataBits,
            StopBits = stopBits
        };

        _serialPort.DataReceived += (_, _) =>
        {
            var bytesToRead = _serialPort.BytesToRead;
            var bytesReaded = _serialPort.Read(_buffer, 0, bytesToRead);

            if (bytesReaded > 0)
            {
                for (var i = 0; i < bytesReaded; i++)
                {
                    _channel.Writer.TryWrite(_buffer[i]);
                }
            }
        };

        _serialPort.Open();
    });

    public override void Write(byte[] data)
    {
        WriteAsync(data).GetAwaiter().GetResult();
    }

    public override async Task WriteAsync(byte[] data) => await Task.Run(() =>
    {
        if (_serialPort is null || !_serialPort.IsOpen)
            throw new Exception("Serial port is not open.");

        if (Use7E1)
        {
            // data = data.ConvertTo7Bit('E');
            Converters.ConvertTo7Bit(data.AsSpan());
        }

        _serialPort.Write(data, 0, data.Length);
    });

    public override void Flush()
    {
        base.Flush();
        _serialPort?.DiscardOutBuffer();
        _serialPort?.DiscardInBuffer();
    }

    public override ValueTask DisposeAsync()
    {
        _serialPort?.Close();
        _serialPort?.Dispose();

        GC.SuppressFinalize(this);
        return base.DisposeAsync();
    }
}
