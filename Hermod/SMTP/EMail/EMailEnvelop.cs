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

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// An e-mail envelop: the SMTP transaction around a message — MAIL FROM, RCPT TO, and the
    /// transaction ("envelope") parameters that travel on those commands but are NOT part of the
    /// RFC 5322 message itself (they have no header representation and are invisible to the
    /// recipient's mail client): DSN requests (RFC 3461), the MT-PRIORITY transport priority
    /// (RFC 6710) and REQUIRETLS (RFC 8689).
    /// </summary>
    public class EMailEnvelop
    {

        #region Properties

        /// <summary>
        /// The remote socket of the incoming SMTP connection.
        /// </summary>
        public IPSocket?         RemoteSocket    { get; }

        /// <summary>
        /// The sender(s) of the e-mail.
        /// </summary>
        public EMailAddressList  MailFrom        { get; }

        /// <summary>
        /// The receiver(s) of the e-mail.
        /// </summary>
        public EMailAddressList  RcptTo          { get; }

        /// <summary>
        /// The embedded e-mail.
        /// </summary>
        public EMail             Mail            { get; }


        /// <summary>
        /// The delivery status notifications requested for this transaction (RFC 3461):
        /// NOTIFY/RET/ENVID, emitted on MAIL FROM / RCPT TO when the receiving server supports DSN.
        /// </summary>
        public DsnParameters     Dsn             { get; init; } = DsnParameters.None;

        /// <summary>
        /// The SMTP transport priority (MT-PRIORITY, RFC 6710): -9..9, default 0, higher is more
        /// urgent. Orders the outbound queue and is passed to the next hop when it supports the
        /// extension. Distinct from the message's header-level <see cref="EMail.Importance"/>.
        /// </summary>
        public SByte             Priority        { get; init; } = 0;

        /// <summary>
        /// Demand authenticated TLS for this transaction (REQUIRETLS, RFC 8689): defer or fail
        /// instead of ever downgrading to cleartext.
        /// </summary>
        public Boolean           RequireTls      { get; init; } = false;

        #endregion

        #region Constructor(s)

        #region EMailEnvelop(MailBuilder, EventTrackingId = null)

        /// <summary>
        /// Create a new e-mail envelop based on the given
        /// e-mail builder data.
        /// </summary>
        /// <param name="MailBuilder">An e-mail builder.</param>
        public EMailEnvelop(AbstractEMail.Builder MailBuilder)
        {

            MailBuilder.EncodeBodyparts();

                             // ToDo: Deep cloning!
            this.MailFrom  = EMailAddressList.Create(MailBuilder.From);
            this.RcptTo    = EMailAddressList.Create(MailBuilder.To);
            this.Mail      = new EMail(MailBuilder);

        }

        #endregion

        #region EMailEnvelop(EMail)

        /// <summary>
        /// Create a new e-mail envelop based on the given e-mail.
        /// </summary>
        /// <param name="EMail">An e-mail.</param>
        public EMailEnvelop(EMail EMail)
        {

            this.MailFrom  = EMailAddressList.Create(EMail.From);
            this.RcptTo    = EMailAddressList.Create(EMail.To);
            this.Mail      = EMail;

        }

        #endregion

        #region EMailEnvelop(MailFrom, RcptTo, EMail,       RemoteSocket = null)

        /// <summary>
        /// Create a new e-mail envelop based on the given sender
        /// and receiver addresses and the e-mail builder data.
        /// </summary>
        /// <param name="MailFrom">The sender(s) of the e-mail.</param>
        /// <param name="RcptTo">The receiver(s) of the e-mail.</param>
        /// <param name="EMail">An e-mail.</param>
        /// <param name="RemoteSocket">The remote socket of the incoming SMTP connection.</param>
        public EMailEnvelop(EMailAddressList  MailFrom,
                            EMailAddressList  RcptTo,
                            EMail             EMail,
                            IPSocket?         RemoteSocket   = null)
        {

            this.MailFrom      = MailFrom;
            this.RcptTo        = RcptTo;
            this.Mail          = EMail;
            this.RemoteSocket  = RemoteSocket;

        }

        #endregion

        #region EMailEnvelop(MailFrom, RcptTo, MailBuilder, RemoteSocket = null)

        /// <summary>
        /// Create a new e-mail envelop based on the given sender
        /// and receiver addresses and the e-mail builder data.
        /// </summary>
        /// <param name="MailFrom">The sender(s) of the e-mail.</param>
        /// <param name="RcptTo">The receiver(s) of the e-mail.</param>
        /// <param name="MailBuilder">An e-mail builder.</param>
        /// <param name="RemoteSocket">The remote socket of the incoming SMTP connection.</param>
        public EMailEnvelop(EMailAddressList       MailFrom,
                            EMailAddressList       RcptTo,
                            AbstractEMail.Builder  MailBuilder,
                            IPSocket?              RemoteSocket   = null)
        {

            this.RemoteSocket  = RemoteSocket;
            this.MailFrom      = MailFrom;
            this.RcptTo        = RcptTo;
            this.Mail          = new EMail(MailBuilder);

        }

        #endregion

        #endregion

    }

}
