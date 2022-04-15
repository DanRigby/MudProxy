using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MudProxy;

public class Proxy
{
    public Proxy(bool enableMccp2)
    {
        _proxyConfig = new ProxyConfiguration(enableMccp2);
    }

    private NetworkStream? _hostNetworkStream;
    private NetworkStream? _primaryClientNetworkStream;
    private bool _compressionEnabled;
    private readonly ConcurrentDictionary<NetworkStream, byte> _clientStreams = new();
    private readonly ProxyConfiguration _proxyConfig;

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

            Task _ = ProcessHostDataAsync(hostClient, cancelToken);
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
                Task _ = ProcessClientDataAsync(client, cancelToken);
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

    private async Task ProcessHostDataAsync(TcpClient hostClient, CancellationToken cancelToken)
    {
        try
        {
            await using NetworkStream hostNetworkStream = hostClient.GetStream();
            _hostNetworkStream = hostNetworkStream;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 8); // 8KB
            while (true)
            {
                int bytesRead = await hostNetworkStream.ReadAsync(buffer, cancelToken);
                if (bytesRead == 0)
                {
                    break;
                }

                int index = 0;
                List<byte> outputBuffer = new();

                Decompress:
                if (_compressionEnabled)
                {
                    ZLibStream zLibStream = new(
                        new MemoryStream(buffer, index, bytesRead), CompressionMode.Decompress, false);
                    bytesRead = await zLibStream.ReadAsync(buffer, cancelToken);
                }

                int clientDataLength = 0;
                byte[] clientData = ArrayPool<byte>.Shared.Rent(bytesRead);

                int hostDebugOutputLength = 0;
                byte[] hostDebugOutput = ArrayPool<byte>.Shared.Rent(bytesRead);

                for (; index < bytesRead; index++)
                {
                    if (buffer[index] == (byte)ProtocolValue.IAC)
                    {
                        (int bytesProcessed, bool passThrough, bool compressionStarted) =
                            TelnetCommandHandler.ProcessCommand(
                                buffer[index..bytesRead], outputBuffer, false, _proxyConfig);
                        if (passThrough)
                        {
                            Array.Copy(buffer, index, clientData, clientDataLength, bytesProcessed);
                            clientDataLength += bytesProcessed;
                        }

                        index += bytesProcessed - 1;

                        if (compressionStarted)
                        {
                            _compressionEnabled = true;
                            break;
                        }
                    }
                    else
                    {
                        clientData[clientDataLength++] = buffer[index];
                        hostDebugOutput[hostDebugOutputLength++] = buffer[index];
                    }
                }

                if (clientDataLength > 0)
                {
                    foreach (NetworkStream clientNetworkStream in _clientStreams.Keys)
                    {
                        await clientNetworkStream.WriteAsync(clientData.AsMemory(0, clientDataLength), cancelToken);
                    }
                }

                if (hostDebugOutputLength > 0)
                {
                    Console.WriteLine("[HOST]: {0}",
                        Encoding.ASCII.GetString(hostDebugOutput, 0, hostDebugOutputLength));
                }

                if (outputBuffer.Count > 0)
                {
                    Console.WriteLine("[PROXY-H]: {0}", TelnetCommandHandler.CommandsToString(outputBuffer.ToArray()));
                    await hostNetworkStream.WriteAsync(
                        outputBuffer.ToArray().AsMemory(0, outputBuffer.Count), cancelToken);
                }

                ArrayPool<byte>.Shared.Return(clientData);
                ArrayPool<byte>.Shared.Return(hostDebugOutput);

                if (index + 1 < bytesRead)
                {
                    // We need to decompress the rest of the data
                    goto Decompress;
                }
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }
        catch (OperationCanceledException)
        {
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

    private async Task ProcessClientDataAsync(TcpClient client, CancellationToken cancelToken)
    {
        try
        {
            await using NetworkStream clientNetworkStream = client.GetStream();

            _clientStreams[clientNetworkStream] = 0;
            _primaryClientNetworkStream = clientNetworkStream;

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

                int hostDataLength = 0;
                byte[] hostData = ArrayPool<byte>.Shared.Rent(bytesRead);

                int clientDebugOutputLength = 0;
                byte[] clientDebugOutput = ArrayPool<byte>.Shared.Rent(bytesRead);

                List<byte> outputBuffer = new();
                for (int index = 0; index < bytesRead; index++)
                {
                    if (buffer[index] == (byte)ProtocolValue.IAC)
                    {
                        (int bytesProcessed, bool passThrough, bool _) =
                            TelnetCommandHandler.ProcessCommand(
                                buffer[index..bytesRead], outputBuffer, true, _proxyConfig);
                        // Only send telnet commands from the most recently connected client to the host
                        if (passThrough && clientNetworkStream == _primaryClientNetworkStream)
                        {
                            Array.Copy(buffer, index, hostData, hostDataLength, bytesProcessed);
                            hostDataLength += bytesProcessed;
                        }

                        index += bytesProcessed - 1;
                    }
                    else
                    {
                        hostData[hostDataLength++] = buffer[index];
                        clientDebugOutput[clientDebugOutputLength++] = buffer[index];
                    }
                }

                if (hostDataLength > 0)
                {
                    if (_hostNetworkStream != null)
                    {
                        await _hostNetworkStream.WriteAsync(hostData.AsMemory(0, hostDataLength), cancelToken);
                    }
                }

                if (clientDebugOutputLength > 0)
                {
                    Console.WriteLine("[CLIENT]: {0}",
                        Encoding.ASCII.GetString(clientDebugOutput, 0, clientDebugOutputLength));
                }

                if (outputBuffer.Count > 0)
                {
                    Console.WriteLine("[PROXY-C]: {0}", TelnetCommandHandler.CommandsToString(outputBuffer.ToArray()));
                    await clientNetworkStream.WriteAsync(
                        outputBuffer.ToArray().AsMemory(0, outputBuffer.Count), cancelToken);
                }

                ArrayPool<byte>.Shared.Return(hostData);
                ArrayPool<byte>.Shared.Return(clientDebugOutput);
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }
        catch (OperationCanceledException)
        {
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
