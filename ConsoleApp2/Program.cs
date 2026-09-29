using System.Net;
using System.Net.Sockets;
using System.Text;

class TcpNmeaBroadcaster
{
    static async Task Main(string[] args)
    {
        // Get port and interval from arguments or use defaults
        int port = args.Length > 0 ? int.Parse(args[0]) : 8080;
        int intervalMs = args.Length > 1 ? int.Parse(args[1]) : 1000;

        // Check for PORT environment variable (used by Railway)
        string? envPort = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out int parsedPort))
        {
            port = parsedPort;
        }

        var nmeaSentence = "$GPRMC,123519,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*6A\r\n";
        var server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine($"TCP server listening on port {port}");
        Console.WriteLine($"Broadcasting interval: {intervalMs}ms");

        var clients = new List<TcpClient>();
        var lockObj = new object();

        // Accept client connections
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var client = await server.AcceptTcpClientAsync();
                    lock (lockObj)
                    {
                        clients.Add(client);
                    }
                    Console.WriteLine($"Client connected. Total clients: {clients.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        });

        // Broadcast NMEA sentence periodically
        while (true)
        {
            await Task.Delay(intervalMs);

            lock (lockObj)
            {
                for (int i = clients.Count - 1; i >= 0; i--)
                {
                    var client = clients[i];
                    try
                    {
                        if (client.Connected)
                        {
                            var stream = client.GetStream();
                            var data = Encoding.ASCII.GetBytes(nmeaSentence);
                            stream.Write(data, 0, data.Length);
                            stream.Flush();
                        }
                        else
                        {
                            clients.RemoveAt(i);
                            Console.WriteLine($"Client disconnected. Total clients: {clients.Count}");
                        }
                    }
                    catch (Exception ex)
                    {
                        clients.RemoveAt(i);
                        Console.WriteLine($"Error sending to client: {ex.Message}. Total clients: {clients.Count}");
                        try { client.Close(); } catch { }
                    }
                }
            }
        }
    }
}

