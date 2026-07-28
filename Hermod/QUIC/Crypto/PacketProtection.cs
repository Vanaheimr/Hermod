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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Crypto;

/// <summary>
/// AEAD algorithm of a cipher suite: AES-GCM (AES-128/256) or ChaCha20-Poly1305.
/// </summary>
public enum AeadAlgorithm
{
    AesGcm,
    ChaCha20Poly1305,
}

/// <summary>
/// Packet protection (AEAD, RFC 9001 §5.3) and header protection (§5.4) for one direction on one
/// encryption level. Encapsulates key, IV and HP key as ready-to-use crypto objects. Supports
/// AES-GCM (HP via AES-ECB) and ChaCha20-Poly1305 (HP via ChaCha20 keystream, RFC 9001 §5.4.4).
/// </summary>
public sealed class PacketProtection : IDisposable
{
    private readonly AesGcm? _aesGcm;
    private readonly ChaCha20Poly1305? _chacha;
    private readonly byte[] _iv;
    private readonly Aes? _headerProtectionAes;   // AES-ECB (AES-GCM suites)
    private readonly byte[]? _headerProtectionKey; // ChaCha20 HP key (ChaCha20 suite)

    private const int TagLength = 16;
    private const int SampleLength = 16;

    /// <summary>
    /// Creates a protection for the given AEAD algorithm (default: AES-GCM, incl. Initial).
    /// </summary>
    public PacketProtection(TrafficKeys keys, AeadAlgorithm algorithm = AeadAlgorithm.AesGcm)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _iv = keys.Iv;
        if (algorithm == AeadAlgorithm.ChaCha20Poly1305)
        {
            _chacha = new ChaCha20Poly1305(keys.Key);
            _headerProtectionKey = keys.HeaderProtectionKey;
        }
        else
        {
            _aesGcm = new AesGcm(keys.Key, TagLength);
            _headerProtectionAes = Aes.Create();
            _headerProtectionAes.Key = keys.HeaderProtectionKey;
        }
    }

    // ---- AEAD nonce (RFC 9001 §5.3) --------------------------------------------------------

    /// <summary>
    /// Nonce = IV XOR (packet number, left-padded with zeros to IV length, big-endian).
    /// The full reconstructed packet number counts, not the truncated wire encoding.
    /// </summary>
    private void ComputeNonce(ulong packetNumber, Span<byte> nonce)
    {
        _iv.CopyTo(nonce);
        for (int i = 0; i < 8; i++)
            nonce[nonce.Length - 1 - i] ^= (byte)(packetNumber >> (8 * i));
    }

    // ---- AEAD encrypt/decrypt --------------------------------------------------------------

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> with the header as associated data.
    /// <paramref name="output"/> must hold <c>plaintext.Length + 16</c> bytes; the return value is the written length.
    /// </summary>
    public int Encrypt(ulong packetNumber, ReadOnlySpan<byte> header, ReadOnlySpan<byte> plaintext, Span<byte> output)
    {
        Span<byte> nonce = stackalloc byte[12];
        ComputeNonce(packetNumber, nonce);

        Span<byte> ciphertext = output[..plaintext.Length];
        Span<byte> tag = output.Slice(plaintext.Length, TagLength);
        if (_chacha is not null)
            _chacha.Encrypt(nonce, plaintext, ciphertext, tag, header);
        else
            _aesGcm!.Encrypt(nonce, plaintext, ciphertext, tag, header);
        return plaintext.Length + TagLength;
    }

    /// <summary>
    /// Decrypts <paramref name="ciphertextWithTag"/> (ciphertext followed by the 16-byte tag).
    /// Returns <c>false</c> when the tag does not match (authentication failure) instead of throwing.
    /// </summary>
    public bool Decrypt(ulong packetNumber, ReadOnlySpan<byte> header, ReadOnlySpan<byte> ciphertextWithTag, Span<byte> plaintext, out int written)
    {
        written = 0;
        if (ciphertextWithTag.Length < TagLength)
            return false;

        Span<byte> nonce = stackalloc byte[12];
        ComputeNonce(packetNumber, nonce);

        ReadOnlySpan<byte> ciphertext = ciphertextWithTag[..^TagLength];
        ReadOnlySpan<byte> tag = ciphertextWithTag[^TagLength..];
        try
        {
            if (_chacha is not null)
                _chacha.Decrypt(nonce, ciphertext, tag, plaintext[..ciphertext.Length], header);
            else
                _aesGcm!.Decrypt(nonce, ciphertext, tag, plaintext[..ciphertext.Length], header);
            written = ciphertext.Length;
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }
    }

    // ---- Header protection (RFC 9001 §5.4) -------------------------------------------------

    /// <summary>
    /// Computes the 5-byte mask from a 16-byte sample of the ciphertext: <c>AES-ECB(hp_key, sample)[0..5]</c>,
    /// or for ChaCha20 the ChaCha20 keystream over the HP key (RFC 9001 §5.4.4).
    /// </summary>
    public void HeaderProtectionMask(ReadOnlySpan<byte> sample, Span<byte> mask)
    {
        if (_headerProtectionKey is not null)
        {
            ChaCha20.HeaderProtectionMask(_headerProtectionKey, sample[..SampleLength], mask);
            return;
        }
        Span<byte> block = stackalloc byte[16];
        _headerProtectionAes!.EncryptEcb(sample[..SampleLength], block, PaddingMode.None);
        block[..5].CopyTo(mask);
    }

    // ---- High level: protect / unprotect a whole packet -------------------------------------

    /// <summary>
    /// Protects a complete packet: encrypts the payload and then applies header protection.
    /// <paramref name="unprotectedHeader"/> ends with the (unmasked) packet-number bytes;
    /// <paramref name="packetNumberLength"/> is their count (1..4).
    /// </summary>
    /// <param name="longHeader"><c>true</c> for long-header packets (Initial/Handshake/0-RTT), otherwise short header.</param>
    /// <returns>The finished, protected packet (header + ciphertext + tag).</returns>
    public byte[] ProtectPacket(
        ReadOnlySpan<byte> unprotectedHeader,
        int packetNumberLength,
        ulong packetNumber,
        ReadOnlySpan<byte> payload,
        bool longHeader)
    {
        if (packetNumberLength is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(packetNumberLength));

        int pnOffset = unprotectedHeader.Length - packetNumberLength;
        byte[] packet = new byte[unprotectedHeader.Length + payload.Length + TagLength];
        unprotectedHeader.CopyTo(packet);

        // 1) Encrypt the payload (AAD = unmasked header).
        Encrypt(packetNumber, unprotectedHeader, payload, packet.AsSpan(unprotectedHeader.Length));

        // 2) Header protection: the sample starts 4 bytes after the start of the packet-number field.
        int sampleOffset = pnOffset + 4;
        Span<byte> mask = stackalloc byte[5];
        HeaderProtectionMask(packet.AsSpan(sampleOffset, SampleLength), mask);
        ApplyHeaderMask(packet, pnOffset, packetNumberLength, mask, longHeader);

        return packet;
    }

    /// <summary>
    /// Unprotects a complete received packet. <paramref name="packetNumberOffset"/> is the offset of
    /// the packet-number field (after version/CIDs/token/length for the long header, or after the
    /// DCID for the short header). On success, the decrypted frames are written into
    /// <paramref name="plaintext"/>.
    /// </summary>
    public bool UnprotectPacket(
        Span<byte> packet,
        int packetNumberOffset,
        long largestAckedPacketNumber,
        bool longHeader,
        Span<byte> plaintext,
        out ulong packetNumber,
        out int plaintextLength)
    {
        plaintextLength = 0;
        if (!RemoveHeaderProtection(packet, packetNumberOffset, largestAckedPacketNumber, longHeader,
                out packetNumber, out int headerLength))
            return false;

        ReadOnlySpan<byte> header = packet[..headerLength];
        ReadOnlySpan<byte> ciphertextWithTag = packet[headerLength..];
        return Decrypt(packetNumber, header, ciphertextWithTag, plaintext, out plaintextLength);
    }

    /// <summary>
    /// Removes only the header protection (RFC 9001 §5.4) and reconstructs the packet number,
    /// <em>without</em> AEAD decryption. The HP key is constant across key updates (§6.1), so the
    /// first byte can be read afterwards (incl. the key-phase bit) to choose the right AEAD keys.
    /// <paramref name="headerLength"/> is the length of the header up to and incl. the packet number
    /// (= start of the ciphertext).
    /// </summary>
    public bool RemoveHeaderProtection(
        Span<byte> packet,
        int packetNumberOffset,
        long largestAckedPacketNumber,
        bool longHeader,
        out ulong packetNumber,
        out int headerLength)
    {
        packetNumber = 0;
        headerLength = 0;

        int sampleOffset = packetNumberOffset + 4;
        if (packet.Length < sampleOffset + SampleLength)
            return false;

        Span<byte> mask = stackalloc byte[5];
        HeaderProtectionMask(packet.Slice(sampleOffset, SampleLength), mask);

        // 1) Unmask the first byte -> obtain the packet-number length (and the key-phase bit).
        byte firstByteMask = longHeader ? (byte)0x0f : (byte)0x1f;
        packet[0] ^= (byte)(mask[0] & firstByteMask);
        int packetNumberLength = (packet[0] & 0x03) + 1;

        // 2) Unmask and reconstruct the packet-number bytes.
        uint truncatedPn = 0;
        for (int i = 0; i < packetNumberLength; i++)
        {
            byte b = (byte)(packet[packetNumberOffset + i] ^ mask[1 + i]);
            packet[packetNumberOffset + i] = b;
            truncatedPn = (truncatedPn << 8) | b;
        }

        packetNumber = PacketNumber.Decode(truncatedPn, packetNumberLength, largestAckedPacketNumber);
        headerLength = packetNumberOffset + packetNumberLength;
        return true;
    }

    private static void ApplyHeaderMask(Span<byte> packet, int pnOffset, int pnLength, ReadOnlySpan<byte> mask, bool longHeader)
    {
        byte firstByteMask = longHeader ? (byte)0x0f : (byte)0x1f;
        packet[0] ^= (byte)(mask[0] & firstByteMask);
        for (int i = 0; i < pnLength; i++)
            packet[pnOffset + i] ^= mask[1 + i];
    }

    public void Dispose()
    {
        _aesGcm?.Dispose();
        _chacha?.Dispose();
        _headerProtectionAes?.Dispose();
    }
}
