// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Arctium.WoW.Launcher.Structures;

[StructLayout(LayoutKind.Explicit, Size = 8)]
struct LargeInteger
{
    [FieldOffset(0)]
    public long Quad;
    [FieldOffset(0)]
    public uint Low;
    [FieldOffset(4)]
    public int High;

    public static int Size => Marshal.SizeOf<LargeInteger>();
}
