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
using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Illias;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.Mail
{

    public class EMail
    {

        #region Properties

        #region From

        private readonly EMailAddress _From;

        /// <summary>
        /// The sender of the e-mail.
        /// </summary>
        public EMailAddress From
        {
            get
            {
                return _From;
            }
        }

        #endregion

        #region To

        private readonly EMailAddressList _To;

        /// <summary>
        /// The receivers of the e-mail.
        /// </summary>
        public EMailAddressList To
        {
            get
            {
                return _To;
            }
        }

        #endregion

        #region ReplyTo

        private readonly EMailAddressList _ReplyTo;

        /// <summary>
        /// The receivers of any reply on this e-mail.
        /// </summary>
        public EMailAddressList ReplyTo
        {
            get
            {
                return _ReplyTo;
            }
        }

        #endregion

        #region Cc

        private readonly EMailAddressList _Cc;

        /// <summary>
        /// Additional receivers of the e-mail.
        /// </summary>
        public EMailAddressList Cc
        {
            get
            {
                return _Cc;
            }
        }

        #endregion

        #region Bcc

        private readonly EMailAddressList _Bcc;

        /// <summary>
        /// Hidden receivers of the e-mail.
        /// </summary>
        public EMailAddressList Bcc
        {
            get
            {
                return _Bcc;
            }
        }

        #endregion

        #region Subject

        private String _Subject;

        public String Subject
        {
            get
            {
                return _Subject;
            }
        }

        #endregion

        #region Date

        private DateTime _Date;

        public DateTime Date
        {
            get
            {
                return _Date;
            }
        }

        #endregion

        #region MessageId

        private MessageId _MessageId;

        /// <summary>
        /// The unique message identification of the e-mail.
        /// </summary>
        public MessageId MessageId
        {
            get
            {
                return _MessageId;
            }
        }

        #endregion

        #region AdditionalHeaders

        private Dictionary<String, String> _AdditionalHeaders;

        public Dictionary<String, String> AdditionalHeaders
        {
            get
            {
                return _AdditionalHeaders;
            }
        }

        #endregion


        #region Body

        protected readonly EMailBodypart _Bodypart;

        /// <summary>
        /// The e-mail body.
        /// </summary>
        public EMailBodypart Bodypart
        {
            get
            {
                return _Bodypart;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region EMail(MailBuilder)

        public EMail(AbstractEMailBuilder MailBuilder)
        {

            MailBuilder.EncodeBodyparts();

            this._From               = MailBuilder.From;
                                       // ToDo: Deep cloning!
            this._To                 = new EMailAddressList(MailBuilder.To);
            this._ReplyTo            = new EMailAddressList(MailBuilder.ReplyTo);
            this._Cc                 = new EMailAddressList(MailBuilder.Cc);
            this._Bcc                = new EMailAddressList(MailBuilder.Bcc);
            this._Subject            = MailBuilder.Subject;
            this._Date               = MailBuilder.Date;
            this._MessageId          = MailBuilder.MessageId;
            this._AdditionalHeaders  = new Dictionary<String, String>();

            MailBuilder._AdditionalHeaders.ForEach(v => { this._AdditionalHeaders.Add(v.Key, v.Value); });
            this._Bodypart           = MailBuilder.Body;

        }

        #endregion

        #endregion


        public IEnumerable<String> Headers
        {

            get
            {

                return new String[] {

                    "MIME-Version: 1.0",
                    "From: "        + From,
                    "To: "          + To,
                    ReplyTo.Any() ? "Reply-To: " + ReplyTo : null,
                    Cc.     Any() ? "Cc: "       + Cc      : null,
                    Bcc.    Any() ? "Bcc: "      + Bcc     : null,

                    // Subject: =?UTF-8?...
                    "Subject: "     + Subject,
                    "Date: "        + Date.ToUniversalTime().ToString("R"),

                    //SendCommand("In-Reply-To: " + Mail.);
                    //SendCommand("References: "  + multiple message Ids);

                    // Content-Transfer-Encoding: quoted-printable

                }.
                Where(line => line != null).

                // Content-Type; Char-Set and more...
                Concat(Bodypart.Headers).

                Concat(AdditionalHeaders.
                           Where (kvp => kvp.Value != null).
                           Select(kvp => kvp.Key + ": " + kvp.Value));

            }

        }

        public IEnumerable<String> Body
        {
            get
            {
                return Bodypart.ToText(true);
            }
        }


    }

}
