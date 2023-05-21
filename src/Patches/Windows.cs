// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Arctium.WoW.Launcher.Patches;

static class Windows
{
#if x64
    public static byte[] Integrity = { 0xC2, 0x00, 0x00 };
    public static byte[] CertBundle = { 0x90, 0x90 };
    public static byte[] CertCommonName = { 0xB0, 0x01 };
    public static byte[] CertChain = { 0xB3, 0x01 };
    public static byte[] ShortJump = { 0xEB };
    public static byte[] NoJump = { 0x00, 0x00, 0x00, 0x00 };
    public static byte[] AuthSeed = { 0x0F, 0x28, 0x05, 0xEF, 0xBE, 0xAD, 0xDE, 0x0F, 0x11, 0x02, 0xC3 };
#elif ARM64
    public static byte[] Integrity = { };
    public static byte[] Branch = { 0xB5 };
    public static byte[] CertCommonName = { 0x20 };
#endif

    // Registry entry used for -launcherlogin.
    public static byte[] LauncherLogin = Encoding.UTF8.GetBytes(@"Software\Custom Game Server Dev\Battle.net\Launch Options\");
}
