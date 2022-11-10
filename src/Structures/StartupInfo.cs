// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Arctium.WoW.Launcher.Structures;

struct StartupInfo
{
    public uint Cb;
    public string Reserved;
    public string Desktop;
    public string Title;
    public uint X;
    public uint Y;
    public uint XSize;
    public uint YSize;
    public uint XCountChars;
    public uint YCountChars;
    public uint FillAttribute;
    public uint Flags;
    public short ShowWindow;
    public short Reserved2;
    public nint ReservedHandle;
    public nint StdInputHandle;
    public nint StdOutputHandle;
    public nint StdErrorHandle;

    public static int Size => Marshal.SizeOf<StartupInfo>();
}
