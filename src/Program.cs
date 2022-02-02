// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

using static Arctium.WoW.Launcher.Misc.Helpers;

namespace Arctium.WoW.Launcher;

class Program
{
    public static CancellationTokenSource CancellationTokenSource { get; private set; }

    static async Task Main(string[] args)
    {
        CancellationTokenSource = new CancellationTokenSource();

        PrintHeader("WoW Client Launcher");

        LaunchOptions.RootCommand.SetHandler((ParseResult parseResult) =>
        {
            var appPath = Launcher.PrepareGameLaunch(parseResult);
            var gameCommandLine = string.Join(" ", parseResult.UnmatchedTokens);

            if (string.IsNullOrEmpty(appPath) || !Launcher.LaunchGame(appPath, gameCommandLine))
                WaitAndExit(5000);
        });

        await LaunchOptions.Instance.InvokeAsync(args);
    }

    public static void WaitAndExit(int ms = 2000)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"Closing in {ms / 1000} seconds...");

        Thread.Sleep(ms);

        Environment.Exit(0);
    }
}
