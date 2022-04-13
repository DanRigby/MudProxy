using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MudProxy;

public class Proxy
{
    private readonly ConcurrentDictionary<NetworkStream, byte> _clientStreams = new();
    private NetworkStream? _hostStream;

    public async Task ConnectToHostAsync(string hostName, int port, CancellationToken cancelToken)
    {
        try
        {
            TcpClient hostClient = new();
            await hostClient.ConnectAsync(hostName, port, cancelToken);

            if (!hostClient.Connected)
            {
                Console.WriteLine($"Failed to connect to {hostName}:{port}");
                return;
            }

            Console.WriteLine($"Connected to {hostName}:{port}");

            Task _ = ProcessHostData(hostClient, cancelToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    public async Task ListenForClientsAsync(int port, CancellationToken cancelToken)
    {
        try
        {
            TcpListener clientListener = new(IPAddress.Any, port);
            clientListener.Start();

            while (true)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    break;
                }

                TcpClient client = await clientListener.AcceptTcpClientAsync(cancelToken);
                if (!client.Connected)
                {
                    continue;
                }

                Console.WriteLine("Client connected {0}:{1}", client.Client.LocalEndPoint,
                    client.Client.RemoteEndPoint);
                Task _ = ProcessClientData(client, cancelToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    private async Task ProcessHostData(TcpClient hostClient, CancellationToken cancelToken)
    {
        try
        {
            await using NetworkStream hostNetworkStream = hostClient.GetStream();
            _hostStream = hostNetworkStream;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 8); // 8KB
            while (true)
            {
                int bytesRead = await hostNetworkStream.ReadAsync(buffer, cancelToken);
                if (bytesRead == 0)
                {
                    break;
                }

                byte[] clientData = ArrayPool<byte>.Shared.Rent(bytesRead);
                int clientDataLength = 0;

                for (int i = 0; i < bytesRead; i++)
                {
                    // Intercept and disable compression, pass everything else through
                    if (
                        i + 2 < bytesRead
                        && buffer[i + 0] == 0xFF // IAC
                        && buffer[i + 1] == 0xfb // WILL
                        && buffer[i + 2] == 0x56 // MCCP2
                    )
                    {
                        i += 2;
                        // IAC DONT MCCP2
                        await hostNetworkStream.WriteAsync(new byte[] { 0xFF, 0xfe, 0x56 }, cancelToken);
                    }
                    else if (
                        i + 2 < bytesRead
                        && buffer[i + 0] == 0xFF // IAC
                        && buffer[i + 1] == 0xfb // WILL
                        && buffer[i + 2] == 0x55 // MCCP1
                    )
                    {
                        i += 2;
                        // IAC DONT MCCP1
                        await hostNetworkStream.WriteAsync(new byte[] { 0xFF, 0xfe, 0x55 }, cancelToken);
                    }
                    else
                    {
                        clientData[clientDataLength++] = buffer[i];
                    }
                }

                foreach (NetworkStream clientStream in _clientStreams.Keys)
                {
                    await clientStream.WriteAsync(clientData.AsMemory(0, clientDataLength), cancelToken);
                }

                Console.WriteLine(Encoding.ASCII.GetString(clientData, 0, clientDataLength));

                ArrayPool<byte>.Shared.Return(clientData);
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }
        catch (Exception e)
        {
            Console.WriteLine("Host Exception: {0}", e);
        }
        finally
        {
            hostClient.Close();
        }
    }

    private async Task ProcessClientData(TcpClient client, CancellationToken cancelToken)
    {
        try
        {
            await using NetworkStream clientNetworkStream = client.GetStream();

            _clientStreams[clientNetworkStream] = 0;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 8); // 8KB
            while (true)
            {
                int bytesRead = await clientNetworkStream.ReadAsync(buffer, cancelToken);
                if (bytesRead == 0)
                {
                    Console.WriteLine("Client disconnected {0}:{1}", client.Client.LocalEndPoint,
                        client.Client.RemoteEndPoint);
                    _clientStreams.TryRemove(clientNetworkStream, out _);
                    break;
                }

                if (_hostStream != null)
                {
                    await _hostStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancelToken);
                }
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }
        catch (Exception e)
        {
            _clientStreams.TryRemove(client.GetStream(), out _);
            Console.WriteLine("Client Exception: {0}", e);
        }
        finally
        {
            client.Close();
        }
    }
}