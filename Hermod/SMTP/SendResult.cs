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

using System.Text;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// The next hop's answer to one recipient.
    /// </summary>
    /// <param name="Recipient">The recipient address.</param>
    /// <param name="Code">The reply code to its RCPT.</param>
    /// <param name="Text">The reply text.</param>
    public sealed record RecipientOutcome(String  Recipient,
                                          Int32   Code,
                                          String  Text)
    {

        /// <summary>
        /// Accepted (2xx), refused for now (4xx, and anything unparseable), or refused (5xx).
        /// </summary>
        public SendStatus Status
            => Code switch {
                   >= 200 and < 300  => SendStatus.Success,
                   >= 500 and < 600  => SendStatus.PermFail,
                   _                 => SendStatus.TempFail
               };

    }


    public sealed record SendResult(SendStatus  Status,
                                    Int32       ResponseCode,
                                    String      ResponseText,
                                    String?     RemoteMx = null,
                                    TimeSpan?   Duration = null)
    {

        /// <summary>
        /// Whether the receiving server advertised the DSN extension. When true, any requested
        /// success notification is that server's responsibility, so we must not also issue a
        /// "relayed" DSN (RFC 3461 §5.3.1) — avoiding a duplicate.
        /// </summary>
        public Boolean RemoteSupportsDsn { get; init; }

        /// <summary>
        /// The next hop's answer to each recipient's RCPT, when the transaction got that far. A
        /// delivery can succeed for some recipients and fail for others (RFC 5321 §3.3): those
        /// refused are bounced, those refused for now tried again - each on its own.
        /// </summary>
        public IReadOnlyList<RecipientOutcome> Recipients { get; init; } = [];

        public static SendResult Success(String response, String mx, TimeSpan duration) =>
            new (SendStatus.Success, 250, response, mx, duration);

        public static SendResult TempFail(Int32 code, String response, String? mx = null) =>
            new (SendStatus.TempFail, code, response, mx);

        public static SendResult PermFail(Int32 code, String response, String? mx = null) =>
            new (SendStatus.PermFail, code, response, mx);

        public static SendResult TempFail(String error) =>
            new (SendStatus.TempFail, 0, error);

        public static SendResult PermFail(String error) =>
            new (SendStatus.PermFail, 0, error);

    }

}
