namespace MudProxy;

// ReSharper disable InconsistentNaming
public enum TelnetCommand : byte
{
    // 240 - Sub Option End
    SE = 0xf0,

    // 250 - Sub Option Begin
    SB = 0xfa,

    // 251 - Will
    WILL = 0xfb,

    // 252 - Wont
    WONT = 0xfc,

    // 253 - Do
    DO = 0xfd,

    // 254 - Dont
    DONT = 0xfe,

    // 255 - Interpret As Command
    IAC = 0xff
}

public enum TelnetOption : byte
{
    // 24 - Terminal Type
    TT = 0x18,

    // 31 - Window Size
    WS = 0x1f,

    // 39 - New Environment
    NE = 0x27,

    // 70 - MUD Server Status Protocol
    // https://mudhalla.net/tintin/protocols/mssp/
    MSSP = 0x46,

    // 85 - MUD Client Compression Protocol v1
    // https://www.zuggsoft.com/zmud/mcp.htm
    // https://tintin.mudhalla.net/protocols/mccp/
    MCCP1 = 0x55,

    // 86 - MUD Client Compression Protocol v2
    // https://www.zuggsoft.com/zmud/mcp.htm
    // https://tintin.mudhalla.net/protocols/mccp/
    MCCP2 = 0x56,

    // 87 - MUD Client Compression Protocol v3
    // https://tintin.mudhalla.net/protocols/mccp/
    MCCP3 = 0x57,

    // 91 - MUD eXtension Protocol
    // https://www.zuggsoft.com/zmud/mxp.htm
    // https://discworld.starturtle.net/lpc/playing/documentation.c?path=%2fconcepts%2fmxp
    MXP = 0x5b,

    // 93 - Zenith MUD Protocol
    // https://discworld.starturtle.net/lpc/playing/documentation.c?path=%2fconcepts%2fzmp
    ZMP = 0x5d,

    // 201 - Generic MUD Control Protocol
    // https://tintin.mudhalla.net/protocols/gmcp/
    // https://discworld.starturtle.net/lpc/playing/documentation.c?path=%2fconcepts%2fgmcp
    GMCP = 0xc9
}
// ReSharper enable InconsistentNaming


/*
https://mudcoders.fandom.com/wiki/List_of_Telnet_Options
 
https://github.com/daxuzi/mushclient/blob/master/MUSHclient/worlds/plugins/Code_Chart.xml

[0xEF] = "EOR"
[0xF0] = "SE"
[0xF1] = "NOP"
[0xF2] = "DM"
[0xF3] = "BRK"
[0xF4] = "IP"
[0xF5] = "AO"
[0xF6] = "AYT"
[0xF7] = "EC"
[0xF8] = "EL"
[0xF9] = "GA"
[0xFA] = "SB"
[0xFB] = "WILL"
[0xFC] = "WONT"
[0xFD] = "DO"
[0xFE] = "DONT"
[0xFF] = "IAC"
 
[0x01] = "Echo",                    --   1 Echo
[0x03] = "Suppress Go-ahead (SGA)", --   3 Suppress go ahead
[0x05] = "Status",                  --   5 Status
[0x06] = "Timing Mark",             --   6 Timing mark
[0x18] = "Termtype",                --  24 Terminal type
[0x19] = "End of record (EOR)",     --  25 EOR
[0x1F] = "Window Size (NAWS)",      --  31 Window size
[0x20] = "Terminal Speed",          --  32 Terminal speed
[0x21] = "RFC",                     --  33 Remote flow control
[0x22] = "Line Mode",               --  34 Line mode
[0x24] = "EV",                      --  36 Environment variables
[0x2A] = "Charset",                 --  42 Character set
[0x46] = "MSSP",                    --  70 MUD Server Status Protocol
[0x55] = "MCCP1",                   --  85 MUD Compression Protocol v1
[0x56] = "MCCP2",                   --  86 MUD Compression Protocol v2
[0x5A] = "MSP",                     --  90 (MUD Sound Protocol)
[0x5B] = "MXP",                     --  91 (MUD eXtension Protocol)
[0x5D] = "ZMP",                     --  93 (Zenith Mud Protocol)
[0x66] = "Aardwolf",                -- 102 (Aardwolf telnet protocol)
[0xC8] = "ATCP",                    -- 200 ATCP (Achaea Telnet Protocol)
[0xC9] = "ATCP2/GMCP",              -- 201 ATCP2/GMCP (Generic Mud Control Protocol)
*/