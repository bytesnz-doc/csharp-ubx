# csharp-ubx

Single-file C# library for sending and receiving UBX protocol frames over any UART stream.  
Drop `CSharpUbx/UbxProtocol.cs` into your project — no other files needed.

Protocol reference: https://content.u-blox.com/sites/default/files/documents/u-blox-M10-SPG-5.30_InterfaceDescription_UBXDOC-304424225-20395.pdf

## Quick start

```csharp
using CSharpUbx;
using System.IO.Ports;

using var serialPort = new SerialPort("/dev/ttyUSB0", 38400);
serialPort.Open();

var client = new UbxClient(serialPort.BaseStream);

// Configure GPS-only constellation (waits for ACK after each command):
await M10Configurator.ConfigureGpsOnlyAsync(client);

// Trigger cold-start reset when needed (no ACK — device resets immediately):
await M10Configurator.TriggerColdStartResetAsync(client);

// Stream all incoming UBX messages (checksum verified, payload extracted):
await foreach (var message in client.ReadMessagesAsync())
{
    Console.WriteLine($"Class=0x{message.MessageClass:X2} Id=0x{message.MessageId:X2} " +
                      $"Payload={message.Payload.Length} bytes");
}
```

## Sending arbitrary messages

```csharp
// Send any UBX message and wait for ACK/NAK (use a CancellationToken for timeout):
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
bool acked = await client.SendConfigAsync(UbxClass.Cfg, (byte)CfgMessageId.Valset,
                                          payload, cts.Token);

// Or fire-and-forget (no ACK wait):
await client.SendAsync(UbxClass.Cfg, (byte)CfgMessageId.Rst, payload);
```

## Included M10 configuration commands

- GPS enable / Galileo, BDS, GLONASS disable (`M10Commands.*`)
- UBX-CFG-RST cold start (`M10Commands.CfgRstColdStart`)
