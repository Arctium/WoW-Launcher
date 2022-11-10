// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Arctium.WoW.Launcher.Misc.NativeWindows;

namespace Arctium.WoW.Launcher.IO;

class WinMemory
{
    public byte[] Data { get; set; }

    public nint ProcessHandle { get; }
    public nint BaseAddress { get; }

    ProcessBasicInformation peb;

    readonly Dictionary<string, (long Address, byte[] Data)> patchList;

    public WinMemory(ProcessInformation processInformation, long binaryLength)
    {
        ProcessHandle = processInformation.ProcessHandle;

        if (processInformation.ProcessHandle == 0)
            throw new InvalidOperationException("No valid process found.");

        BaseAddress = ReadImageBaseFromPEB(processInformation.ProcessHandle);

        if (BaseAddress == 0)
            throw new InvalidOperationException("Error while reading PEB data.");

        Data = Read(BaseAddress, (int)binaryLength);

        patchList = new();
    }

    public void RefreshMemoryData(int size)
    {
        // Reset previous memory data.
        Data = null;

        while (Data == null)
        {
            Console.WriteLine("Refreshing client data...");

            Data = Read(BaseAddress, size);
        }
    }

    public nint Read(nint address)
    {
        try
        {
            var buffer = new byte[8];

            if (ReadProcessMemory(ProcessHandle, address, buffer, buffer.Length, out var dummy))
                return buffer.ToNint();

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return 0;
    }

    public nint Read(long address) => Read((nint)address);

    public byte[] Read(nint address, int size)
    {
        try
        {
            var buffer = new byte[size];

            if (ReadProcessMemory(ProcessHandle, address, buffer, size, out var dummy))
                return buffer;

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return null;
    }

    public byte[] Read(long address, int size) => Read((nint)address, size);

    public int ReadDataLength(nint address, string seperator)
    {
        var length = 0L;
        var seperatorBytes = Encoding.UTF8.GetBytes(seperator).Select(b => (short)b).ToArray();
        var dataLength = 1000;

        // Read in batches here.
        while (length == 0)
        {
            length = Read(address, dataLength)?.FindPattern(seperatorBytes) ?? 0;

            dataLength += 1000;

            // Not found!
            if (dataLength >= 100_000)
                return -1;
        }

        return (int)length;
    }

    public void Write(nint address, byte[] data, MemProtection newProtection = MemProtection.ReadWrite)
    {
        try
        {
            VirtualProtectEx(ProcessHandle, address, (uint)data.Length, (uint)newProtection, out var oldProtect);

            WriteProcessMemory(ProcessHandle, address, data, data.Length, out var written);

            FlushInstructionCache(ProcessHandle, address, (uint)data.Length);
            VirtualProtectEx(ProcessHandle, address, (uint)data.Length, oldProtect, out oldProtect);

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public void Write(long address, byte[] data, MemProtection newProtection = MemProtection.ReadWrite) => Write((nint)address, data, newProtection);

    public Task PatchMemory(short[] pattern, byte[] patch, string patchName)
    {
        Console.WriteLine($"[{patchName}] Patching...");

        long patchOffset = Data.FindPattern(pattern, BaseAddress);

        // No result for the given pattern.
        if (patchOffset == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;

            Console.WriteLine($"[{patchName}] No result found.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

            Launcher.CancellationTokenSource.Cancel();
        }

        while (Read(patchOffset, patch.Length)?.SequenceEqual(patch) == false)
            Write(patchOffset, patch);

        Console.Write($"[{patchName}]");

        Console.ForegroundColor = ConsoleColor.Green;

        Console.WriteLine(" Done.");

        Console.ForegroundColor = ConsoleColor.Gray;

        Console.WriteLine();

        return Task.CompletedTask;
    }

    public Task QueuePatch(long patchOffset, byte[] patch, string patchName)
    {
        Launcher.CancellationTokenSource.Token.ThrowIfCancellationRequested();

        Console.WriteLine($"[{patchName}] Adding...");

        patchList[patchName] = (patchOffset, patch);

        Console.Write($"[{patchName}]");

        Console.ForegroundColor = ConsoleColor.Green;

        Console.WriteLine(" Done.");

        Console.ForegroundColor = ConsoleColor.Gray;

        Console.WriteLine();

        return Task.CompletedTask;
    }

    public Task QueuePatch(short[] pattern, byte[] patch, string patchName, int offsetBase = 0)
    {
        long patchOffset = Data.FindPattern(pattern);

        // No result for the given pattern.
        if (patchOffset == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine($"[{patchName}] No result found.");

            Console.ForegroundColor = ConsoleColor.Gray;

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

            Launcher.CancellationTokenSource.Cancel();

            return Task.CompletedTask;
        }

        return QueuePatch(patchOffset + offsetBase, patch, patchName);
    }

    public bool RemapAndPatch(nint viewAddress, int viewSize)
    {
        // Suspend before remapping to prevent crashes.
        NtSuspendProcess(ProcessHandle);

        Data = Read(viewAddress, viewSize);

        if (Data != null)
        {
            nint newViewHandle = 0;
            var maxSize = new LargeInteger { Quad = viewSize };

            try
            {
                if (NtCreateSection(ref newViewHandle, 0xF001F, 0, ref maxSize, 0x40u, 0x8000000 | 0x400000, 0) == NtStatus.Success &&
                    NtUnmapViewOfSection(ProcessHandle, viewAddress) == NtStatus.Success)
                {
                    var viewBase = viewAddress;

                    // Map the view with original protections.
                    var result = NtMapViewOfSection(newViewHandle, ProcessHandle, ref viewBase, 0, (ulong)viewSize, out var viewOffset,
                                           out var newViewSize, 2, 0, (int)MemProtection.ExecuteRead);

                    if (result == NtStatus.Success)
                    {
                        // Apply our patches.
                        foreach (var p in patchList)
                        {
                            var address = p.Value.Address;

                            if (address == 0)
                                continue;

                            var patch = p.Value.Data;

                            // We are in a different section here.
                            if (address > Data.Length)
                            {
                                if (address < BaseAddress)
                                    address += BaseAddress;

                                Write(address, patch, MemProtection.ReadWrite);
                                continue;
                            }

                            for (var i = 0; i < patch.Length; i++)
                                Data[address + i] = patch[i];
                        }

                        nint viewBase2 = 0;

                        // Create a writable view to write our patches through to preserve the original protections.
                        result = NtMapViewOfSection(newViewHandle, ProcessHandle, ref viewBase2, 0, (uint)viewSize, out var viewOffset2,
                                               out var newViewSize2, 2, 0, (int)MemProtection.ReadWrite);

                        if (result == NtStatus.Success)
                        {
                            // Write our patched data trough the writable view to the memory.
                            if (WriteProcessMemory(ProcessHandle, viewBase2, Data, viewSize, out var dummy))
                            {
                                // Unmap them writeable view, it's not longer needed.
                                NtUnmapViewOfSection(ProcessHandle, viewBase2);

                                // Check if the allocation protections is the right one.
                                if (VirtualQueryEx(ProcessHandle, BaseAddress, out MemoryBasicInformation mbi, MemoryBasicInformation.Size) != 0 && mbi.AllocationProtect == MemProtection.ExecuteRead)
                                {
                                    // Also check if we can change the page protection.
                                    if (!VirtualProtectEx(ProcessHandle, BaseAddress, 0x4000, (uint)MemProtection.ReadWrite, out var oldProtect))
                                        NtResumeProcess(ProcessHandle);

                                    return true;
                                }
                            }
                        }
                    }

                    Console.WriteLine("Error while mapping the view with the given protection.");
                }
            }
            finally
            {
                NtClose(newViewHandle);
            }
        }
        else
            Console.WriteLine("Error while creating the view backup.");

        NtResumeProcess(ProcessHandle);

        return false;
    }

    public bool RemapAndPatch()
    {
        if (VirtualQueryEx(ProcessHandle, BaseAddress, out MemoryBasicInformation mbi, MemoryBasicInformation.Size) != 0)
            return RemapAndPatch(mbi.BaseAddress, (int)mbi.RegionSize);

        return false;
    }

    /// Private functions.
    nint ReadImageBaseFromPEB(nint processHandle)
    {
        try
        {
            if (NtQueryInformationProcess(processHandle, 0, ref peb, ProcessBasicInformation.Size, out int sizeInfoReturned) == NtStatus.Success)
                return Read(peb.PebBaseAddress + 0x10);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsShortJump(byte[] instructions, int startIndex = 0)
    {
        return instructions[startIndex] >= 0x70 && instructions[startIndex] < 0x7F;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsJump(byte[] instructions, int startIndex = 0)
    {
        return instructions[startIndex] == 0x0F && instructions[startIndex + 1] >= 0x80 && instructions[startIndex + 1] <= 0x8F;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUnconditionalJump(byte[] instructions, int startIndex = 0)
    {
        return instructions[startIndex] == 0xE9 || instructions[startIndex] == 0xEB;
    }
}
