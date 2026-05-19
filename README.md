# csharp-ubx

Minimal C# library for reading u-blox UBX frames over UART streams, with RXM-MEAS50 support for M10 modules.

Protocol reference: https://content.u-blox.com/sites/default/files/documents/u-blox-M10-SPG-5.30_InterfaceDescription_UBXDOC-304424225-20395.pdf

## Quick start

```csharp
using CSharpUbx;
using System.IO.Ports;

using var serialPort = new SerialPort("/dev/ttyUSB0", 38400);
serialPort.Open();

var reader = new UbxStreamReader(serialPort.BaseStream);
await foreach (var message in reader.ReadMessagesAsync())
{
    if (RxmMeas50Message.TryParse(message, out var meas50))
    {
        // Forward meas50.Payload (50 bytes) to your application / cloud pipeline.
        Console.WriteLine($"RXM-MEAS50 payload length: {meas50.Payload.Length}");
    }
}
```
