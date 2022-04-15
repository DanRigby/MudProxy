using System.Text;

namespace MudProxy;

public static class TelnetCommandHandler
{
    public static int ProcessCommand(
        byte[] inputBuffer, List<byte> outputBuffer, bool isClient, ProxyConfiguration proxyConfig)
    {
        int bytesProcessed = 1;

        if (inputBuffer.Length > 1)
        {
            switch (inputBuffer[1])
            {
                case (byte)ProtocolValue.WILL:
                case (byte)ProtocolValue.WONT:
                case (byte)ProtocolValue.DO:
                case (byte)ProtocolValue.DONT:
                    bytesProcessed = 3;
                    break;
                case (byte)ProtocolValue.SB:
                {
                    for (int i = 2; i < inputBuffer.Length; i++)
                    {
                        if (inputBuffer[i] == (byte)ProtocolValue.SE)
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

        if (ProtocolSequence.IsTerminalTypeNegotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add((byte)ProtocolValue.WILL);
            outputBuffer.Add((byte)ProtocolValue.TERMTYPE);
        }
        else if (ProtocolSequence.IsTerminalTypeSendRequest(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add((byte)ProtocolValue.SB);
            outputBuffer.Add((byte)ProtocolValue.TERMTYPE);
            outputBuffer.Add(0x00); // Value / Is
            outputBuffer.AddRange(Encoding.ASCII.GetBytes(proxyConfig.TerminalType));
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add((byte)ProtocolValue.SE);
        }
        else if (ProtocolSequence.IsWindowSizeNegotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add((byte)ProtocolValue.WONT);
            outputBuffer.Add((byte)ProtocolValue.NAWS);
        }
        else if (ProtocolSequence.IsNewEnvironmentNegotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add((byte)ProtocolValue.WONT);
            outputBuffer.Add((byte)ProtocolValue.NEWENVIRONMENT);
        }
        else if (ProtocolSequence.IsMccp2Negotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add(proxyConfig.EnableMccp2 && !isClient ? (byte)ProtocolValue.DO : (byte)ProtocolValue.DONT);
            outputBuffer.Add((byte)ProtocolValue.MCCP2);
        }
        else if (ProtocolSequence.IsMccp2Confirmation(inputBuffer) && !isClient)
        {
            proxyConfig.DecompressionRequired = true;
        }
        else if (ProtocolSequence.IsMccp1Negotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add((byte)ProtocolValue.DONT);
            outputBuffer.Add((byte)ProtocolValue.MCCP1);
        }
        else if (ProtocolSequence.IsMxpNegotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add(proxyConfig.EnableMxp ? (byte)ProtocolValue.WILL : (byte)ProtocolValue.WONT);
            outputBuffer.Add((byte)ProtocolValue.MXP);
        }
        else if (ProtocolSequence.IsMxpClientNegotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add((byte)ProtocolValue.SB);
            outputBuffer.Add((byte)ProtocolValue.MXP);
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add((byte)ProtocolValue.SE);
        }
        else if (ProtocolSequence.IsMxpConfirmation(inputBuffer))
        {
            // Nothing to do here.
        }
        else if (ProtocolSequence.IsGmcpNegotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add((byte)ProtocolValue.DONT);
            outputBuffer.Add((byte)ProtocolValue.GMCP);
        }
        else if (ProtocolSequence.IsZmpNegotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add((byte)ProtocolValue.DONT);
            outputBuffer.Add((byte)ProtocolValue.ZMP);
        }
        else if (ProtocolSequence.IsMsspNegotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add((byte)ProtocolValue.DONT);
            outputBuffer.Add((byte)ProtocolValue.MSSP);
        }

        Console.WriteLine("{0}: {1}",
            isClient ? "[CLIENT]" : "[HOST]",
            CommandsToString(inputBuffer.AsSpan()[..bytesProcessed]));

        return bytesProcessed;
    }

    public static string CommandsToString(Span<byte> commandBuffer)
    {
        StringBuilder sb = new();
        sb.Append("IAC");
        for (int i = 1; i < commandBuffer.Length; i++)
        {
            sb.Append(' ');
            sb.Append(
                Enum.GetName(typeof(ProtocolValue), commandBuffer[i])
                ?? string.Format("0x{0:X2} ({0})", commandBuffer[i]));
        }

        return sb.ToString();
    }
}
