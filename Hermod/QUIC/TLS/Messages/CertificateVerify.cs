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

using System.Text;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

/// <summary>
/// Helpers around the CertificateVerify message (RFC 8446 §4.4.3). The signed content is
/// <c>64×0x20 ‖ context string ‖ 0x00 ‖ transcript hash</c>; the message body is
/// <c>SignatureScheme (2) ‖ signature&lt;0..2^16-1&gt;</c>.
/// </summary>
public static class CertificateVerify
{
    /// <summary>
    /// Context string when the server signs.
    /// </summary>
    public const string ServerContext = "TLS 1.3, server CertificateVerify";

    /// <summary>
    /// Context string when the client signs (client authentication).
    /// </summary>
    public const string ClientContext = "TLS 1.3, client CertificateVerify";

    /// <summary>
    /// Builds the content to sign or verify (RFC 8446 §4.4.3).
    /// </summary>
    public static byte[] BuildSignatureContent(string context, ReadOnlySpan<byte> transcriptHash)
    {
        byte[] label = Encoding.ASCII.GetBytes(context);
        byte[] content = new byte[64 + label.Length + 1 + transcriptHash.Length];
        content.AsSpan(0, 64).Fill(0x20); // 64 spaces
        label.CopyTo(content, 64);
        content[64 + label.Length] = 0x00; // separator byte
        transcriptHash.CopyTo(content.AsSpan(64 + label.Length + 1));
        return content;
    }

    /// <summary>
    /// Splits the CertificateVerify body into (signature scheme, signature).
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> body, out SignatureScheme scheme, out byte[] signature)
    {
        scheme = default;
        signature = [];

        var r = new BufferReader(body);
        if (!r.TryReadUInt16(out ushort schemeValue) ||
            !r.TryReadUInt16(out ushort sigLen) ||
            !r.TryReadBytes(sigLen, out ReadOnlySpan<byte> sig))
            return false;

        scheme = (SignatureScheme)schemeValue;
        signature = sig.ToArray();
        return true;
    }
}
