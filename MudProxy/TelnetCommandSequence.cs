namespace MudProxy;

public static class TelnetCommandSequence
{
    public static readonly ReadOnlyMemory<byte> Mccp1Negotiation =
        new[] { (byte)TelnetCommand.IAC, (byte)TelnetCommand.WILL, (byte)TelnetOption.MCCP1 };
    public static readonly string Mccp1NegotiationString = "IAC WILL MCCP1";

    public static readonly ReadOnlyMemory<byte> Mccp2Negotiation =
        new[] { (byte)TelnetCommand.IAC, (byte)TelnetCommand.WILL, (byte)TelnetOption.MCCP2 };
    public static readonly string Mccp2NegotiationString = "IAC WILL MCCP2";

    public static readonly ReadOnlyMemory<byte> Mccp3Negotiation =
        new[] { (byte)TelnetCommand.IAC, (byte)TelnetCommand.WILL, (byte)TelnetOption.MCCP3 };
    public static readonly string Mccp3NegotiationString = "IAC WILL MCCP3";

    public static readonly ReadOnlyMemory<byte> Mccp2Confirmation = new[]
    {
        (byte)TelnetCommand.IAC,
        (byte)TelnetCommand.SB,
        (byte)TelnetOption.MCCP2,
        (byte)TelnetCommand.IAC,
        (byte)TelnetCommand.SE
    };
    public static readonly string Mccp2ConfirmationString = "IAC SB MCCP2 IAC SE";

    public static readonly ReadOnlyMemory<byte> Mccp1NotSupported =
        new[] { (byte)TelnetCommand.IAC, (byte)TelnetCommand.DONT, (byte)TelnetOption.MCCP1 };
    public static readonly string Mccp1NotSupportedString = "IAC DONT MCCP1";

    public static readonly ReadOnlyMemory<byte> Mccp2NotSupported =
        new[] { (byte)TelnetCommand.IAC, (byte)TelnetCommand.DONT, (byte)TelnetOption.MCCP2 };
    public static readonly string Mccp2NotSupportedString = "IAC DONT MCCP2";

    public static readonly ReadOnlyMemory<byte> Mccp3NotSupported =
        new[] { (byte)TelnetCommand.IAC, (byte)TelnetCommand.DONT, (byte)TelnetOption.MCCP3 };
    public static readonly string Mccp3NotSupportedString = "IAC DONT MCCP3";

    public static readonly ReadOnlyMemory<byte> Mccp2Supported =
        new[] { (byte)TelnetCommand.IAC, (byte)TelnetCommand.DO, (byte)TelnetOption.MCCP2 };
    public static readonly string Mccp2SupportedString = "IAC DO MCCP2";
}
