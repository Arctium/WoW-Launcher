// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Arctium.WoW.Launcher.Constants;

enum MemProtection
{
    NoAccess         = 0x1,
    ReadOnly         = 0x2,
    ReadWrite        = 0x4,
    WriteCopy        = 0x8,
    Execute          = 0x10,
    ExecuteRead      = 0x20,
    ExecuteReadWrite = 0x40,
    ExecuteWriteCopy = 0x80,
    Guard            = 0x100,
    NoCache          = 0x200,
    WriteCombine     = 0x400,
}
