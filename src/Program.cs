// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Arctium.WoW.Launcher.Misc.Helpers;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.CommandLine.Invocation;

namespace Arctium.WoW.Launcher;

class Program
{
    static async Task Main(string[] args)
    {
        PrintHeader("WoW Client Launcher");

        LaunchOptions.RootCommand.Handler = CommandHandler.Create((ParseResult parseResult) =>
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
