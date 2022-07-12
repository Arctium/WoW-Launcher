// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Arctium.WoW.Launcher.Misc;

static class Helpers
{
    public static (int Major, int Minor, int Revision, int Build) GetVersionValueFromClient(string fileName)
    {
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(fileName);

        return (fileVersionInfo.FileMajorPart, fileVersionInfo.FileMinorPart,
                fileVersionInfo.FileBuildPart, fileVersionInfo.FilePrivatePart);
    }

    public static void PrintHeader(string serverName)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;

        Console.WriteLine(@"_____________World of Warcraft___________");
        Console.WriteLine(@"                   _   _                 ");
        Console.WriteLine(@"    /\            | | (_)                ");
        Console.WriteLine(@"   /  \   _ __ ___| |_ _ _   _ _ __ ___  ");
        Console.WriteLine(@"  / /\ \ | '__/ __| __| | | | | '_ ` _ \ ");
        Console.WriteLine(@" / ____ \| | | (__| |_| | |_| | | | | | |");
        Console.WriteLine(@"/_/    \_\_|  \___|\__|_|\__,_|_| |_| |_|");
        Console.WriteLine("");

        var sb = new StringBuilder();

        sb.Append("_________________________________________");

        var nameStart = (42 - serverName.Length) / 2;

        sb.Insert(nameStart, serverName);
        sb.Remove(nameStart + serverName.Length, serverName.Length);

        Console.WriteLine(sb);
        Console.WriteLine("{0,30}", "https://arctium.io");

        Console.WriteLine();
        Console.WriteLine($"Operating System: {RuntimeInformation.OSDescription}");
    }

    public static bool IsFileClosed(string filename)
    {
        try
        {
            using (var inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                return false;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
