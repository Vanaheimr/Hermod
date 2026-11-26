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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    using System.Buffers.Binary;
    using System.Security.Cryptography;
    using System.Text;


    /// <summary>
    /// The "aes128gcm" content coding (RFC 8188, Encrypted Content-Encoding for
    /// HTTP): a body encrypted with AEAD_AES_128_GCM in records of a fixed size,
    /// behind a header that carries everything a recipient needs but the key.
    ///
    /// Unlike the codings of <see cref="HTTPContentCoding"/>, this one cannot be
    /// undone by whoever receives it: "How this key is acquired is not defined in
    /// this document" (Section 2). So it is not among
    /// <see cref="HTTPContentCoding.Supported"/>, no client advertises it, and a
    /// message that arrives with it is left as it came by the automatic decoding
    /// of either role. The application that holds the key encrypts and decrypts
    /// with the methods here; the header's keyid names the key it should use.
    ///
    /// Everything RFC 8188 makes a decrypter fail on fails here, with an
    /// <see cref="InvalidDataException"/>, as any other coding fails on octets
    /// it cannot undo: a header shorter than it says, a record size under 18, no
    /// record at all, a record that does not authenticate — tampered with,
    /// reordered, under another key — a record with no non-zero octet, a last
    /// record whose delimiter is not 2, and any other record whose delimiter is
    /// not 1, which is what makes a body cut off at a record boundary fail.
    /// </summary>
    public static class HTTPEncryptedContentCoding
    {

        #region Data

        /// <summary>
        /// The name of the content coding.
        /// </summary>
        public const String AES128GCM = "aes128gcm";

        /// <summary>
        /// The length of the header without its keyid: salt, record size, idlen.
        /// </summary>
        public const Int32 FixedHeaderLength = 16 + 4 + 1;

        /// <summary>
        /// The smallest valid record size: a padding delimiter and a tag.
        /// </summary>
        public const UInt32 MinimumRecordSize = 18;

        /// <summary>
        /// The record size used when the caller names none, the one of RFC 8188's
        /// first example.
        /// </summary>
        public const UInt32 DefaultRecordSize = 4096;

        private const Int32 SaltLength   = 16;
        private const Int32 KeyLength    = 16;
        private const Int32 NonceLength  = 12;
        private const Int32 TagLength    = 16;

        private const Byte  Delimiter     = 0x01;
        private const Byte  LastDelimiter = 0x02;

        private static readonly Byte[] KeyInfo    = Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0");
        private static readonly Byte[] NonceInfo  = Encoding.ASCII.GetBytes("Content-Encoding: nonce\0");

        #endregion


        #region Encrypt (Plaintext, InputKeyingMaterial, KeyId = default, RecordSize = DefaultRecordSize, Salt = default)

        /// <summary>
        /// Apply the "aes128gcm" content coding: the header, then the plaintext in
        /// records of <paramref name="RecordSize"/> octets each, the last one
        /// shorter. A record carries no padding beyond its delimiter.
        /// </summary>
        /// <param name="Plaintext">The representation to encrypt.</param>
        /// <param name="InputKeyingMaterial">The key the recipient knows (Section 2.2: the IKM of HKDF).</param>
        /// <param name="KeyId">What names that key to the recipient, at most 255 octets; it SHOULD be UTF-8.</param>
        /// <param name="RecordSize">The record size, at least 18 octets.</param>
        /// <param name="Salt">16 octets that MUST NOT be used again with this key; random ones when none are given.</param>
        public static Byte[] Encrypt(ReadOnlySpan<Byte>  Plaintext,
                                     ReadOnlySpan<Byte>  InputKeyingMaterial,
                                     ReadOnlySpan<Byte>  KeyId        = default,
                                     UInt32              RecordSize   = DefaultRecordSize,
                                     ReadOnlySpan<Byte>  Salt         = default)
        {

            if (RecordSize < MinimumRecordSize)
                throw new ArgumentOutOfRangeException(nameof(RecordSize), RecordSize, $"The record size must be at least {MinimumRecordSize} octets!");

            if (KeyId.Length > Byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(KeyId), KeyId.Length, "The keyid must be at most 255 octets long!");

            if (!Salt.IsEmpty && Salt.Length != SaltLength)
                throw new ArgumentException($"The salt must be {SaltLength} octets long!", nameof(Salt));

            var salt     = Salt.IsEmpty ? RandomNumberGenerator.GetBytes(SaltLength) : Salt.ToArray();

            var (key, nonceBase) = DeriveKeyAndNonce(InputKeyingMaterial, salt);

            // At most rs - 17 octets of content per record: the delimiter and the
            // tag take the rest. An empty body is still one record, the last.
            var perRecord  = (Int64) RecordSize - 1 - TagLength;
            var records    = Math.Max(1, (Plaintext.Length + perRecord - 1) / perRecord);
            var lastLength = Plaintext.Length - (records - 1) * perRecord;

            var output     = new Byte[FixedHeaderLength + KeyId.Length +
                                      (records - 1) * RecordSize +
                                      lastLength + 1 + TagLength];

            salt.CopyTo(output, 0);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(SaltLength), RecordSize);
            output[SaltLength + 4] = (Byte) KeyId.Length;
            KeyId.CopyTo(output.AsSpan(FixedHeaderLength));

            using var aes     = new AesGcm(key, TagLength);

            var nonce         = new Byte[NonceLength];
            var record        = new Byte[Math.Min(perRecord, Plaintext.Length) + 1];
            var position      = FixedHeaderLength + KeyId.Length;

            for (var sequence = 0L; sequence < records; sequence++)
            {

                var isLast    = sequence == records - 1;
                var content   = Plaintext.Slice((Int32) (sequence * perRecord),
                                                (Int32) (isLast ? lastLength : perRecord));

                var plain     = record.AsSpan(0, content.Length + 1);
                content.CopyTo(plain);
                plain[^1]     = isLast ? LastDelimiter : Delimiter;

                NonceFor(nonceBase, (UInt64) sequence, nonce);

                aes.Encrypt(nonce,
                            plain,
                            output.AsSpan(position, plain.Length),
                            output.AsSpan(position + plain.Length, TagLength));

                position     += plain.Length + TagLength;

            }

            return output;

        }

        #endregion

        #region ReadKeyId (Body)

        /// <summary>
        /// The keyid of an "aes128gcm" body's header: what names the key to
        /// decrypt it with.
        /// </summary>
        /// <exception cref="InvalidDataException">The body is shorter than its header.</exception>
        public static Byte[] ReadKeyId(ReadOnlySpan<Byte> Body)

            => Body.Slice(FixedHeaderLength, ReadHeader(Body).KeyIdLength).ToArray();

        #endregion

        #region Decrypt (Body, InputKeyingMaterial, MaxDecryptedSize)

        /// <summary>
        /// Undo the "aes128gcm" content coding with this key, whatever the keyid
        /// of the header, refusing to produce more than
        /// <paramref name="MaxDecryptedSize"/> octets.
        /// </summary>
        /// <exception cref="InvalidDataException">The body is not a valid "aes128gcm" body under this key, or decrypts to more than the limit.</exception>
        public static Byte[] Decrypt(ReadOnlySpan<Byte>  Body,
                                     ReadOnlySpan<Byte>  InputKeyingMaterial,
                                     Int64               MaxDecryptedSize)
        {

            var (recordSize, keyIdLength) = ReadHeader(Body);

            var records = Body[(FixedHeaderLength + keyIdLength)..];

            if (records.IsEmpty)
                throw new InvalidDataException("The 'aes128gcm' body has no record: it ends before its last one");

            var (key, nonceBase) = DeriveKeyAndNonce(InputKeyingMaterial, Body[..SaltLength].ToArray());

            using var aes      = new AesGcm(key, TagLength);
            using var output   = new MemoryStream();

            var nonce          = new Byte[NonceLength];
            var plain          = new Byte[Math.Min(recordSize, (UInt32) records.Length)];
            var position       = 0;

            for (var sequence = 0UL; position < records.Length; sequence++)
            {

                var size    = (Int32) Math.Min(recordSize, (UInt32) (records.Length - position));
                var isLast  = position + size == records.Length;

                if (size < 1 + TagLength)
                    throw new InvalidDataException($"Record {sequence} of the 'aes128gcm' body is {size} octets: shorter than a delimiter and a tag");

                var record  = records.Slice(position, size);
                var content = plain.AsSpan(0, size - TagLength);

                NonceFor(nonceBase, sequence, nonce);

                try
                {
                    aes.Decrypt(nonce,
                                record[..^TagLength],
                                record[^TagLength..],
                                content);
                }
                catch (CryptographicException e)
                {
                    throw new InvalidDataException($"Record {sequence} of the 'aes128gcm' body does not authenticate: tampered with, reordered, or under another key", e);
                }

                // Section 2: the delimiter is the last octet that is not zero.
                var delimiterAt = content.LastIndexOfAnyExcept((Byte) 0x00);

                if (delimiterAt < 0)
                    throw new InvalidDataException($"Record {sequence} of the 'aes128gcm' body has no padding delimiter");

                var delimiter = content[delimiterAt];

                if (isLast && delimiter != LastDelimiter)
                    throw new InvalidDataException($"The last record of the 'aes128gcm' body has the delimiter {delimiter}, not {LastDelimiter}: the body is cut off");

                if (!isLast && delimiter != Delimiter)
                    throw new InvalidDataException($"Record {sequence} of the 'aes128gcm' body has the delimiter {delimiter}, not {Delimiter}, and is not the last");

                if (output.Length + delimiterAt > MaxDecryptedSize)
                    throw new InvalidDataException($"Decrypted 'aes128gcm' content exceeds the {MaxDecryptedSize}-byte limit");

                output.Write(content[..delimiterAt]);

                position += size;

            }

            return output.ToArray();

        }

        #endregion

        #region Decrypt (Body, KeyFor, MaxDecryptedSize)

        /// <summary>
        /// Undo the "aes128gcm" content coding with the key that
        /// <paramref name="KeyFor"/> gives for the header's keyid.
        /// </summary>
        /// <param name="Body">The encrypted body, header included.</param>
        /// <param name="KeyFor">The key for a keyid, or null if there is none.</param>
        /// <param name="MaxDecryptedSize">Hard ceiling on the decrypted size.</param>
        /// <exception cref="KeyNotFoundException">There is no key for the header's keyid.</exception>
        /// <exception cref="InvalidDataException">The body is not a valid "aes128gcm" body under that key, or decrypts to more than the limit.</exception>
        public static Byte[] Decrypt(ReadOnlySpan<Byte>    Body,
                                     Func<Byte[], Byte[]?>  KeyFor,
                                     Int64                 MaxDecryptedSize)
        {

            var keyId = ReadKeyId(Body);

            var key   = KeyFor(keyId) ?? throw new KeyNotFoundException($"No key for the keyid '{Convert.ToHexString(keyId)}' of the 'aes128gcm' body");

            return Decrypt(Body, key, MaxDecryptedSize);

        }

        #endregion


        #region (private) ReadHeader(Body)

        /// <summary>
        /// The record size and the keyid's length of a body's header (Section
        /// 2.1): the salt is its first 16 octets, the keyid follows the fixed
        /// part, and the records follow the keyid.
        /// </summary>
        private static (UInt32 RecordSize, Int32 KeyIdLength) ReadHeader(ReadOnlySpan<Byte> Body)
        {

            if (Body.Length < FixedHeaderLength)
                throw new InvalidDataException($"The 'aes128gcm' body is {Body.Length} octets: shorter than its header");

            var recordSize   = BinaryPrimitives.ReadUInt32BigEndian(Body[SaltLength..]);
            var keyIdLength  = Body[SaltLength + 4];

            if (Body.Length < FixedHeaderLength + keyIdLength)
                throw new InvalidDataException($"The 'aes128gcm' body is {Body.Length} octets: shorter than its header with a keyid of {keyIdLength}");

            if (recordSize < MinimumRecordSize)
                throw new InvalidDataException($"The record size of the 'aes128gcm' body is {recordSize}: under {MinimumRecordSize}");

            return (recordSize, keyIdLength);

        }

        #endregion

        #region (private) DeriveKeyAndNonce(InputKeyingMaterial, Salt)

        /// <summary>
        /// Sections 2.2 and 2.3: PRK = HMAC-SHA-256(salt, IKM), and from it the
        /// content-encryption key and the base of every record's nonce.
        /// </summary>
        private static (Byte[] Key, Byte[] NonceBase) DeriveKeyAndNonce(ReadOnlySpan<Byte>  InputKeyingMaterial,
                                                                        Byte[]              Salt)
        {

            var prk = HKDF.Extract(HashAlgorithmName.SHA256, InputKeyingMaterial.ToArray(), Salt);

            return (HKDF.Expand(HashAlgorithmName.SHA256, prk, KeyLength,   KeyInfo),
                    HKDF.Expand(HashAlgorithmName.SHA256, prk, NonceLength, NonceInfo));

        }

        #endregion

        #region (private) NonceFor(NonceBase, Sequence, Nonce)

        /// <summary>
        /// Section 2.3: the nonce base XOR the record's sequence number, a 96-bit
        /// unsigned integer in network byte order.
        /// </summary>
        private static void NonceFor(Byte[]      NonceBase,
                                     UInt64      Sequence,
                                     Span<Byte>  Nonce)
        {

            NonceBase.CopyTo(Nonce);

            var low = BinaryPrimitives.ReadUInt64BigEndian(Nonce[4..]);

            BinaryPrimitives.WriteUInt64BigEndian(Nonce[4..], low ^ Sequence);

        }

        #endregion

    }

}
