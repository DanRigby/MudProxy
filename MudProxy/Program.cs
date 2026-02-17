// ReSharper disable RedundantLambdaParameterType

using System.CommandLine;
using MudProxy;

CancellationTokenSource cancelTokenSource = new();
CancellationToken cancelToken = cancelTokenSource.Token;

Console.CancelKeyPress += (object? _, ConsoleCancelEventArgs e) =>
{
    e.Cancel = true;
    cancelTokenSource.Cancel();
};

RootCommand rootCommand = new("MUD Proxy");

Option<string> hostNameOption =
    new("--hostname") { Description = "Hostname of the MUD server to connect to.", Required = true };
hostNameOption.Aliases.Add("-h");
rootCommand.Add(hostNameOption);

Option<int> hostPortOption =
    new("--host-port") { Description = "Port to connect to the MUD server on.", Required = true };
hostPortOption.Aliases.Add("-p");
rootCommand.Add(hostPortOption);

Option<int> proxyPortOption =
    new("--proxy-port") { Description = "Port the MUD proxy will listen for clients on.", Required = true };
proxyPortOption.Aliases.Add("-l");
rootCommand.Add(proxyPortOption);

Option<bool> mccp2Option =
    new("--mccp") { Description = "Enable MUD Client Compression V2 (MCCP2) if the server supports it." };
mccp2Option.Aliases.Add("-c");
rootCommand.Add(mccp2Option);

rootCommand.SetAction(async parseResult =>
{
    string hostName = parseResult.GetValue(hostNameOption)!;
    int hostPort = parseResult.GetValue(hostPortOption);
    int proxyPort = parseResult.GetValue(proxyPortOption);
    bool enableMccp2 = parseResult.GetValue(mccp2Option);

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
});

await rootCommand.Parse(args).InvokeAsync();

return 0;
