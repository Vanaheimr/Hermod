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

    public static class OpenPGP2
    {

        private static void VerifySignature2(String  fileName,
                                             Stream  inputStream,
                                             Stream  keyIn)
        {

            inputStream = PgpUtilities.GetDecoderStream(inputStream);

            var pgpFact = new PgpObjectFactory(inputStream);
            PgpSignatureList p3 = null;
            var PGPObject = pgpFact.NextPgpObject();

            if (PGPObject is PgpCompressedData c1)
            {
                pgpFact = new PgpObjectFactory(c1.GetDataStream());
                p3      = (PgpSignatureList) pgpFact.NextPgpObject();
            }

            else
                p3 = (PgpSignatureList)PGPObject;

            var pgpPubRingCollection = new PgpPublicKeyRingBundle(PgpUtilities.GetDecoderStream(keyIn));
            Stream dIn = File.OpenRead(fileName);
            var sig = p3[0];
            var key = pgpPubRingCollection.GetPublicKey(sig.KeyId);
            sig.InitVerify(key);

            int ch;
            while ((ch = dIn.ReadByte()) >= 0)
            {
                sig.Update((byte)ch);
            }

            dIn.Close();

            if (sig?.Verify() == true)
                Console.WriteLine("signature verified.");
            else
                Console.WriteLine("signature verification failed.");

        }


        public class res
        {

            #region Properties

            public PgpSignature  Signature    { get; }

            /// <summary>
            /// Verifies the signature.
            /// (Will consume as constant verification time for security reasons!)
            /// </summary>
            public Boolean       IsValid      { get; }

            public PgpPublicKey  PublicKey    { get; }

            #endregion

            #region Constructor(s)

            public res(PgpSignature  Signature,
                       PgpPublicKey  PublicKey)
            {
                this.Signature  = Signature;
                this.PublicKey  = PublicKey;
                this.IsValid    = Signature.Verify();
            }

            #endregion


            public DateTime CreationTime
                => Signature.CreationTime;

            public HashAlgorithmTag HashAlgorithm
                => Signature.HashAlgorithm;

            public PublicKeyAlgorithmTag KeyAlgorithm
                => Signature.KeyAlgorithm;

            public String KeyIdHex
                => "0x" + ((UInt64) Signature.KeyId).ToString("X");

            public UInt64 KeyId
                => (UInt64) Signature.KeyId;

        }

        private static res VerifySignature(String  FileToVerify,
                                           Stream  SignatureInputStream,
                                           Stream  keyIn)
        {

            SignatureInputStream = PgpUtilities.GetDecoderStream(SignatureInputStream);

            var               pgpFact        = new PgpObjectFactory(SignatureInputStream);
            PgpSignatureList  SignatureList  = null;
            var               PGPObject      = pgpFact.NextPgpObject();

            if (PGPObject is PgpCompressedData)
            {
                var c1         = (PgpCompressedData) PGPObject;
                pgpFact        = new PgpObjectFactory(c1.GetDataStream());
                SignatureList  = (PgpSignatureList) pgpFact.NextPgpObject();
            }

            else
                SignatureList  = (PgpSignatureList) PGPObject;

            var pgpPubRingCollection  = new PgpPublicKeyRingBundle(PgpUtilities.GetDecoderStream(keyIn));
            var FileToVerifyStream    = File.OpenRead(FileToVerify);
            var Signature             = SignatureList[0];
            var PublicKey             = pgpPubRingCollection.GetPublicKey(Signature.KeyId);

            Signature.InitVerify(PublicKey);

            int ch;
            while ((ch = FileToVerifyStream.ReadByte()) >= 0)
            {
                Signature.Update((byte) ch);
            }

            FileToVerifyStream.Close();

            return new res(Signature,
                           PublicKey);

        }

    }

}
