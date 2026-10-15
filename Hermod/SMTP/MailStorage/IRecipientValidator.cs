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
    /// What an <see cref="IRecipientValidator"/> says about a recipient on a local domain.
    /// </summary>
    public enum RecipientCheck
    {

        /// <summary>
        /// The mailbox exists: RCPT is answered 250.
        /// </summary>
        Accept,

        /// <summary>
        /// There is no such mailbox: RCPT is answered 550 5.1.1 (RFC 3463 "bad destination
        /// mailbox address"), and the sending server bounces the message - instead of this server
        /// accepting it and having nowhere to put it.
        /// </summary>
        Unknown,

        /// <summary>
        /// The question cannot be answered right now: RCPT is answered 451 4.3.0, and the sending
        /// server tries again later.
        /// </summary>
        TryLater

    }


    /// <summary>
    /// Decides at RCPT whether a recipient on one of the local domains exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without one, every address on a local domain is accepted (catch-all), and a message for a
    /// mailbox that does not exist is accepted first and has to be bounced afterwards - to an
    /// envelope sender that is, for spam, somebody else (backscatter). Asking at RCPT lets the
    /// sending server do the bouncing, which is the only place it is harmless.
    /// </para>
    /// <para>
    /// It is asked only for recipients on local domains; a relayed recipient is the relay rules'
    /// business. It is never asked about "postmaster": RFC 5321 §4.5.1 requires that mailbox to
    /// exist, so the storage has to put such mail somewhere whatever it would have said. A
    /// validator that throws counts as <see cref="RecipientCheck.TryLater"/>.
    /// </para>
    /// <para>
    /// A mail store that knows its mailboxes usually implements this beside
    /// <see cref="IMailStorage"/>, and is passed to the <see cref="SMTPServer"/> as both.
    /// </para>
    /// </remarks>
    public interface IRecipientValidator
    {

        /// <summary>
        /// Whether the given recipient on a local domain exists.
        /// </summary>
        /// <param name="recipient">The address from RCPT TO, as the client sent it.</param>
        /// <param name="ct">A cancellation token.</param>
        Task<RecipientCheck> CheckAsync(String             recipient,
                                        CancellationToken  ct = default);

    }

}
