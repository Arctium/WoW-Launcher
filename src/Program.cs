// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the proje root for full license information.

using static Arctium.WoW.Launcher.Misc.Helpers;

namespace Arctium.WoW.Launcher;

class Program
{
    static string wowBinary = "Wow.exe";
    static string wowPath = string.Empty;
    static bool keepCache = false;
    static string consoleArgs = string.Empty;

    static Dictionary<string, (long, byte[])> patches = new();

    static void Main(string[] args)
    {
        PrintHeader("WoW Client Launcher");

        // Handle console launch args.
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--path")
            {
                wowPath = args[i + 1];
                i++;

                continue;
            }
            else if (args[i] == "--binary")
            {
                wowBinary = args[i + 1];
                i++;

                continue;
            }
            else if (args[i] == "-keepcache")
            {
                keepCache = true;

                continue;
            }

            consoleArgs += " " + args[i];
        }

        var appPath = PrepareGameLaunch();

        LaunchGame(appPath);
    }

    static string PrepareGameLaunch()
    {
        // App info
        var curDir = AppDomain.CurrentDomain.BaseDirectory;
        var dataDir = $"{curDir}/_retail_";
        var appPath = $"{curDir}/_retail_/{wowBinary}";

        if (!string.IsNullOrEmpty(wowPath))
        {
            dataDir = wowPath;
            appPath = $"{dataDir}/{wowBinary}";
        }

        // Also support game installations without branch sub folders.
        if (!File.Exists(appPath))
        {
            dataDir = $"{curDir}";
            appPath = $"{curDir}/{wowBinary}";
        }

        if (!File.Exists(appPath) || GetVersionValueFromClient(appPath, 3) != 9)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Copy this launcher to your game folder!");
            Console.WriteLine();
            Console.WriteLine($"OR");
            Console.WriteLine();

            Console.WriteLine("Use the '--path \"Your WoW Path\"' option to specify your custom wow sub path.");
            Console.WriteLine("Use the '--binary \"YourWoWBinaryname\"' option to specify your custom wow binary name.");
            Console.WriteLine();

            WaitAndExit(300000);
        }

        if (GetVersionValueFromClient(appPath, 0) < 37862 && GetVersionValueFromClient(appPath, 0) != 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Your client version {GetVersionValueFromClient(appPath, 0)} is not supported.");
            Console.WriteLine($"The minimum required build is 9.0.5.37862");

            WaitAndExit(10000);
        }

        // Delete the cache folder by default.
        if (!keepCache)
        {
            try
            {
                // Trying to delete the cache folder.
                Directory.Delete($"{dataDir}/Cache", true);
            }
            catch (Exception)
            {
                // We don't care if it worked. Swallow it!
            }
        }

        return appPath;
    }

    static void LaunchGame(string appPath)
    {
        var startupInfo = new StartupInfo();
        var processInfo = new ProcessInformation();

        try
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Starting WoW client...");

            var createSuccess = NativeWindows.CreateProcess(null, $"{appPath} {consoleArgs}", 0, 0, false, 4U, 0, new FileInfo(appPath)?.DirectoryName, ref startupInfo, out processInfo);

            // On some systems we have to launch the game with the application name used.
            if (!createSuccess)
                createSuccess = NativeWindows.CreateProcess(appPath, $"- {consoleArgs}", 0, 0, false, 4U, 0, null, ref startupInfo, out processInfo);

            // Start process with suspend flags.
            if (createSuccess)
            {
                var memory = new WinMemory(processInfo.ProcessHandle);

                // Get cert bundle offset from file.
                var bundleOffset = File.ReadAllBytes(appPath).FindPattern(Patterns.Common.CertBundle, memory.BaseAddress).ToNint();
                var sectionOffset = memory.Read(bundleOffset, 0x10000).FindPattern(Patterns.Common.CertBundle);

                bundleOffset = (bundleOffset + sectionOffset).ToNint();

                var certBundleData = Convert.FromBase64String(Patches.Common.CertBundleData);
                var originalBundleLength = memory.ReadDataLength(bundleOffset, "NGIS");

                if (originalBundleLength == -1)
                {
                    Console.WriteLine("Can't get cert bundle data length.");

                    WaitAndExit(5000);
                }

                Console.WriteLine("Patching cert bundle data...");

                // Zero the original cert bundle data.
                while (memory.Read(bundleOffset, 1)?[0] != 0)
                    memory.Write(bundleOffset, new byte[originalBundleLength + 4 + 256]);

                // Be sure that the modulus is written before the client is initialized.
                while (memory.Read(bundleOffset, 1)?[0] != certBundleData[0])
                    memory.Write(bundleOffset, certBundleData);

                Console.WriteLine("Done");
                Console.WriteLine("Patching ConnectTo modulus...");

                // Get ConnectTo RSA modulus offset from file.
                var modulusOffset = File.ReadAllBytes(appPath).FindPattern(Patterns.Common.ConnectToModulus, memory.BaseAddress).ToNint();
                sectionOffset = memory.Read(modulusOffset, 0x10000).FindPattern(Patterns.Common.ConnectToModulus);

                modulusOffset = (modulusOffset + sectionOffset).ToNint();

                // Be sure that the modulus is written before the client is initialized.
                while (memory.Read(modulusOffset, 1)?[0] != Patches.Common.Modulus[0])
                    memory.Write(modulusOffset, Patches.Common.Modulus);

                Console.WriteLine("Done");
                Console.WriteLine("Patching ChangeProtocol modulus...");

                // Get ChangeProtocol RSA modulus offset from file.
                modulusOffset = File.ReadAllBytes(appPath).FindPattern(Patterns.Common.ChangeProtocolModulus, memory.BaseAddress).ToNint();
                sectionOffset = memory.Read(modulusOffset, 0x10000).FindPattern(Patterns.Common.ChangeProtocolModulus);

                modulusOffset = (modulusOffset + sectionOffset).ToNint();

                // Be sure that the modulus is written before the client is initialized.
                while (memory.Read(modulusOffset, 1)?[0] != Patches.Common.Modulus[0])
                    memory.Write(modulusOffset, Patches.Common.Modulus);

                Console.WriteLine("Done");
                Console.WriteLine("Patching portal...");

                // Portal patch
                nint portalOffset = 0;

                // Get portal offset from file.
                portalOffset = File.ReadAllBytes(appPath).FindPattern(Patterns.Common.Portal, memory.BaseAddress).ToNint();
                sectionOffset = memory.Read(portalOffset, 0x2000).FindPattern(Patterns.Common.Portal);

                portalOffset = (portalOffset + sectionOffset).ToNint();

                // Be sure that the portal is written before the client is initialized.
                while (memory.Read(portalOffset, 1)?[0] != Patches.Common.Portal[0])
                    memory.Write(portalOffset, Patches.Common.Portal);

                Console.WriteLine("Done");

                Console.WriteLine("Patching version url...");

                // Version patch
                nint versionOffset = 0;

                // Get version offset from file.
                versionOffset = File.ReadAllBytes(appPath).FindPattern(Patterns.Common.VersionUrl, memory.BaseAddress).ToNint();

                if (versionOffset == 0)
                {
                    Console.WriteLine($"Can't find {nameof(versionOffset)}. Custom version URL is used!!!");
                    Console.WriteLine("Done.");
                }
                else
                {
                    sectionOffset = memory.Read(versionOffset, 0x2000).FindPattern(Patterns.Common.VersionUrl);
                    versionOffset = (versionOffset + sectionOffset).ToNint();

                    var wowBuild = GetVersionValueFromClient(appPath, 0);
                    var versionPatch = Patches.Common.GetVersionUrl(wowBuild);

                    while (memory.Read(versionOffset, 1)?[0] != versionPatch[0])
                        memory.Write(versionOffset, versionPatch);

                    Console.WriteLine("Done");
                }

                // Resume the process to initialize it.
                NativeWindows.NtResumeProcess(processInfo.ProcessHandle);

                var mbi = new MemoryBasicInformation();

                // Wait for the memory region to be initialized.
                while (NativeWindows.VirtualQueryEx(processInfo.ProcessHandle, memory.BaseAddress, out mbi, mbi.Size) == 0 || (int)mbi.RegionSize <= 0x1000)
                { }

                if (mbi.BaseAddress != 0)
                {
                    var binary = Array.Empty<byte>();

                    PrepareAntiCrash(memory, binary, ref mbi, ref processInfo);

                    // Recheck binary data.
                    while (binary?.Length == 0)
                    {
                        Console.WriteLine("Waiting for client data...");

                        binary = memory.Read(mbi.BaseAddress, (int)mbi.RegionSize);
                    }

                    // Get patch locations.
                    var certBundleOffset = binary.FindPattern(Patterns.Windows.CertBundle);
                    var certSignatureMagicOffset = binary.FindPattern(Patterns.Windows.CertSignatureMagic);
                    var certSignatureOffset = binary.FindPattern(Patterns.Windows.CertSignature);
                    var certCommonNameOffset = binary.FindPattern(Patterns.Windows.CertCommonName);

                    if (certBundleOffset == 0 || certSignatureOffset == 0 || certCommonNameOffset == 0)
                    {
                        NativeWindows.TerminateProcess(processInfo.ProcessHandle, 0);

                        Console.ForegroundColor = ConsoleColor.Red;

                        Console.WriteLine("Not all patterns could be found:");
                        Console.WriteLine($"CertBundle: {certBundleOffset != 0}");
                        Console.WriteLine($"CertSignatureMagic: {certBundleOffset != 0}");
                        Console.WriteLine($"CertSignature: {certSignatureOffset != 0}");
                        Console.WriteLine($"CertCommonName: {certCommonNameOffset != 0}");
                        Console.WriteLine();

                        Console.ForegroundColor = ConsoleColor.Yellow;

                        Console.WriteLine("Please contact the developer.");

                        WaitAndExit(5000);
                    }

                    patches["CertBundle"] = (certBundleOffset, Patches.Windows.CertBundle);
                    patches["CertSignatureMagic"] = (certSignatureMagicOffset + 6, Patches.Windows.SignatureMagic);
                    patches["CertSignature"] = (certSignatureOffset, Patches.Windows.Signature);
                    patches["CertCommonName"] = (certCommonNameOffset + 5, Patches.Windows.CertCommonName);

                    NativeWindows.NtResumeProcess(processInfo.ProcessHandle);

                    if (memory.RemapAndPatch(patches))
                    {
                        Console.WriteLine("Done :) ");

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("You can login now.");

                        WaitAndExit(0);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error while launching the client.");

                        NativeWindows.TerminateProcess(processInfo.ProcessHandle, 0);

                        WaitAndExit(5000);
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
    }

    static void PrepareAntiCrash(WinMemory memory, byte[] binary, ref MemoryBasicInformation mbi, ref ProcessInformation processInfo)
    {
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
            binary = memory.Read(mbi.BaseAddress, (int)mbi.RegionSize);

        // Recheck binary data.
        while (binary?.Length == 0)
        {
            Console.WriteLine("Waiting for client data...");

            binary = memory.Read(mbi.BaseAddress, (int)mbi.RegionSize);
        }

        // Suspend the process and handle the patches.
        NativeWindows.NtSuspendProcess(processInfo.ProcessHandle);

        // Get Integrity check locations
        var integrityOffsets = binary.FindPattern(Patterns.Windows.Integrity, int.MaxValue, (int)mbi.RegionSize).ToArray();

        // Encrypt integrity offsets and patches and add them to the patch list.
        for (var i = 0; i < integrityOffsets.Length; i++)
            patches[$"Integrity{i}"] = (integrityOffsets[i], Patches.Windows.Integrity);

        // Get Integrity check locations
        var integrityOffsets2 = binary.FindPattern(Patterns.Windows.Integrity2, int.MaxValue, (int)mbi.RegionSize).ToArray();

        // Encrypt integrity offsets and patches and add them to the patch list.
        for (var i = 0; i < integrityOffsets2.Length; i++)
            patches[$"Integrity{integrityOffsets.Length + i}"] = (integrityOffsets2[i], Patches.Windows.Integrity);

        // Get Remap check locations.
        var remapOffsets = binary.FindPattern(Patterns.Windows.Remap, int.MaxValue, (int)mbi.RegionSize);
        var lastAddress = 0;

        foreach (var a in remapOffsets)
        {
            var instructionStart = (int)a + 4;
            var instructionEnd = (int)a + 4 + 6;
            var instructions = new byte[6];

            Buffer.BlockCopy(binary, instructionStart, instructions, 0, 6);

            // Skip unconditional jumps.
            if (memory.IsUnconditionalJump(instructions))
                continue;

            var operandValue = 0;

            if (memory.IsShortJump(instructions))
                operandValue = instructions[1] + 2;
            else if (memory.IsJump(instructions))
                operandValue = BitConverter.ToInt32(instructions, 2) + 6;
            else
                throw new InvalidDataException("Invalid operand value.");

            var jumpToValue = a + operandValue + 4;
            var tempPatches = new ConcurrentDictionary<string, (long, byte[])>();

            // Find all references of real code parts inside the remap check functions.
            Parallel.For(lastAddress, binary.Length, i =>
            {
                if (memory.IsJump(binary, i))
                {
                    var jumpOperand = BitConverter.ToInt32(binary, i + 2);
                    var jumpSize = (int)jumpToValue - i - 6;

                    if (jumpOperand == jumpSize)
                    {
                    // Add 1 because we patch the instruction start.
                    // This results in a shorter overall instruction length.
                    var jumpBytes = new byte[] { 0xE9 }.Concat(BitConverter.GetBytes(jumpSize + 1)).ToArray();

                        tempPatches.TryAdd($"Jump{i}", (i, jumpBytes));
                    }
                }
                else if (memory.IsShortJump(binary, i))
                {
                    var jumpOperand = binary[i + 1];
                    var jumpSize = (int)jumpToValue - i - 2;

                    if (jumpOperand == jumpSize)
                    {
                    // Check for 0x48 here. This is an indicator for the test instructions.
                    // Might need some better checks or future updates.
                    if (binary[i - 3] == 0x48)
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
                patches[p.Key] = (p.Value.Item1, p.Value.Item2);

            lastAddress = (int)a;
        }
    }

    static void WaitAndExit(int ms = 2000)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"Closing in {ms / 1000} seconds...");

        Thread.Sleep(ms);

        Environment.Exit(0);
    }
}
