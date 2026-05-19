# csharp-ubx

Single-file C# library for sending and receiving UBX protocol frames over any UART stream.  
Drop `CSharpUbx/UbxProtocol.cs` into your project — no other files needed.

Compatible with .NET Framework 4.7.2.

Protocol reference: https://content.u-blox.com/sites/default/files/documents/u-blox-M10-SPG-5.30_InterfaceDescription_UBXDOC-304424225-20395.pdf

## Quick start

```csharp
using CSharpUbx;
using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

var serialPort = new SerialPort("COM3", 38400);
serialPort.Open();

var client = new UbxClient(serialPort.BaseStream);

// Configure GPS-only constellation (waits for ACK after each command):
await M10Configurator.ConfigureGpsOnlyAsync(client);

// Trigger cold-start reset when needed (no ACK — device resets immediately):
await M10Configurator.TriggerColdStartResetAsync(client);

// Read one chunk and parse all valid UBX messages from it:
var messages = await client.ReceiveMessagesAsync(1024, CancellationToken.None);
foreach (var message in messages)
{
    Console.WriteLine("Class=0x{0:X2} Id=0x{1:X2} Payload={2} bytes",
        message.MessageClass,
        message.MessageId,
        message.Payload.Length);
}
```

## Sending arbitrary messages

```csharp
// Send any UBX message and wait for ACK/NAK (use CancellationToken for timeout):
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
bool acked = await client.SendConfigAsync(
    UbxClass.Cfg,
    (byte)CfgMessageId.Valset,
    payload,
    cts.Token);

// Or fire-and-forget (no ACK wait):
await client.SendAsync(UbxClass.Cfg, (byte)CfgMessageId.Rst, payload);
```

## Included M10 configuration commands

- GPS enable / Galileo, BDS, GLONASS disable (`M10Commands.*`)
- UBX-CFG-RST cold start (`M10Commands.CfgRstColdStart`)
