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

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

#endregion

// SYSLIB5006: ML-DSA (FIPS 204) is still "experimental" within .NET 10 - suppressed
// for this file, as the whole feature is built on it.
#pragma warning disable SYSLIB5006

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// A public key that may authorize rendezvous control commands,
    /// limited to the time span between NotBefore and NotAfter.
    ///
    /// Only the raw public key is kept, and every verification creates its own
    /// algorithm instance: neither the BouncyCastle signers nor the ML-DSA
    /// instances are thread safe, and control connections are served concurrently.
    /// </summary>
    public sealed class ControlKey
    {

        #region Data

        /// <summary>
        /// The Ed448 context of this protocol (RFC 8032), empty for plain Ed448.
        /// Signer and verifier must agree on it, therefore it is fixed here.
        /// </summary>
        internal static readonly Byte[] Ed448Context = [];

        private readonly Byte[] publicKey;

        #endregion

        #region Properties

        /// <summary>
        /// The unique identification of this key, as sent within a signature.
        /// </summary>
        public String              Id                { get; }

        /// <summary>
        /// The type of this key, which also determines its signature size.
        /// </summary>
        public SignatureKeyType    KeyType           { get; }

        /// <summary>
        /// The COSE signature algorithm this key signs with.
        /// A key is bound to its algorithm on purpose: accepting whatever the
        /// sender claims would invite algorithm confusion attacks.
        /// </summary>
        public SignatureAlgorithm  Algorithm
            => KeyType.Algorithm();

        /// <summary>
        /// The raw public key.
        /// </summary>
        public Byte[]              PublicKey
            => [.. publicKey];

        /// <summary>
        /// The key is not valid before this timestamp. Null means "since ever".
        /// </summary>
        public DateTimeOffset?     NotBefore         { get; }

        /// <summary>
        /// The key is not valid after this timestamp. Null means "until further notice".
        /// </summary>
        public DateTimeOffset?     NotAfter          { get; }

        /// <summary>
        /// An optional description, e.g. who owns this key.
        /// </summary>
        public String?             Description       { get; }

        /// <summary>
        /// Whether this key may also close the rendezvous of somebody else.
        ///
        /// Every key may close what it opened itself; this is the override for
        /// the operator who has to clean up after a colleague who is not around
        /// any more, and it should be given to as few keys as possible.
        /// </summary>
        public Boolean             IsAdministrator   { get; }

        /// <summary>
        /// When this key was configured.
        /// </summary>
        public DateTimeOffset      Created           { get; }

        /// <summary>
        /// Who configured this key, if known.
        /// </summary>
        public String?             CreatedBy         { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new control key from a raw public key.
        /// </summary>
        /// <param name="Id">The unique identification of this key.</param>
        /// <param name="KeyType">The type of this key.</param>
        /// <param name="PublicKey">The raw public key.</param>
        /// <param name="NotBefore">An optional timestamp before which this key is not valid.</param>
        /// <param name="NotAfter">An optional timestamp after which this key is not valid.</param>
        /// <param name="Description">An optional description.</param>
        /// <param name="IsAdministrator">Whether this key may also close the rendezvous of somebody else.</param>
        /// <param name="Created">When this key was configured, the current time is used otherwise.</param>
        /// <param name="CreatedBy">Who configured this key.</param>
        /// <exception cref="ArgumentException">When the key does not match its type.</exception>
        public ControlKey(String             Id,
                          SignatureKeyType   KeyType,
                          Byte[]             PublicKey,
                          DateTimeOffset?    NotBefore         = null,
                          DateTimeOffset?    NotAfter          = null,
                          String?            Description       = null,
                          Boolean            IsAdministrator   = false,
                          DateTimeOffset?    Created           = null,
                          String?            CreatedBy         = null)
        {

            ArgumentException.ThrowIfNullOrWhiteSpace(Id);
            ArgumentNullException.ThrowIfNull(PublicKey);

            var expectedLength = KeyType.PublicKeySize();

            if (PublicKey.Length != expectedLength)
                throw new ArgumentException(
                          $"An {KeyType} public key must be {expectedLength} bytes long, but is {PublicKey.Length}!",
                          nameof(PublicKey)
                      );

            if (NotBefore.HasValue && NotAfter.HasValue && NotAfter.Value < NotBefore.Value)
                throw new ArgumentException(
                          $"NotAfter ({NotAfter.Value:u}) must not be before NotBefore ({NotBefore.Value:u})!",
                          nameof(NotAfter)
                      );

            this.Id               = Id;
            this.KeyType          = KeyType;
            this.publicKey        = [.. PublicKey];
            this.NotBefore        = NotBefore;
            this.NotAfter         = NotAfter;
            this.Description      = Description;
            this.IsAdministrator  = IsAdministrator;
            this.Created          = Created ?? DateTimeOffset.UtcNow;
            this.CreatedBy        = CreatedBy;

        }

        #endregion


        #region (static) FromMLDsa(Id, PublicKey, NotBefore = null, NotAfter = null, ...)

        /// <summary>
        /// Create a new control key from the given ML-DSA public key.
        /// </summary>
        /// <param name="Id">The unique identification of this key.</param>
        /// <param name="PublicKey">An ML-DSA key, only its public key is used.</param>
        /// <param name="NotBefore">An optional timestamp before which this key is not valid.</param>
        /// <param name="NotAfter">An optional timestamp after which this key is not valid.</param>
        /// <param name="Description">An optional description.</param>
        /// <param name="IsAdministrator">Whether this key may also close the rendezvous of somebody else.</param>
        /// <param name="Created">When this key was configured, the current time is used otherwise.</param>
        /// <param name="CreatedBy">Who configured this key.</param>
        public static ControlKey FromMLDsa(String            Id,
                                           MLDsa             PublicKey,
                                           DateTimeOffset?   NotBefore         = null,
                                           DateTimeOffset?   NotAfter          = null,
                                           String?           Description       = null,
                                           Boolean           IsAdministrator   = false,
                                           DateTimeOffset?   Created           = null,
                                           String?           CreatedBy         = null)
        {

            ArgumentNullException.ThrowIfNull(PublicKey);

            var keyType = PublicKey.Algorithm.Name switch {
                              "ML-DSA-44"  => SignatureKeyType.MLDsa44,
                              "ML-DSA-65"  => SignatureKeyType.MLDsa65,
                              "ML-DSA-87"  => SignatureKeyType.MLDsa87,
                              _            => throw new ArgumentException(
                                                  $"Unsupported ML-DSA parameter set: {PublicKey.Algorithm.Name}!",
                                                  nameof(PublicKey)
                                              )
                          };

            return new ControlKey(
                       Id,
                       keyType,
                       PublicKey.ExportMLDsaPublicKey(),
                       NotBefore,
                       NotAfter,
                       Description,
                       IsAdministrator,
                       Created,
                       CreatedBy
                   );

        }

        #endregion

        #region (static) IsMLDsaSupported

        /// <summary>
        /// Whether this platform offers ML-DSA. Ed25519 and Ed448 are always
        /// available, as they are implemented within BouncyCastle rather than
        /// by the platform.
        /// </summary>
        public static Boolean IsMLDsaSupported
            => MLDsa.IsSupported;

        #endregion


        #region IsValidAt(Timestamp)

        /// <summary>
        /// Whether this key may be used at the given timestamp.
        /// </summary>
        /// <param name="Timestamp">A timestamp.</param>
        public Boolean IsValidAt(DateTimeOffset Timestamp)

            => (!NotBefore.HasValue || Timestamp >= NotBefore.Value) &&
               (!NotAfter. HasValue || Timestamp <= NotAfter. Value);

        #endregion

        #region Verify(Data, Signature)

        /// <summary>
        /// Whether the given signature of the given data was made by this key.
        /// </summary>
        /// <param name="Data">The signed data.</param>
        /// <param name="Signature">The signature to verify.</param>
        public Boolean Verify(ReadOnlySpan<Byte>  Data,
                              ReadOnlySpan<Byte>  Signature)
        {

            // A wrong signature size can not be a valid signature, and checking
            // it first keeps malformed input away from the algorithms.
            if (Signature.Length != KeyType.SignatureSize())
                return false;

            try
            {

                switch (KeyType)
                {

                    case SignatureKeyType.Ed25519:
                    {
                        var verifier = new Ed25519Signer();
                        verifier.Init(false, new Ed25519PublicKeyParameters(publicKey, 0));
                        verifier.BlockUpdate(Data);
                        return verifier.VerifySignature(Signature.ToArray());
                    }

                    case SignatureKeyType.Ed448:
                    {
                        var verifier = new Ed448Signer(Ed448Context);
                        verifier.Init(false, new Ed448PublicKeyParameters(publicKey, 0));
                        verifier.BlockUpdate(Data);
                        return verifier.VerifySignature(Signature.ToArray());
                    }

                    default:
                    {

                        if (!MLDsa.IsSupported)
                            return false;

                        using var mlDsa = MLDsa.ImportMLDsaPublicKey(
                                              MLDsaAlgorithmOf(KeyType),
                                              publicKey
                                          );

                        return mlDsa.VerifyData(Data, Signature);

                    }

                }

            }
            catch (CryptographicException)
            {
                // A malformed signature is an invalid signature, not an incident.
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

        }

        #endregion

        #region (static) MLDsaAlgorithmOf(KeyType)

        /// <summary>
        /// Return the ML-DSA parameter set of the given key type.
        /// </summary>
        /// <param name="KeyType">A key type.</param>
        public static MLDsaAlgorithm MLDsaAlgorithmOf(SignatureKeyType KeyType)

            => KeyType switch {
                   SignatureKeyType.MLDsa44  => MLDsaAlgorithm.MLDsa44,
                   SignatureKeyType.MLDsa65  => MLDsaAlgorithm.MLDsa65,
                   SignatureKeyType.MLDsa87  => MLDsaAlgorithm.MLDsa87,
                   _                         => throw new ArgumentException($"Not an ML-DSA key type: {KeyType}!", nameof(KeyType))
               };

        #endregion


        #region ToString()

        /// <summary>
        /// Return a text representation of this key.
        /// </summary>
        public override String ToString()

            => $"{Id} ({KeyType}){(IsAdministrator ? ", administrator" : "")}{(NotBefore.HasValue || NotAfter.HasValue ? $", valid {NotBefore?.ToString("u") ?? "..."} - {NotAfter?.ToString("u") ?? "..."}" : "")}{(Description is not null ? $": {Description}" : "")}";

        #endregion

    }

}

#pragma warning restore SYSLIB5006
