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

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Crypto.Parameters;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// An Ed25519 key pair for the <c>ssh-ed25519</c> host-key and public-key signature algorithm
    /// (RFC 8032, RFC 8709). Used to sign the key-exchange hash and to authenticate.
    /// </summary>
    public sealed class Ed25519KeyPair : ISshHostKey
    {

        #region Data

        private readonly Ed25519PrivateKeyParameters  privateKey;

        /// <summary>The length in bytes of an Ed25519 public key / seed (32).</summary>
        public const     Int32                        KeySize        = 32;

        /// <summary>The length in bytes of an Ed25519 signature (64).</summary>
        public const     Int32                        SignatureSize  = 64;

        #endregion

        #region Properties

        /// <summary>The 32-byte public key.</summary>
        public Byte[] PublicKey { get; }

        /// <summary>The 32-byte private seed (for key export).</summary>
        internal Byte[] PrivateSeed => privateKey.GetEncoded();

        #endregion

        #region Constructor(s)

        private Ed25519KeyPair(Ed25519PrivateKeyParameters PrivateKey)
        {
            this.privateKey  = PrivateKey;
            this.PublicKey   = PrivateKey.GeneratePublicKey().GetEncoded();
        }

        #endregion


        #region (static) Generate(Random = null)

        /// <summary>
        /// Generate a fresh, random Ed25519 key pair.
        /// </summary>
        /// <param name="Random">An optional secure random source; a fresh one is used by default.</param>
        public static Ed25519KeyPair Generate(SecureRandom? Random = null)
            => new (new Ed25519PrivateKeyParameters(Random ?? new SecureRandom()));

        #endregion

        #region (static) FromSeed(Seed)

        /// <summary>
        /// Reconstruct an Ed25519 key pair from its 32-byte seed (private key).
        /// </summary>
        /// <param name="Seed">The 32-byte Ed25519 seed.</param>
        public static Ed25519KeyPair FromSeed(ReadOnlySpan<Byte> Seed)
        {

            if (Seed.Length != KeySize)
                throw new ArgumentException($"An Ed25519 seed must be {KeySize} bytes!", nameof(Seed));

            return new (new Ed25519PrivateKeyParameters(Seed.ToArray(), 0));

        }

        #endregion


        #region Sign(Message)

        /// <summary>
        /// Produce a 64-byte Ed25519 signature over the given message.
        /// </summary>
        /// <param name="Message">The message to sign (e.g. the key-exchange hash H).</param>
        public Byte[] Sign(ReadOnlySpan<Byte> Message)
        {

            var signer = new Ed25519Signer();
            signer.Init(true, privateKey);
            signer.BlockUpdate(Message);

            return signer.GenerateSignature();

        }

        #endregion

        #region ISshHostKey

        /// <summary>The signature algorithm this key supports (<c>ssh-ed25519</c>).</summary>
        public IReadOnlyList<String> AlgorithmNames => [ SshAlgorithmNames.HostKey.Ed25519 ];

        /// <summary>The SSH public-key blob (<c>string "ssh-ed25519" || string publickey</c>).</summary>
        public Byte[] PublicKeyBlob => SshEd25519.EncodePublicKeyBlob(PublicKey);

        /// <summary>Sign the data and return the SSH signature blob (<c>string "ssh-ed25519" || string sig</c>).</summary>
        public Byte[] Sign(String AlgorithmName, ReadOnlySpan<Byte> Data)
            => SshEd25519.EncodeSignatureBlob(Sign(Data));

        #endregion

        #region (static) Verify(PublicKey, Message, Signature)

        /// <summary>
        /// Verify an Ed25519 signature over a message against a public key.
        /// </summary>
        /// <param name="PublicKey">The 32-byte Ed25519 public key.</param>
        /// <param name="Message">The signed message.</param>
        /// <param name="Signature">The 64-byte signature.</param>
        /// <returns>True if the signature is valid.</returns>
        public static Boolean Verify(ReadOnlySpan<Byte>  PublicKey,
                                     ReadOnlySpan<Byte>  Message,
                                     ReadOnlySpan<Byte>  Signature)
        {

            if (PublicKey.Length != KeySize || Signature.Length != SignatureSize)
                return false;

            var verifier = new Ed25519Signer();
            verifier.Init(false, new Ed25519PublicKeyParameters(PublicKey.ToArray(), 0));
            verifier.BlockUpdate(Message);

            return verifier.VerifySignature(Signature.ToArray());

        }

        #endregion

    }

}
