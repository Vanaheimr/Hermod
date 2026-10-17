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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server
{

    /// <summary>
    /// A reply that refuses a step of a mail transaction: the reply code and the text after
    /// it, with its enhanced status code - 550 and "5.7.1 ...", say.
    /// </summary>
    public sealed record SMTPRefusal(Int32   Code,
                                     String  Text);


    /// <summary>
    /// What takes the mail of a client that signed in (RFC 6409 message submission): the
    /// whole message, all its recipients - here and elsewhere - and the account. With one,
    /// the server neither stores such a message nor queues it itself: the application
    /// decides which addresses the account may send from, signs, sends, keeps a copy.
    /// </summary>
    public interface IMailSubmission
    {

        /// <summary>
        /// Whether the signed-in account may send with this envelope sender - asked at MAIL
        /// FROM, so that a mail program hears at once. Null: it may.
        /// </summary>
        /// <param name="User">Whom the client signed in as - as the user store accepted it.</param>
        /// <param name="EnvelopeFrom">The reverse-path, empty for a null sender.</param>
        Task<SMTPRefusal?> CheckSenderAsync(String             User,
                                            String             EnvelopeFrom,
                                            CancellationToken  CancellationToken   = default);

        /// <summary>
        /// Take a message the signed-in account submitted. Null: taken - the client is told
        /// 250 - else the refusal is its answer. An exception is a temporary failure (451).
        /// </summary>
        /// <param name="Message">The message, with this server's Received: on top.</param>
        /// <param name="User">Whom the client signed in as.</param>
        /// <param name="EnvelopeFrom">The reverse-path.</param>
        /// <param name="EnvelopeTo">Every recipient taken at RCPT, in their order.</param>
        Task<SMTPRefusal?> SubmitAsync(EMailMessage           Message,
                                       String                 User,
                                       String                 EnvelopeFrom,
                                       IReadOnlyList<String>  EnvelopeTo,
                                       CancellationToken      CancellationToken   = default);

    }

}
