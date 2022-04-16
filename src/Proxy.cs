using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression;

namespace MudProxy;

public class Proxy
{
    public Proxy(bool enableMccp2)
    {
        _proxyConfig = new ProxyConfiguration(enableMccp2);
    }

    private NetworkStream? _hostNetworkStream;
    private NetworkStream? _primaryClientNetworkStream;
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

            Inflater zLibInflater = new();
            bool compressionEnabled = false;
            bool processingIncomplete = false;

            byte[] rawBuffer = ArrayPool<byte>.Shared.Rent(1024 * 8); // 8KB
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 8); // 8KB

            int index = 0;
            int bytesRead = 0;
            while (true)
            {
                if (!processingIncomplete)
                {
                    index = 0;
                    bytesRead = await hostNetworkStream.ReadAsync(rawBuffer, cancelToken);
                }
                else
                {
                    processingIncomplete = false;
                }

                if (compressionEnabled)
                {
                    zLibInflater.SetInput(rawBuffer, index, bytesRead - index);
                    bytesRead = zLibInflater.Inflate(buffer, index, buffer.Length - index);
                }
                else
                {
                    Array.Copy(rawBuffer, index, buffer, index, bytesRead - index);
                }

                if (bytesRead == 0)
                {
                    // Host disconnected
                    break;
                }

                int clientDataLength = 0;
                byte[] clientData = ArrayPool<byte>.Shared.Rent(bytesRead);

                int hostDebugOutputLength = 0;
                byte[] hostDebugOutput = ArrayPool<byte>.Shared.Rent(bytesRead);

                List<byte> outputBuffer = new();
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
                            compressionEnabled = true;
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
                    processingIncomplete = true;
                }
            }

            ArrayPool<byte>.Shared.Return(buffer);
            ArrayPool<byte>.Shared.Return(rawBuffer);
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
            _primaryClientNetworkStream ??= clientNetworkStream;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 8); // 8KB
            while (true)
            {
                int bytesRead = await clientNetworkStream.ReadAsync(buffer, cancelToken);
                if (bytesRead == 0)
                {
                    Console.WriteLine("Client disconnected {0}:{1}", client.Client.LocalEndPoint,
                        client.Client.RemoteEndPoint);

                    _clientStreams.TryRemove(clientNetworkStream, out _);

                    if (_primaryClientNetworkStream == clientNetworkStream)
                    {
                        _primaryClientNetworkStream = null;
                    }

                    break;
                }

                bool isPrimaryClient = clientNetworkStream == _primaryClientNetworkStream;

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
                                buffer[index..bytesRead], outputBuffer, true, _proxyConfig, isPrimaryClient);

                        // Only send telnet commands from the client that connected first (the primary)
                        // If the first connected client disconnects, the next client to connect will become the primary
                        if (passThrough && isPrimaryClient)
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
