// ReSharper disable IdentifierTypo

namespace MudProxy;

public static class ProtocolSequence
{
    public static bool IsMccp3Negotiation(byte[] data)
    {
        return
            data.Length >= 3
            && data[0] == (byte)ProtocolValue.IAC
            && data[1] == (byte)ProtocolValue.WILL
            && data[2] == (byte)ProtocolValue.MCCP3;
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
}
