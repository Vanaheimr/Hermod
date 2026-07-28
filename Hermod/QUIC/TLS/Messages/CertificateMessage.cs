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
/// Splits the body of a Certificate message (RFC 8446 §4.4.2) into the individual DER certificates.
/// Layout: <c>certificate_request_context&lt;0..2^8-1&gt; ‖ CertificateEntry certificate_list&lt;0..2^24-1&gt;</c>,
/// where each entry is <c>cert_data&lt;1..2^24-1&gt; ‖ extensions&lt;0..2^16-1&gt;</c>. The first
/// certificate is the end-entity (leaf) certificate.
/// </summary>
public static class CertificateMessage
{
    /// <summary>
    /// Builds the complete message (including the handshake header). <paramref name="der"/> may be
    /// empty: that is the client's way of saying "I have no certificate for you" (§4.4.2.4), which
    /// the server may accept or refuse. An empty message from a SERVER is always an error.
    /// </summary>
    /// <param name="context">
    /// The <c>certificate_request_context</c> echoed from the CertificateRequest. Empty in the main
    /// handshake, which is the only kind QUIC allows (RFC 9001 §4.4).
    /// </param>
    public static byte[] Build(ReadOnlySpan<byte> der, ReadOnlySpan<byte> context = default)
    {
        var w = new BufferWriter(der.Length + 32);
        try
        {
            w.WriteByte((byte)HandshakeType.Certificate);
            int bodyLen = TlsWriter.BeginVector(ref w, 3);

            w.WriteByte((byte)context.Length);
            w.WriteBytes(context);

            int listLen = TlsWriter.BeginVector(ref w, 3);
            if (!der.IsEmpty)
            {
                int certLen = TlsWriter.BeginVector(ref w, 3);
                w.WriteBytes(der);
                TlsWriter.EndVector(ref w, certLen, 3);
                w.WriteUInt16(0); // no per-certificate extensions
            }
            TlsWriter.EndVector(ref w, listLen, 3);

            TlsWriter.EndVector(ref w, bodyLen, 3);
            return w.WrittenSpan.ToArray();
        }
        finally { w.Dispose(); }
    }

    /// <summary>
    /// Parses the body. An empty certificate list is <b>well-formed</b> — see
    /// <see cref="TryParseWithContext"/> if the distinction between "empty" and "malformed" matters,
    /// as it does when a server receives a client's Certificate.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> body, out List<byte[]> certificates)
        => TryParseWithContext(body, out certificates, out _) && certificates.Count > 0;

    /// <summary>
    /// Parses the body, keeping an empty certificate list as a valid outcome and returning the
    /// <c>certificate_request_context</c> so a server can check that the client echoed the right one.
    /// </summary>
    public static bool TryParseWithContext(ReadOnlySpan<byte> body, out List<byte[]> certificates,
                                           out byte[] context)
    {
        certificates = [];
        context = [];

        var r = new BufferReader(body);
        if (!r.TryReadByte(out byte contextLength) ||
            !r.TryReadBytes(contextLength, out ReadOnlySpan<byte> contextBytes))
            return false;
        context = contextBytes.ToArray();

        if (!TryReadUInt24(ref r, out int listLength) || listLength > r.Remaining)
            return false;

        int listEnd = r.Position + listLength;
        while (r.Position < listEnd)
        {
            if (!TryReadUInt24(ref r, out int certLength) ||
                !r.TryReadBytes(certLength, out ReadOnlySpan<byte> der))
                return false;
            certificates.Add(der.ToArray());

            if (!r.TryReadUInt16(out ushort extLength) || !r.TrySkip(extLength))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Reads a 3-byte length (big-endian), as is customary in TLS for certificate vectors.
    /// </summary>
    private static bool TryReadUInt24(ref BufferReader r, out int value)
    {
        value = 0;
        if (!r.TryReadBytes(3, out ReadOnlySpan<byte> b))
            return false;
        value = (b[0] << 16) | (b[1] << 8) | b[2];
        return true;
    }
}
