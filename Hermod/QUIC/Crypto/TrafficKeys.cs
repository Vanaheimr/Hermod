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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Crypto;

/// <summary>
/// A set of keys for one direction/encryption level, derived from a traffic secret
/// (RFC 9001, §5.1): packet-protection key, IV and header-protection key.
/// </summary>
public sealed class TrafficKeys
{
    /// <summary>
    /// The underlying traffic secret (for later key updates).
    /// </summary>
    public byte[] Secret { get; }

    /// <summary>
    /// AEAD key for packet protection ("quic key").
    /// </summary>
    public byte[] Key { get; }

    /// <summary>
    /// IV for the AEAD nonce ("quic iv"), 12 bytes.
    /// </summary>
    public byte[] Iv { get; }

    /// <summary>
    /// Key for header protection ("quic hp").
    /// </summary>
    public byte[] HeaderProtectionKey { get; }

    private TrafficKeys(byte[] secret, byte[] key, byte[] iv, byte[] hp)
    {
        Secret = secret;
        Key = key;
        Iv = iv;
        HeaderProtectionKey = hp;
    }

    /// <summary>
    /// Derives key/IV/HP from a traffic secret.
    /// </summary>
    /// <param name="hash">Hash of the cipher suite (SHA-256 for the Initial suite).</param>
    /// <param name="secret">The traffic secret.</param>
    /// <param name="keyLength">AEAD key length in bytes (16 for AES-128, 32 for AES-256/ChaCha20).</param>
    public static TrafficKeys FromSecret(HashAlgorithmName hash, ReadOnlySpan<byte> secret, int keyLength)
    {
        // RFC 9001 §5.1: the IV is always 12 bytes; the HP key has the same length as the AEAD key.
        byte[] key = TlsHkdf.ExpandLabel(hash, secret, "quic key", keyLength);
        byte[] iv = TlsHkdf.ExpandLabel(hash, secret, "quic iv", 12);
        byte[] hp = TlsHkdf.ExpandLabel(hash, secret, "quic hp", keyLength);
        return new TrafficKeys(secret.ToArray(), key, iv, hp);
    }

    /// <summary>
    /// Derives the next generation for a key update (RFC 9001 §6.1):
    /// <c>secret_&lt;n+1&gt; = HKDF-Expand-Label(secret_&lt;n&gt;, "quic ku", "", Hash.length)</c>,
    /// from which new key and IV follow. The <b>header-protection key stays unchanged</b>.
    /// </summary>
    /// <param name="hash">Hash of the suite.</param>
    /// <param name="hashLength">Length of the hash output (= secret length; 32 for SHA-256).</param>
    public TrafficKeys Next(HashAlgorithmName hash, int hashLength)
    {
        byte[] nextSecret = TlsHkdf.ExpandLabel(hash, Secret, "quic ku", hashLength);
        byte[] key = TlsHkdf.ExpandLabel(hash, nextSecret, "quic key", Key.Length);
        byte[] iv = TlsHkdf.ExpandLabel(hash, nextSecret, "quic iv", 12);
        return new TrafficKeys(nextSecret, key, iv, HeaderProtectionKey);
    }
}
