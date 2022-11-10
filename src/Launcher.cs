// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;

using static Arctium.WoW.Launcher.Misc.Helpers;

namespace Arctium.WoW.Launcher;

class Launcher
{
    public static CancellationTokenSource CancellationTokenSource => new();

    public static string PrepareGameLaunch(ParseResult commandLineResult)
    {
        var gameVersion = commandLineResult.GetValueForOption(LaunchOptions.Version);
        var (SubFolder, BinaryName, MajorGameVersion, MinGameBuild) = gameVersion switch
        {
#if x64
            GameVersion.Retail => ("_retail_", "Wow.exe", new[] { 9, 10 }, 37862),
            GameVersion.Classic => ("_classic_", "WowClassic.exe", new[] { 2, 3 }, 39926),
            GameVersion.ClassicEra => ("_classic_era_", "WowClassic.exe", new[] { 1 }, 40347),
#elif ARM64
            GameVersion.Retail => ("_retail_", "Wow-ARM64.exe", new[] { 9, 10 }, 37862),
            GameVersion.Classic => ("_classic_", "WowClassic-arm64.exe", new[] { 2, 3 }, 39926),
            GameVersion.ClassicEra => ("_classic_era_", "WowClassic-arm64.exe", new[] { 1 }, 40347),
#endif
            _ => throw new NotImplementedException("Invalid game version specified."),
        };

        Console.ForegroundColor = ConsoleColor.Yellow;

        Console.WriteLine($"Mode: Custom Server ({gameVersion})");
        Console.WriteLine();
        Console.ResetColor();

        var currentFolder = AppDomain.CurrentDomain.BaseDirectory;
        var gameFolder = $"{currentFolder}/{SubFolder}";

        if (commandLineResult.HasOption(LaunchOptions.GameBinary))
            BinaryName = commandLineResult.GetValueForOption(LaunchOptions.GameBinary);

        var gameBinaryPath = $"{gameFolder}/{BinaryName}";

        if (commandLineResult.HasOption(LaunchOptions.GamePath))
        {
            gameFolder = commandLineResult.GetValueForOption(LaunchOptions.GamePath);
            gameBinaryPath = $"{gameFolder}/{BinaryName}";
        }
        else if (!File.Exists(gameBinaryPath))
        {
            // Also support game installations without branch sub folders.
            gameFolder = currentFolder;
            gameBinaryPath = $"{gameFolder}/{BinaryName}";
        }

        if (!File.Exists(gameBinaryPath) || !MajorGameVersion.Contains(GetVersionValueFromClient(gameBinaryPath).Major))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] No {gameVersion} client found.");

            return string.Empty;
        }

        var gameClientBuild = GetVersionValueFromClient(gameBinaryPath).Build;

        if (gameClientBuild < MinGameBuild && gameClientBuild != 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Your found client version {gameClientBuild} is not supported.");
            Console.WriteLine($"The minimum required build is {MinGameBuild}");

            return string.Empty;
        }

        // Delete the cache folder by default.
        if (!commandLineResult.GetValueForOption(LaunchOptions.KeepCache))
        {
            try
            {
                // Trying to delete the cache folder.
                Directory.Delete($"{gameFolder}/Cache", true);
            }
            catch (Exception)
            {
                // We don't care if it worked. Swallow it!
            }
        }

        return gameBinaryPath;
    }

    public static bool LaunchGame(string appPath, string gameCommandLine)
    {
        var startupInfo = new StartupInfo();
        var processInfo = new ProcessInformation();

        try
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Starting WoW client...");

            var createSuccess = NativeWindows.CreateProcess(null, $"{appPath} {gameCommandLine}", 0, 0, false, 4, 0, new FileInfo(appPath)?.DirectoryName, ref startupInfo, out processInfo);

            // On some systems we have to launch the game with the application name used.
            if (!createSuccess)
                createSuccess = NativeWindows.CreateProcess(appPath, $" {gameCommandLine}", 0, 0, false, 4, 0, null, ref startupInfo, out processInfo);

            // Start process with suspend flags.
            if (createSuccess)
            {
                using var gameAppData = new MemoryStream(File.ReadAllBytes(appPath));

                var memory = new WinMemory(processInfo, gameAppData.Length);

                // Resume the process to initialize it.
                NativeWindows.NtResumeProcess(processInfo.ProcessHandle);

                var mbi = new MemoryBasicInformation();

                // Wait for the memory region to be initialized.
                while (NativeWindows.VirtualQueryEx(processInfo.ProcessHandle, memory.BaseAddress, out mbi, MemoryBasicInformation.Size) == 0 || mbi.RegionSize <= 0x1000)
                { }

                if (mbi.BaseAddress != 0)
                {
                    NativeWindows.NtSuspendProcess(processInfo.ProcessHandle);

                    byte[] certBundleData = Convert.FromBase64String(Patches.Common.CertBundleData);

                    // Build the version URL from the game binary build.
                    var clientVersion = GetVersionValueFromClient(appPath);
                    byte[] versionPatch = Patches.Common.GetVersionUrl(clientVersion.Build);

                    // Refresh the client data before patching.
                    memory.RefreshMemoryData((int)gameAppData.Length);

                    // Wait for all direct memory patch tasks to complete,
                    Task.WaitAll(new[]
                    {
                        memory.PatchMemory(Patterns.Common.CertBundle, certBundleData, "Certificate Bundle"),
                        memory.PatchMemory(Patterns.Common.SignatureModulus, Patches.Common.SignatureModulus, "Certificate Signature RsaModulus"),
                        memory.PatchMemory(Patterns.Common.ConnectToModulus, Patches.Common.RsaModulus, "ConnectTo RsaModulus"),

                        // Recent clients have a different signing algorithm in EnterEncryptedMode.
                        (clientVersion is (9, 2, 7, _) or (3, _, _, _) or (10, _, _, _))
                        ? memory.PatchMemory(Patterns.Common.CryptoEdPublicKey, Patches.Common.CryptoEdPublicKey, "GameCrypto Ed25519 PublicKey")
                        : memory.PatchMemory(Patterns.Common.CryptoRsaModulus, Patches.Common.RsaModulus, "GameCrypto RsaModulus"),

                        memory.PatchMemory(Patterns.Common.Portal, Patches.Common.Portal, "Login Portal"),
                        memory.PatchMemory(Patterns.Common.VersionUrl, versionPatch, "Version URL"),
                        memory.PatchMemory(Patterns.Windows.LauncherLogin, Patches.Windows.LauncherLogin, "Launcher Login Registry")
                    }, CancellationTokenSource.Token);

                    NativeWindows.NtResumeProcess(processInfo.ProcessHandle);

                    // Wait for client initialization.
                    var initOffset = memory?.Read(mbi.BaseAddress, (int)mbi.RegionSize)?.FindPattern(Patterns.Windows.Init) ?? 0;

                    while (initOffset == 0)
                    {
                        initOffset = memory?.Read(mbi.BaseAddress, (int)mbi.RegionSize)?.FindPattern(Patterns.Windows.Init) ?? 0;

                        Console.WriteLine("Waiting for client initialization...");
                    }

                    initOffset += BitConverter.ToUInt32(memory.Read(initOffset + memory.BaseAddress + 2, 4), 0) + 10;

                    while (memory?.Read(initOffset + memory.BaseAddress, 1)?[0] == null ||
                           memory?.Read(initOffset + memory.BaseAddress, 1)?[0] == 0)
                        memory.Data = memory.Read(mbi.BaseAddress, (int)mbi.RegionSize);

                    PrepareAntiCrash(memory, ref mbi, ref processInfo);

                    memory.RefreshMemoryData((int)mbi.RegionSize);

#if x64
                    Task.WaitAll(new[]
                    {
                        memory.QueuePatch(Patterns.Windows.CertBundle, Patches.Windows.CertBundle, "CertBundle"),
                        memory.QueuePatch(Patterns.Windows.CertCommonName, Patches.Windows.CertCommonName, "CertCommonName", 5)
                    }, CancellationTokenSource.Token);
#if CUSTOM_FILES
                    Task.WaitAll(new[]
                    {
                        memory.QueuePatch(Patterns.Windows.LoadByFileId, Patches.Windows.NoJump, "LoadByFileId", 6),

                        // 10.0.0 (Prepatch) changed a pattern.
                        (clientVersion is (10, _, _, _))
                        ? memory.QueuePatch(Patterns.Windows.LoadByFilePathAlternate, Patches.Windows.NoJump, "LoadByFilePath", 3)
                        : memory.QueuePatch(Patterns.Windows.LoadByFilePath, Patches.Windows.NoJump, "LoadByFilePath", 3)
                    }, CancellationTokenSource.Token);

                    var (idAlloc, stringAlloc) = ModLoader.LoadFileMappings(processInfo.ProcessHandle);

                    if (idAlloc != 0 && stringAlloc != 0)
                    {
                        if (!ModLoader.HookClient(memory, processInfo.ProcessHandle, idAlloc, stringAlloc))
                            return false;
                    }
#endif

#elif ARM64
                    Task.WaitAll(new[]
                    {
                        memory.QueuePatch(Patterns.Windows.CertBundle, Patches.Windows.CertBundle, "CertBundle", 19),
                        memory.QueuePatch(Patterns.Windows.CertCommonName, Patches.Windows.CertCommonName, "CertCommonName", 6),
                    }, Program.CancellationTokenSource.Token);
#endif

                    NativeWindows.NtResumeProcess(processInfo.ProcessHandle);

                    if (memory.RemapAndPatch())
                    {
                        Console.WriteLine("Done :) ");

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("You can login now.");

                        Console.ResetColor();

                        return true;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error while launching the client.");

                        NativeWindows.TerminateProcess(processInfo.ProcessHandle, 0);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Just print out the exception we have and kill the game process.
            Console.WriteLine(ex);
            Console.WriteLine(ex.StackTrace);

            NativeWindows.TerminateProcess(processInfo.ProcessHandle, 0);
        }
        finally
        {
            NativeWindows.CloseHandle(processInfo.ProcessHandle);
            NativeWindows.CloseHandle(processInfo.ThreadHandle);
        }

        return false;
    }

    static void PrepareAntiCrash(WinMemory memory, ref MemoryBasicInformation mbi, ref ProcessInformation processInfo)
    {
        memory.RefreshMemoryData((int)mbi.RegionSize);

        // Suspend the process and handle the patches.
        NativeWindows.NtSuspendProcess(processInfo.ProcessHandle);

        // Get Integrity check locations
        var integrityOffsets = memory.Data.FindPattern(Patterns.Windows.Integrity, int.MaxValue, (int)mbi.RegionSize).ToArray();

        // Encrypt integrity offsets and patches and add them to the patch list.
        for (var i = 0; i < integrityOffsets.Length; i++)
            memory.QueuePatch(integrityOffsets[i], Patches.Windows.Integrity, $"Integrity{i}");

        // Get Integrity check locations
        var integrityOffsets2 = memory.Data.FindPattern(Patterns.Windows.Integrity2, int.MaxValue, (int)mbi.RegionSize).ToArray();

        // Encrypt integrity offsets and patches and add them to the patch list.
        for (var i = 0; i < integrityOffsets2.Length; i++)
            memory.QueuePatch(integrityOffsets2[i], Patches.Windows.Integrity, $"Integrity{integrityOffsets.Length + i}");

        // Get Remap check locations.
        var remapOffsets = memory.Data.FindPattern(Patterns.Windows.Remap, int.MaxValue, (int)mbi.RegionSize);
        var lastAddress = 0;

        foreach (var a in remapOffsets)
        {
            var instructionStart = (int)a + 4;
            var instructionEnd = (int)a + 4 + 6;
            var instructions = new byte[6];

            Buffer.BlockCopy(memory.Data, instructionStart, instructions, 0, 6);

            // Skip unconditional jumps.
            if (WinMemory.IsUnconditionalJump(instructions))
                continue;

            var operandValue = 0;

            if (WinMemory.IsShortJump(instructions))
                operandValue = instructions[1] + 2;
            else if (WinMemory.IsJump(instructions))
                operandValue = BitConverter.ToInt32(instructions, 2) + 6;
            else
                throw new InvalidDataException("Invalid operand value.");

            var jumpToValue = a + operandValue + 4;
            var tempPatches = new ConcurrentDictionary<string, (long, byte[])>();

            // Find all references of real code parts inside the remap check functions.
            Parallel.For(lastAddress, memory.Data.Length, i =>
            {
                if (WinMemory.IsJump(memory.Data, i))
                {
                    var jumpOperand = BitConverter.ToInt32(memory.Data, i + 2);
                    var jumpSize = (int)jumpToValue - i - 6;

                    if (jumpOperand == jumpSize)
                    {
                        // Add 1 because we patch the instruction start.
                        // This results in a shorter overall instruction length.
                        var jumpBytes = new byte[] { 0xE9 }.Concat(BitConverter.GetBytes(jumpSize + 1)).ToArray();

                        tempPatches.TryAdd($"Jump{i}", (i, jumpBytes));
                    }
                }
                else if (WinMemory.IsShortJump(memory.Data, i))
                {
                    var jumpOperand = memory.Data[i + 1];
                    var jumpSize = (int)jumpToValue - i - 2;

                    if (jumpOperand == jumpSize)
                    {
                        // Check for 0x48 here. This is an indicator for the test instructions.
                        // Might need some better checks or future updates.
                        if (memory.Data[i - 3] == 0x48)
                        {
                            var iBytes = BitConverter.GetBytes(i);
                            var jumpBytes = new byte[] { 0xEB };

                            tempPatches.TryAdd($"ShortJump{i}", (i, jumpBytes));
                        }
                    }
                }
            });

            // Add the remap crash patches to the patch list.
            foreach (var p in tempPatches)
                memory.QueuePatch(p.Value.Item1, p.Value.Item2, p.Key);

            lastAddress = (int)a;
        }
    }
}
