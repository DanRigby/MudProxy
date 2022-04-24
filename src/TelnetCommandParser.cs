using System.Buffers;
using System.Text;

namespace MudProxy;

public class TelnetCommandParser
{
    private readonly StringBuilder _stringCommandValueBuilder = new();
    private readonly ArrayBufferWriter<byte> _commandBuffer = new(1024 * 8);
    private readonly ArrayBufferWriter<byte> _subOptionBuffer = new(1024 * 8);
    private readonly byte[] _commandByteArray = new byte[1];

    private bool _inSubOption;
    private bool _isComplete;
    private bool _clearOnNextByte;

    public (ReadOnlyMemory<byte>, string) ProcessCommandByte(byte commandByte)
    {
        if (_clearOnNextByte)
        {
            _clearOnNextByte = false;
            _commandBuffer.Clear();
        }

        if (_commandBuffer.WrittenCount == 0 && commandByte != (byte)TelnetCommand.IAC)
        {
            throw new InvalidOperationException("First byte is not IAC, invalid command data.");
        }

        _commandByteArray[0] = commandByte;
        _commandBuffer.Write(_commandByteArray);

        if (_commandBuffer.WrittenCount == 1 && commandByte == (byte)TelnetCommand.IAC)
        {
            _stringCommandValueBuilder.Append(commandByte.ToCommandString());
        }
        else if (_commandBuffer.WrittenCount == 2)
        {
            if (commandByte == (byte)TelnetCommand.SB)
            {
                _inSubOption = true;
            }
            else if (commandByte is not (
                (byte)TelnetCommand.WILL or
                (byte)TelnetCommand.WONT or
                (byte)TelnetCommand.DO or
                (byte)TelnetCommand.DONT))
            {
                _isComplete = true;
            }

            _stringCommandValueBuilder.Append(' ');
            _stringCommandValueBuilder.Append(commandByte.ToCommandString());
        }
        else if (_commandBuffer.WrittenCount == 3)
        {
            if (_inSubOption is false)
            {
                _isComplete = true;
            }

            _stringCommandValueBuilder.Append(' ');
            _stringCommandValueBuilder.Append(commandByte.ToOptionString());
        }
        else if (_commandBuffer.WrittenCount >= 4)
        {
            // Assuming _isSubOption is true as all non sub-option commands are 2 or 3 bytes long
            if (commandByte == (byte)TelnetCommand.IAC)
            {
                if (_subOptionBuffer.WrittenCount > 0)
                {
                    _stringCommandValueBuilder.Append(' ');
                    _stringCommandValueBuilder.Append(
                        Encoding.ASCII.GetString(_subOptionBuffer.WrittenSpan));

                    _subOptionBuffer.Clear();

                    _stringCommandValueBuilder.Append(' ');
                    _stringCommandValueBuilder.Append(commandByte.ToCommandString());
                }
            }
            else if (commandByte == (byte)TelnetCommand.SE)
            {
                _stringCommandValueBuilder.Append(' ');
                _stringCommandValueBuilder.Append(commandByte.ToCommandString());
                _inSubOption = false;
                _isComplete = true;
            }
            else
            {
                // 32 -> 126 are printable ASCII characters
                if (commandByte is >= 32 and <= 126)
                {
                    _subOptionBuffer.Write(_commandByteArray);
                }
                else
                {
                    _stringCommandValueBuilder.Append(' ');
                    _stringCommandValueBuilder.Append(commandByte.ToOptionString());
                }
            }
        }

        if (_isComplete)
        {
            ReadOnlyMemory<byte> byteResult = _commandBuffer.WrittenMemory;
            string stringResult = _stringCommandValueBuilder.ToString();

            _stringCommandValueBuilder.Clear();
            _isComplete = false;
            _clearOnNextByte = true;

            return (byteResult, stringResult);
        }

        return (ReadOnlyMemory<byte>.Empty, string.Empty);
    }
}
