/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// An e-mail envelop.
    /// </summary>
    public class EMailEnvelop
    {

        #region Properties

        /// <summary>
        /// The remote socket of the incoming SMTP connection.
        /// </summary>
        public IPSocket?         RemoteSocket       { get; }

        /// <summary>
        /// The sender(s) of the e-mail.
        /// </summary>
        public EMailAddressList  MailFrom           { get; }

        /// <summary>
        /// The receiver(s) of the e-mail.
        /// </summary>
        public EMailAddressList  RcptTo             { get; }

        /// <summary>
        /// The embedded e-mail.
        /// </summary>
        public EMail             Mail               { get; }

        /// <summary>
        /// The event tracking identification of this e-mail.
        /// </summary>
        public EventTracking_Id  EventTrackingId    { get; }

        #endregion

        #region Constructor(s)

        #region EMailEnvelop(MailBuilder, EventTrackingId = null)

        /// <summary>
        /// Create a new e-mail envelop based on the given
        /// e-mail builder data.
        /// </summary>
        /// <param name="MailBuilder">An e-mail builder.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        public EMailEnvelop(AbstractEMailBuilder MailBuilder,
                            EventTracking_Id     EventTrackingId  = null)
        {

            MailBuilder.EncodeBodyparts();

                                    // ToDo: Deep cloning!
            this.MailFrom         = EMailAddressList.Create(MailBuilder.From);
            this.RcptTo           = EMailAddressList.Create(MailBuilder.To);
            this.Mail             = new EMail(MailBuilder);
            this.EventTrackingId  = EventTrackingId ??
                                    (MailBuilder.MessageId.HasValue
                                         ? EventTracking_Id.Parse(MailBuilder.MessageId.ToString())
                                         : EventTracking_Id.New);

        }

        #endregion

        #region EMailEnvelop(EMail,       EventTrackingId = null)

        /// <summary>
        /// Create a new e-mail envelop based on the given e-mail.
        /// </summary>
        /// <param name="EMail">An e-mail.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        public EMailEnvelop(EMail             EMail,
                            EventTracking_Id  EventTrackingId  = null)
        {

            this.MailFrom         = EMailAddressList.Create(EMail.From);
            this.RcptTo           = EMailAddressList.Create(EMail.To);
            this.Mail             = EMail;
            this.EventTrackingId  = EventTrackingId ??
                                    (EMail.MessageId.HasValue
                                         ? EventTracking_Id.Parse(EMail.MessageId.ToString())
                                         : EventTracking_Id.New);

        }

        #endregion

        #region EMailEnvelop(MailFrom, RcptTo, EMail,       RemoteSocket = null, EventTrackingId = null)

        /// <summary>
        /// Create a new e-mail envelop based on the given sender
        /// and receiver addresses and the e-mail builder data.
        /// </summary>
        /// <param name="MailFrom">The sender(s) of the e-mail.</param>
        /// <param name="RcptTo">The receiver(s) of the e-mail.</param>
        /// <param name="EMail">An e-mail.</param>
        /// <param name="RemoteSocket">The remote socket of the incoming SMTP connection.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        public EMailEnvelop(EMailAddressList  MailFrom,
                            EMailAddressList  RcptTo,
                            EMail             EMail,
                            IPSocket?         RemoteSocket     = null,
                            EventTracking_Id  EventTrackingId  = null)
        {

            this.MailFrom         = MailFrom;
            this.RcptTo           = RcptTo;
            this.Mail             = EMail;
            this.RemoteSocket     = RemoteSocket;
            this.EventTrackingId  = EventTrackingId ??
                                    (EMail.MessageId.HasValue
                                         ? EventTracking_Id.Parse(EMail.MessageId.ToString())
                                         : EventTracking_Id.New);

        }

        #endregion

        #region EMailEnvelop(MailFrom, RcptTo, MailBuilder, RemoteSocket = null, EventTrackingId = null)

        /// <summary>
        /// Create a new e-mail envelop based on the given sender
        /// and receiver addresses and the e-mail builder data.
        /// </summary>
        /// <param name="MailFrom">The sender(s) of the e-mail.</param>
        /// <param name="RcptTo">The receiver(s) of the e-mail.</param>
        /// <param name="MailBuilder">An e-mail builder.</param>
        /// <param name="RemoteSocket">The remote socket of the incoming SMTP connection.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        public EMailEnvelop(EMailAddressList      MailFrom,
                            EMailAddressList      RcptTo,
                            AbstractEMailBuilder  MailBuilder,
                            IPSocket?             RemoteSocket     = null,
                            EventTracking_Id      EventTrackingId  = null)
        {

            this.RemoteSocket     = RemoteSocket;
            this.MailFrom         = MailFrom;
            this.RcptTo           = RcptTo;
            this.Mail             = new EMail(MailBuilder);
            this.EventTrackingId  = EventTrackingId ??
                                    (MailBuilder.MessageId.HasValue
                                         ? EventTracking_Id.Parse(MailBuilder.MessageId.ToString())
                                         : EventTracking_Id.New);

        }

        #endregion

        #endregion

    }

}
