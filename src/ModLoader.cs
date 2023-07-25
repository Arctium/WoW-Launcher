// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Arctium.WoW.Launcher;

#if x64
class ModLoader
{
    static readonly HashSet<uint> _loadedFileIds = new();

    public static bool HookClient(WinMemory memory, nint processHandle, nint idAlloc, nint stringAlloc)
    {
        var asm = new byte[]
        {
            0x85, 0xD2, 0x0F, 0x88, 0x72, 0xAF, 0xFF, 0xFF, 0x33, 0xC0,
            0x49, 0xB9, 0x22, 0x22, 0x22, 0x22, 0x11, 0x11, 0x11, 0x11,
            0x8D, 0x0C, 0x40, 0xC1, 0xE1, 0x02, 0x42, 0x39, 0x14, 0x09,
            0x74, 0x0C, 0xFF, 0xC0, 0x3D, 0xEF, 0xBE, 0xAD, 0xDE, 0x72,
            0xEB, 0x33, 0xC0, 0xC3, 0x4A, 0x8B, 0x44, 0x09, 0x04, 0xC3
        };

        var trampolineInjectAddress = NativeWindows.VirtualAllocEx(processHandle, IntPtr.Zero, (uint)asm.Length + 32, 0x00001000, (uint)MemProtection.ExecuteRead);
        var hookInstructions = new byte[] { 0x48, 0xB8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xE0, 0x90 };

        // Copy the asm code address.
        Buffer.BlockCopy(BitConverter.GetBytes(trampolineInjectAddress), 0, hookInstructions, 2, 8);

        // Inside the file load func!!!
        var hookAddress = memory.Data.FindPattern(Patterns.Windows.CustomFileIdHook);

        if (hookAddress == 0)
            throw new InvalidDataException("CustomFileIdHook");

        // Read original data from the hook function.
        var originalBytes = memory.Read(memory.BaseAddress + hookAddress, 13);

        // Apply the hook.
        memory.QueuePatch(hookAddress, hookInstructions, "CustomFileIdHook");

        // Copy count bytes.
        Buffer.BlockCopy(BitConverter.GetBytes(_loadedFileIds.Count), 0, asm, 35, 4);

        // Copy mapping ptr bytes.
        Buffer.BlockCopy(BitConverter.GetBytes(idAlloc), 0, asm, 12, 8);

        // Calculate the jump address bytes.
        var trampolineJmpAddress = (uint)(trampolineInjectAddress + asm.Length - (trampolineInjectAddress + 2) - 6);

        // Copy trampoline bytes.
        Buffer.BlockCopy(BitConverter.GetBytes(trampolineJmpAddress), 0, asm, 4, 4);

        memory.Write(trampolineInjectAddress, asm);

        var jmpInstruction = new byte[] { 0x48, 0xB8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xE0, 0x90 };

        // Copy the address where we continue to the jump instruction set.
        Buffer.BlockCopy(BitConverter.GetBytes((ulong)(hookAddress + memory.BaseAddress + 13)), 0, jmpInstruction, 2, 8);

        memory.Write(trampolineInjectAddress + asm.Length, originalBytes.Concat(jmpInstruction).ToArray());

        return true;
    }

    public static (nint idAlloc, nint stringAlloc) LoadFileMappings(nint processHandle)
    {
        var loadedMappings = new List<(byte[] fileId, byte[] path, uint StringPos)>();
        var count = 0u;
        var localStringLength = 0u;

        Console.WriteLine();
        Console.WriteLine($"Loading file mappings from '{AppDomain.CurrentDomain.BaseDirectory}files':");

        Directory.CreateDirectory($"{AppDomain.CurrentDomain.BaseDirectory}mappings");

        // Add all file mappings
        foreach (var f in Directory.EnumerateFiles($"{AppDomain.CurrentDomain.BaseDirectory}mappings", "*.txt", SearchOption.TopDirectoryOnly))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"- {new FileInfo(f).Name}");

            try
            {
                GetFileMappingData(loadedMappings, out var localCount, ref localStringLength, f);

                count += localCount;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" (Done)");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(" (Failed)");
                Console.ForegroundColor = ConsoleColor.Gray;

                Console.WriteLine(ex);
                Console.WriteLine(ex.StackTrace);
            }
        }

        Console.WriteLine();

        // Just startup without doing anything.
        if (loadedMappings.Count == 0)
        {
            Console.WriteLine("No custom files.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"Loaded {loadedMappings.Count} file mappings.");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine();

            // Allocate file mapping data.
            return AllocateFileMapping(processHandle, loadedMappings, count, localStringLength);
        }

        return default;
    }

    static void GetFileMappingData(List<(byte[] fileId, byte[] path, uint StringPos)> loadedMappings, out uint count, ref uint stringLength, string mappingFile)
    {
        var mappings = File.ReadAllLines(mappingFile).Where(s => s.Trim() != "")
            .Select(s => s.ToLowerInvariant().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            .ToLookup(k => k[1], k => k[0]);

        var filesDir = $@"{AppDomain.CurrentDomain.BaseDirectory}files";

        // Create if it doesn't exist.
        Directory.CreateDirectory(filesDir);

        Console.WriteLine();

        foreach (var f in Directory.EnumerateFiles(filesDir, "*", SearchOption.AllDirectories))
        {
            // Get the sub path inside the files folder.
            var filesDirLength = Encoding.UTF8.GetByteCount(filesDir);
            var pathBytes = Encoding.UTF8.GetBytes(f);
            var path = Encoding.UTF8.GetString(pathBytes.Skip(filesDirLength + 1).ToArray()).Replace("\\", "/").ToLowerInvariant();

            foreach (var fileId in mappings[path])
            {
                var id = uint.Parse(fileId);

                if (_loadedFileIds.Contains(id))
                {
                    Console.WriteLine($"Skipping overlapping file '{id} - {f}'.");
                    continue;
                }

                _loadedFileIds.Add(id);

                loadedMappings.Add((BitConverter.GetBytes(id), pathBytes, stringLength));

                stringLength += ((uint)pathBytes.Length + 1);
            }
        }

        count = (uint)loadedMappings.Count;
    }

    static (nint, nint) AllocateFileMapping(nint handle, List<(byte[] fileId, byte[] path, uint StringPos)> loadedMappings, uint count, uint stringLength)
    {
        var idAlloc = NativeWindows.VirtualAllocEx(handle, IntPtr.Zero, (count * (4 + 8)), 0x00001000, 0x04);
        var stringAlloc = NativeWindows.VirtualAllocEx(handle, IntPtr.Zero, stringLength, 0x00001000, 0x04);
        var idAllocData = new byte[(count * (4 + 8))];
        var stringAllocData = new byte[stringLength];

        Parallel.For(0, loadedMappings.Count, m =>
        {
            var (fileId, path, stringPos) = loadedMappings[m];

            Buffer.BlockCopy(fileId, 0, idAllocData, m * 12, fileId.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(stringAlloc + stringPos), 0, idAllocData, (m * 12) + 4, 8);
            Buffer.BlockCopy(path, 0, stringAllocData, (int)stringPos, path.Length);
        });

        NativeWindows.WriteProcessMemory(handle, idAlloc, idAllocData, idAllocData.Length, out var _);
        NativeWindows.WriteProcessMemory(handle, stringAlloc, stringAllocData, stringAllocData.Length, out var _);

        // READ ONLY
        NativeWindows.VirtualProtectEx(handle, idAlloc, (count * (4 + 8)), 0x02, out var _);
        NativeWindows.VirtualProtectEx(handle, stringAlloc, stringLength, 0x02, out var _);

        return (idAlloc, stringAlloc);
    }
}
#endif
