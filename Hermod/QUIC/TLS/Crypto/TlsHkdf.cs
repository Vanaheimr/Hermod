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

#region Usings

using System.Security.Cryptography;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

/// <summary>
/// HKDF per the TLS 1.3 convention (RFC 8446, §7.1) – the basis both for the TLS key schedule and
/// for QUIC's key derivation (RFC 9001). Lives in the TLS layer because both use it.
/// </summary>
public static class TlsHkdf
{
    /// <summary>
    /// <c>HKDF-Expand-Label(Secret, Label, Context, Length)</c>.
    /// <para>
    /// The info parameter for HKDF-Expand is the structured <c>HkdfLabel</c>:
    /// </para>
    /// <code>
    /// struct {
    ///     uint16 length = Length;
    ///     opaque label&lt;7..255&gt;   = "tls13 " + Label;   // 1-byte length prefix
    ///     opaque context&lt;0..255&gt; = Context;            // 1-byte length prefix
    /// } HkdfLabel;
    /// </code>
    /// </summary>
    public static byte[] ExpandLabel(
        HashAlgorithmName hash,
        ReadOnlySpan<byte> secret,
        string label,
        ReadOnlySpan<byte> context,
        int length)
    {
        byte[] info = BuildHkdfLabel(label, context, length);
        return HKDF.Expand(hash, secret.ToArray(), length, info);
    }

    /// <summary>
    /// Like <see cref="ExpandLabel(HashAlgorithmName, ReadOnlySpan{byte}, string, ReadOnlySpan{byte}, int)"/>, but with an empty context.
    /// </summary>
    public static byte[] ExpandLabel(HashAlgorithmName hash, ReadOnlySpan<byte> secret, string label, int length)
        => ExpandLabel(hash, secret, label, ReadOnlySpan<byte>.Empty, length);

    /// <summary>
    /// <c>HKDF-Extract(Salt, IKM)</c>. In QUIC Initial: IKM = connection ID, salt = version-specific.
    /// </summary>
    public static byte[] Extract(HashAlgorithmName hash, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> ikm)
        => HKDF.Extract(hash, ikm.ToArray(), salt.ToArray());

    internal static byte[] BuildHkdfLabel(string label, ReadOnlySpan<byte> context, int length)
    {
        // "tls13 " + label, ASCII.
        ReadOnlySpan<byte> fullLabel = Encoding.ASCII.GetBytes("tls13 " + label);
        if (fullLabel.Length is < 7 or > 255)
            throw new ArgumentOutOfRangeException(nameof(label), "Label length outside 7..255.");
        if (context.Length > 255)
            throw new ArgumentOutOfRangeException(nameof(context), "Context longer than 255 bytes.");

        int size = 2 + 1 + fullLabel.Length + 1 + context.Length;
        byte[] info = new byte[size];
        int pos = 0;

        info[pos++] = (byte)(length >> 8);
        info[pos++] = (byte)length;
        info[pos++] = (byte)fullLabel.Length;
        fullLabel.CopyTo(info.AsSpan(pos));
        pos += fullLabel.Length;
        info[pos++] = (byte)context.Length;
        context.CopyTo(info.AsSpan(pos));

        return info;
    }
}
