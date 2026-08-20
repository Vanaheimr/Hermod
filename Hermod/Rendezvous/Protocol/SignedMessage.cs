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
    /// A signed message of the rendezvous control protocol - the same envelope
    /// in both directions, for requests as well as for responses:
    ///
    ///     message = {
    ///         1: bstr,          ; the payload, signed exactly as these bytes
    ///         2: [+ signature]  ; one or more signatures
    ///     }
    ///
    /// The payload travels as a byte string, exactly like within COSE: the
    /// signature covers the bytes that were received, so no re-encoding of the
    /// parsed structure can ever change what was verified.
    /// </summary>
    public sealed class SignedMessage
    {

        #region Data

        private const Int64 keyPayload     = 1;
        private const Int64 keySignatures  = 2;

        /// <summary>
        /// The maximum number of signatures of one message.
        /// </summary>
        public const Int32 MaxSignatures = 8;

        private readonly Byte[] payload;

        #endregion

        #region Properties

        /// <summary>
        /// The signed payload.
        /// </summary>
        public Byte[]                            Payload
            => [.. payload];

        /// <summary>
        /// The signatures of the payload.
        /// </summary>
        public IReadOnlyList<ControlSignature>   Signatures   { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new signed message.
        /// </summary>
        /// <param name="Payload">The signed payload.</param>
        /// <param name="Signatures">The signatures of the payload.</param>
        public SignedMessage(Byte[]                           Payload,
                             IEnumerable<ControlSignature>    Signatures)
        {

            ArgumentNullException.ThrowIfNull(Payload);
            ArgumentNullException.ThrowIfNull(Signatures);

            this.payload     = [.. Payload];
            this.Signatures  = [.. Signatures];

        }

        #endregion


        #region (static) Create(Payload, Signers)

        /// <summary>
        /// Sign the given payload with all given signers.
        /// </summary>
        /// <param name="Payload">The payload to sign.</param>
        /// <param name="Signers">One or more signers.</param>
        public static SignedMessage Create(Byte[]                    Payload,
                                           params ControlSigner[]    Signers)
        {

            ArgumentNullException.ThrowIfNull(Payload);

            if (Signers is null || Signers.Length == 0)
                throw new ArgumentException("At least one signer is required!", nameof(Signers));

            if (Signers.Length > MaxSignatures)
                throw new ArgumentException($"A message must not have more than {MaxSignatures} signatures!", nameof(Signers));

            return new SignedMessage(
                       Payload,
                       Signers.Select(signer => signer.SignatureFor(Payload))
                   );

        }

        #endregion

        #region ToCBOR() / ToByteArray()

        /// <summary>
        /// Return the CBOR representation of this signed message.
        /// </summary>
        public CBORValue ToCBOR()

            => CBORValue.FromMap([
                   new (CBORValue.FromInt64(keyPayload),     CBORValue.FromBytes(payload)),
                   new (CBORValue.FromInt64(keySignatures),  CBORValue.FromArray(Signatures.Select(signature => signature.ToCBOR())))
               ]);

        /// <summary>
        /// Return the deterministically encoded CBOR data of this signed message.
        /// </summary>
        public Byte[] ToByteArray()

            => ToCBOR().ToByteArray(CBORWriterOptions.Canonical);

        #endregion


        #region (static) TryParse(Bytes, out Message, out ErrorResponse)

        /// <summary>
        /// Try to parse the given CBOR data as a signed message.
        /// </summary>
        /// <param name="Bytes">The CBOR data to be parsed.</param>
        /// <param name="Message">The parsed signed message.</param>
        /// <param name="ErrorResponse">An error response, when parsing failed.</param>
        public static Boolean TryParse(ReadOnlySpan<Byte>                       Bytes,
                                       [NotNullWhen(true)]  out SignedMessage?  Message,
                                       [NotNullWhen(false)] out String?         ErrorResponse)
        {

            Message = null;

            if (!CBORValue.TryParse(Bytes, out var cbor, out ErrorResponse))
                return false;

            if (cbor.Kind != CBORValueKind.Map)
            {
                ErrorResponse = "A signed message must be a CBOR map!";
                return false;
            }

            if (!cbor.ParseMandatoryBytes(keyPayload, "payload", out var payload, out ErrorResponse))
                return false;

            if (payload.Length == 0)
            {
                ErrorResponse = "The payload must not be empty!";
                return false;
            }

            if (!cbor.TryGetValue(CBORValue.FromInt64(keySignatures), out var signaturesCBOR) ||
                signaturesCBOR.Kind != CBORValueKind.Array)
            {
                ErrorResponse = "A signed message must contain an array of signatures!";
                return false;
            }

            var signatureValues = signaturesCBOR.AsArray();

            if (signatureValues.Count == 0)
            {
                ErrorResponse = "A signed message must carry at least one signature!";
                return false;
            }

            if (signatureValues.Count > MaxSignatures)
            {
                ErrorResponse = $"A message must not have more than {MaxSignatures} signatures, but has {signatureValues.Count}!";
                return false;
            }

            var signatures = new List<ControlSignature>(signatureValues.Count);

            foreach (var signatureValue in signatureValues)
            {

                if (!ControlSignature.TryParse(signatureValue, out var signature, out ErrorResponse))
                    return false;

                signatures.Add(signature);

            }

            Message        = new SignedMessage(payload, signatures);
            ErrorResponse  = null;

            return true;

        }

        #endregion

        #region TryVerify(KeyRing, Timestamp, RequiredSignatures, out VerifiedBy, out ErrorResponse)

        /// <summary>
        /// Verify the signatures of this message against the given key ring.
        ///
        /// Every signature must belong to a known key that is valid at the given
        /// timestamp, and the required number of *distinct* keys must have signed:
        /// signing twice with the same key does not make a message twice as trusted.
        /// </summary>
        /// <param name="KeyRing">The known keys.</param>
        /// <param name="Timestamp">The timestamp to check the key validity against.</param>
        /// <param name="RequiredSignatures">How many distinct valid signatures are required.</param>
        /// <param name="VerifiedBy">The keys that really signed this message.</param>
        /// <param name="ErrorResponse">An error response, when the verification failed.</param>
        public Boolean TryVerify(ControlKeyRing                             KeyRing,
                                 DateTimeOffset                            Timestamp,
                                 Int32                                     RequiredSignatures,
                                 out IReadOnlyList<ControlKey>             VerifiedBy,
                                 [NotNullWhen(false)] out String?          ErrorResponse)
        {

            ArgumentNullException.ThrowIfNull(KeyRing);

            var verifiedKeys = new List<ControlKey>();

            foreach (var signature in Signatures)
            {

                if (!KeyRing.TryGet(signature.KeyId, out var key))
                    continue;

                if (!key.IsValidAt(Timestamp))
                    continue;

                // The key knows which algorithm it signs with - a sender claiming
                // a different one does not get to choose.
                if (key.Algorithm != signature.Algorithm)
                    continue;

                if (verifiedKeys.Any(verifiedKey => verifiedKey.Id == key.Id))
                    continue;

                if (key.Verify(payload, signature.Signature))
                    verifiedKeys.Add(key);

            }

            VerifiedBy = verifiedKeys;

            if (verifiedKeys.Count < RequiredSignatures)
            {

                ErrorResponse = verifiedKeys.Count == 0
                                    ? "None of the signatures could be verified!"
                                    : $"Only {verifiedKeys.Count} of {RequiredSignatures} required signatures could be verified!";

                return false;

            }

            ErrorResponse = null;
            return true;

        }

        #endregion


        #region ToString()

        /// <summary>
        /// Return a text representation of this signed message.
        /// </summary>
        public override String ToString()

            => $"{payload.Length} bytes, signed by {String.Join(", ", Signatures.Select(signature => signature.KeyId))}";

        #endregion

    }

}
