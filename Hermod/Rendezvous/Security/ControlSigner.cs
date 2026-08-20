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

using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

#endregion

// SYSLIB5006: ML-DSA (FIPS 204) is still "experimental" within .NET 10 - suppressed
// for this file, as the whole feature is built on it.
#pragma warning disable SYSLIB5006

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// A private key signing rendezvous control requests.
    ///
    /// The raw private key is kept and an algorithm instance is created per
    /// signature: the underlying signers are not thread safe, and a client may
    /// well sign from several threads. Dispose zeroes the private key.
    /// </summary>
    public sealed class ControlSigner : IDisposable
    {

        #region Data

        private readonly Byte[]   privateKey;
        private readonly Byte[]   publicKey;
        private          Boolean  disposed;

        #endregion

        #region Properties

        /// <summary>
        /// The unique identification of this key, sent along with every signature.
        /// </summary>
        public String            KeyId      { get; }

        /// <summary>
        /// The type of this key.
        /// </summary>
        public SignatureKeyType  KeyType    { get; }

        /// <summary>
        /// The raw public key.
        /// </summary>
        public Byte[]            PublicKey
            => [.. publicKey];

        #endregion

        #region Constructor(s)

        private ControlSigner(String            KeyId,
                              SignatureKeyType  KeyType,
                              Byte[]            PrivateKey,
                              Byte[]            PublicKey)
        {

            this.KeyId       = KeyId;
            this.KeyType     = KeyType;
            this.privateKey  = PrivateKey;
            this.publicKey   = PublicKey;

        }

        #endregion


        #region (static) GenerateEd25519(KeyId)

        /// <summary>
        /// Generate a new Ed25519 signer.
        /// </summary>
        /// <param name="KeyId">The unique identification of the new key.</param>
        public static ControlSigner GenerateEd25519(String KeyId)
        {

            ArgumentException.ThrowIfNullOrWhiteSpace(KeyId);

            var generator = new Ed25519KeyPairGenerator();
            generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));

            var keyPair = generator.GenerateKeyPair();

            return new ControlSigner(
                       KeyId,
                       SignatureKeyType.Ed25519,
                       ((Ed25519PrivateKeyParameters) keyPair.Private).GetEncoded(),
                       ((Ed25519PublicKeyParameters)  keyPair.Public). GetEncoded()
                   );

        }

        #endregion

        #region (static) GenerateEd448  (KeyId)

        /// <summary>
        /// Generate a new Ed448 signer.
        /// </summary>
        /// <param name="KeyId">The unique identification of the new key.</param>
        public static ControlSigner GenerateEd448(String KeyId)
        {

            ArgumentException.ThrowIfNullOrWhiteSpace(KeyId);

            var generator = new Ed448KeyPairGenerator();
            generator.Init(new Ed448KeyGenerationParameters(new SecureRandom()));

            var keyPair = generator.GenerateKeyPair();

            return new ControlSigner(
                       KeyId,
                       SignatureKeyType.Ed448,
                       ((Ed448PrivateKeyParameters) keyPair.Private).GetEncoded(),
                       ((Ed448PublicKeyParameters)  keyPair.Public). GetEncoded()
                   );

        }

        #endregion

        #region (static) GenerateMLDsa  (KeyId, KeyType)

        /// <summary>
        /// Generate a new ML-DSA signer.
        /// </summary>
        /// <param name="KeyId">The unique identification of the new key.</param>
        /// <param name="KeyType">An ML-DSA key type.</param>
        /// <exception cref="PlatformNotSupportedException">When this platform does not offer ML-DSA.</exception>
        public static ControlSigner GenerateMLDsa(String            KeyId,
                                                  SignatureKeyType  KeyType = SignatureKeyType.MLDsa65)
        {

            ArgumentException.ThrowIfNullOrWhiteSpace(KeyId);

            if (!MLDsa.IsSupported)
                throw new PlatformNotSupportedException("This platform does not offer ML-DSA!");

            using var mlDsa = MLDsa.GenerateKey(ControlKey.MLDsaAlgorithmOf(KeyType));

            return new ControlSigner(
                       KeyId,
                       KeyType,
                       mlDsa.ExportMLDsaPrivateKey(),
                       mlDsa.ExportMLDsaPublicKey()
                   );

        }

        #endregion

        #region (static) Create         (KeyId, KeyType, PrivateKey, PublicKey)

        /// <summary>
        /// Create a signer from an existing raw private key.
        /// </summary>
        /// <param name="KeyId">The unique identification of the key.</param>
        /// <param name="KeyType">The type of the key.</param>
        /// <param name="PrivateKey">The raw private key.</param>
        /// <param name="PublicKey">The raw public key.</param>
        public static ControlSigner Create(String            KeyId,
                                           SignatureKeyType  KeyType,
                                           Byte[]            PrivateKey,
                                           Byte[]            PublicKey)
        {

            ArgumentException.ThrowIfNullOrWhiteSpace(KeyId);
            ArgumentNullException.ThrowIfNull(PrivateKey);
            ArgumentNullException.ThrowIfNull(PublicKey);

            if (PublicKey.Length != KeyType.PublicKeySize())
                throw new ArgumentException(
                          $"An {KeyType} public key must be {KeyType.PublicKeySize()} bytes long, but is {PublicKey.Length}!",
                          nameof(PublicKey)
                      );

            return new ControlSigner(KeyId, KeyType, [.. PrivateKey], [.. PublicKey]);

        }

        #endregion


        #region Sign(Data)

        /// <summary>
        /// Sign the given data.
        /// </summary>
        /// <param name="Data">The data to sign.</param>
        public Byte[] Sign(ReadOnlySpan<Byte> Data)
        {

            ObjectDisposedException.ThrowIf(disposed, this);

            switch (KeyType)
            {

                case SignatureKeyType.Ed25519:
                {
                    var signer = new Ed25519Signer();
                    signer.Init(true, new Ed25519PrivateKeyParameters(privateKey, 0));
                    signer.BlockUpdate(Data);
                    return signer.GenerateSignature();
                }

                case SignatureKeyType.Ed448:
                {
                    var signer = new Ed448Signer(ControlKey.Ed448Context);
                    signer.Init(true, new Ed448PrivateKeyParameters(privateKey, 0));
                    signer.BlockUpdate(Data);
                    return signer.GenerateSignature();
                }

                default:
                {

                    if (!MLDsa.IsSupported)
                        throw new PlatformNotSupportedException("This platform does not offer ML-DSA!");

                    using var mlDsa = MLDsa.ImportMLDsaPrivateKey(
                                          ControlKey.MLDsaAlgorithmOf(KeyType),
                                          privateKey
                                      );

                    var signature = new Byte[KeyType.SignatureSize()];
                    mlDsa.SignData(Data, signature);

                    return signature;

                }

            }

        }

        #endregion

        #region SignatureFor(Data)

        /// <summary>
        /// Create a control signature of the given data.
        /// </summary>
        /// <param name="Data">The data to sign.</param>
        public ControlSignature SignatureFor(ReadOnlySpan<Byte> Data)

            => new (KeyId,
                    KeyType.Algorithm(),
                    Sign(Data));

        #endregion

        #region ToControlKey(NotBefore = null, NotAfter = null, ...)

        /// <summary>
        /// Return the public control key belonging to this signer,
        /// ready to be added to the key ring of a control endpoint.
        /// </summary>
        /// <param name="NotBefore">An optional timestamp before which the key is not valid.</param>
        /// <param name="NotAfter">An optional timestamp after which the key is not valid.</param>
        /// <param name="Description">An optional description.</param>
        /// <param name="IsAdministrator">Whether this key may also close the rendezvous of somebody else.</param>
        /// <param name="Created">When this key was configured, the current time is used otherwise.</param>
        /// <param name="CreatedBy">Who configured this key.</param>
        public ControlKey ToControlKey(DateTimeOffset?  NotBefore         = null,
                                       DateTimeOffset?  NotAfter          = null,
                                       String?          Description       = null,
                                       Boolean          IsAdministrator   = false,
                                       DateTimeOffset?  Created           = null,
                                       String?          CreatedBy         = null)

            => new (KeyId,
                    KeyType,
                    publicKey,
                    NotBefore,
                    NotAfter,
                    Description,
                    IsAdministrator,
                    Created,
                    CreatedBy);

        #endregion


        #region Dispose()

        /// <summary>
        /// Zero the private key.
        /// </summary>
        public void Dispose()
        {

            if (disposed)
                return;

            CryptographicOperations.ZeroMemory(privateKey);

            disposed = true;

            GC.SuppressFinalize(this);

        }

        #endregion

        #region ToString()

        /// <summary>
        /// Return a text representation of this signer.
        /// </summary>
        public override String ToString()

            => $"{KeyId} ({KeyType})";

        #endregion

    }

}

#pragma warning restore SYSLIB5006
