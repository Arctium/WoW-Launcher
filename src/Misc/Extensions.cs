// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Arctium.WoW.Launcher.Misc;

static class Extensions
{
    public static nint ToNint(this byte[] buffer) => (nint)BitConverter.ToInt64(buffer, 0);
    public static nint ToNint(this long value) => (nint)value;

    public static byte[] GetCopy(this byte[] data)
    {
        var copy = new byte[data.Length];

        Array.Copy(data, copy, data.Length);

        return copy;
    }

    public static short[] GetCopy(this short[] data)
    {
        var copy = new short[data.Length];

        Array.Copy(data, copy, data.Length);

        return copy;
    }

    public static long FindPattern(this byte[] data, short[] pattern, long start, long baseOffset = 0)
    {
        long matches;

        for (long i = start; i < data.Length; i++)
        {
            if (pattern.Length > (data.Length - i))
                return 0;

            for (matches = 0; matches < pattern.Length; matches++)
            {
                if ((pattern[matches] != -1) && (data[i + matches] != (byte)pattern[matches]))
                    break;
            }

            if (matches == pattern.Length)
                return baseOffset + i;
        }

        return 0;
    }

    public static long FindPattern(this byte[] data, short[] pattern, long baseOffset = 0) => FindPattern(data, pattern, 0L, baseOffset);

    public static HashSet<long> FindPattern(this byte[] data, short[] pattern, int maxMatches, long maxOffset)
    {
        var matchList = new HashSet<long>();

        long match = 0;

        do
        {
            match = data.FindPattern(pattern, match, 0);

            if (match == 0)
                continue;
            
            matchList.Add(match);
            
            match += pattern.Length;

        } while ((matchList.Count < maxMatches || match < maxOffset) && match != 0);

        return matchList;
    }

    public static short[] ToPattern(this string data) => Encoding.UTF8.GetBytes(data).Select(b => (short)b).ToArray();
}
