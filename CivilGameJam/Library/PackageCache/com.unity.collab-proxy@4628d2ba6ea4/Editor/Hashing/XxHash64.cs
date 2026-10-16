// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Ported from dotnet/runtime (release/9.0):
// src/libraries/System.IO.Hashing/src/System/IO/Hashing/XxHash64.State.cs
// Only the Avalanche mixer required by XxHash128 is included. The XXH64
// primes match XxHashShared.Prime64_2/Prime64_3, so those constants are used.

namespace Unity.PlasticSCM.Editor.Hashing
{
    internal static class XxHash64
    {
        internal static ulong Avalanche(ulong hash)
        {
            hash ^= hash >> 33;
            hash *= XxHashShared.Prime64_2;
            hash ^= hash >> 29;
            hash *= XxHashShared.Prime64_3;
            hash ^= hash >> 32;
            return hash;
        }
    }
}
