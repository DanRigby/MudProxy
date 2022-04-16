namespace MudProxy;

public class ProxyConfiguration
{
    public ProxyConfiguration(bool enableMccp)
    {
        EnableMccp = enableMccp;
    }

    public bool EnableMccp { get; }
}
