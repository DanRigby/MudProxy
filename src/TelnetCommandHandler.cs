using System.Buffers;
using System.Text;

namespace MudProxy;

public record HandlerResult(int BytesProcessed, bool PassThrough, bool CompressionStarted);

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
                    // Length being < 3 shouldn't happen in this case, but we'll check anyway
                    bytesProcessed = inputBuffer.Length > 2 ? 3 : 2;
                    break;
                case (byte)ProtocolValue.SB:
                {
                    for (int i = 2; i < inputBuffer.Length; i++)
                    {
                        bytesProcessed = i + 1;
                        if (inputBuffer[i] == (byte)ProtocolValue.SE)
                        {
                            break;
                        }
                    }

                    break;
                }
                // The SE value can arrive in a different packet then the SB value.
                case (byte)ProtocolValue.SE:
                    bytesProcessed = 2;
                    break;
                default:
                    bytesProcessed = 2;
                    break;
            }
        }

        if (ProtocolSequence.IsMccp3Negotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add((byte)ProtocolValue.DONT);
            outputBuffer.Add((byte)ProtocolValue.MCCP3);
            shouldPassThrough = false;
        }
        else if (ProtocolSequence.IsMccp2Negotiation(inputBuffer))
        {
            outputBuffer.Add((byte)ProtocolValue.IAC);
            outputBuffer.Add(proxyConfig.EnableMccp && !isClient ? (byte)ProtocolValue.DO : (byte)ProtocolValue.DONT);
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

        return new HandlerResult(bytesProcessed, shouldPassThrough, compressionStarted);
    }

    public static string CommandsToString(Span<byte> commandBuffer)
    {
        StringBuilder sb = new();
        sb.Append("IAC");

        bool inSubOption = false;
        bool isSubOptionFirstByte = false;
        byte[] subOptionBytes = ArrayPool<byte>.Shared.Rent(commandBuffer.Length);
        int subOptionBytesLength = 0;
        for (int i = 1; i < commandBuffer.Length; i++)
        {
            if (commandBuffer[i] == (byte)ProtocolValue.IAC)
            {
                inSubOption = false;

                if (subOptionBytesLength > 0)
                {
                    sb.Append(' ');
                    sb.Append(Encoding.ASCII.GetString(subOptionBytes, 0, subOptionBytesLength));
                    subOptionBytesLength = 0;
                }
            }

            if (inSubOption
                && !isSubOptionFirstByte
                // 32 -> 126 are printable ASCII characters
                && commandBuffer[i] >= 32
                && commandBuffer[i] <= 126)
            {
                subOptionBytes[subOptionBytesLength++] = commandBuffer[i];
            }
            else
            {
                sb.Append(' ');
                string? enumValue = Enum.GetName(typeof(ProtocolValue), commandBuffer[i]);
                sb.Append(string.IsNullOrEmpty(enumValue)
                    ? string.Format("0x{0:X2} ({0})", commandBuffer[i])
                    : enumValue);
            }

            if (isSubOptionFirstByte)
            {
                isSubOptionFirstByte = false;
            }

            if (commandBuffer[i] == (byte)ProtocolValue.SB)
            {
                inSubOption = true;
                isSubOptionFirstByte = true;
            }
        }

        if (subOptionBytesLength > 0)
        {
            sb.Append(' ');
            sb.Append(Encoding.ASCII.GetString(subOptionBytes, 0, subOptionBytesLength));
        }

        ArrayPool<byte>.Shared.Return(subOptionBytes);

        return sb.ToString();
    }
}
