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

using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// The per-recipient outcome of an SMTP transaction: the server's reply to the
    /// <c>RCPT TO</c> for a single address, with the raw status code, the reply text and the
    /// parsed RFC 3463 enhanced status code (e.g. "2.1.5").
    /// </summary>
    /// <param name="Address">The recipient address this reply is for.</param>
    /// <param name="StatusCode">The SMTP status code the server returned for this recipient.</param>
    /// <param name="Response">The SMTP reply text (without the status code).</param>
    /// <param name="EnhancedStatusCode">The RFC 3463 enhanced status code (e.g. "2.1.5"), if the server sent one.</param>
    public class SMTPRecipientResult(SimpleEMailAddress  Address,
                                     SMTPStatusCodes     StatusCode,
                                     String              Response             = "",
                                     String?             EnhancedStatusCode   = null)
    {

        #region Properties

        /// <summary>
        /// The recipient address this reply is for.
        /// </summary>
        public SimpleEMailAddress  Address               { get; } = Address;

        /// <summary>
        /// The SMTP status code the server returned for this recipient.
        /// </summary>
        public SMTPStatusCodes     StatusCode            { get; } = StatusCode;

        /// <summary>
        /// The SMTP reply text (without the status code).
        /// </summary>
        public String              Response              { get; } = Response;

        /// <summary>
        /// The RFC 3463 enhanced status code (e.g. "2.1.5"), if the server sent one.
        /// </summary>
        public String?             EnhancedStatusCode    { get; } = EnhancedStatusCode;

        /// <summary>
        /// Whether the server accepted this recipient (a 250 or a 251 "will forward").
        /// </summary>
        public Boolean             Accepted
            => StatusCode is SMTPStatusCodes.Ok
                          or SMTPStatusCodes.UserNotLocalWillForward;

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{Address} => {(Int32) StatusCode} {(EnhancedStatusCode is not null ? EnhancedStatusCode + " " : "")}{Response}";

        #endregion

    }

}
