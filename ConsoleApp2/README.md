# ConsoleApp2 TCP NMEA Broadcaster

This sample console app listens for TCP connections and repeatedly sends the same NMEA sentence to every connected client.

## Run locally

```powershell
cd C:\dev\test_tcp\ConsoleApp2

dotnet run --project .\ConsoleApp2\ConsoleApp2.csproj -- 5000 1000
```

Arguments:

- `5000` = TCP port to listen on
- `1000` = broadcast interval in milliseconds

## Deploy on Railway

This repo now includes a root-level `Dockerfile`, so Railway can detect it automatically when the service is created from the repo root.

Important:

- Railway provides a `PORT` environment variable. The app uses it automatically.
- Keep the service build context at the repo root.
- No extra web hosting layer is needed; this is a raw TCP server.

If you want to test the same container locally before pushing to Railway:

```powershell
docker build -t consoleapp2-tcp .
docker run -p 5000:5000 -e PORT=5000 consoleapp2-tcp
```

## Test

Connect from another terminal with any TCP client and you should see the sentence repeated with CRLF line endings:

```powershell
# Example using PowerShell 7+ / Windows PowerShell with .NET APIs
$client = [System.Net.Sockets.TcpClient]::new('127.0.0.1', 5000)
$stream = $client.GetStream()
$buffer = New-Object byte[] 1024
while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
    [Text.Encoding]::ASCII.GetString($buffer, 0, $read)
}
```

Press `Ctrl+C` in the server window to stop it.

