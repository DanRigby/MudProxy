namespace MudProxy;

public class ProxyConfiguration
{
    public ProxyConfiguration(string terminalType, bool enableMccp2, bool enableMxp, bool decompressionRequired)
    {
        TerminalType = terminalType;
        EnableMccp2 = enableMccp2;
        EnableMxp = enableMxp;
        DecompressionRequired = decompressionRequired;
    }

    public string TerminalType { get; }
    public bool EnableMccp2 { get; }
    public bool EnableMxp { get; }
    public bool DecompressionRequired { get; set; }

    public void Deconstruct(
        out string terminalType, out bool enableMccp2, out bool enableMxp, out bool decompressionRequired)
    {
        terminalType = TerminalType;
        enableMccp2 = EnableMccp2;
        enableMxp = EnableMxp;
        decompressionRequired = DecompressionRequired;
    }
}
