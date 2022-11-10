// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Builder;
using System.CommandLine.Parsing;

namespace Arctium.WoW.Launcher.Misc;

static class LaunchOptions
{
    public static Option<GameVersion> Version = new("--version", () => GameVersion.Retail);
    public static Option<string> GamePath = new("--path");
    public static Option<string> GameBinary = new("--binary");
    public static Option<bool> KeepCache = new("--keepcache", () => true);

    public static Parser Instance => new CommandLineBuilder(ConfigureCommandLine(RootCommand))
        .UseHelp()
        .UseParseDirective()
        .CancelOnProcessTermination()
        .UseParseErrorReporting()
        .UseSuggestDirective()
        .Build();

    public static RootCommand RootCommand = new("Arctium WoW Launcher")
    {
        Version,
        GamePath,
        GameBinary,
        KeepCache
    };

    static Command ConfigureCommandLine(Command rootCommand)
    {
        // Do not show errors for unknown command line parameters.
        rootCommand.TreatUnmatchedTokensAsErrors = false;

        return rootCommand;
    }
}
