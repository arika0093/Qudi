using System;
using System.Collections.Generic;
using System.Text;

namespace Qudi.Generator.Utility;

internal static class FastHashGenerator
{
    /// <summary>
    /// Generates a Fast Hash (FNV-1a 64bit) from the given input string.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>12 character hexadecimal hash string. e.g. "3f9d5b7c2a1e"</returns>
    public static string Generate(string input, int length = 12)
    {
        const ulong prime = 1099511628211UL;
        ulong hash = 14695981039346656037UL;
        foreach (char c in input)
        {
            hash ^= c;
            hash *= prime;
        }
        return hash.ToString("x").PadLeft(length, '0')[..length];
    }
}
