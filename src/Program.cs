// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;
using Arctium.WoW.Launcher;

using static Arctium.WoW.Launcher.Misc.Helpers;

// "Arctium" should not be removed from the final binary name.
if (!Process.GetCurrentProcess().ProcessName.Contains("arctium", StringComparison.InvariantCultureIgnoreCase))
    WaitAndExit();

PrintHeader("WoW Client Launcher");

LaunchOptions.RootCommand.SetHandler(async context =>
{
    CreateDevIPFilter(out var ipFilter);

    // Prefer / instead of \ for the client path.
    var appPath = (await Launcher.PrepareGameLaunch(context.ParseResult, ipFilter)).Replace("\\", "/");
    var gameCommandLine = string.Join(" ", context.ParseResult.UnmatchedTokens);

    // Add config parameter to the game command line.
    gameCommandLine += $" -config {context.ParseResult.GetValueForOption(LaunchOptions.GameConfig)}";

    if (string.IsNullOrEmpty(appPath) || !Launcher.LaunchGame(appPath, gameCommandLine, context.ParseResult))
        WaitAndExit(5000);
});

await LaunchOptions.Instance.InvokeAsync(args);
return;

void CreateDevIPFilter(out IPFilter ipFilter)
{
    ipFilter = new IPFilter();

    ipFilter.AddCidrRange("0.0.0.0/8");
    ipFilter.AddCidrRange("10.0.0.0/8");
    ipFilter.AddCidrRange("100.64.0.0/10");
    ipFilter.AddCidrRange("127.0.0.0/8");
    ipFilter.AddCidrRange("169.254.0.0/16");
    ipFilter.AddCidrRange("172.16.0.0/12");
    ipFilter.AddCidrRange("192.0.0.0/24");
    ipFilter.AddCidrRange("192.0.0.0/29");
    ipFilter.AddCidrRange("192.0.0.8/32");
    ipFilter.AddCidrRange("192.0.0.9/32");
    ipFilter.AddCidrRange("192.0.0.170/32");
    ipFilter.AddCidrRange("192.0.0.171/32");
    ipFilter.AddCidrRange("192.0.2.0/24");
    ipFilter.AddCidrRange("192.31.196.0/24");
    ipFilter.AddCidrRange("192.52.193.0/24");
    ipFilter.AddCidrRange("192.88.99.0/24");
    ipFilter.AddCidrRange("192.168.0.0/16");
    ipFilter.AddCidrRange("192.175.48.0/24");
    ipFilter.AddCidrRange("198.18.0.0/15");
    ipFilter.AddCidrRange("198.51.100.0/24");
    ipFilter.AddCidrRange("203.0.113.0/24");
    ipFilter.AddCidrRange("240.0.0.0/4");
    ipFilter.AddCidrRange("255.255.255.255/32");
}

static void WaitAndExit(int ms = 2000)
{
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine($"Closing in {ms / 1000} seconds...");

    Thread.Sleep(ms);

    Environment.Exit(0);
}
