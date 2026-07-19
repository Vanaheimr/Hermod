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

using Org.BouncyCastle.Bcpg;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// The outcome of an OpenPGP signature verification.
    /// </summary>
    public enum PgpVerificationStatus
    {

        /// <summary>
        /// The message carried no OpenPGP signature to verify.
        /// </summary>
        NoSignature,

        /// <summary>
        /// A signature was present, but no public key for the signer was supplied,
        /// so it could not be verified either way.
        /// </summary>
        NoMatchingKey,

        /// <summary>
        /// The signature is present and cryptographically valid.
        /// </summary>
        Valid,

        /// <summary>
        /// The signature is present but does not validate (wrong key, tampered content, ...).
        /// </summary>
        Invalid

    }


    /// <summary>
    /// The result of verifying an OpenPGP signature: the status plus, when a signature was
    /// present, the signer's key id and signature metadata.
    /// </summary>
    public class PgpSignatureVerification
    {

        #region Properties

        /// <summary>
        /// The verification outcome.
        /// </summary>
        public PgpVerificationStatus  Status           { get; }

        /// <summary>
        /// The key id of the signer, if a signature was present.
        /// </summary>
        public Int64?                 SignerKeyId      { get; }

        /// <summary>
        /// The hash algorithm used by the signature, if present.
        /// </summary>
        public HashAlgorithmTag?      HashAlgorithm    { get; }

        /// <summary>
        /// The signature's creation timestamp, if present.
        /// </summary>
        public DateTime?              CreationTime     { get; }

        /// <summary>
        /// Whether the signature is present and valid.
        /// </summary>
        public Boolean                IsValid
            => Status == PgpVerificationStatus.Valid;

        /// <summary>
        /// The signer key id in the conventional "0x…" hex notation, or "" if none.
        /// </summary>
        public String                 SignerKeyIdHex
            => SignerKeyId.HasValue
                   ? "0x" + ((UInt64) SignerKeyId.Value).ToString("X16")
                   : "";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new OpenPGP signature verification result.
        /// </summary>
        public PgpSignatureVerification(PgpVerificationStatus  Status,
                                        Int64?                 SignerKeyId     = null,
                                        HashAlgorithmTag?      HashAlgorithm   = null,
                                        DateTime?              CreationTime    = null)
        {
            this.Status         = Status;
            this.SignerKeyId    = SignerKeyId;
            this.HashAlgorithm  = HashAlgorithm;
            this.CreationTime   = CreationTime;
        }

        #endregion

        #region (static) Factory helpers

        /// <summary>
        /// No signature was present.
        /// </summary>
        public static PgpSignatureVerification NoSignature
            => new (PgpVerificationStatus.NoSignature);

        /// <summary>
        /// A signature was present, but no public key for the signer was available.
        /// </summary>
        public static PgpSignatureVerification NoMatchingKey(Int64 SignerKeyId)
            => new (PgpVerificationStatus.NoMatchingKey, SignerKeyId);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => SignerKeyId.HasValue
                   ? $"{Status} (signer {SignerKeyIdHex})"
                   : Status.ToString();

        #endregion

    }

}
