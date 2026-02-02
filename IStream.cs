namespace Psxbox.Streams
{
    public interface IStream : IAsyncDisposable
    {
        int OperationTimeout { get; set; }
        bool Available { get; }
        string Name { get; }

        bool IsConnected { get; }
        void Connect();
        Task ConnectAsync();
        
        void Close();
        Task CloseAsync();
        
        byte Read();
        int Read(byte[] data);
        ValueTask<byte> ReadAsync(CancellationToken ct = default);
        ValueTask<int> ReadAsync(byte[] data, CancellationToken ct = default);
        
        byte[] ReadUntil(string str);
        byte[] ReadUntil(char ch);
        byte[] ReadUntil(byte searchByte);
        byte[] ReadUntil(byte[] searchBytes);
        
        Task<byte[]> ReadUntilAsync(string str);
        Task<byte[]> ReadUntilAsync(char ch);
        Task<byte[]> ReadUntilAsync(byte searchByte);
        Task<byte[]> ReadUntilAsync(byte[] searchBytes, CancellationToken ct = default);
        
        void Write(byte[] data);
        Task WriteAsync(byte[] data);
        
        void Flush();
    }
}