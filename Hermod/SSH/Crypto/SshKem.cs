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

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Pqc.Crypto.NtruPrime;

using BclMLKem          = System.Security.Cryptography.MLKem;
using BclMLKemAlgorithm = System.Security.Cryptography.MLKemAlgorithm;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A key-encapsulation mechanism (KEM) used as the post-quantum half of a hybrid key exchange. The
    /// client generates an ephemeral key pair and sends its public key; the server encapsulates against
    /// it, producing a ciphertext and a shared secret; the client decapsulates the ciphertext to recover
    /// the same secret. ML-KEM-768 uses the .NET BCL; sntrup761 uses BouncyCastle (no BCL support).
    /// </summary>
    public abstract class SshKem
    {

        /// <summary>
        /// The KEM name (for diagnostics).
        /// </summary>
        public abstract String  Name              { get; }

        /// <summary>
        /// The encoded length of a public (encapsulation) key.
        /// </summary>
        public abstract Int32   PublicKeyLength    { get; }

        /// <summary>
        /// The encoded length of a ciphertext.
        /// </summary>
        public abstract Int32   CiphertextLength   { get; }

        /// <summary>
        /// Client side: generate a fresh ephemeral key pair to receive an encapsulation into.
        /// </summary>
        public abstract SshKemKeyPair GenerateKeyPair();

        /// <summary>
        /// Server side: encapsulate against a client's public key, yielding a ciphertext and the shared secret.
        /// </summary>
        public abstract (Byte[] Ciphertext, Byte[] SharedSecret) Encapsulate(ReadOnlySpan<Byte> PublicKey);


        #region (static) MlKem768() / SNtruP761()

        /// <summary>
        /// ML-KEM-768 (FIPS 203), via the .NET BCL.
        /// </summary>
        public static SshKem MlKem768()
            => new MlKem768Kem();

        /// <summary>
        /// Streamlined NTRU Prime sntrup761, via BouncyCastle.
        /// </summary>
        public static SshKem SNtruP761()
            => new SNtruPrime761Kem();

        #endregion

    }


    /// <summary>
    /// A client-side ephemeral KEM key pair: its public key is sent to the server, and its private key
    /// decapsulates the server's ciphertext back to the shared secret.
    /// </summary>
    public abstract class SshKemKeyPair : IDisposable
    {

        /// <summary>
        /// The encoded public (encapsulation) key to send to the server.
        /// </summary>
        public abstract Byte[] PublicKey { get; }

        /// <summary>
        /// Recover the shared secret from the server's ciphertext.
        /// </summary>
        public abstract Byte[] Decapsulate(ReadOnlySpan<Byte> Ciphertext);

        /// <summary>
        /// Release any key material.
        /// </summary>
        public virtual void Dispose()
            => GC.SuppressFinalize(this);

    }


    #region ML-KEM-768 (BCL)

    /// <summary>
    /// ML-KEM-768 (FIPS 203) via <see cref="System.Security.Cryptography.MLKem"/>.
    /// </summary>
    internal sealed class MlKem768Kem : SshKem
    {

        public override String  Name              => "ML-KEM-768";
        public override Int32   PublicKeyLength    => 1184;
        public override Int32   CiphertextLength   => 1088;

        public override SshKemKeyPair GenerateKeyPair()
            => new MlKem768KeyPair(BclMLKem.GenerateKey(BclMLKemAlgorithm.MLKem768));

        public override (Byte[] Ciphertext, Byte[] SharedSecret) Encapsulate(ReadOnlySpan<Byte> PublicKey)
        {
            using var peer = BclMLKem.ImportEncapsulationKey(BclMLKemAlgorithm.MLKem768, PublicKey);
            peer.Encapsulate(out var ciphertext, out var sharedSecret);
            return (ciphertext, sharedSecret);
        }

    }

    internal sealed class MlKem768KeyPair : SshKemKeyPair
    {

        private readonly BclMLKem key;

        public override Byte[] PublicKey { get; }

        public MlKem768KeyPair(BclMLKem Key)
        {
            this.key        = Key;
            this.PublicKey  = Key.ExportEncapsulationKey();
        }

        public override Byte[] Decapsulate(ReadOnlySpan<Byte> Ciphertext)
            => key.Decapsulate(Ciphertext.ToArray());

        public override void Dispose()
        {
            key.Dispose();
            base.Dispose();
        }

    }

    #endregion

    #region sntrup761 (BouncyCastle)

    /// <summary>
    /// Streamlined NTRU Prime sntrup761 via BouncyCastle's <c>SNtruPrime</c> KEM.
    /// </summary>
    internal sealed class SNtruPrime761Kem : SshKem
    {

        public override String  Name              => "sntrup761";
        public override Int32   PublicKeyLength    => 1158;
        public override Int32   CiphertextLength   => 1039;

        public override SshKemKeyPair GenerateKeyPair()
        {

            var generator = new SNtruPrimeKeyPairGenerator();
            generator.Init(new SNtruPrimeKeyGenerationParameters(new SecureRandom(), SNtruPrimeParameters.sntrup761));

            var keyPair = generator.GenerateKeyPair();

            return new SNtruPrime761KeyPair((SNtruPrimePublicKeyParameters)  keyPair.Public,
                                            (SNtruPrimePrivateKeyParameters) keyPair.Private);

        }

        public override (Byte[] Ciphertext, Byte[] SharedSecret) Encapsulate(ReadOnlySpan<Byte> PublicKey)
        {

            var publicKey  = new SNtruPrimePublicKeyParameters(SNtruPrimeParameters.sntrup761, PublicKey.ToArray());
            var generator  = new SNtruPrimeKemGenerator(new SecureRandom());
            var secret     = generator.GenerateEncapsulated(publicKey);

            return (secret.GetEncapsulation(), secret.GetSecret());

        }

    }

    internal sealed class SNtruPrime761KeyPair : SshKemKeyPair
    {

        private readonly SNtruPrimePrivateKeyParameters privateKey;

        public override Byte[] PublicKey { get; }

        public SNtruPrime761KeyPair(SNtruPrimePublicKeyParameters PublicKey, SNtruPrimePrivateKeyParameters PrivateKey)
        {
            this.privateKey  = PrivateKey;
            this.PublicKey   = PublicKey.GetEncoded();
        }

        public override Byte[] Decapsulate(ReadOnlySpan<Byte> Ciphertext)
        {
            var extractor = new SNtruPrimeKemExtractor(privateKey);
            return extractor.ExtractSecret(Ciphertext.ToArray());
        }

    }

    #endregion

}
