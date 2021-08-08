// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the proje root for full license information.

namespace Arctium.WoW.Launcher.Patches;

static class Windows
{
    public static byte[] Integrity = { 0xC2, 0x00, 0x00 };
    public static byte[] CertBundle = { 0x90, 0x90 };
    public static byte[] CertCommonName = { 0xB0, 0x01 };
    public static byte[] SignatureMagic = { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };
    public static byte[] Signature = { 0x90, 0x90 };
    public static byte[] ShortJump = { 0xEB };
}
