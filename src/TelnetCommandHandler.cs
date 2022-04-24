namespace MudProxy;

public class TelnetCommandHandler
{
    private readonly ProxyConfiguration _proxyConfiguration;
    private readonly Stream _networkStream;
    private readonly bool _isHost;
    private readonly string _consolePrefix;

    public TelnetCommandHandler(ProxyConfiguration proxyConfiguration, Stream networkStream, bool isHost)
    {
        _proxyConfiguration = proxyConfiguration;
        _networkStream = networkStream;
        _isHost = isHost;
        _consolePrefix = isHost ? "[PROXY-H]: " : "[PROXY-C]:";
    }

    public async Task<bool> HandleCommand(ReadOnlyMemory<byte> telnetCommand, CancellationToken cancelToken)
    {
        bool suppressCommand = false;

        if (telnetCommand.Span.SequenceEqual(TelnetCommandSequence.Mccp1Negotiation.Span))
        {
            suppressCommand = true;
            await _networkStream.WriteAsync(TelnetCommandSequence.Mccp1NotSupported, cancelToken);
            Console.WriteLine(string.Concat(_consolePrefix, TelnetCommandSequence.Mccp1NotSupportedString));
        }
        else if (telnetCommand.Span.SequenceEqual(TelnetCommandSequence.Mccp2Negotiation.Span))
        {
            suppressCommand = true;
            if (_proxyConfiguration.EnableMccp && _isHost)
            {
                await _networkStream.WriteAsync(TelnetCommandSequence.Mccp2Supported, cancelToken);
                Console.WriteLine(string.Concat(_consolePrefix, TelnetCommandSequence.Mccp2SupportedString));
            }
            else
            {
                await _networkStream.WriteAsync(TelnetCommandSequence.Mccp2NotSupported, cancelToken);
                Console.WriteLine(string.Concat(_consolePrefix, TelnetCommandSequence.Mccp2NotSupportedString));
            }
        }
        else if (telnetCommand.Span.SequenceEqual(TelnetCommandSequence.Mccp2Confirmation.Span))
        {
            suppressCommand = true;
        }
        else if (telnetCommand.Span.SequenceEqual(TelnetCommandSequence.Mccp3Negotiation.Span))
        {
            suppressCommand = true;
            await _networkStream.WriteAsync(TelnetCommandSequence.Mccp3NotSupported, cancelToken);
            Console.WriteLine(string.Concat(_consolePrefix, TelnetCommandSequence.Mccp3NotSupportedString));
        }

        return suppressCommand;
    }
}
