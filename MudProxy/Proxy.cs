using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MudProxy;

public class Proxy
{
    private readonly ConcurrentDictionary<NetworkStream, byte> _clientStreams = new();
    private readonly ProxyConfiguration _proxyConfig;

    private NetworkStream? _hostNetworkStream;
    private NetworkStream? _primaryClientNetworkStream;

    public Proxy(bool enableMccp2)
    {
        _proxyConfig = new ProxyConfiguration(enableMccp2);
    }

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
            Console.WriteLine();

            Task _ = Task.Run(() => ProcessHostDataAsync(hostClient, cancelToken), cancelToken);
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
                Task _ = Task.Run(() => ProcessClientDataAsync(client, cancelToken), cancelToken);
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
            await using ZLibStream hostZLibNetworkStream = new(hostNetworkStream, CompressionMode.Decompress);
            _hostNetworkStream = hostNetworkStream;

            TelnetCommandParser commandParser = new();
            TelnetCommandHandler commandHandler = new(_proxyConfig, hostNetworkStream, isHost: true);
            ArrayBufferWriter<byte> consoleOutput = new(1024 * 8);
            byte[] currentByteArray = new byte[1];

            bool compressionEnabled = false;
            bool inCommand = false;

            void FlushConsoleOutput()
            {
                if (consoleOutput.WrittenCount > 0)
                {
                    Console.WriteLine("[HOST]: {0}",
                        Encoding.ASCII.GetString(consoleOutput.WrittenSpan));

                    consoleOutput.Clear();
                }
            }

            while (true)
            {
                int readResult = compressionEnabled ? hostZLibNetworkStream.ReadByte() : hostNetworkStream.ReadByte();
                if (readResult is -1)
                {
                    Console.WriteLine(
                        "Host disconnected {0}:{1}", hostClient.Client.LocalEndPoint, hostClient.Client.RemoteEndPoint);
                    break;
                }

                byte currentByte = (byte)readResult;
                currentByteArray[0] = currentByte;

                if (currentByte is (byte)TelnetCommand.IAC)
                {
                    inCommand = true;
                    FlushConsoleOutput();
                }

                if (inCommand)
                {
                    (ReadOnlyMemory<byte> parsedCommand, string parsedCommandString)
                        = commandParser.ProcessCommandByte(currentByte);

                    if (parsedCommand.Length > 0)
                    {
                        inCommand = false;

                        if (parsedCommand.Span.SequenceEqual(TelnetCommandSequence.Mccp2Confirmation.Span))
                        {
                            compressionEnabled = true;
                        }

                        Console.WriteLine($"[HOST]: {parsedCommandString}");
                        bool suppressCommand = await commandHandler.HandleCommand(parsedCommand, cancelToken);

                        if (suppressCommand is not true)
                        {
                            foreach (NetworkStream clientNetworkStream in _clientStreams.Keys)
                            {
                                await clientNetworkStream.WriteAsync(parsedCommand, cancelToken);
                            }
                        }
                    }
                }
                else
                {
                    foreach (NetworkStream clientNetworkStream in _clientStreams.Keys)
                    {
                        clientNetworkStream.WriteByte(currentByte);
                    }

                    if (currentByte is 0x0A /* LF */)
                    {
                        FlushConsoleOutput();
                    }
                    else
                    {
                        consoleOutput.Write(currentByteArray);

                        if (consoleOutput.WrittenCount >= 4 &&
                            consoleOutput.WrittenSpan[^4] == 0x3C /* < */ &&
                            consoleOutput.WrittenSpan[^3] is 0x42 /* B */ &&
                            consoleOutput.WrittenSpan[^2] is 0x52 /* R */ &&
                            consoleOutput.WrittenSpan[^1] is 0x3E /* > */
                        )
                        {
                            FlushConsoleOutput();
                        }
                    }
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

            TelnetCommandParser commandParser = new();
            TelnetCommandHandler commandHandler = new(_proxyConfig, clientNetworkStream, isHost: false);
            ArrayBufferWriter<byte> consoleOutput = new(1024 * 8);
            ArrayBufferWriter<byte> clientData = new(1024 * 8);
            byte[] currentByteArray = new byte[1];

            bool inCommand = false;

            void FlushConsoleOutput()
            {
                if (consoleOutput.WrittenCount > 0)
                {
                    Console.WriteLine("[CLIENT]: {0}",
                        Encoding.ASCII.GetString(consoleOutput.WrittenSpan));

                    consoleOutput.Clear();
                }
            }

            while (true)
            {
                bool isPrimaryClient = clientNetworkStream == _primaryClientNetworkStream;

                if (!clientNetworkStream.DataAvailable)
                {
                    if (clientData.WrittenCount > 0 && _hostNetworkStream is not null && _hostNetworkStream.CanWrite)
                    {
                        await _hostNetworkStream.WriteAsync(clientData.WrittenMemory, cancelToken);
                        clientData.Clear();

                        FlushConsoleOutput();
                    }
                }

                int readResult = clientNetworkStream.ReadByte();
                if (readResult is -1)
                {
                    Console.WriteLine(
                        "Client disconnected {0}:{1}", client.Client.LocalEndPoint, client.Client.RemoteEndPoint);

                    _clientStreams.TryRemove(clientNetworkStream, out _);
                    if (_primaryClientNetworkStream == clientNetworkStream)
                    {
                        _primaryClientNetworkStream = null;
                    }

                    break;
                }

                byte currentByte = (byte)readResult;
                currentByteArray[0] = currentByte;

                if (currentByte is (byte)TelnetCommand.IAC)
                {
                    inCommand = true;
                    FlushConsoleOutput();
                }

                if (inCommand)
                {
                    (ReadOnlyMemory<byte> parsedCommand, string parsedCommandString)
                        = commandParser.ProcessCommandByte(currentByte);

                    if (parsedCommand.Length > 0)
                    {
                        inCommand = false;

                        bool suppressCommand = await commandHandler.HandleCommand(parsedCommand, cancelToken);

                        if (isPrimaryClient)
                        {
                            Console.WriteLine($"[CLIENT]: {parsedCommandString}");

                            if (suppressCommand is not true)
                            {
                                if (_hostNetworkStream is not null && _hostNetworkStream.CanWrite)
                                {
                                    await _hostNetworkStream.WriteAsync(parsedCommand, cancelToken);
                                }
                            }
                        }
                    }
                }
                else
                {
                    clientData.Write(currentByteArray);

                    if (currentByte is 0x0A /* LF */)
                    {
                        FlushConsoleOutput();
                    }
                    else
                    {
                        consoleOutput.Write(currentByteArray);
                    }
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
            client.Close();
        }
    }
}
