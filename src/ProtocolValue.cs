// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

namespace MudProxy;

public enum ProtocolValue : byte
{
    //
    // Telnet Commands
    //

    // 239 - End Of Record
    EF = 0xEF,

    // 240 - Sub Option End
    SE = 0xF0,

    // 241 - No Operation
    NOP = 0xF1,

    // 242 - Data Mark
    DM = 0xF2,

    // 243 - Break
    BRK = 0xF3,

    // 244 - Interrupt Process
    IP = 0xF4,

    // 245 - Abort Output
    AO = 0xF5,

    // 246 - Are You There
    AYT = 0xF6,

    // 247 - Erase Character
    EC = 0xF7,

    // 248 - Erase Line
    EL = 0xF8,

    // 249 - Go Ahead
    GA = 0xF9,

    // 250 - Sub Option Begin
    SB = 0xFA,

    // 251 - Will
    WILL = 0xFB,

    // 252 - Wont
    WONT = 0xFC,

    // 253 - Do
    DO = 0xFD,

    // 254 - Dont
    DONT = 0xFE,

    // 255 - Interpret As Command
    IAC = 0xFF,

    //
    // Telnet Options
    //

    // 0 - Null
    NULL = 0x00,

    // 1 - Echo
    ECHO = 0x01,

    // 3 - Suppress Go Ahead
    SGA = 0x03,

    // 5 - Status
    STATUS = 0x05,

    // 6 - Timing Mark
    TIMINGMARK = 0x06,

    // 32 - Terminal Speed
    TERMSPEED = 0x20,

    // 33 - Remote Flow Control
    RFC = 0x21,

    // 34 - Line Mode
    LINEMODE = 0x22,

    // 36 - Environment Variables
    EV = 0x24,

    // 24 - Terminal Type
    TERMTYPE = 0x18,

    // 25 - End Of Record
    EOR = 0x19,

    // 31 - Window Size
    NAWS = 0x1F,

    // 39 - New Environment
    NEWENVIRONMENT = 0x27,

    // 42 - Character Set
    CHARSET = 0x2A,

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
    MXP = 0x5B,

    // 93 - Zenith MUD Protocol
    // https://discworld.starturtle.net/lpc/playing/documentation.c?path=%2fconcepts%2fzmp
    ZMP = 0x5D,

    // 102 - Aardwolf Telnet Protocol
    AARDWOLF = 0x66,

    // 200 - Achaea Telnet Protocol
    ATCP = 0xC8,

    // 201 - Generic MUD Control Protocol
    // https://tintin.mudhalla.net/protocols/gmcp/
    // https://discworld.starturtle.net/lpc/playing/documentation.c?path=%2fconcepts%2fgmcp
    GMCP = 0xC9
}

// https://mudcoders.fandom.com/wiki/List_of_Telnet_Options

// https://github.com/daxuzi/mushclient/blob/master/MUSHclient/worlds/plugins/Code_Chart.xml
