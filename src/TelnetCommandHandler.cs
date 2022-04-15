using System.Text;

namespace MudProxy;

public static class TelnetCommandHandler
{
    public static HandlerResult ProcessCommand(
        byte[] inputBuffer, List<byte> outputBuffer, bool isClient, ProxyConfiguration proxyConfig)
    {
        int bytesProcessed = 1;
        bool shouldPassThrough = true;
        bool compressionStarted = false;

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

        if (ProtocolSequence.IsMccp2Negotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add(proxyConfig.EnableMccp2 && !isClient ? (byte)ProtocolValue.DO : (byte)ProtocolValue.DONT);
            outputBuffer.Add((byte)ProtocolValue.MCCP2);
            shouldPassThrough = false;
        }
        else if (ProtocolSequence.IsMccp2Confirmation(inputBuffer) && !isClient)
        {
            compressionStarted = true;
            shouldPassThrough = false;
        }
        else if (ProtocolSequence.IsMccp1Negotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add((byte)ProtocolValue.DONT);
            outputBuffer.Add((byte)ProtocolValue.MCCP1);
            shouldPassThrough = false;
        }

        Console.WriteLine("{0}: {1}",
            isClient ? "[CLIENT]" : "[HOST]",
            CommandsToString(inputBuffer.AsSpan()[..bytesProcessed]));

        return new HandlerResult(bytesProcessed, shouldPassThrough, compressionStarted);
    }

    public static string CommandsToString(Span<byte> commandBuffer)
    {
        StringBuilder sb = new();
        sb.Append("IAC");

        bool inSubOption = false;
        for (int i = 1; i < commandBuffer.Length; i++)
        {
            sb.Append(' ');

            if (commandBuffer[i] == (byte)ProtocolValue.SE)
            {
                inSubOption = false;
            }

            string? enumValue = Enum.GetName(typeof(ProtocolValue), commandBuffer[i]);
            if (inSubOption || string.IsNullOrEmpty(enumValue))
            {
                sb.Append(string.Format("0x{0:X2} ({0})", commandBuffer[i]));
            }
            else
            {
                sb.Append(enumValue);
            }

            if (commandBuffer[i] == (byte)ProtocolValue.SB)
            {
                inSubOption = true;
            }
        }

        return sb.ToString();
    }
}
