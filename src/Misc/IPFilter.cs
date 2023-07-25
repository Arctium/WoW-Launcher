// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;

namespace Arctium.WoW.Launcher.Misc;

public class IPFilter
{
    readonly List<(IPAddress, IPAddress)> _ipRanges = new();

    public void AddCidrRange(ReadOnlySpan<char> cidr)
    {
        var separatorIndex = cidr.IndexOf('/');

        if (separatorIndex < 0)
            return;

        var subnetMaskLengthSpan = cidr[(separatorIndex + 1)..];

        if (!int.TryParse(subnetMaskLengthSpan, out var subnetMaskLength))
            return;

        Span<byte> subnetMaskBytes = stackalloc byte[4];

        for (var i = 0; i < subnetMaskLength; i++)
            subnetMaskBytes[i / 8] |= (byte)(1 << (7 - i % 8));

        if (!IPAddress.TryParse(cidr[..separatorIndex], out var ip))
            return;

        ReadOnlySpan<byte> ipBytes = ip.GetAddressBytes();

        Span<byte> networkAddressBytes = stackalloc byte[4];
        Span<byte> broadcastAddressBytes = stackalloc byte[4];

        for (var i = 0; i < 4; i++)
        {
            networkAddressBytes[i] = (byte)(ipBytes[i] & subnetMaskBytes[i]);
            broadcastAddressBytes[i] = (byte)(networkAddressBytes[i] | ~subnetMaskBytes[i]);
        }

        _ipRanges.Add((new(networkAddressBytes), new(broadcastAddressBytes)));
    }

    public bool IsInRange(ReadOnlySpan<char> targetIP)
    {
        if (!IPAddress.TryParse(targetIP, out var ip))
            return false;

        ReadOnlySpan<byte> targetBytes = ip.GetAddressBytes();

        foreach (var range in _ipRanges)
        {
            ReadOnlySpan<byte> networkAddressBytes = range.Item1.GetAddressBytes();
            ReadOnlySpan<byte> broadcastAddressBytes = range.Item2.GetAddressBytes();

            var isInRange = true;

            for (var i = 0; i < 4; i++)
            {
                if ((targetBytes[i] & networkAddressBytes[i]) != networkAddressBytes[i] ||
                    (targetBytes[i] & broadcastAddressBytes[i]) != targetBytes[i])
                {
                    isInRange = false;
                    break;
                }
            }

            if (isInRange)
                return true;
        }

        return false;
    }
}
