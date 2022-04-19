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

    private const int BufferSize = 1024 * 8; // 8KB

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
        byte[] rawHostData = ArrayPool<byte>.Shared.Rent(BufferSize);
        byte[] hostData = ArrayPool<byte>.Shared.Rent(BufferSize);
        byte[] clientData = ArrayPool<byte>.Shared.Rent(BufferSize);
        byte[] consoleOutput = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            await using NetworkStream hostNetworkStream = hostClient.GetStream();
            _hostNetworkStream = hostNetworkStream;

            Inflater zLibInflater = new();
            bool compressionEnabled = false;
            bool processingIncomplete = false;

            int index = 0;
            int bytesRead = 0;

            while (true)
            {
                if (!processingIncomplete)
                {
                    bytesRead = await hostNetworkStream.ReadAsync(
                        rawHostData.AsMemory(0, rawHostData.Length), cancelToken);
                }
                else
                {
                    Array.Copy(hostData, index, rawHostData, 0, bytesRead);
                    processingIncomplete = false;
                }

                index = 0;

                if (compressionEnabled)
                {
                    zLibInflater.SetInput(rawHostData, 0, bytesRead);
                    bytesRead = zLibInflater.Inflate(hostData, 0, hostData.Length);
                }
                else
                {
                    Array.Copy(rawHostData, 0, hostData, 0, bytesRead);
                }

                if (bytesRead == 0)
                {
                    // Host disconnected
                    break;
                }

                int clientDataLength = 0;
                int consoleOutputLength = 0;
                List<byte> outputBuffer = new();

                for (; index < bytesRead; index++)
                {
                    if (hostData[index] == (byte)ProtocolValue.IAC)
                    {
                        if (consoleOutputLength > 0)
                        {
                            Console.WriteLine("[HOST]: {0}",
                                Encoding.ASCII.GetString(consoleOutput, 0, consoleOutputLength));
                            consoleOutputLength = 0;
                        }

                        (int bytesProcessed, bool passThrough, bool compressionStarted) =
                            TelnetCommandHandler.ProcessCommand(
                                hostData[index..bytesRead], outputBuffer, false, _proxyConfig);

                        if (passThrough)
                        {
                            Array.Copy(hostData, index, clientData, clientDataLength, bytesProcessed);
                            clientDataLength += bytesProcessed;
                        }

                        Console.WriteLine("[HOST]: {0}",
                            TelnetCommandHandler.CommandsToString(hostData.AsSpan()[index..(index + bytesProcessed)]));

                        index += bytesProcessed - 1;

                        if (compressionStarted)
                        {
                            compressionEnabled = true;
                            break;
                        }
                    }
                    else
                    {
                        clientData[clientDataLength++] = hostData[index];
                        consoleOutput[consoleOutputLength++] = hostData[index];
                    }
                }

                if (clientDataLength > 0)
                {
                    foreach (NetworkStream clientNetworkStream in _clientStreams.Keys)
                    {
                        await clientNetworkStream.WriteAsync(clientData.AsMemory(0, clientDataLength), cancelToken);
                    }
                }

                if (consoleOutputLength > 0)
                {
                    Console.WriteLine("[HOST]: {0}",
                        Encoding.ASCII.GetString(consoleOutput, 0, consoleOutputLength));
                }

                if (outputBuffer.Count > 0)
                {
                    Console.WriteLine("[PROXY-H]: {0}", TelnetCommandHandler.CommandsToString(outputBuffer.ToArray()));
                    await hostNetworkStream.WriteAsync(
                        outputBuffer.ToArray().AsMemory(0, outputBuffer.Count), cancelToken);
                }

                if (index + 1 < bytesRead)
                {
                    processingIncomplete = true;
                    bytesRead -= index;
                }
            }
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
            ArrayPool<byte>.Shared.Return(hostData);
            ArrayPool<byte>.Shared.Return(rawHostData);
            ArrayPool<byte>.Shared.Return(clientData);
            ArrayPool<byte>.Shared.Return(consoleOutput);

            hostClient.Close();
        }
    }

    private async Task ProcessClientDataAsync(TcpClient client, CancellationToken cancelToken)
    {
        byte[] clientData = ArrayPool<byte>.Shared.Rent(BufferSize);
        byte[] hostData = ArrayPool<byte>.Shared.Rent(BufferSize);
        byte[] consoleOutput = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            await using NetworkStream clientNetworkStream = client.GetStream();

            _clientStreams[clientNetworkStream] = 0;
            _primaryClientNetworkStream ??= clientNetworkStream;

            while (true)
            {
                int bytesRead = await clientNetworkStream.ReadAsync(clientData, cancelToken);
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
                int consoleOutputLength = 0;
                List<byte> outputBuffer = new();

                for (int index = 0; index < bytesRead; index++)
                {
                    if (clientData[index] == (byte)ProtocolValue.IAC)
                    {
                        if (consoleOutputLength > 0)
                        {
                            Console.WriteLine("[CLIENT]: {0}",
                                Encoding.ASCII.GetString(consoleOutput, 0, consoleOutputLength));
                            consoleOutputLength = 0;
                        }

                        (int bytesProcessed, bool passThrough, bool _) =
                            TelnetCommandHandler.ProcessCommand(
                                clientData[index..bytesRead], outputBuffer, true, _proxyConfig);

                        // Only send telnet commands from the client that connected first (the primary)
                        // If the first connected client disconnects, the next client to connect will become the primary
                        if (passThrough && isPrimaryClient)
                        {
                            Array.Copy(clientData, index, hostData, hostDataLength, bytesProcessed);
                            hostDataLength += bytesProcessed;
                        }

                        if (isPrimaryClient)
                        {
                            Console.WriteLine("[CLIENT]: {0}",
                                TelnetCommandHandler.CommandsToString(
                                    hostData.AsSpan()[index..(index + bytesProcessed)]));
                        }

                        index += bytesProcessed - 1;
                    }
                    else
                    {
                        hostData[hostDataLength++] = clientData[index];
                        consoleOutput[consoleOutputLength++] = clientData[index];
                    }
                }

                if (hostDataLength > 0)
                {
                    if (_hostNetworkStream != null)
                    {
                        await _hostNetworkStream.WriteAsync(hostData.AsMemory(0, hostDataLength), cancelToken);
                    }
                }

                if (consoleOutputLength > 0)
                {
                    Console.WriteLine("[CLIENT]: {0}",
                        Encoding.ASCII.GetString(consoleOutput, 0, consoleOutputLength));
                }

                if (outputBuffer.Count > 0)
                {
                    Console.WriteLine("[PROXY-C]: {0}", TelnetCommandHandler.CommandsToString(outputBuffer.ToArray()));
                    await clientNetworkStream.WriteAsync(
                        outputBuffer.ToArray().AsMemory(0, outputBuffer.Count), cancelToken);
                }
            }
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
            ArrayPool<byte>.Shared.Return(clientData);
            ArrayPool<byte>.Shared.Return(hostData);
            ArrayPool<byte>.Shared.Return(consoleOutput);

            client.Close();
        }
    }
}
