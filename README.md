# MudProxy
 
![screenshot](https://user-images.githubusercontent.com/114417/163694839-1b3c6dc5-b092-45ed-ade6-afb13d07315d.png)

## What is MudProxy?

MudProxy is a simple multi-client proxy for use with Multi User Dungeon (MUD) servers.

## Features

 * Enables multiple clients (desktop, tablet, phone, etc) to share a single connection to a MUD server.
 * Supports MUD Client Compression Protocol V2 (MCCP2) between the proxy and MUD server.
   * Communication between the proxy and clients is done in plain text.
 * Displays the communication between the proxy, server, and clients in the console, including Telnet command sequences.

## Concepts

### Primary Client

 * The first client to connect to the proxy is deemed the primary client.
 * So as not to flood the server with multiple responses to every telnet command, only the primary client's telnet commands are sent to the server. Non Telnet command data is sent from all clients to the MUD server.
 * If the primary client disconnects, the next client to connect will become the new primary.

## Warnings

 * Usage of a proxy such as this, especially to enable connecting with multiple clients may be against the rules for some MUD servers. Please check the rules for your server before using.
 * The proxy displays all client communications in plain text in the console. This includes passwords that are sent as part of logging in to a MUD server.
 * There are probably bugs. I've only tested this proxy with a few servers. Please report any bugs you find.

## Using

* There's no prebuilt binaries currently. It *should* compile and run on Windows, macOS, and Linux, but I've only tested it on Windows so far.
* You'll need the .NET 6 SDK or higher installed to build and run it.
* You can build and run the proxy from the project folder by using: `dotnet run -- [options]`
* Supported Options:
  * `--hostname <server>`: (Required) The hostname of the MUD server to connect to.
  * `--host-port <port>`: (Required) The port of the MUD server to connect on.
  * `--proxy-port <port>`: (Required) The port for the proxy to listen for connections from MUD clients on.
  * `--mccp`: (Optional) If specified, the proxy will attempt to use MUD Client Compression V2 (MCCP2) for communication with the MUD server if it supports it.
* Example: `dotnet run -- --hostname aardmud.org --host-port 23 --proxy-port 9898 --mccp`

## Contributing

Just open an Issue, or submit a Pull Request. This is a hobby project, so please be patient. (:
