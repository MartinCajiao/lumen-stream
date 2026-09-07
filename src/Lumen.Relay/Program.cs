using Lumen.Core.Wan.Relay;

// Lumen relay server. Deploy on any cheap/free cloud VM with one public TCP port.
//   dotnet run --project src/Lumen.Relay -- 47991 my-shared-secret
// Then in Lumen (Ajustes) set: relay = host:47991:my-shared-secret
// The host PC connects out to this relay (works behind CGNAT); the client PC does too.
// No overlay app on the user's PCs, no third-party account.
var cmd = Environment.GetCommandLineArgs();
var port = cmd.Length > 1 && int.TryParse(cmd[1], out var p) ? p : 47991;
var secret = cmd.Length > 2 ? cmd[2] : "";

Console.WriteLine($"Lumen relay listening on TCP {port} (secret {(string.IsNullOrEmpty(secret) ? "off" : "on")})");
using var relay = new RelayServer(port, secret);
relay.Start();

Console.WriteLine("Press Ctrl+C to stop.");
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
try { await Task.Delay(Timeout.Infinite, cts.Token); }
catch (OperationCanceledException) { }

relay.Stop();
Console.WriteLine("Stopped.");
