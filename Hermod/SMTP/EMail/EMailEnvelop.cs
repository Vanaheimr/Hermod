/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using System;
using System.Linq;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.Mail
{

    /// <summary>
    /// An e-mail envelop.
    /// </summary>
    public class EMailEnvelop
    {

        #region Properties

        #region RemoteSocket

        private readonly IPSocket _RemoteSocket;

        /// <summary>
        /// The remote socket of the incoming SMTP connection.
        /// </summary>
        public IPSocket RemoteSocket
        {
            get
            {
                return _RemoteSocket;
            }
        }

        #endregion

        #region MailFrom

        private readonly EMailAddressList _MailFrom;

        /// <summary>
        /// The sender(s) of the e-mail.
        /// </summary>
        public EMailAddressList MailFrom
        {
            get
            {
                return _MailFrom;
            }
        }

        #endregion

        #region RcptTo

        private readonly EMailAddressList _RcptTo;

        /// <summary>
        /// The receiver(s) of the e-mail.
        /// </summary>
        public EMailAddressList RcptTo
        {
            get
            {
                return _RcptTo;
            }
        }

        #endregion

        #region MailText

        protected readonly IEnumerable<String> _MailText;

        /// <summary>
        /// The embedded e-mail as text.
        /// </summary>
        public IEnumerable<String> MailText
        {
            get
            {
                return _MailText;
            }
        }

        #endregion

        #region MailHeader

        /// <summary>
        /// The embedded e-mail header as text.
        /// </summary>
        public IEnumerable<String> MailHeader
        {
            get
            {

                return _MailText != null
                    ? _MailText.TakeWhile(line => line.IsNotNullOrEmpty())
                    : new String[] { "" };

            }
        }

        #endregion

        #region MailBody

        /// <summary>
        /// The embedded e-mail body as text.
        /// </summary>
        public IEnumerable<String> MailBody
        {
            get
            {

                // Skip the mail header and skip the newline after the mail header...
                return _MailText != null
                    ? _MailText.SkipWhile(line => line.IsNotNullOrEmpty()).Skip(1)
                    : new String[] { "" };

            }
        }

        #endregion

        #region Mail

        protected readonly EMail _Mail;

        /// <summary>
        /// The embedded e-mail.
        /// </summary>
        public EMail Mail
        {
            get
            {
                return _Mail;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region EMailEnvelop(MailBuilder)

        /// <summary>
        /// Create a new e-mail envelop based on the given
        /// e-mail builder data.
        /// </summary>
        /// <param name="MailBuilder">An e-mail builder.</param>
        public EMailEnvelop(AbstractEMailBuilder MailBuilder)
        {

            MailBuilder.EncodeBodyparts();

                              // ToDo: Deep cloning!
            this._MailFrom  = new EMailAddressList(MailBuilder.From);
            this._RcptTo    = new EMailAddressList(MailBuilder.To);
            this._Mail      = new EMail(MailBuilder);

        }

        #endregion

        #region EMailEnvelop(EMail)

        /// <summary>
        /// Create a new e-mail envelop based on the given e-mail.
        /// </summary>
        /// <param name="EMail">An e-mail.</param>
        public EMailEnvelop(EMail EMail)
        {

            this._MailFrom  = new EMailAddressList(EMail.From);
            this._RcptTo    = new EMailAddressList(EMail.To);
            this._Mail      = EMail;

        }

        #endregion

        #region EMailEnvelop(MailFrom, RcptTo, EMail)

        /// <summary>
        /// Create a new e-mail envelop based on the given sender
        /// and receiver addresses and the e-mail builder data.
        /// </summary>
        /// <param name="MailFrom">The sender(s) of the e-mail.</param>
        /// <param name="RcptTo">The receiver(s) of the e-mail.</param>
        /// <param name="EMail">An e-mail.</param>
        public EMailEnvelop(EMailAddressList  MailFrom,
                            EMailAddressList  RcptTo,
                            EMail             EMail)
        {

            this._MailFrom  = MailFrom;
            this._RcptTo    = RcptTo;
            this._Mail      = EMail;

        }

        #endregion

        #region EMailEnvelop(MailFrom, RcptTo, MailText)

        /// <summary>
        /// Create a new e-mail envelop based on the given sender
        /// and receiver addresses and the e-mail builder data.
        /// </summary>
        /// <param name="MailFrom">The sender(s) of the e-mail.</param>
        /// <param name="RcptTo">The receiver(s) of the e-mail.</param>
        /// <param name="MailText">The e-mail as enumeration of strings.</param>
        /// <param name="RemoteSocket">The remote socket of the incoming SMTP connection.</param>
        public EMailEnvelop(EMailAddressList      MailFrom,
                            EMailAddressList      RcptTo,
                            IEnumerable<String>   MailText,
                            IPSocket              RemoteSocket  = null)
        {

            this._RemoteSocket  = RemoteSocket;
            this._MailFrom      = MailFrom;
            this._RcptTo        = RcptTo;
            this._MailText      = MailText;

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
        public EMailEnvelop(EMailAddressList      MailFrom,
                            EMailAddressList      RcptTo,
                            AbstractEMailBuilder  MailBuilder,
                            IPSocket              RemoteSocket  = null)
        {

            MailBuilder.EncodeBodyparts();

            this._RemoteSocket  = RemoteSocket;
            this._MailFrom      = MailFrom;
            this._RcptTo        = RcptTo;
            this._Mail          = new EMail(MailBuilder);
            this._MailText      = _Mail.MailHeaders.
                                        Concat(new String[] { "" }).
                                        Concat(_Mail.Bodypart.ToText(false));

        }

        #endregion

        #endregion

    }

}
