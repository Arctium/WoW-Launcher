// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

using static Arctium.WoW.Launcher.Misc.Helpers;

namespace Arctium.WoW.Launcher;

static class Launcher
{
    public static readonly CancellationTokenSource CancellationTokenSource = new();

    public static async ValueTask<string> PrepareGameLaunch(ParseResult commandLineResult, IPFilter ipFilter)
    {
        var gameVersion = commandLineResult.GetValueForOption(LaunchOptions.Version);
        var (subFolder, binaryName, majorGameVersion, minGameBuild) = gameVersion switch
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
        var gameFolder = $"{currentFolder}/{subFolder}";

        if (commandLineResult.HasOption(LaunchOptions.GameBinary))
            binaryName = commandLineResult.GetValueForOption(LaunchOptions.GameBinary);

        var gameBinaryPath = $"{gameFolder}/{binaryName}";

        if (commandLineResult.HasOption(LaunchOptions.GamePath))
        {
            gameFolder = commandLineResult.GetValueForOption(LaunchOptions.GamePath);
            gameBinaryPath = $"{gameFolder}/{binaryName}";
        }
        else if (!File.Exists(gameBinaryPath))
        {
            // Also support game installations without branch sub folders.
            gameFolder = currentFolder;
            gameBinaryPath = $"{gameFolder}/{binaryName}";
        }

        if (!File.Exists(gameBinaryPath) || !majorGameVersion.Contains(GetVersionValueFromClient(gameBinaryPath).Major))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] No {gameVersion} client found.");

            return string.Empty;
        }

        var gameClientBuild = GetVersionValueFromClient(gameBinaryPath).Build;

        if (gameClientBuild < minGameBuild && gameClientBuild != 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Your found client version {gameClientBuild} is not supported.");
            Console.WriteLine($"The minimum required build is {minGameBuild}");

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

        var configPath = $"{gameFolder}/WTF/{commandLineResult.GetValueForOption(LaunchOptions.GameConfig)}";
        (string IPAddress, string HostName, int Port) portal = new();

        if (!File.Exists(configPath))
            LaunchOptions.IsDevModeAllowed = false;
        else
        {
            var config = File.ReadAllText(configPath);

            portal = ParsePortal(config);

            LaunchOptions.IsDevModeAllowed = IsDevModeAllowed(ipFilter, portal.IPAddress);
        }

        if (!LaunchOptions.IsDevModeAllowed)
            LaunchOptions.DevMode = new("--dev", () => false);

        var devModeEnabled = commandLineResult.GetValueForOption(LaunchOptions.DevMode) && LaunchOptions.IsDevModeAllowed;

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Developer mode: {(devModeEnabled ? "Enabled" : "Disabled")}");
        Console.WriteLine();
        Console.WriteLine($"Client Portal '{portal.HostName}'");
        Console.ForegroundColor = ConsoleColor.Gray;

        // Check for valid certificate when dev mode is disabled.
        if (!devModeEnabled)
        {
            try
            {
                using var tcpClient = new TcpClient();
                using var tcpClientTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                
                await tcpClient.ConnectAsync(portal.HostName, portal.Port, tcpClientTimeout.Token);
                
                using var sslStream = new SslStream(tcpClient.GetStream(), false,
                    (_, _, _, sslPolicyErrors) =>
                    {
                        // Redirect to the trusted cert warning.
                        if (sslPolicyErrors != SslPolicyErrors.None)
                            throw new AuthenticationException();
                        
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Certificate for server '{portal.HostName}' successfully validated.");
                        Console.WriteLine();
                        Console.ResetColor();

                        return true;
                    },
                    null
                );

                sslStream.AuthenticateAsClient(portal.HostName);
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{portal.HostName}:{portal.Port} is offline.");
                Console.ResetColor();

                return string.Empty;
            }
            catch (AuthenticationException)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Server with host name {portal.HostName} does not have a trusted certificate attached.");
                Console.WriteLine("If you are the server owner be sure to generate one and replace the default bnet server certificate.");
                Console.WriteLine("One way to generate one is through Let's Encrypt.");
                Console.ResetColor();

                return string.Empty;
            }
        }

        return gameBinaryPath;
    }

    public static bool LaunchGame(string appPath, string gameCommandLine, ParseResult commandLineResult)
    {
        // Build the version URL from the game binary build.
        var clientVersion = GetVersionValueFromClient(appPath);

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Client Build {clientVersion}");
        Console.WriteLine($"Client Path '{appPath}'");
        Console.WriteLine();
        Console.ResetColor();

        // Assign the region and product dependent version url to check it's online status.
        var versionUrl = commandLineResult.GetValueForOption(LaunchOptions.VersionUrl)
            ?? Patches.Common.GetVersionUrl(clientVersion.Build, commandLineResult.GetValueForOption(LaunchOptions.CdnRegion),
                                            commandLineResult.GetValueForOption(LaunchOptions.ProductName));

        if (!CheckUrl(versionUrl, fallbackUrl: Patterns.Common.VersionUrl).GetAwaiter().GetResult())
            versionUrl = Patterns.Common.VersionUrl;
        else
            // Assign the region and product independent version url.
            versionUrl = commandLineResult.GetValueForOption(LaunchOptions.VersionUrl) ?? Patches.Common.GetVersionUrl(clientVersion.Build);

        var cdnsUrl = commandLineResult.GetValueForOption(LaunchOptions.CdnsUrl) ?? Patches.Common.CdnsUrl;

        if (!CheckUrl(cdnsUrl, fallbackUrl: Patterns.Common.CdnsUrl).GetAwaiter().GetResult())
            cdnsUrl = Patterns.Common.CdnsUrl;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Game CDN connection info:");
        Console.WriteLine($"Version file: {versionUrl}");
        Console.WriteLine($"CDNs file: {cdnsUrl}");
        Console.WriteLine();
        Console.ResetColor();

        var startupInfo = new StartupInfo();
        var processInfo = new ProcessInformation();

        try
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Starting WoW client...");

            var createSuccess = NativeWindows.CreateProcess(null, $"{appPath} {gameCommandLine}", 0, 0, false, 4, 0, new FileInfo(appPath).DirectoryName,
                ref startupInfo, out processInfo);

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

                MemoryBasicInformation mbi;

                // Wait for the memory region to be initialized.
                while (NativeWindows.VirtualQueryEx(processInfo.ProcessHandle, memory.BaseAddress, out mbi, MemoryBasicInformation.Size) == 0 ||
                       mbi.RegionSize <= 0x1000)
                { }

                if (mbi.BaseAddress != 0)
                {
                    NativeWindows.NtSuspendProcess(processInfo.ProcessHandle);

                    byte[] certBundleData = Convert.FromBase64String(Patches.Common.CertBundleData);


                    // Refresh the client data before patching.
                    memory.RefreshMemoryData((int)gameAppData.Length);

                    // We need to cache this here since we are using our RSA modulus as auth seed.
                    var modulusOffset = memory.Data.FindPattern(Patterns.Common.SignatureModulus);
                    var legacyCertMode = clientVersion is (1, >= 14, <= 3, _) or (3, 4, <= 1, _) or (9, _, _, _) or (10, <= 1, < 5, _);

                    if (legacyCertMode)
                    {
                        Task.WaitAll(new[]
                        {
                            memory.PatchMemory(Patterns.Common.CertBundle, certBundleData, "Certificate Bundle"),
                            memory.PatchMemory(Patterns.Common.SignatureModulus, Patches.Common.SignatureModulus, "Certificate Signature RsaModulus")
                        }, CancellationTokenSource.Token);
                    }


                    // Wait for all direct memory patch tasks to complete,
                    Task.WaitAll(new[]
                    {
                        memory.PatchMemory(Patterns.Common.ConnectToModulus, Patches.Common.RsaModulus, "ConnectTo RsaModulus"),

                        // Recent clients have a different signing algorithm in EnterEncryptedMode.
                        clientVersion is (9, 2, 7, _) or (3, _, _, _) or (10, _, _, _) or (1, >= 14, >= 4, _)
                            ? memory.PatchMemory(Patterns.Common.CryptoEdPublicKey, Patches.Common.CryptoEdPublicKey, "GameCrypto Ed25519 PublicKey")
                            : memory.PatchMemory(Patterns.Common.CryptoRsaModulus, Patches.Common.RsaModulus, "GameCrypto RsaModulus"),

                        memory.PatchMemory(Patterns.Common.Portal, Patches.Common.Portal, "Login Portal"),
                        memory.PatchMemory(Patterns.Common.VersionUrl.ToPattern(), Encoding.UTF8.GetBytes(versionUrl), "Version URL"),
                        memory.PatchMemory(Patterns.Common.CdnsUrl.ToPattern(), Encoding.UTF8.GetBytes(cdnsUrl), "CDNs URL"),
                        memory.PatchMemory(Patterns.Windows.LauncherLogin, Patches.Windows.LauncherLogin, "Launcher Login Registry")
                    }, CancellationTokenSource.Token);

                    NativeWindows.NtResumeProcess(processInfo.ProcessHandle);

                    // Enable anti crash in dev mode, custom file mode or static auth seed mode.
#if CUSTOM_FILES
                    var antiCrash = true;
#else
                    var antiCrash = legacyCertMode || commandLineResult.HasOption(LaunchOptions.UseStaticAuthSeed) ||
                                commandLineResult.GetValueForOption(LaunchOptions.DevMode) && LaunchOptions.IsDevModeAllowed;
#endif

                    WaitForUnpack(ref processInfo, memory, ref mbi, gameAppData, antiCrash);

#if x64
                    if (legacyCertMode)
                    {
                        Task.WaitAll(new[]
                        {
                            memory.QueuePatch(Patterns.Windows.CertBundle, Patches.Windows.CertBundle, "CertBundle"),
                            memory.QueuePatch(Patterns.Windows.CertCommonName, Patches.Windows.CertCommonName, "CertCommonName", 5)
                        }, CancellationTokenSource.Token);
                    }
                    else if (LaunchOptions.IsDevModeAllowed && commandLineResult.GetValueForOption(LaunchOptions.DevMode))
                    {
                        Task.WaitAll(new[]
                        {
                            memory.QueuePatch(Patterns.Windows.CertChain, Patches.Windows.CertChain, "CertChain"),
                            memory.QueuePatch(Patterns.Windows.CertCommonName, Patches.Windows.CertCommonName, "CertCommonName", 5)
                        }, CancellationTokenSource.Token);
                    }

                    if (commandLineResult.HasOption(LaunchOptions.UseStaticAuthSeed))
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("Static auth seed used. Be sure that the server you are connecting to supports it.");
                        Console.ResetColor();

                        // Generates a patch for the auth seed so we don't have to update them on each build.
                        var authSeedFunctionOffset = GenerateAuthSeedFunctionPatch(memory, modulusOffset);

                        Task.WaitAll(new[]
                        {
                            memory.QueuePatch(authSeedFunctionOffset, Patches.Windows.AuthSeed, "CustomAuthSeedFunction")
                        }, CancellationTokenSource.Token);
                    }
#if CUSTOM_FILES
                    Task.WaitAll(new[]
                    {
                        (clientVersion is (10, _, _, _))
                            ? memory.QueuePatch(Patterns.Windows.LoadByFileIdAlternate, Patches.Windows.NoJump, "LoadByFileId", 3)
                            : memory.QueuePatch(Patterns.Windows.LoadByFileId, Patches.Windows.NoJump, "LoadByFileId", 6),

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
                        memory.QueuePatch(Patterns.Windows.CertBundle, Patches.Windows.Branch, "CertBundle", 19),
                        memory.QueuePatch(Patterns.Windows.CertCommonName, Patches.Windows.CertCommonName, "CertCommonName", 6),
                    }, CancellationTokenSource.Token);
#endif

                    NativeWindows.NtResumeProcess(processInfo.ProcessHandle);

                    if (memory.RemapAndPatch(antiCrash))
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
        // Only exit and do not print any exception messages to the console.
        catch (OperationCanceledException)
        {
            NativeWindows.TerminateProcess(processInfo.ProcessHandle, 0);
        }
        // Just print out the exception we have and kill the game process.
        catch (Exception ex)
        {
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

    static bool IsDevModeAllowed(IPFilter ipFilter, string portalIP) => ipFilter.IsInRange(portalIP);

    static long GenerateAuthSeedFunctionPatch(WinMemory memory, long modulusOffset)
    {
#if x64
        var authSeedLoadOffset = memory.Data.FindPattern(Patterns.Windows.AuthSeed);

        if (authSeedLoadOffset == 0)
            throw new InvalidDataException("authSeedLoadOffset");

        var leaStartOffset = authSeedLoadOffset + 9;
        var leaValue = Unsafe.ReadUnaligned<int>(ref memory.Data[leaStartOffset + 3]);
        var authSeedWrapperOffset = leaStartOffset + leaValue + 7;
        var jmpValue = Unsafe.ReadUnaligned<uint>(ref memory.Data[authSeedWrapperOffset + 6]);
        var authSeedFunctionOffset = authSeedWrapperOffset + 5 + jmpValue + 5;

        // Write the modulus offset to our custom get seed functions.
        // Resulting static auth seed is: 179D3DC3235629D07113A9B3867F97A7
        Unsafe.WriteUnaligned(ref Patches.Windows.AuthSeed[3], (uint)(modulusOffset - authSeedFunctionOffset - 7));

        return authSeedFunctionOffset;
#else
        throw new NotImplementedException();
#endif
    }

    static void WaitForUnpack(ref ProcessInformation processInfo, WinMemory memory, ref MemoryBasicInformation mbi, Stream gameAppData, bool antiCrash)
    {
#if x64
        // Wait for client initialization.
        var initOffset = memory.Read(mbi.BaseAddress, (int)mbi.RegionSize)?.FindPattern(Patterns.Windows.Init) ?? 0;

        while (initOffset == 0)
        {
            initOffset = memory.Read(mbi.BaseAddress, (int)mbi.RegionSize)?.FindPattern(Patterns.Windows.Init) ?? 0;

            Console.WriteLine("Waiting for client initialization...");
        }

        initOffset += BitConverter.ToUInt32(memory.Read(initOffset + memory.BaseAddress + 2, 4), 0) + 10;

        while (memory.Read(initOffset + memory.BaseAddress, 1)?[0] == null ||
               memory.Read(initOffset + memory.BaseAddress, 1)?[0] == 0)
            memory.Data = memory.Read(mbi.BaseAddress, (int)mbi.RegionSize);
#else
        // Get PE header info for client initialization.
        var peHeaders = new PEHeaders(gameAppData);

        SectionHeader textSectionHeader = peHeaders.SectionHeaders.Single(sectionHeader => sectionHeader.Name.ToLower() == ".text");

        gameAppData.Position = textSectionHeader.VirtualSize + textSectionHeader.PointerToRawData;

        var textSectionEndValue = gameAppData.ReadByte();

        Console.WriteLine("Waiting for client initialization...");

        var virtualTextSectionEnd = memory.BaseAddress + textSectionHeader.VirtualAddress + textSectionHeader.VirtualSize;

        while (memory?.Read(virtualTextSectionEnd, 1)?[0] == null || memory?.Read(virtualTextSectionEnd, 1)?[0] == textSectionEndValue)
            Thread.Sleep(100);
#endif
        if (antiCrash)
            PrepareAntiCrash(memory, ref mbi, ref processInfo);

        memory.RefreshMemoryData((int)mbi.RegionSize);
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
            var instructions = new byte[6];

            Buffer.BlockCopy(memory.Data, instructionStart, instructions, 0, 6);

            // Skip unconditional jumps.
            if (WinMemory.IsUnconditionalJump(instructions))
                continue;

            int operandValue;

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
