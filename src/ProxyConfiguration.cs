namespace MudProxy;

public class ProxyConfiguration
{
    public ProxyConfiguration(bool enableMccp2)
    {
        EnableMccp2 = enableMccp2;
    }

    public bool EnableMccp2 { get; }
}
