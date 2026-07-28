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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

/// <summary>
/// The fields of a parsed ServerHello essential to the key schedule (RFC 8446 §4.1.3).
/// </summary>
public sealed class ServerHelloInfo
{
    /// <summary>
    /// <c>true</c> when this is a HelloRetryRequest (special random value).
    /// </summary>
    public required bool IsHelloRetryRequest { get; init; }
    public required CipherSuite CipherSuite { get; init; }
    public ushort? SelectedVersion { get; init; }

    /// <summary>
    /// Group of the server key share (from the key_share extension).
    /// </summary>
    public NamedGroup? KeyShareGroup { get; init; }

    /// <summary>
    /// The server's public key (uncompressed point for EC groups).
    /// </summary>
    public byte[]? KeySharePublicKey { get; init; }

    /// <summary>
    /// Index of the PSK identity accepted by the server (pre_shared_key); <c>null</c> = no resumption.
    /// </summary>
    public ushort? SelectedPskIdentity { get; init; }
}

/// <summary>
/// Parses a ServerHello handshake message (read-only – the server produces it).
/// </summary>
public static class ServerHello
{
    // SHA-256("HelloRetryRequest") – this random value marks an HRR (RFC 8446 §4.1.3).
    private static ReadOnlySpan<byte> HelloRetryRequestRandom =>
    [
        0xCF, 0x21, 0xAD, 0x74, 0xE5, 0x9A, 0x61, 0x11, 0xBE, 0x1D, 0x8C, 0x02, 0x1E, 0x65, 0xB8, 0x91,
        0xC2, 0xA2, 0x11, 0x16, 0x7A, 0xBB, 0x8C, 0x5E, 0x07, 0x9E, 0x09, 0xE2, 0xC8, 0xA8, 0x33, 0x9C
    ];

    /// <summary>
    /// Builds a ServerHello handshake message: legacy_version, random, session-ID echo, chosen
    /// cipher suite, and the extensions supported_versions (only the chosen version) + key_share
    /// (the server key share). <paramref name="keySharePublicKey"/> is the uncompressed EC point.
    /// </summary>
    public static byte[] Build(CipherSuite cipherSuite, NamedGroup keyShareGroup, ReadOnlySpan<byte> keySharePublicKey,
        ushort? selectedPskIdentity = null)
    {
        var w = new BufferWriter(128);
        try
        {
            w.WriteByte((byte)HandshakeType.ServerHello);
            int bodyLen = TlsWriter.BeginVector(ref w, 3);

            w.WriteUInt16(TlsVersions.Tls12); // legacy_version

            Span<byte> random = stackalloc byte[32];
            RandomNumberGenerator.Fill(random);
            w.WriteBytes(random);

            w.WriteByte(0);                        // legacy_session_id_echo: empty (QUIC)
            w.WriteUInt16((ushort)cipherSuite);
            w.WriteByte(0);                        // legacy_compression_method

            int extLen = TlsWriter.BeginVector(ref w, 2);
            {
                // supported_versions (ServerHello form: only the chosen version 0x0304).
                TlsWriter.WriteExtension(ref w, ExtensionType.SupportedVersions, [0x03, 0x04]);

                // key_share: one KeyShareEntry (group + key_exchange<2-byte length>).
                w.WriteUInt16((ushort)ExtensionType.KeyShare);
                int ks = TlsWriter.BeginVector(ref w, 2);
                w.WriteUInt16((ushort)keyShareGroup);
                w.WriteUInt16((ushort)keySharePublicKey.Length);
                w.WriteBytes(keySharePublicKey);
                TlsWriter.EndVector(ref w, ks, 2);

                // pre_shared_key (only with resumption): the chosen identity index (RFC 8446 §4.2.11).
                if (selectedPskIdentity is { } identity)
                    TlsWriter.WriteExtension(ref w, ExtensionType.PreSharedKey,
                        [(byte)(identity >> 8), (byte)identity]);
            }
            TlsWriter.EndVector(ref w, extLen, 2);

            TlsWriter.EndVector(ref w, bodyLen, 3);
            return w.WrittenSpan.ToArray();
        }
        finally { w.Dispose(); }
    }

    /// <summary>
    /// Builds a HelloRetryRequest (RFC 8446 §4.1.4): formally a ServerHello with the fixed
    /// HRR random value; the key_share extension contains only the requested <paramref name="requestedGroup"/>
    /// (without a key).
    /// </summary>
    public static byte[] BuildHelloRetryRequest(CipherSuite cipherSuite, NamedGroup requestedGroup)
    {
        var w = new BufferWriter(64);
        try
        {
            w.WriteByte((byte)HandshakeType.ServerHello);
            int bodyLen = TlsWriter.BeginVector(ref w, 3);

            w.WriteUInt16(TlsVersions.Tls12);
            w.WriteBytes(HelloRetryRequestRandom); // marks the message as an HRR
            w.WriteByte(0);
            w.WriteUInt16((ushort)cipherSuite);
            w.WriteByte(0);

            int extLen = TlsWriter.BeginVector(ref w, 2);
            {
                TlsWriter.WriteExtension(ref w, ExtensionType.SupportedVersions, [0x03, 0x04]);
                // key_share (HRR form): only the requested group.
                TlsWriter.WriteExtension(ref w, ExtensionType.KeyShare,
                    [(byte)((ushort)requestedGroup >> 8), (byte)(ushort)requestedGroup]);
            }
            TlsWriter.EndVector(ref w, extLen, 2);

            TlsWriter.EndVector(ref w, bodyLen, 3);
            return w.WrittenSpan.ToArray();
        }
        finally { w.Dispose(); }
    }

    /// <summary>
    /// Parses the handshake body (including the handshake header with type + 3-byte length).
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> handshakeMessage, out ServerHelloInfo? info)
    {
        info = null;
        var reader = new BufferReader(handshakeMessage);

        if (!reader.TryReadByte(out byte msgType) || msgType != (byte)HandshakeType.ServerHello)
            return false;
        if (!reader.TryReadByte(out byte l0) || !reader.TryReadByte(out byte l1) || !reader.TryReadByte(out byte l2))
            return false;
        int bodyLength = (l0 << 16) | (l1 << 8) | l2;
        if (bodyLength > reader.Remaining)
            return false;

        if (!reader.TryReadUInt16(out _)) // legacy_version
            return false;

        if (!reader.TryReadBytes(32, out ReadOnlySpan<byte> random))
            return false;
        bool isHrr = random.SequenceEqual(HelloRetryRequestRandom);

        // legacy_session_id_echo<0..32>
        if (!reader.TryReadByte(out byte sessionIdLen) || !reader.TrySkip(sessionIdLen))
            return false;

        if (!reader.TryReadUInt16(out ushort cipherSuite))
            return false;

        if (!reader.TryReadByte(out _)) // legacy_compression_method
            return false;

        if (!reader.TryReadUInt16(out ushort extensionsLength) || extensionsLength > reader.Remaining)
            return false;

        ushort? selectedVersion = null;
        NamedGroup? keyShareGroup = null;
        byte[]? keySharePublicKey = null;
        ushort? selectedPskIdentity = null;

        if (!reader.TryReadBytes(extensionsLength, out ReadOnlySpan<byte> extensions))
            return false;
        if (!ParseExtensions(extensions, ref selectedVersion, ref keyShareGroup, ref keySharePublicKey, ref selectedPskIdentity))
            return false;

        info = new ServerHelloInfo
        {
            IsHelloRetryRequest = isHrr,
            CipherSuite = (CipherSuite)cipherSuite,
            SelectedVersion = selectedVersion,
            KeyShareGroup = keyShareGroup,
            KeySharePublicKey = keySharePublicKey,
            SelectedPskIdentity = selectedPskIdentity,
        };
        return true;
    }

    private static bool ParseExtensions(
        ReadOnlySpan<byte> extensions,
        ref ushort? selectedVersion,
        ref NamedGroup? keyShareGroup,
        ref byte[]? keySharePublicKey,
        ref ushort? selectedPskIdentity)
    {
        var reader = new BufferReader(extensions);
        while (!reader.IsEmpty)
        {
            if (!reader.TryReadUInt16(out ushort type) ||
                !reader.TryReadUInt16(out ushort length) ||
                !reader.TryReadBytes(length, out ReadOnlySpan<byte> data))
                return false;

            switch ((ExtensionType)type)
            {
                case ExtensionType.SupportedVersions when data.Length >= 2:
                    selectedVersion = (ushort)((data[0] << 8) | data[1]);
                    break;

                case ExtensionType.KeyShare when data.Length == 2:
                    // HelloRetryRequest key share: only the requested group, no key.
                    keyShareGroup = (NamedGroup)((data[0] << 8) | data[1]);
                    break;

                case ExtensionType.KeyShare when data.Length >= 4:
                    // ServerHello key share: KeyShareEntry = group(2) + key_exchange<1..2^16-1>.
                    var ksReader = new BufferReader(data);
                    if (ksReader.TryReadUInt16(out ushort group) &&
                        ksReader.TryReadUInt16(out ushort keyLen) &&
                        ksReader.TryReadBytes(keyLen, out ReadOnlySpan<byte> key))
                    {
                        keyShareGroup = (NamedGroup)group;
                        keySharePublicKey = key.ToArray();
                    }
                    break;

                case ExtensionType.PreSharedKey when data.Length == 2:
                    // selected_identity (RFC 8446 §4.2.11): the server accepted our PSK.
                    selectedPskIdentity = (ushort)((data[0] << 8) | data[1]);
                    break;
            }
        }
        return true;
    }
}
