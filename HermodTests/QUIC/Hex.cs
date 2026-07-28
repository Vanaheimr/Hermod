/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC;

/// <summary>
/// Helper functions to parse the RFC hex vectors (often grouped with spaces/line breaks).
/// </summary>
internal static class Hex
{
    public static byte[] Parse(string hex)
    {
        Span<char> compact = stackalloc char[hex.Length];
        int n = 0;
        foreach (char c in hex)
        {
            if (c is ' ' or '\n' or '\r' or '\t')
                continue;
            compact[n++] = c;
        }
        return Convert.FromHexString(compact[..n]);
    }

    public static string ToHex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
