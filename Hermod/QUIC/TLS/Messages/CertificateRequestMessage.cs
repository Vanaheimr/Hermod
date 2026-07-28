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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

/// <summary>
/// CertificateRequest (RFC 8446 §4.3.2) — how a server asks the client to authenticate.
/// <para>
/// Body: <c>certificate_request_context&lt;0..2^8-1&gt; ‖ Extension extensions&lt;2..2^16-1&gt;</c>.
/// The context "SHALL be zero length unless used for the post-handshake authentication exchanges",
/// which QUIC forbids outright (RFC 9001 §4.4) — so here it is always empty, and the client echoes
/// it in its Certificate message. The <c>signature_algorithms</c> extension MUST be present; TLS 1.3
/// moved the algorithm list out of the message body and into extensions.
/// </para>
/// </summary>
public static class CertificateRequestMessage
{
    /// <summary>
    /// Builds the complete message (including the 4-byte handshake header) advertising the signature
    /// schemes the server is willing to verify.
    /// </summary>
    public static byte[] Build(IReadOnlyList<SignatureScheme> signatureSchemes)
    {
        ArgumentOutOfRangeException.ThrowIfZero(signatureSchemes.Count);

        var w = new BufferWriter(64);
        try
        {
            w.WriteByte((byte)HandshakeType.CertificateRequest);
            int bodyLen = TlsWriter.BeginVector(ref w, 3);

            w.WriteByte(0); // certificate_request_context: empty in the main handshake (§4.3.2)

            int extLen = TlsWriter.BeginVector(ref w, 2);
            {
                w.WriteUInt16((ushort)ExtensionType.SignatureAlgorithms);
                int extBody = TlsWriter.BeginVector(ref w, 2);
                {
                    int listLen = TlsWriter.BeginVector(ref w, 2);
                    foreach (SignatureScheme scheme in signatureSchemes)
                        w.WriteUInt16((ushort)scheme);
                    TlsWriter.EndVector(ref w, listLen, 2);
                }
                TlsWriter.EndVector(ref w, extBody, 2);
            }
            TlsWriter.EndVector(ref w, extLen, 2);

            TlsWriter.EndVector(ref w, bodyLen, 3);
            return w.WrittenSpan.ToArray();
        }
        finally { w.Dispose(); }
    }

    /// <summary>
    /// Parses the message body. Unknown extensions are ignored, as §4.3.2 requires ("Clients MUST
    /// ignore unrecognized extensions"); a missing <c>signature_algorithms</c> makes the message
    /// invalid.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> body,
                                out byte[] context,
                                out List<SignatureScheme> signatureSchemes)
    {
        context = [];
        signatureSchemes = [];

        var r = new BufferReader(body);
        if (!r.TryReadByte(out byte contextLength) ||
            !r.TryReadBytes(contextLength, out ReadOnlySpan<byte> contextBytes))
            return false;
        context = contextBytes.ToArray();

        if (!r.TryReadUInt16(out ushort extensionsLength) || extensionsLength > r.Remaining)
            return false;

        int end = r.Position + extensionsLength;
        while (r.Position < end)
        {
            if (!r.TryReadUInt16(out ushort type) ||
                !r.TryReadUInt16(out ushort length) ||
                !r.TryReadBytes(length, out ReadOnlySpan<byte> data))
                return false;

            if (type != (ushort)ExtensionType.SignatureAlgorithms)
                continue; // §4.3.2: ignore what we do not know

            var list = new BufferReader(data);
            if (!list.TryReadUInt16(out ushort listLength) || listLength > list.Remaining || (listLength & 1) != 0)
                return false;
            for (int i = 0; i < listLength; i += 2)
            {
                if (!list.TryReadUInt16(out ushort scheme))
                    return false;
                signatureSchemes.Add((SignatureScheme)scheme);
            }
        }

        return signatureSchemes.Count > 0;
    }
}
