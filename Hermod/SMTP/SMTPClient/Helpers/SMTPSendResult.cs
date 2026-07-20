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

using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// The detailed outcome of an SMTP submission: the coarse <see cref="MailSentStatus"/> as before,
    /// plus the final server reply (status code, text and the parsed RFC 3463 enhanced status code), the
    /// per-recipient results, and transaction metadata (attempts, TLS/authentication state, runtime).
    ///
    /// Implicitly converts to <see cref="MailSentStatus"/>, so existing call sites that only look at the
    /// coarse status keep working unchanged.
    /// </summary>
    public partial class SMTPSendResult
    {

        #region (static) EnhancedStatusCode regex

        // RFC 3463: an enhanced status code is "class.subject.detail" (e.g. "2.1.5"), appearing as the
        // leading token of the reply text when the server advertised ENHANCEDSTATUSCODES.
        [GeneratedRegex(@"^([245]\.\d{1,3}\.\d{1,3})(\s|$)")]
        private static partial Regex EnhancedStatusCodeRegex();

        /// <summary>
        /// Extract the RFC 3463 enhanced status code (e.g. "2.1.5") from the leading token of an SMTP
        /// reply text, or null if there is none.
        /// </summary>
        public static String? ExtractEnhancedStatusCode(String? Response)
        {

            if (Response is null)
                return null;

            var match = EnhancedStatusCodeRegex().Match(Response.TrimStart());

            return match.Success
                       ? match.Groups[1].Value
                       : null;

        }

        #endregion

        #region Properties

        /// <summary>
        /// The coarse send status (backwards compatible with the previous return type).
        /// </summary>
        public MailSentStatus                     Status                { get; }

        /// <summary>
        /// The SMTP status code of the final relevant server reply (the end-of-DATA acknowledgement on
        /// success, or the reply that caused the failure), if the transaction reached the server.
        /// </summary>
        public SMTPStatusCodes?                   StatusCode            { get; }

        /// <summary>
        /// The text of the final relevant server reply, if any.
        /// </summary>
        public String?                            Response              { get; }

        /// <summary>
        /// The RFC 3463 enhanced status code of the final relevant server reply (e.g. "2.0.0"), if any.
        /// </summary>
        public String?                            EnhancedStatusCode    { get; }

        /// <summary>
        /// The per-recipient results (the server's reply to each RCPT TO).
        /// </summary>
        public IReadOnlyList<SMTPRecipientResult> Recipients            { get; }

        /// <summary>
        /// The number of send attempts that were made (1 = succeeded/failed on the first try).
        /// </summary>
        public Byte                               Attempts              { get; }

        /// <summary>
        /// Whether the connection was TLS-secured at the time the message was submitted.
        /// </summary>
        public Boolean                            TLSActive             { get; }

        /// <summary>
        /// Whether an SMTP authentication (AUTH) succeeded during this transaction.
        /// </summary>
        public Boolean                            Authenticated         { get; }

        /// <summary>
        /// The total runtime of the send (including retries and events).
        /// </summary>
        public TimeSpan                           Runtime               { get; }

        /// <summary>
        /// The event tracking identification of the send.
        /// </summary>
        public EventTracking_Id                   EventTrackingId       { get; }

        /// <summary>
        /// Whether the message was successfully submitted.
        /// </summary>
        public Boolean                            IsSuccess
            => Status == MailSentStatus.ok;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new detailed SMTP send result.
        /// </summary>
        public SMTPSendResult(MailSentStatus                      Status,
                              EventTracking_Id                    EventTrackingId,
                              SMTPStatusCodes?                    StatusCode     = null,
                              String?                             Response       = null,
                              IEnumerable<SMTPRecipientResult>?   Recipients     = null,
                              Byte                                Attempts       = 1,
                              Boolean                             TLSActive      = false,
                              Boolean                             Authenticated  = false,
                              TimeSpan?                           Runtime        = null)
        {

            this.Status              = Status;
            this.EventTrackingId     = EventTrackingId;
            this.StatusCode          = StatusCode;
            this.Response            = Response;
            this.EnhancedStatusCode  = ExtractEnhancedStatusCode(Response);
            this.Recipients          = Recipients?.ToArray() ?? [];
            this.Attempts            = Attempts;
            this.TLSActive           = TLSActive;
            this.Authenticated       = Authenticated;
            this.Runtime             = Runtime ?? TimeSpan.Zero;

        }

        #endregion


        #region Operator overloading: implicit -> MailSentStatus

        /// <summary>
        /// Implicitly reduce the detailed result to its coarse <see cref="MailSentStatus"/>, so existing
        /// call sites remain source-compatible.
        /// </summary>
        public static implicit operator MailSentStatus(SMTPSendResult Result)
            => Result.Status;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   Status,

                   StatusCode.HasValue
                       ? $" ({(Int32) StatusCode.Value}{(EnhancedStatusCode is not null ? " " + EnhancedStatusCode : "")}{(Response.IsNotNullOrEmpty() ? " " + Response : "")})"
                       : "",

                   Recipients.Count > 0
                       ? $", {Recipients.Count(r => r.Accepted)}/{Recipients.Count} recipients accepted"
                       : "",

                   Attempts > 1
                       ? $", {Attempts} attempts"
                       : ""

               );

        #endregion

    }

}
