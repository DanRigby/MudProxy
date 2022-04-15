using System.CommandLine;
using MudProxy;

CancellationTokenSource cancelTokenSource = new();
CancellationToken cancelToken = cancelTokenSource.Token;

Console.CancelKeyPress += delegate(object? _, ConsoleCancelEventArgs e)
{
    e.Cancel = true;
    cancelTokenSource.Cancel();
};

RootCommand rootCommand = new("MUD Proxy");

Option<string> hostNameOption =
    new("--hostname", "Hostname of the MUD server to connect to.") { IsRequired = true };
hostNameOption.AddAlias("-h");
rootCommand.Add(hostNameOption);

Option<int> hostPortOption =
    new("--host-port", "Port to connect to the MUD server on.") { IsRequired = true };
hostPortOption.AddAlias("-p");
rootCommand.Add(hostPortOption);

Option<int> proxyPortOption =
    new("--proxy-port", "Port the MUD proxy will listen for clients on.") { IsRequired = true };
proxyPortOption.AddAlias("-l");
rootCommand.Add(proxyPortOption);

Option<string> terminalTypeOption =
    new("--term-type", () => "xterm", "Terminal type to use for the client.");
terminalTypeOption.AddAlias("-t");
rootCommand.Add(terminalTypeOption);

Option<bool> mccpOption =
    new("--mccp2", "Enable Mud Client Compression V2 if the server supports it.");
mccpOption.AddAlias("-c");
rootCommand.Add(mccpOption);

Option<bool> mxpOption = new("--mxp", "Enable Mud Extension Protocol if the server supports it.");
mxpOption.AddAlias("-x");
rootCommand.Add(mxpOption);

rootCommand.SetHandler(async (
    string hostName, int hostPort, int proxyPort, string terminalType, bool enableMccp2, bool enableMxp
) =>
{
    Proxy proxy = new(terminalType, enableMccp2, enableMxp);

    Task clientTask = proxy.ListenForClientsAsync(proxyPort, cancelToken);
    Console.WriteLine("Listening for clients on port {0}", proxyPort);
    Console.WriteLine("Press Enter to connect to the MUD server.");
    Console.WriteLine();

    Console.ReadLine();
    Task hostTask = proxy.ConnectToHostAsync(hostName, hostPort, cancelToken);

    Console.WriteLine("Proxy running. Press CTRL+C to exit.");
    Console.WriteLine();

    await Task.WhenAll(clientTask, hostTask);

    Console.WriteLine("Program exiting.");
}, hostNameOption, hostPortOption, proxyPortOption, terminalTypeOption, mccpOption, mxpOption);

await rootCommand.InvokeAsync(args);

return 0;