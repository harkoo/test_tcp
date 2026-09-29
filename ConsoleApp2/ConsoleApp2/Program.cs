using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

const string sentence = "!AIVDM,1,1,,A,15Muq?001oJr>tpE`Gqv4?wN0<0,0*5C";
const int defaultPort = 5000;
const int defaultIntervalMs = 1000;

var port = GetPortFromEnvironment() ??
	(args.Length > 0 && int.TryParse(args[0], out var parsedPort) && parsedPort is > 0 and <= 65535
	? parsedPort
	: defaultPort);

var intervalMs = args.Length > 1 && int.TryParse(args[1], out var parsedInterval) && parsedInterval > 0
	? parsedInterval
	: defaultIntervalMs;

var payload = Encoding.ASCII.GetBytes(sentence + "\r\n");
var interval = TimeSpan.FromMilliseconds(intervalMs);
var cancellationSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
	eventArgs.Cancel = true;
	cancellationSource.Cancel();
};

var clients = new ConcurrentDictionary<Guid, TcpClient>();
var listener = new TcpListener(IPAddress.Any, port);

listener.Start();
Console.WriteLine($"Listening on 0.0.0.0:{port}");
Console.WriteLine($"Broadcasting every {interval.TotalMilliseconds:0} ms:");
Console.WriteLine(sentence);
Console.WriteLine("Press Ctrl+C to stop.");

var acceptTask = AcceptClientsAsync(listener, clients, payload, cancellationSource.Token);
var broadcastTask = BroadcastLoopAsync(clients, payload, interval, cancellationSource.Token);

try
{
	await Task.WhenAll(acceptTask, broadcastTask);
}
catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
{
}
finally
{
	listener.Stop();

	foreach (var client in clients.Values)
	{
		client.Dispose();
	}
}

static async Task AcceptClientsAsync(
	TcpListener listener,
	ConcurrentDictionary<Guid, TcpClient> clients,
	byte[] payload,
	CancellationToken cancellationToken)
{
	while (!cancellationToken.IsCancellationRequested)
	{
		TcpClient? client = null;

		try
		{
			client = await listener.AcceptTcpClientAsync(cancellationToken);
			client.NoDelay = true;

			if (!await TrySendAsync(client, payload, cancellationToken))
			{
				client.Dispose();
				continue;
			}

			var clientId = Guid.NewGuid();
			if (!clients.TryAdd(clientId, client))
			{
				client.Dispose();
				continue;
			}

			Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			client?.Dispose();
			break;
		}
		catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
		{
			client?.Dispose();
			break;
		}
		catch (Exception ex)
		{
			client?.Dispose();
			Console.WriteLine($"Accept loop error: {ex.Message}");

			try
			{
				await Task.Delay(250, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
		}
	}
}

static async Task BroadcastLoopAsync(
	ConcurrentDictionary<Guid, TcpClient> clients,
	byte[] payload,
	TimeSpan interval,
	CancellationToken cancellationToken)
{
	while (!cancellationToken.IsCancellationRequested)
	{
		foreach (var clientEntry in clients.ToArray())
		{
			var client = clientEntry.Value;

			if (await TrySendAsync(client, payload, cancellationToken))
			{
				continue;
			}

			if (clients.TryRemove(clientEntry.Key, out var removedClient))
			{
				removedClient.Dispose();
				Console.WriteLine("Client disconnected.");
			}
		}

		try
		{
			await Task.Delay(interval, cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			break;
		}
	}
}

static async Task<bool> TrySendAsync(TcpClient client, byte[] payload, CancellationToken cancellationToken)
{
	try
	{
		var stream = client.GetStream();
		await stream.WriteAsync(payload, cancellationToken);
		await stream.FlushAsync(cancellationToken);
		return true;
	}
	catch
	{
		return false;
	}
}

static int? GetPortFromEnvironment()
{
	var portValue = Environment.GetEnvironmentVariable("PORT");
	return int.TryParse(portValue, out var port) && port is > 0 and <= 65535 ? port : null;
}

