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

Option<bool> mccp2Option =
    new("--mccp2", "Enable MUD Client Compression V2 (MCCP2) if the server supports it.");
mccp2Option.AddAlias("-c");
rootCommand.Add(mccp2Option);

rootCommand.SetHandler(async (
    string hostName, int hostPort, int proxyPort, bool enableMccp2
) =>
{
    Proxy proxy = new(enableMccp2);

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
}, hostNameOption, hostPortOption, proxyPortOption, mccp2Option);

await rootCommand.InvokeAsync(args);

return 0;
