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

using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// One signature of a rendezvous control request:
    ///
    ///     signature = {
    ///         1: tstr,      ; the identification of the signing key
    ///         2: int,       ; the COSE signature algorithm
    ///         3: bstr       ; the signature itself
    ///     }
    ///
    /// </summary>
    public sealed class ControlSignature
    {

        #region Data

        /// <summary>
        /// The maximum length of a key identification.
        /// </summary>
        public const Int32 MaxKeyIdLength = 128;

        private const Int64 keyKeyId      = 1;
        private const Int64 keyAlgorithm  = 2;
        private const Int64 keySignature  = 3;

        private readonly Byte[] signature;

        #endregion

        #region Properties

        /// <summary>
        /// The identification of the signing key.
        /// </summary>
        public String              KeyId       { get; }

        /// <summary>
        /// The signature algorithm.
        /// </summary>
        public SignatureAlgorithm  Algorithm   { get; }

        /// <summary>
        /// The signature itself.
        /// </summary>
        public Byte[]              Signature
            => [.. signature];

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new control signature.
        /// </summary>
        /// <param name="KeyId">The identification of the signing key.</param>
        /// <param name="Algorithm">The signature algorithm.</param>
        /// <param name="Signature">The signature itself.</param>
        public ControlSignature(String              KeyId,
                                SignatureAlgorithm  Algorithm,
                                Byte[]              Signature)
        {

            ArgumentException.ThrowIfNullOrWhiteSpace(KeyId);
            ArgumentNullException.ThrowIfNull(Signature);

            this.KeyId      = KeyId;
            this.Algorithm  = Algorithm;
            this.signature  = [.. Signature];

        }

        #endregion


        #region ToCBOR()

        /// <summary>
        /// Return the CBOR representation of this signature.
        /// </summary>
        public CBORValue ToCBOR()

            => CBORValue.FromMap([
                   new (CBORValue.FromInt64(keyKeyId),      CBORValue.FromText (KeyId)),
                   new (CBORValue.FromInt64(keyAlgorithm),  CBORValue.FromInt64((Int64) Algorithm)),
                   new (CBORValue.FromInt64(keySignature),  CBORValue.FromBytes(signature))
               ]);

        #endregion

        #region (static) TryParse(CBOR, out Signature, out ErrorResponse)

        /// <summary>
        /// Try to parse the given CBOR value as a control signature.
        /// </summary>
        /// <param name="CBOR">The CBOR value to be parsed.</param>
        /// <param name="Signature">The parsed control signature.</param>
        /// <param name="ErrorResponse">An error response, when parsing failed.</param>
        public static Boolean TryParse(CBORValue                                  CBOR,
                                       [NotNullWhen(true)]  out ControlSignature?  Signature,
                                       [NotNullWhen(false)] out String?            ErrorResponse)
        {

            Signature = null;

            if (CBOR.Kind != CBORValueKind.Map)
            {
                ErrorResponse = "A signature must be a CBOR map!";
                return false;
            }

            if (!CBOR.ParseMandatoryText(keyKeyId, "key identification", out var keyId, out ErrorResponse))
                return false;

            if (keyId.Length == 0 || keyId.Length > MaxKeyIdLength)
            {
                ErrorResponse = $"A key identification must be 1..{MaxKeyIdLength} characters long!";
                return false;
            }

            if (!CBOR.TryGetValue(CBORValue.FromInt64(keyAlgorithm), out var algorithmCBOR) ||
                algorithmCBOR.Kind is not (CBORValueKind.NegativeInteger or CBORValueKind.UnsignedInteger))
            {
                ErrorResponse = "A signature must contain its algorithm!";
                return false;
            }

            var algorithm = (SignatureAlgorithm) algorithmCBOR.AsInt64();

            if (!algorithm.IsDefined())
            {
                ErrorResponse = $"Unknown signature algorithm: {algorithmCBOR.AsInt64()}!";
                return false;
            }

            if (!CBOR.ParseMandatoryBytes(keySignature, "signature", out var signatureBytes, out ErrorResponse))
                return false;

            // EdDSA covers Ed25519 and Ed448, so it has two valid signature sizes.
            // Which one applies follows from the key, and is checked again there.
            var validSizes = SignatureKeyTypeExtensions.SignatureSizesOf(algorithm).ToArray();

            if (!validSizes.Contains(signatureBytes.Length))
            {
                ErrorResponse = $"A {algorithm} signature must be {String.Join(" or ", validSizes)} bytes long, but is {signatureBytes.Length}!";
                return false;
            }

            Signature      = new ControlSignature(keyId, algorithm, signatureBytes);
            ErrorResponse  = null;

            return true;

        }

        #endregion


        #region ToString()

        /// <summary>
        /// Return a text representation of this signature.
        /// </summary>
        public override String ToString()

            => $"{KeyId} ({Algorithm}, {signature.Length} bytes)";

        #endregion

    }

}
