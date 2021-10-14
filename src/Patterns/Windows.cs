// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Arctium.WoW.Launcher.Patterns;

static class Windows
{
    // Initialization pointer pattern.
    public static short[] Init = { 0xC7, 0x05, -1, -1, -1, -1, 0x01, 0x00, 0x00, 0x00, 0x48, 0x8D, -1, -1, -1, -1, -1, 0x48, 0x8D, -1, -1, -1, -1, -1, 0xE8, -1, -1, -1, -1, 0x85 };

    // Anti Crash.
    public static short[] Integrity = { 0x44, 0x89, -1, 0x24, -1, 0x44, 0x89, -1, 0x24, -1, 0x89, -1, 0x24, -1, 0x48, 0x89, -1, 0x24, -1, 0x53, 0x56, 0x57 };
    public static short[] Integrity2 = { 0x44, 0x89, -1, 0x24, -1, 0x44, 0x89, -1, 0x24, -1, 0x89, -1, 0x24, -1, 0x48, 0x89, -1, 0x24, -1, 0x53, 0x57 };
    public static short[] Remap = { 0x48, -1, -1, 0x1E, 0x0F, 0x83 };

    // Certificate bundle loading.
    public static short[] CertBundle = { 0x75, 0x06, 0x48, -1, -1, 0x60, 0x5F, 0xC3 };
    public static short[] CertCommonName = { 0x80, -1, 0x2A, 0x75, -1, 0x32, 0xC0, 0x48 };
    public static short[] CertSignatureMagic = { 0x3B, 0x0D, -1, -1, -1, -1, 0x0F, 0x85, -1, -1, -1, -1, 0x48, 0x8D, 0x15, -1, -1, -1, -1, 0x48, 0x8D, -1, -1, -1, -1, 0x00, 0x00, 0xE8, -1, -1, -1, -1, 0x48 };
    public static short[] CertSignature = { 0x74, -1, 0x4C, 0x8B, -1, 0x08, 0x48, 0x8B, -1, 0x48, 0x8B, -1, 0x49, 0x81, -1, 0xFC };

    // Registry entry used for -launcherlogin.
    public static short[] LauncherLogin = @"Software\Blizzard Entertainment\Battle.net\Launch Options\".ToPattern();
}
