namespace MudProxy;

public static class TelnetExtensions
{
    public static string ToCommandString(this byte command)
    {
        string? enumValue = Enum.GetName((TelnetCommand)command);
        return enumValue ?? $"0x{command:X2}";
    }

    public static string ToOptionString(this byte option)
    {
        string? enumValue = Enum.GetName((TelnetOption)option);
        return enumValue ?? $"0x{option:X2}";
    }
}
