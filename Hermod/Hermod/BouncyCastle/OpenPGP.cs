/*
 * Copyright (c) 2014-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr BouncyCastle <http://www.github.com/Vanaheimr/BouncyCastle>
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

namespace org.GraphDefined.Vanaheimr.BouncyCastle
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
            if (InputStream == null)
                throw new ArgumentException("The given text input is not valid PGP/GPG data!");

            PgpPublicKeyRing PublicKeyRing = null;

            var PublicKeyRingBundle  = new PgpPublicKeyRingBundle(InputStream);
            if (PublicKeyRingBundle != null)
                PublicKeyRing = PublicKeyRingBundle.GetKeyRings().Cast<PgpPublicKeyRing>().First();

            else
                PublicKeyRing = new PgpPublicKeyRing(InputStream);

            if (PublicKeyRing == null)
                throw new ArgumentException("The given text input does not contain a valid PGP/GPG public key ring!");

            return PublicKeyRing;

        }

        #endregion

        #region TryReadPublicKeyRing(Text, out PublicKeyRing)

        public static Boolean TryReadPublicKeyRing(String Text, out PgpPublicKeyRing PublicKeyRing)
        {

            PublicKeyRing = null;

            try
            {

                var InputStream = PgpUtilities.GetDecoderStream(Text.ToMemoryStream());
                if (InputStream == null)
                    return false;

                var PublicKeyRingBundle = new PgpPublicKeyRingBundle(InputStream);
                if (PublicKeyRingBundle != null)
                    PublicKeyRing = PublicKeyRingBundle.GetKeyRings().Cast<PgpPublicKeyRing>().First();

                else
                    PublicKeyRing = new PgpPublicKeyRing(InputStream);

                return PublicKeyRing != null;

            }
            catch (Exception e)
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

        #region WriteTo<T>(this Signature, OutputStream, ArmoredOutput = true, CloseOutputStream = true)

        public static T WriteTo<T>(this PgpSignature  Signature,
                                   T                  OutputStream,
                                   Boolean            ArmoredOutput      = true,
                                   Boolean            CloseOutputStream  = true)

            where T : Stream

        {

            #region Open/create output streams

            BcpgOutputStream WrappedOutputStream = null;

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
                SignatureSubpacketGenerator.SetSignerUserId(isCritical: false, userId: UserId);
                SignatureGenerator.SetHashedSubpackets(SignatureSubpacketGenerator.Generate());
                break; //wtf?
            }

            return SignatureGenerator;

        }

        #endregion

        #region (internal) EncryptWith(this InputStream, PublicKey, SymmetricKeyAlgorithm, BufferSize = 0x10000)

        internal static Stream EncryptWith(this Stream               InputStream,
                                           PgpPublicKey              PublicKey,
                                           SymmetricKeyAlgorithmTag  SymmetricKeyAlgorithm,
                                           UInt32                    BufferSize = 0x10000)
        {

            var EncryptedDataGenerator = new PgpEncryptedDataGenerator(SymmetricKeyAlgorithm, true, new SecureRandom());
            EncryptedDataGenerator.AddMethod(PublicKey);

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
        internal static Stream LiteralOutputPipe(String    Name,
                                                 UInt64    Length,
                                                 DateTime  ModificationTime,
                                                 Stream    OutputStream)
        {

            var LiteralDataGenerator = new PgpLiteralDataGenerator();

            return LiteralDataGenerator.Open(OutputStream,
                                             PgpLiteralData.Binary,
                                             Name,
                                             (Int64) Length,
                                             ModificationTime);

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

        #region EncryptSignAndZip(InputStream, Length, SecretKey, Passphrase, PublicKey, OutputStream, SymmetricKeyAlgorithm, HashAlgorithm, CompressionAlgorithm, ArmoredOutput, Filename, LastModificationTime)

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
                                             DateTime?                 LastModificationTime    = null)
        {

            #region Initial checks

            if (InputStream == null)
                throw new ArgumentNullException("The Input stream must not be null!");

            if (SecretKey == null)
                throw new ArgumentNullException("The secret key must not be null!");

            if (Passphrase == null)
                throw new ArgumentNullException("The pass phrase must not be null!");

            if (PublicKey == null)
                throw new ArgumentNullException("The public key must not be null!");

            if (OutputStream == null)
                throw new ArgumentNullException("The output stream must not be null!");

            #endregion

            var InternalOutputStream = ArmoredOutput ? new ArmoredOutputStream(OutputStream) : OutputStream;

            using (var EncryptionPipe          = InternalOutputStream.EncryptWith(PublicKey, SymmetricKeyAlgorithm))
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
