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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Why a SIG(0)-signed message was or was not accepted.
    /// </summary>
    /// <remarks>
    /// SIG(0) has no error field of its own — RFC 2931 §3.2 gives a verifier one
    /// bit on the wire, RCODE 9 (NOTAUTH), and nothing to say which of half a
    /// dozen things went wrong. The distinction is still worth keeping on this
    /// side of the wire, because "no KEY at that name" and "the clock is off by
    /// an hour" call for completely different fixes.
    /// </remarks>
    public class SIG0VerificationResult
    {

        #region Properties

        /// <summary>Whether the message is authentic.</summary>
        public Boolean            IsValid       { get; }

        /// <summary>What went wrong, or None.</summary>
        public SIG0Failure        Failure       { get; }

        /// <summary>A description for logs rather than for the wire.</summary>
        public String?            Description   { get; }

        /// <summary>The SIG record that was checked, when there was one.</summary>
        public SIG?               Record        { get; }

        #endregion

        #region Constructor(s)

        private SIG0VerificationResult(Boolean      IsValid,
                                       SIG0Failure  Failure,
                                       String?      Description,
                                       SIG?         Record)
        {

            this.IsValid      = IsValid;
            this.Failure      = Failure;
            this.Description  = Description;
            this.Record       = Record;

        }

        #endregion


        #region (static) Success(Record) / Failed(Failure, Description, Record = null)

        /// <summary>The message is authentic.</summary>
        public static SIG0VerificationResult Success(SIG Record)

            => new (true, SIG0Failure.None, null, Record);


        /// <summary>The message is not authentic.</summary>
        public static SIG0VerificationResult Failed(SIG0Failure  Failure,
                                                    String       Description,
                                                    SIG?         Record   = null)

            => new (false, Failure, Description, Record);

        #endregion


        #region (override) ToString()

        /// <inheritdoc/>
        public override String ToString()

            => IsValid
                   ? "valid"
                   : $"invalid ({Failure}): {Description}";

        #endregion

    }


    /// <summary>
    /// The ways a SIG(0) can fail to authenticate a message.
    /// </summary>
    public enum SIG0Failure
    {

        /// <summary>No failure.</summary>
        None,

        /// <summary>The message carries no SIG(0) as its last additional record.</summary>
        NotSigned,

        /// <summary>The signer's name or key tag does not match the KEY offered to verify with.</summary>
        UnknownKey,

        /// <summary>The signature does not verify under that key.</summary>
        BadSignature,

        /// <summary>The current time is outside the inception/expiration window (RFC 2931 §3.1).</summary>
        OutsideValidityPeriod,

        /// <summary>The algorithm is one this implementation cannot check.</summary>
        UnsupportedAlgorithm,

        /// <summary>The record is not a SIG(0) at all — its "type covered" is not zero.</summary>
        NotATransactionSignature

    }

}
