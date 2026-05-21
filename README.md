# csharp-ubx

Single-file C# library for sending and receiving UBX protocol frames over `SerialPort`, including event-driven message callbacks.  
Drop `CSharpUbx/UbxProtocol.cs` into your project — no other files needed.

Compatible with .NET Framework 4.7.2 and .NET 8.0.

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

var client = new UbxClient(serialPort);

// Configure GPS-only constellation (waits for ACK after each command):
await M10Configurator.ConfigureGpsOnlyAsync(client);

// Trigger cold-start reset when needed (no ACK — device resets immediately):
await M10Configurator.TriggerColdStartResetAsync(client);

// Receive parsed UBX messages as they arrive:
client.MessageReceived += (sender, e) =>
{
    Console.WriteLine("Class=0x{0:X2} Id=0x{1:X2} Payload={2} bytes",
        e.Message.MessageClass,
        e.Message.MessageId,
        e.Message.Payload.Length);
};
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
