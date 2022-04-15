// ReSharper disable IdentifierTypo

namespace MudProxy;

public static class ProtocolSequence
{
    public static bool IsTerminalTypeNegotiation(byte[] data)
    {
        return
            data.Length >= 3
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.DO
            && data[2] == (byte)ProtocolValue.TERMTYPE;
    }

    public static bool IsTerminalTypeSendRequest(byte[] data)
    {
        return
            data.Length >= 6
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.SB
            && data[2] == (byte)ProtocolValue.TERMTYPE
            && data[3] == 0x01 // Send request
            && data[4] == (byte)ProtocolValue.IAC
            && data[5] == (byte)ProtocolValue.SE;
    }

    public static bool IsWindowSizeNegotiation(byte[] data)
    {
        return
            data.Length >= 3
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.DO
            && data[2] == (byte)ProtocolValue.NAWS;
    }

    public static bool IsNewEnvironmentNegotiation(byte[] data)
    {
        return
            data.Length >= 3
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.DO
            && data[2] == (byte)ProtocolValue.NEWENVIRONMENT;
    }

    public static bool IsMccp2Negotiation(byte[] data)
    {
        return
            data.Length >= 3
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.WILL
            && data[2] == (byte)ProtocolValue.MCCP2;
    }

    public static bool IsMccp2Confirmation(byte[] data)
    {
        return
            data.Length >= 5
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.SB
            && data[2] == (byte)ProtocolValue.MCCP2
            && data[3] == (byte)ProtocolValue.IAC
            && data[4] == (byte)ProtocolValue.SE;
    }

    public static bool IsMccp1Negotiation(byte[] data)
    {
        return
            data.Length >= 3
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.WILL
            && data[2] == (byte)ProtocolValue.MCCP1;
    }

    public static bool IsMxpNegotiation(byte[] data)
    {
        return
            data.Length >= 3
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.DO
            && data[2] == (byte)ProtocolValue.MXP;
    }

    public static bool IsMxpClientNegotiation(byte[] data)
    {
        return
            data.Length >= 3
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.WILL
            && data[2] == (byte)ProtocolValue.MXP;
    }

    public static bool IsMxpConfirmation(byte[] data)
    {
        return
            data.Length >= 5
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.SB
            && data[2] == (byte)ProtocolValue.MXP
            && data[3] == (byte)ProtocolValue.IAC
            && data[4] == (byte)ProtocolValue.SE;
    }

    public static bool IsGmcpNegotiation(byte[] data)
    {
        return
            data.Length >= 3
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.WILL
            && data[2] == (byte)ProtocolValue.GMCP;
    }

    public static bool IsMsspNegotiation(byte[] data)
    {
        return
            data.Length >= 3
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.WILL
            && data[2] == (byte)ProtocolValue.MSSP;
    }

    public static bool IsZmpNegotiation(byte[] data)
    {
        return
            data.Length >= 3
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.WILL
            && data[2] == (byte)ProtocolValue.ZMP;
    }
}
