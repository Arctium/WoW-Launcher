// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Builder;
using System.CommandLine.Parsing;

namespace Arctium.WoW.Launcher;

static class LaunchOptions
{
    public static bool IsDevModeAllowed { get; set; }

    public static Option<GameVersion> Version = new("--version", () => GameVersion.Retail);
    public static Option<string> GamePath = new("--path");
    public static Option<string> GameBinary = new("--binary");
    public static Option<bool> KeepCache = new("--keepcache", () => true);
    public static Option<bool> UseStaticAuthSeed = new("--staticseed");
    public static Option<bool> DevMode = new("--dev", () => true);
    public static Option<string> VersionUrl = new("--versionurl");
    public static Option<string> CdnsUrl = new("--cdnsurl");
    public static Option<string> ProductName = new("--product", () => "wow");
    public static Option<string> CdnRegion = new("--region", () => "EU");

    // Game command line options.
    public static Option<string> GameConfig = new("-config", () => "Config.wtf");

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
        KeepCache,
        UseStaticAuthSeed,
        DevMode,
        VersionUrl,
        CdnsUrl,
        ProductName,
        CdnRegion,
        GameConfig
    };

    static Command ConfigureCommandLine(Command rootCommand)
    {
        // Do not show errors for unknown command line parameters.
        rootCommand.TreatUnmatchedTokensAsErrors = false;

        return rootCommand;
    }
}
