// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;
using Arctium.WoW.Launcher;
using static Arctium.WoW.Launcher.Misc.Helpers;

// "Arctium" should not be removed from the final binary name.
if (!Process.GetCurrentProcess().ProcessName.ToLowerInvariant().Contains("arctium"))
    WaitAndExit();

PrintHeader("WoW Client Launcher");

LaunchOptions.RootCommand.SetHandler(context =>
{
    var appPath = Launcher.PrepareGameLaunch(context.ParseResult);
    var gameCommandLine = string.Join(" ", context.ParseResult.UnmatchedTokens);

    if (string.IsNullOrEmpty(appPath) || !Launcher.LaunchGame(appPath, gameCommandLine))
        WaitAndExit(5000);
});

await LaunchOptions.Instance.InvokeAsync(args);


static void WaitAndExit(int ms = 2000)
{
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine($"Closing in {ms / 1000} seconds...");

    Thread.Sleep(ms);

    Environment.Exit(0);
}
