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

using System;
using System.IO;
using System.Linq;
using System.Text;

using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Security;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public static class OpenPGP
    {

        #region KeyManagement

        #region ToMemoryStream(this Text)

        public static MemoryStream ToMemoryStream(this String Text)
        {

            var OutputStream = new MemoryStream();
            var Bytes = Encoding.UTF8.GetBytes(Text);
            OutputStream.Write(Bytes, 0, Bytes.Length);
            OutputStream.Seek(0, SeekOrigin.Begin);

            return OutputStream;

        }

        #endregion


        #region ReadPublicKeyRingBundle(Text)

        public static PgpPublicKeyRingBundle ReadPublicKeyRingBundle(String Text)
            => new PgpPublicKeyRingBundle(PgpUtilities.GetDecoderStream(Text.ToMemoryStream()));

        #endregion

        #region ReadPublicKeyRingBundle(InputStream)

        public static PgpPublicKeyRingBundle ReadPublicKeyRingBundle(Stream InputStream)
            => new PgpPublicKeyRingBundle(PgpUtilities.GetDecoderStream(InputStream));

        #endregion

        #region ReadPublicKeyRing(Text)

        public static PgpPublicKeyRing ReadPublicKeyRing(String Text)
        {

            var InputStream = PgpUtilities.GetDecoderStream(Text.ToMemoryStream());
            if (InputStream is null)
                throw new ArgumentException("The given text input is not valid PGP/GPG data!");

            PgpPublicKeyRing? PublicKeyRing = null;

            var PublicKeyRingBundle  = new PgpPublicKeyRingBundle(InputStream);
            if (PublicKeyRingBundle is not null)
                PublicKeyRing = PublicKeyRingBundle.GetKeyRings().Cast<PgpPublicKeyRing>().First();

            else
                PublicKeyRing = new PgpPublicKeyRing(InputStream);

            if (PublicKeyRing is null)
                throw new ArgumentException("The given text input does not contain a valid PGP/GPG public key ring!");

            return PublicKeyRing;

        }

        #endregion

        #region TryReadPublicKeyRing(Text, out PublicKeyRing)

        public static Boolean TryReadPublicKeyRing(String Text, out PgpPublicKeyRing? PublicKeyRing)
        {

            PublicKeyRing = null;

            try
            {

                var InputStream = PgpUtilities.GetDecoderStream(Text.ToMemoryStream());
                if (InputStream is null)
                    return false;

                var PublicKeyRingBundle = new PgpPublicKeyRingBundle(InputStream);
                if (PublicKeyRingBundle is not null)
                    PublicKeyRing = PublicKeyRingBundle.GetKeyRings().Cast<PgpPublicKeyRing>().First();

                else
                    PublicKeyRing = new PgpPublicKeyRing(InputStream);

                return PublicKeyRing is not null;

            }
            catch
            {
                return false;
            }

        }

        #endregion

        #region ReadPublicKeyRing(ByteArray)

        public static PgpPublicKeyRing? ReadPublicKeyRing(Byte[] ByteArray)
            => ReadPublicKeyRing(new MemoryStream(ByteArray));

        #endregion

        #region ReadPublicKeyRing(InputStream)

        public static PgpPublicKeyRing? ReadPublicKeyRing(Stream InputStream)
            => new PgpPublicKeyRingBundle(PgpUtilities.GetDecoderStream(InputStream)).GetKeyRings().Cast<PgpPublicKeyRing>().FirstOrDefault();

        #endregion

        #region ReadPublicKey(Text)

        public static PgpPublicKey? ReadPublicKey(String Text)
            => ReadPublicKeyRing(Text.ToMemoryStream())?.GetPublicKeys().Cast<PgpPublicKey>().First();

        #endregion

        #region ReadPublicKey(InputStream)

        public static PgpPublicKey? ReadPublicKey(Stream InputStream)
            => ReadPublicKeyRing(InputStream)?.GetPublicKeys().Cast<PgpPublicKey>().First();

        #endregion


        #region ReadSecretKeyRingBundle(Text)

        public static PgpSecretKeyRingBundle ReadSecretKeyRingBundle(String Text)
            => new PgpSecretKeyRingBundle(PgpUtilities.GetDecoderStream(Text.ToMemoryStream()));

        #endregion

        #region ReadSecretKeyRingBundle(InputStream)

        public static PgpSecretKeyRingBundle ReadSecretKeyRingBundle(Stream InputStream)
            => new PgpSecretKeyRingBundle(PgpUtilities.GetDecoderStream(InputStream));

        #endregion

        #region ReadSecretKeyRing(Text)

        public static PgpSecretKeyRing ReadSecretKeyRing(String Text)
            => new PgpSecretKeyRing(Text.ToMemoryStream());

        #endregion

        #region ReadSecretKeyRing(ByteArray)

        public static PgpSecretKeyRing? ReadSecretKeyRing(Byte[] ByteArray)
            => ReadSecretKeyRing(new MemoryStream(ByteArray));

        #endregion

        #region ReadSecretKeyRing(InputStream)

        public static PgpSecretKeyRing? ReadSecretKeyRing(Stream InputStream)
        {

            var SecretKeyRingBundle = new PgpSecretKeyRingBundle(PgpUtilities.GetDecoderStream(InputStream));

            // we just loop through the collection till we find a key suitable for encryption, in the real
            // world you would probably want to be a bit smarter about this.
            foreach (var SecretKeyRing in SecretKeyRingBundle.GetKeyRings().Cast<PgpSecretKeyRing>())
            {
                foreach (var SecretKey in SecretKeyRing?.GetSecretKeys().Cast<PgpSecretKey>() ?? Array.Empty<PgpSecretKey>())
                {
                    if (SecretKey?.IsSigningKey == true)
                        return SecretKeyRing;
                }
            }

            throw new ArgumentException("Can't find signing key in key ring.");

        }

        #endregion

        #region ReadSecretKey(Text)

        public static PgpSecretKey? ReadSecretKey(String Text)
            => ReadSecretKeyRing(Text.ToMemoryStream())?.GetSecretKeys().Cast<PgpSecretKey>().First();

        #endregion

        #region ReadSecretKey(InputStream)

        public static PgpSecretKey? ReadSecretKey(Stream InputStream)
            => ReadSecretKeyRing(InputStream)?.GetSecretKeys().Cast<PgpSecretKey>().First();

        #endregion

        #endregion


        #region CreateSignature(InputStream, SecretKey, Passphrase, HashAlgorithm = HashAlgorithms.Sha512, BufferSize = 2 MByte)

        public static PgpSignature CreateSignature(Stream            InputStream,
                                                   PgpSecretKey      SecretKey,
                                                   String            Passphrase,
                                                   HashAlgorithmTag  HashAlgorithm      = HashAlgorithmTag.Sha512,
                                                   UInt32            BufferSize         = 2*1024*1024) // Bytes
        {

            #region Init signature generator

            var SignatureGenerator  = new PgpSignatureGenerator(SecretKey.PublicKey.Algorithm,
                                                                HashAlgorithm);

            SignatureGenerator.InitSign(PgpSignature.BinaryDocument,
                                        SecretKey.ExtractPrivateKey(Passphrase.ToCharArray()));

            #endregion

            #region Read input and update the signature generator

            var InputBuffer  = new Byte[BufferSize];
            var read         = 0;

            do
            {

                read = InputStream.Read(InputBuffer, 0, InputBuffer.Length);
                SignatureGenerator.Update(InputBuffer, 0, read);

            } while (read == BufferSize);

            InputStream.Close();

            #endregion

            return SignatureGenerator.Generate();

        }

        #endregion

        #region VerifyDetachedSignature(SignedData, ArmoredSignature, PublicKeys)

        /// <summary>
        /// Verify a detached OpenPGP signature (as used by multipart/signed, RFC 3156) over the given
        /// signed data, using the signer's public key looked up from <paramref name="PublicKeys"/>.
        /// </summary>
        /// <param name="SignedData">The exact bytes that were signed.</param>
        /// <param name="ArmoredSignature">The armored (or binary) detached signature stream.</param>
        /// <param name="PublicKeys">A bundle of candidate signer public keys.</param>
        public static PgpSignatureVerification VerifyDetachedSignature(Byte[]                  SignedData,
                                                                       Stream                  ArmoredSignature,
                                                                       PgpPublicKeyRingBundle  PublicKeys)
        {

            using var decoder = PgpUtilities.GetDecoderStream(ArmoredSignature);

            var factory   = new PgpObjectFactory(decoder);
            var pgpObject = factory.NextPgpObject();

            // Signatures may be wrapped in a compressed-data packet.
            if (pgpObject is PgpCompressedData compressed)
            {
                factory   = new PgpObjectFactory(compressed.GetDataStream());
                pgpObject = factory.NextPgpObject();
            }

            if (pgpObject is not PgpSignatureList signatureList || signatureList.Count == 0)
                return PgpSignatureVerification.NoSignature;

            var signature = signatureList[0];
            var publicKey = PublicKeys.GetPublicKey(signature.KeyId);

            if (publicKey is null)
                return PgpSignatureVerification.NoMatchingKey(signature.KeyId);

            signature.InitVerify(publicKey);
            signature.Update(SignedData);

            return new PgpSignatureVerification(
                       signature.Verify()
                           ? PgpVerificationStatus.Valid
                           : PgpVerificationStatus.Invalid,
                       signature.KeyId,
                       signature.HashAlgorithm,
                       signature.CreationTime
                   );

        }

        #endregion

        #region Decrypt(ArmoredCiphertext, SecretKeys, Passphrase)

        /// <summary>
        /// Decrypt an OpenPGP message (as produced by <see cref="EncryptSignAndZip(Stream, UInt64, PgpSecretKey, String, IEnumerable{PgpPublicKey}, Stream, SymmetricKeyAlgorithmTag, HashAlgorithmTag, CompressionAlgorithmTag, Boolean, String, DateTimeOffset?)"/>)
        /// using whichever recipient secret key from <paramref name="SecretKeys"/> the message was
        /// encrypted to. Returns the recovered plaintext bytes.
        /// </summary>
        /// <param name="ArmoredCiphertext">The armored (or binary) OpenPGP message.</param>
        /// <param name="SecretKeys">The recipient's secret key ring bundle.</param>
        /// <param name="Passphrase">The passphrase protecting the matching secret key.</param>
        public static Byte[] Decrypt(Stream                  ArmoredCiphertext,
                                     PgpSecretKeyRingBundle  SecretKeys,
                                     String                  Passphrase)

            => DecryptAndVerify(ArmoredCiphertext, SecretKeys, Passphrase, null).Plaintext;

        #endregion

        #region DecryptAndVerify(ArmoredCiphertext, SecretKeys, Passphrase, SenderPublicKeys)

        /// <summary>
        /// Decrypt an OpenPGP message (as produced by <see cref="EncryptSignAndZip(Stream, UInt64, PgpSecretKey, String, IEnumerable{PgpPublicKey}, Stream, SymmetricKeyAlgorithmTag, HashAlgorithmTag, CompressionAlgorithmTag, Boolean, String, DateTimeOffset?)"/>,
        /// which signs as well as encrypts) and, in the same pass, verify the embedded one-pass signature
        /// against <paramref name="SenderPublicKeys"/>. Returns the recovered plaintext together with the
        /// signature verification result.
        /// </summary>
        /// <param name="ArmoredCiphertext">The armored (or binary) OpenPGP message.</param>
        /// <param name="SecretKeys">The recipient's secret key ring bundle.</param>
        /// <param name="Passphrase">The passphrase protecting the matching secret key.</param>
        /// <param name="SenderPublicKeys">Candidate signer public keys; null to skip signature verification.</param>
        public static (Byte[] Plaintext, PgpSignatureVerification Signature) DecryptAndVerify(Stream                   ArmoredCiphertext,
                                                                                              PgpSecretKeyRingBundle   SecretKeys,
                                                                                              String                   Passphrase,
                                                                                              PgpPublicKeyRingBundle?  SenderPublicKeys)
        {

            using var decoder = PgpUtilities.GetDecoderStream(ArmoredCiphertext);

            var factory   = new PgpObjectFactory(decoder);
            var pgpObject = factory.NextPgpObject();

            // Skip a leading PGP marker packet, if any.
            var encryptedList = pgpObject as PgpEncryptedDataList
                                    ?? factory.NextPgpObject() as PgpEncryptedDataList;

            if (encryptedList is null)
                throw new PgpException("The message does not begin with an encrypted-data list!");

            // Find the public-key encrypted session-key packet we hold a secret key for.
            PgpPublicKeyEncryptedData?  encrypted   = null;
            PgpPrivateKey?              privateKey  = null;

            foreach (PgpPublicKeyEncryptedData candidate in encryptedList.GetEncryptedDataObjects())
            {
                var secretKey = SecretKeys.GetSecretKey(candidate.KeyId);
                if (secretKey is not null)
                {
                    privateKey  = secretKey.ExtractPrivateKey(Passphrase.ToCharArray());
                    encrypted   = candidate;
                    break;
                }
            }

            if (encrypted is null || privateKey is null)
                throw new PgpException("No matching secret key for any of the message's recipients!");

            var clearFactory = new PgpObjectFactory(encrypted.GetDataStream(privateKey));
            var message      = clearFactory.NextPgpObject();

            // The payload is compressed (EncryptSignAndZip zips it).
            if (message is PgpCompressedData compressed)
            {
                clearFactory = new PgpObjectFactory(compressed.GetDataStream());
                message      = clearFactory.NextPgpObject();
            }

            // A one-pass signature precedes the literal data (the message is signed as well as encrypted).
            // Initialise it for verification if the signer's public key was supplied.
            PgpOnePassSignature?  onePass      = null;
            Int64?                signerKeyId  = null;

            if (message is PgpOnePassSignatureList onePassList && onePassList.Count > 0)
            {

                var candidate = onePassList[0];
                signerKeyId   = candidate.KeyId;

                var signerKey = SenderPublicKeys?.GetPublicKey(candidate.KeyId);
                if (signerKey is not null)
                {
                    candidate.InitVerify(signerKey);
                    onePass = candidate;
                }

                message = clearFactory.NextPgpObject();

            }

            if (message is not PgpLiteralData literal)
                throw new PgpException("The decrypted message did not contain literal data!");

            using var literalStream = literal.GetInputStream();
            using var output        = new MemoryStream();

            // Copy the plaintext, feeding the one-pass signature (if we are verifying) as we go.
            var buffer = new Byte[0x10000];
            int read;
            while ((read = literalStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                onePass?.Update(buffer, 0, read);
            }

            var plaintext = output.ToArray();

            // Determine the signature verification result.
            PgpSignatureVerification signature;

            if (signerKeyId is null)
                signature = PgpSignatureVerification.NoSignature;

            else if (onePass is null)
                signature = PgpSignatureVerification.NoMatchingKey(signerKeyId.Value);

            else
            {

                var signatureTrailer = clearFactory.NextPgpObject() as PgpSignatureList;

                signature = signatureTrailer is null || signatureTrailer.Count == 0
                                ? PgpSignatureVerification.NoSignature
                                : new PgpSignatureVerification(
                                      onePass.Verify(signatureTrailer[0])
                                          ? PgpVerificationStatus.Valid
                                          : PgpVerificationStatus.Invalid,
                                      signatureTrailer[0].KeyId,
                                      signatureTrailer[0].HashAlgorithm,
                                      signatureTrailer[0].CreationTime
                                  );

            }

            return (plaintext, signature);

        }

        #endregion

        #region WriteTo<T>(this Signature, OutputStream, ArmoredOutput = true, CloseOutputStream = true)

        public static T WriteTo<T>(this PgpSignature  Signature,
                                   T                  OutputStream,
                                   Boolean            ArmoredOutput      = true,
                                   Boolean            CloseOutputStream  = true)

            where T : Stream

        {

            #region Open/create output streams

            BcpgOutputStream? WrappedOutputStream = null;

            if (ArmoredOutput)
                WrappedOutputStream = new BcpgOutputStream(new ArmoredOutputStream(OutputStream));
            else
                WrappedOutputStream = new BcpgOutputStream(OutputStream);

            #endregion

            Signature.Encode(WrappedOutputStream);

            #region Close streams, if requested

            WrappedOutputStream.Flush();
            WrappedOutputStream.Close();

            // ArmoredOutputStream will not close the underlying stream!
            if (ArmoredOutput)
                OutputStream.Flush();

            if (CloseOutputStream)
                OutputStream.Close();

            #endregion

            return OutputStream;

        }

        #endregion


        #region (internal) CreateSignatureGenerator(SecretKey, Passphrase, HashAlgorithm)

        internal static PgpSignatureGenerator CreateSignatureGenerator(PgpSecretKey      SecretKey,
                                                                       String            Passphrase,
                                                                       HashAlgorithmTag  HashAlgorithm)
        {

            var SignatureGenerator = new PgpSignatureGenerator(SecretKey.PublicKey.Algorithm, HashAlgorithm);
            SignatureGenerator.InitSign(PgpSignature.CanonicalTextDocument, SecretKey.ExtractPrivateKey(Passphrase.ToCharArray()));

            foreach (var UserId in SecretKey.PublicKey.GetUserIds().Cast<String>())
            {
                var SignatureSubpacketGenerator = new PgpSignatureSubpacketGenerator();
                SignatureSubpacketGenerator.AddSignerUserId(isCritical: false, userId: UserId);
                SignatureGenerator.SetHashedSubpackets(SignatureSubpacketGenerator.Generate());
                break; //wtf?
            }

            return SignatureGenerator;

        }

        #endregion

        #region (internal) EncryptWith(this InputStream, PublicKey,  SymmetricKeyAlgorithm, BufferSize = 0x10000)

        internal static Stream EncryptWith(this Stream               InputStream,
                                           PgpPublicKey              PublicKey,
                                           SymmetricKeyAlgorithmTag  SymmetricKeyAlgorithm,
                                           UInt32                    BufferSize = 0x10000)

            => InputStream.EncryptWith([ PublicKey ], SymmetricKeyAlgorithm, BufferSize);

        #endregion

        #region (internal) EncryptWith(this InputStream, PublicKeys, SymmetricKeyAlgorithm, BufferSize = 0x10000)

        internal static Stream EncryptWith(this Stream                InputStream,
                                           IEnumerable<PgpPublicKey>  PublicKeys,
                                           SymmetricKeyAlgorithmTag   SymmetricKeyAlgorithm,
                                           UInt32                     BufferSize = 0x10000)
        {

            var EncryptedDataGenerator = new PgpEncryptedDataGenerator(SymmetricKeyAlgorithm, true, new SecureRandom());

            // Add one public-key encrypted session-key packet per recipient, so any single
            // recipient's private key can recover the (shared) symmetric session key.
            foreach (var publicKey in PublicKeys)
                EncryptedDataGenerator.AddMethod(publicKey);

            return EncryptedDataGenerator.Open(InputStream, new Byte[BufferSize]);

        }

        #endregion

        #region (internal) CompressWith(this InputStream, CompressionAlgorithm)

        internal static Stream CompressWith(this Stream InputStream, CompressionAlgorithmTag CompressionAlgorithm)
        {

            var CompressedDataGenerator = new PgpCompressedDataGenerator(CompressionAlgorithm);

            return CompressedDataGenerator.Open(InputStream);

        }

        #endregion

        #region (internal) LiteralOutputPipe(Name, Length, ModificationTime, OutputStream)

        /// <summary>
        /// Returns a stream to write your data into.
        /// </summary>
        internal static Stream LiteralOutputPipe(String          Name,
                                                 UInt64          Length,
                                                 DateTimeOffset  ModificationTime,
                                                 Stream          OutputStream)
        {

            var LiteralDataGenerator = new PgpLiteralDataGenerator();

            return LiteralDataGenerator.Open(OutputStream,
                                             PgpLiteralData.Binary,
                                             Name,
                                             (Int64) Length,
                                             ModificationTime.DateTime);

        }

        #endregion

        #region (internal) CopyAndHash(this InputStream, LiteralOutputStream, SignatureGenerator, OutputStream, BufferSize = 0x10000)

        /// <summary>
        /// Will read the input stream and copy it to the literal output stream and updates the signature generator.
        /// </summary>
        /// <param name="InputStream">A data source.</param>
        /// <param name="LiteralOutputStream">A literal output stream.</param>
        /// <param name="SignatureGenerator">A signature generator.</param>
        /// <param name="BufferSize">An optional buffer size.</param>
        internal static void CopyAndHash(this Stream            InputStream,
                                         Stream                 LiteralOutputStream,
                                         PgpSignatureGenerator  SignatureGenerator,
                                         UInt32                 BufferSize = 0x10000)
        {

            var Read    = 0;
            var Buffer  = new Byte[BufferSize];

            while ((Read = InputStream.Read(Buffer, 0, Buffer.Length)) > 0)
            {
                LiteralOutputStream.Write (Buffer, 0, Read);
                SignatureGenerator. Update(Buffer, 0, Read);
            }

        }

        #endregion

        #region EncryptSignAndZip(InputStream, Length, SecretKey, Passphrase, PublicKey,  OutputStream, ...)

        public static void EncryptSignAndZip(Stream                    InputStream,
                                             UInt64                    Length,
                                             PgpSecretKey              SecretKey,
                                             String                    Passphrase,
                                             PgpPublicKey              PublicKey,
                                             Stream                    OutputStream,
                                             SymmetricKeyAlgorithmTag  SymmetricKeyAlgorithm   = SymmetricKeyAlgorithmTag.Aes256,
                                             HashAlgorithmTag          HashAlgorithm           = HashAlgorithmTag.Sha512,
                                             CompressionAlgorithmTag   CompressionAlgorithm    = CompressionAlgorithmTag.Zip,
                                             Boolean                   ArmoredOutput           = true,
                                             String                    Filename                = "encrypted.asc",
                                             DateTimeOffset?           LastModificationTime    = null)

            => EncryptSignAndZip(InputStream,
                                 Length,
                                 SecretKey,
                                 Passphrase,
                                 [ PublicKey ],
                                 OutputStream,
                                 SymmetricKeyAlgorithm,
                                 HashAlgorithm,
                                 CompressionAlgorithm,
                                 ArmoredOutput,
                                 Filename,
                                 LastModificationTime);

        #endregion

        #region EncryptSignAndZip(InputStream, Length, SecretKey, Passphrase, PublicKeys, OutputStream, ...)

        /// <summary>
        /// Sign the input with <paramref name="SecretKey"/> and encrypt it to every recipient in
        /// <paramref name="PublicKeys"/> (one public-key encrypted session-key packet per recipient,
        /// so any of them can decrypt the same message).
        /// </summary>
        public static void EncryptSignAndZip(Stream                     InputStream,
                                             UInt64                     Length,
                                             PgpSecretKey               SecretKey,
                                             String                     Passphrase,
                                             IEnumerable<PgpPublicKey>  PublicKeys,
                                             Stream                     OutputStream,
                                             SymmetricKeyAlgorithmTag   SymmetricKeyAlgorithm   = SymmetricKeyAlgorithmTag.Aes256,
                                             HashAlgorithmTag           HashAlgorithm           = HashAlgorithmTag.Sha512,
                                             CompressionAlgorithmTag    CompressionAlgorithm    = CompressionAlgorithmTag.Zip,
                                             Boolean                    ArmoredOutput           = true,
                                             String                     Filename                = "encrypted.asc",
                                             DateTimeOffset?            LastModificationTime    = null)
        {

            #region Initial checks

            if (InputStream is null)
                throw new ArgumentNullException("The Input stream must not be null!");

            if (SecretKey is null)
                throw new ArgumentNullException("The secret key must not be null!");

            if (Passphrase is null)
                throw new ArgumentNullException("The pass phrase must not be null!");

            if (PublicKeys is null)
                throw new ArgumentNullException("The public keys must not be null!");

            var publicKeys = PublicKeys.ToList();

            if (publicKeys.Count == 0)
                throw new ArgumentException("At least one recipient public key is required!", nameof(PublicKeys));

            if (OutputStream is null)
                throw new ArgumentNullException("The output stream must not be null!");

            #endregion

            var InternalOutputStream = ArmoredOutput ? new ArmoredOutputStream(OutputStream) : OutputStream;

            using (var EncryptionPipe          = InternalOutputStream.EncryptWith(publicKeys, SymmetricKeyAlgorithm))
            using (var CompressAndEncryptPipe  = EncryptionPipe.CompressWith(CompressionAlgorithm))
            {

                // Create signature generator...
                var SignatureGenerator = CreateSignatureGenerator(SecretKey,
                                                                  Passphrase,
                                                                  HashAlgorithm);

                // ...and write signature infos to the output stream
                SignatureGenerator.
                    GenerateOnePassVersion(isNested: false).
                    Encode(CompressAndEncryptPipe);


                using (var _LiteralOutputPipe = LiteralOutputPipe(Filename,
                                                                  Length,
                                                                  LastModificationTime ?? Illias.Timestamp.Now,
                                                                  CompressAndEncryptPipe))
                {

                    // Read plaintext and copy&hash it...
                    InputStream.CopyAndHash(_LiteralOutputPipe,
                                             SignatureGenerator);

                    // Write the generated signature to the output stream
                    SignatureGenerator.
                        Generate().
                        Encode(CompressAndEncryptPipe);

                }

            }

            InternalOutputStream.Flush();
            InternalOutputStream.Close();

        }

        #endregion

        #region EncryptSignAndZip(InputFile, SecretKey, Passphrase, PublicKey, OutputFile, ...)

        public static void EncryptSignAndZip(FileInfo                  InputFile,
                                             PgpSecretKey              SecretKey,
                                             String                    Passphrase,
                                             PgpPublicKey              PublicKey,
                                             FileInfo                  OutputFile,
                                             SymmetricKeyAlgorithmTag  SymmetricKeyAlgorithm   = SymmetricKeyAlgorithmTag.Aes256,
                                             HashAlgorithmTag          HashAlgorithm           = HashAlgorithmTag.Sha512,
                                             CompressionAlgorithmTag   CompressionAlgorithm    = CompressionAlgorithmTag.Zip,
                                             Boolean                   ArmoredOutput           = true)
        {

            EncryptSignAndZip(InputFile.OpenRead(),
                              (UInt64) InputFile.Length,
                              SecretKey,
                              Passphrase,
                              PublicKey,
                              OutputFile.OpenWrite(),
                              SymmetricKeyAlgorithm,
                              HashAlgorithm,
                              CompressionAlgorithm,
                              ArmoredOutput,
                              InputFile.Name,
                              InputFile.LastWriteTime);

        }

        #endregion

    }

}
