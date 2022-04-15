using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MudProxy;

public class Proxy
{
    public Proxy(string terminalType, bool enableMccp2, bool enableMxp)
    {
        TerminalType = terminalType;
        EnableMccp2 = enableMccp2;
        EnableMxp = enableMxp;
    }

    private string TerminalType { get; }
    private bool EnableMccp2 { get; }
    private bool EnableMxp { get; }

    private readonly ConcurrentDictionary<NetworkStream, byte> _clientStreams = new();
    private NetworkStream? _hostStream;
    private bool _decompressionRequired;

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
            // This happens when the user hits Ctrl-C
            // We don't need to do anything here
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
            // This happens when the user hits Ctrl-C
            // We don't need to do anything here
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
            _hostStream = hostNetworkStream;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 8); // 8KB
            while (true)
            {
                int bytesRead = await hostNetworkStream.ReadAsync(buffer, cancelToken);
                if (bytesRead == 0)
                {
                    break;
                }

                int index = 0;

                Decompress:
                if (_decompressionRequired)
                {
                    ZLibStream zLibStream = new(
                        new MemoryStream(buffer, index, bytesRead), CompressionMode.Decompress, false);
                    bytesRead = await zLibStream.ReadAsync(buffer, cancelToken);
                }

                int clientDataLength = 0;
                byte[] clientData = ArrayPool<byte>.Shared.Rent(bytesRead);

                for (; index < bytesRead; index++)
                {
                    if (buffer[index] == (byte)ProtocolValue.IAC)
                    {
                        int bytesProcessed =
                            await ProcessTelnetCommandAsync(buffer[index..bytesRead], hostNetworkStream, cancelToken);
                        index += bytesProcessed - 1;
                    }
                    else
                    {
                        clientData[clientDataLength++] = buffer[index];
                    }
                }

                foreach (NetworkStream clientStream in _clientStreams.Keys)
                {
                    await clientStream.WriteAsync(clientData.AsMemory(0, clientDataLength), cancelToken);
                }

                Console.WriteLine(Encoding.ASCII.GetString(clientData, 0, clientDataLength));

                ArrayPool<byte>.Shared.Return(clientData);

                if (index + 1 < bytesRead)
                {
                    // We need to decompress the rest of the data
                    goto Decompress;
                }
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

    private async Task ProcessClientDataAsync(TcpClient client, CancellationToken cancelToken)
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

    private async Task<int> ProcessTelnetCommandAsync(
        byte[] buffer, NetworkStream hostNetworkStream, CancellationToken cancelToken)
    {
        int bytesProcessed = 1;

        if (buffer.Length > 1)
        {
            switch (buffer[1])
            {
                case (byte)ProtocolValue.WILL:
                case (byte)ProtocolValue.WONT:
                case (byte)ProtocolValue.DO:
                case (byte)ProtocolValue.DONT:
                    bytesProcessed = 3;
                    break;
                case (byte)ProtocolValue.SB:
                {
                    for (int i = 2; i < buffer.Length; i++)
                    {
                        if (buffer[i] == (byte)ProtocolValue.SE)
                        {
                            bytesProcessed = i + 1;
                            break;
                        }
                    }

                    break;
                }
                default:
                    bytesProcessed = 2;
                    break;
            }
        }

        if (ProtocolSequence.IsTerminalTypeNegotiation(buffer))
        {
            await hostNetworkStream.WriteAsync(
                new[] { (byte)ProtocolValue.IAC, (byte)ProtocolValue.WILL, (byte)ProtocolValue.TERMTYPE },
                cancelToken);
        }
        else if (ProtocolSequence.IsTerminalTypeSendRequest(buffer))
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            List<byte> responseBytes = new();
            responseBytes.Add((byte)ProtocolValue.IAC);
            responseBytes.Add((byte)ProtocolValue.SB);
            responseBytes.Add((byte)ProtocolValue.TERMTYPE);
            responseBytes.Add(0x00); // Value / Is
            responseBytes.AddRange(Encoding.ASCII.GetBytes(TerminalType));
            responseBytes.Add((byte)ProtocolValue.IAC);
            responseBytes.Add((byte)ProtocolValue.SE);

            await hostNetworkStream.WriteAsync(responseBytes.ToArray(), cancelToken);
        }
        else if (ProtocolSequence.IsWindowSizeNegotiation(buffer))
        {
            await hostNetworkStream.WriteAsync(
                new[] { (byte)ProtocolValue.IAC, (byte)ProtocolValue.WONT, (byte)ProtocolValue.WINDOWSIZE },
                cancelToken);
        }
        else if (ProtocolSequence.IsNewEnvironmentNegotiation(buffer))
        {
            await hostNetworkStream.WriteAsync(
                new[] { (byte)ProtocolValue.IAC, (byte)ProtocolValue.WONT, (byte)ProtocolValue.NEWENVIRONMENT },
                cancelToken);
        }
        else if (ProtocolSequence.IsMccp2Negotiation(buffer))
        {
            await hostNetworkStream.WriteAsync(new[]
                {
                    (byte)ProtocolValue.IAC,
                    EnableMccp2 ? (byte)ProtocolValue.DO : (byte)ProtocolValue.DONT,
                    (byte)ProtocolValue.MCCP2
                }, cancelToken);
        }
        else if (ProtocolSequence.IsMccp2Confirmation(buffer))
        {
            _decompressionRequired = true;
        }
        else if (ProtocolSequence.IsMccp1Negotiation(buffer))
        {
            await hostNetworkStream.WriteAsync(
                new[] { (byte)ProtocolValue.IAC, (byte)ProtocolValue.DONT, (byte)ProtocolValue.MCCP1 },
                cancelToken);
        }
        else if (ProtocolSequence.IsMxpNegotiation(buffer))
        {
            await hostNetworkStream.WriteAsync(new[]
            {
                (byte)ProtocolValue.IAC,
                EnableMxp ? (byte)ProtocolValue.WILL : (byte)ProtocolValue.WONT,
                (byte)ProtocolValue.MXP
            }, cancelToken);
        }
        else if (ProtocolSequence.IsGmcpNegotiation(buffer))
        {
            await hostNetworkStream.WriteAsync(
                new[] { (byte)ProtocolValue.IAC, (byte)ProtocolValue.DONT, (byte)ProtocolValue.GMCP },
                cancelToken);
        }
        else if (ProtocolSequence.IsZmpNegotiation(buffer))
        {
            await hostNetworkStream.WriteAsync(
                new[] { (byte)ProtocolValue.IAC, (byte)ProtocolValue.DONT, (byte)ProtocolValue.ZMP },
                cancelToken);
        }
        else if (ProtocolSequence.IsMsspNegotiation(buffer))
        {
            await hostNetworkStream.WriteAsync(
                new[] { (byte)ProtocolValue.IAC, (byte)ProtocolValue.DONT, (byte)ProtocolValue.MSSP },
                cancelToken);
        }

        Console.Write("IAC");
        for (int i = 1; i < bytesProcessed; i++)
        {
            Console.Write(" " +
                (Enum.GetName(typeof(ProtocolValue), buffer[i]) ?? string.Format("0x{0:X2} ({0})", buffer[i])));
        }
        Console.WriteLine();

        return bytesProcessed;
    }
}