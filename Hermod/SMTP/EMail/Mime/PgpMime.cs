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

using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// PGP/MIME (RFC 3156) around what was signed or encrypted elsewhere - in a
    /// browser, with the keys that never left it: multipart/signed from the
    /// signed part and its detached signature, multipart/encrypted from a
    /// ciphertext. Nothing here signs, encrypts or needs a key.
    /// </summary>
    public static class PgpMime
    {

        #region Signed(SignedPart, ArmoredSignature)

        /// <summary>
        /// multipart/signed (RFC 3156 §5): the signed part written octet for octet,
        /// and the signature - with the micalg the signature itself names.
        /// </summary>
        /// <param name="SignedPart">The part as it was signed: headers, an empty line, the body - 7-bit, with CRLF line breaks.</param>
        /// <param name="ArmoredSignature">The detached signature over it, ASCII-armored.</param>
        /// <exception cref="ArgumentException">The part is not 7-bit with CRLF, or the signature is none.</exception>
        public static MimeMultipart Signed(ReadOnlyMemory<Byte>  SignedPart,
                                           String                ArmoredSignature)
        {

            CheckSignedPart(SignedPart.Span);

            return new MimeMultipart(
                       "signed",
                       [
                           new MimeRawEntity(SignedPart),
                           new MimeArmored("application/pgp-signature", ArmoredSignature, "signature.asc", "OpenPGP digital signature")
                       ],
                       [
                           ("micalg",    MicAlg(ArmoredSignature)),
                           ("protocol",  "application/pgp-signature")
                       ]
                   );

        }

        #endregion

        #region Encrypted(ArmoredMessage)

        /// <summary>
        /// multipart/encrypted (RFC 3156 §4): the version part, and the ciphertext.
        /// </summary>
        /// <param name="ArmoredMessage">The encrypted message, ASCII-armored.</param>
        /// <exception cref="ArgumentException">It is no encrypted OpenPGP message.</exception>
        public static MimeMultipart Encrypted(String ArmoredMessage)
        {

            CheckEncrypted(ArmoredMessage);

            return new MimeMultipart(
                       "encrypted",
                       [
                           new MimeArmored("application/pgp-encrypted", "Version: 1", null,            "PGP/MIME version identification"),
                           new MimeArmored("application/octet-stream",  ArmoredMessage, "encrypted.asc", "OpenPGP encrypted message", Inline: true)
                       ],
                       [
                           ("protocol", "application/pgp-encrypted")
                       ]
                   );

        }

        #endregion

        #region MicAlg(ArmoredSignature)

        /// <summary>
        /// The micalg of a signature - "pgp-sha256" - from the hash algorithm it was
        /// made with (RFC 3156 §5, RFC 4880 §9.4).
        /// </summary>
        /// <exception cref="ArgumentException">It is no OpenPGP signature, or one made with a hash that has no name there.</exception>
        public static String MicAlg(String ArmoredSignature)
        {

            PgpSignature signature;

            try
            {

                using var input = PgpUtilities.GetDecoderStream(new MemoryStream(Encoding.ASCII.GetBytes(ArmoredSignature)));

                var found = new PgpObjectFactory(input).NextPgpObject();

                if (found is PgpCompressedData compressed)
                    found = new PgpObjectFactory(compressed.GetDataStream()).NextPgpObject();

                signature = found is PgpSignatureList list && list.Count > 0
                                ? list[0]
                                : throw new ArgumentException("That is no OpenPGP signature.");

            }
            catch (Exception e) when (e is IOException or PgpException)
            {
                throw new ArgumentException($"That is no OpenPGP signature: {e.Message}");
            }

            return signature.HashAlgorithm switch {
                       HashAlgorithmTag.Sha256     => "pgp-sha256",
                       HashAlgorithmTag.Sha384     => "pgp-sha384",
                       HashAlgorithmTag.Sha512     => "pgp-sha512",
                       HashAlgorithmTag.Sha224     => "pgp-sha224",
                       HashAlgorithmTag.Sha1       => "pgp-sha1",
                       HashAlgorithmTag.RipeMD160  => "pgp-ripemd160",
                       var other                   => throw new ArgumentException($"A signature made with {other} has no micalg.")
                   };

        }

        #endregion


        #region (private) CheckSignedPart(Octets) / CheckEncrypted(Armored)

        /// <summary>
        /// What is signed has to arrive as it was: 7-bit, lines ending in CRLF and
        /// no longer than 998 octets (RFC 3156 §3, RFC 5322 §2.1.1) - anything else
        /// some relay on the way would change, and the signature with it.
        /// </summary>
        private static void CheckSignedPart(ReadOnlySpan<Byte> Octets)
        {

            if (Octets.Length == 0)
                throw new ArgumentException("There is nothing signed.");

            var line = 0;

            for (var i = 0; i < Octets.Length; i++)
            {

                var octet = Octets[i];

                if (octet > 127 || octet == 0)
                    throw new ArgumentException("What is signed has to be 7-bit: quoted-printable or base64.");

                if (octet == '\n' && (i == 0 || Octets[i - 1] != '\r'))
                    throw new ArgumentException("What is signed has to have CRLF line breaks.");

                if (octet == '\r' && (i + 1 == Octets.Length || Octets[i + 1] != '\n'))
                    throw new ArgumentException("What is signed has to have CRLF line breaks.");

                line = octet is (Byte) '\r' or (Byte) '\n' ? 0 : line + 1;

                if (line > 998)
                    throw new ArgumentException("A line of what is signed is longer than 998 octets.");

            }

        }

        private static void CheckEncrypted(String Armored)
        {

            try
            {

                using var input = PgpUtilities.GetDecoderStream(new MemoryStream(Encoding.ASCII.GetBytes(Armored)));

                if (!Armored.TrimStart().StartsWith("-----BEGIN PGP MESSAGE-----", StringComparison.Ordinal) ||
                    new PgpObjectFactory(input).NextPgpObject() is not PgpEncryptedDataList)
                {
                    throw new ArgumentException("That is no encrypted OpenPGP message.");
                }

            }
            catch (Exception e) when (e is IOException or PgpException)
            {
                throw new ArgumentException($"That is no encrypted OpenPGP message: {e.Message}");
            }

        }

        #endregion


        #region (private) MimeArmored

        /// <summary>
        /// ASCII armor - a signature, a ciphertext, "Version: 1" - as a 7-bit part.
        /// </summary>
        private sealed class MimeArmored(String   ContentType,
                                         String   Text,
                                         String?  FileName,
                                         String   Description,
                                         Boolean  Inline = false) : MimeEntity
        {

            protected override IEnumerable<String> ContentHeaders()
            {

                yield return MimeEncoding.ParameterizedHeader("Content-Type", ContentType, FileName is null ? [] : [ ("name", FileName) ]);
                yield return $"Content-Description: {Description}";

                if (FileName is not null)
                    yield return MimeEncoding.ParameterizedHeader("Content-Disposition", Inline ? "inline" : "attachment", [ ("filename", FileName) ]);

            }

            protected override Byte[] Body()
            {

                if (!MimeEncoding.IsPrintableAscii(Text.Replace("\r", "").Replace("\n", "")))
                    throw new ArgumentException("ASCII armor that is not ASCII is none.");

                var body = MimeEncoding.Lines(Encoding.ASCII.GetBytes(Text.Trim()));

                return [ .. body, (Byte) '\r', (Byte) '\n' ];

            }

        }

        #endregion

    }

}
