# Psxbox.Streams

**Psxbox.Streams** - turli transport protokollari (MQTT, TCP, Serial) orqali ma'lumot oqimlarini boshqarish uchun yagona interfeys ta'minlovchi kutubxona.

## Xususiyatlari

- ?? **MQTT Stream** - MQTTnet asosida MQTT protokoli orqali ma'lumot almashinuvi
- ?? **TCP Stream** - SuperSimpleTcp yordamida TCP ulanishlarini boshqarish
- ?? **Serial Stream** - System.IO.Ports orqali serial port (RS-232/RS-485) aloqasi
- ?? **Yagona interfeys** - barcha transport turlari uchun `IStream` interfeysi
- ? **Async/Await** - to'liq asinxron operatsiyalar qo'llab-quvvatlanadi
- ?? **Timeout boshqaruvi** - operatsiyalar uchun sozlanuvchi timeout

## O'rnatish

```bash
dotnet add reference Shared/Psxbox.Streams/Psxbox.Streams.csproj
```

## Bog'liqliklar

- `MQTTnet` (v5.0.1.1416) - MQTT protokol implementatsiyasi
- `SuperSimpleTcp` (v3.0.20) - TCP mijoz/server funksiyalari
- `System.IO.Ports` (v10.0.0) - Serial port aloqasi
- `Psxbox.Utils` - yordamchi utilital��
- `Psxbox.MQTTClient` - MQTT mijoz boshqaruvi

## Foydalanish

### TCP Stream

```csharp
using Psxbox.Streams;

var tcpStream = new TcpStream("192.168.1.100:502");
await tcpStream.ConnectAsync();

byte[] request = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
await tcpStream.WriteAsync(request);

byte[] response = new byte[256];
int bytesRead = await tcpStream.ReadAsync(response);

await tcpStream.CloseAsync();
```

### Serial Stream

```csharp
var serialStream = new SerialStream("COM1,9600,8,N,1");
await serialStream.ConnectAsync();

await serialStream.WriteAsync(request);
byte[] response = await serialStream.ReadUntilAsync(new byte[] { 0x0D, 0x0A });

await serialStream.CloseAsync();
```

### MQTT Stream

```csharp
var mqttStream = new MqttStream("mqtt://broker.hivemq.com:1883");
await mqttStream.ConnectAsync();

// Subscribe qilish va ma'lumot olish
await mqttStream.WriteAsync(subscribeData);

await mqttStream.CloseAsync();
```

## IStream Interfeysi

Barcha stream turlari quyidagi umumiy interfeys orqali ishlaydi:

```csharp
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
    byte[] ReadUntil(byte searchByte);
    byte[] ReadUntil(byte[] searchBytes);
    Task<byte[]> ReadUntilAsync(byte[] searchBytes, CancellationToken ct = default);
    
    void Write(byte[] data);
    Task WriteAsync(byte[] data);
    void Flush();
}
```

## Arxitektura

```
BaseStream (abstract)
    ??? TcpStream       ? TCP mijoz ulanishlari
    ??? SerialStream    ? Serial port aloqasi (RS-232/RS-485)
    ??? MqttStream      ? MQTT pub/sub oqimlari
```

## MyGateway Loyihasida Foydalanish

Ushbu kutubxona MyGateway tizimida sanoat qurilmalari (PLC, RTU, smart meters) bilan turli protokollar orqali aloqa qilish uchun ishlatiladi:

- **Modbus RTU/ASCII** ? SerialStream
- **Modbus TCP** ? TcpStream  
- **MQTT telemetriya** ? MqttStream

## Litsenziya

MIT License


