// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace MudProxy;

public enum TelnetCommand : byte
{
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
    IAC = 0xFF
}
