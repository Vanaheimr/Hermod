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
    /// The verdict on a TSIG-signed message.
    /// </summary>
    /// <remarks>
    /// The error codes are the ones RFC 8945 §4.3 puts in the TSIG record's own
    /// Error field, not RCODEs — they are what a responder echoes back so the
    /// sender can tell a wrong key from a wrong clock.
    /// </remarks>
    public class TSIGVerificationResult
    {

        #region Properties

        /// <summary>
        /// Whether the message is authentic.
        /// </summary>
        public Boolean  IsValid       { get; }

        /// <summary>
        /// The TSIG error code, zero when valid.
        /// </summary>
        public UInt16   Error         { get; }

        /// <summary>
        /// Why verification failed, for logs rather than for the wire.
        /// </summary>
        public String?  Description   { get; }

        /// <summary>
        /// The TSIG record that was checked, when there was one to check.
        /// </summary>
        public TSIG?    Record        { get; }

        /// <summary>
        /// The MAC of the verified message — needed to verify the response that
        /// answers it, which folds this value in (RFC 8945 §4.3.1).
        /// </summary>
        public Byte[]?  MAC           { get; }

        #endregion

        #region Constructor(s)

        private TSIGVerificationResult(Boolean  IsValid,
                                       UInt16   Error,
                                       String?  Description,
                                       TSIG?    Record)
        {

            this.IsValid      = IsValid;
            this.Error        = Error;
            this.Description  = Description;
            this.Record       = Record;
            this.MAC          = Record?.MAC;

        }

        #endregion


        #region (static) Success(Record)

        /// <summary>
        /// The message is authentic.
        /// </summary>
        /// <param name="Record">The TSIG record that verified.</param>
        public static TSIGVerificationResult Success(TSIG Record)

            => new (true, 0, null, Record);

        #endregion

        #region (static) Failed (Error, Description, Record = null)

        /// <summary>
        /// The message is not authentic.
        /// </summary>
        /// <param name="Error">The TSIG error code to report.</param>
        /// <param name="Description">Why it failed.</param>
        /// <param name="Record">The offending TSIG record, when one was parsed.</param>
        public static TSIGVerificationResult Failed(UInt16   Error,
                                                    String   Description,
                                                    TSIG?    Record   = null)

            => new (false, Error, Description, Record);

        #endregion


        #region (override) ToString()

        /// <inheritdoc/>
        public override String ToString()

            => IsValid
                   ? "valid"
                   : $"invalid ({Error}): {Description}";

        #endregion

    }

}
