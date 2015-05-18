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

        #region Well-known e-mail headers

        /// <summary>
        /// The sender of the e-mail.
        /// </summary>
        public readonly EMailAddress        From;

        /// <summary>
        /// The receivers of the e-mail.
        /// </summary>
        public readonly EMailAddressList    To;

        /// <summary>
        /// Additional receivers of the e-mail.
        /// </summary>
        public readonly EMailAddressList    Cc;

        /// <summary>
        /// Hidden receivers of the e-mail.
        /// </summary>
        public readonly EMailAddressList    Bcc;

        /// <summary>
        /// The receivers of any reply on this e-mail.
        /// </summary>
        public readonly EMailAddressList    ReplyTo;

        /// <summary>
        /// The subject of the e-mail.
        /// </summary>
        public readonly String              Subject;

        /// <summary>
        /// The sending timestamp of the e-mail.
        /// </summary>
        public readonly DateTime            Date;

        /// <summary>
        /// The unique message identification of the e-mail.
        /// </summary>
        public readonly MessageId           MessageId;

        #endregion

        #region Properties

        #region MailHeaders

        private readonly List<KeyValuePair<String, String>> _MailHeaders;

        /// <summary>
        /// The E-Mail header as enumeration of strings.
        /// </summary>
        public IEnumerable<KeyValuePair<String, String>> MailHeaders
        {
            get
            {
                return _MailHeaders;
            }
        }

        #endregion

        #region MailBody

        /// <summary>
        /// The E-Mail body as enumeration of strings.
        /// </summary>
        public readonly IEnumerable<String> MailBody;

        #endregion

        #region MailText

        /// <summary>
        /// The E-Mail as enumeration of strings.
        /// </summary>
        public IEnumerable<String> MailText
        {
            get
            {

                return _MailHeaders.
                            Select(headers => headers.Key + ": " + headers.Value).
                            Concat(new String[] { "" }).
                            Concat(MailBody);

            }
        }

        #endregion


        


        #region Body

        public readonly EMailBodypart Body;

        #endregion

        #endregion

        #region Constructor(s)

        #region EMail(MailBuilder)

        public EMail(AbstractEMailBuilder MailBuilder)
        {

            MailBuilder.EncodeBodyparts();

            _MailHeaders    = new List<KeyValuePair<String, String>>(MailBuilder.MailHeaders.Where(header => !header.Key.ToLower().StartsWith("content")));
            _MailHeaders.AddRange(MailBuilder.Body.MailHeaders);

            this.From       = EMailAddress.    Parse(MailBuilder.GetEMailHeader("From"));
            this.To         = EMailAddressList.Parse(MailBuilder.GetEMailHeader("To"));
            this.Cc         = EMailAddressList.Parse(MailBuilder.GetEMailHeader("Cc"));
            this.Bcc        = EMailAddressList.Parse(MailBuilder.GetEMailHeader("Bcc"));
            this.ReplyTo    = EMailAddressList.Parse(MailBuilder.GetEMailHeader("ReplyTo"));
            this.Subject    =                        MailBuilder.GetEMailHeader("Subject");
            this.Date       = DateTime.        Parse(MailBuilder.GetEMailHeader("Date"));
            this.MessageId  = MessageId.       Parse(MailBuilder.GetEMailHeader("Message-ID"));

            Body            = MailBuilder.Body;
            MailBody        = Body.ToText(false);

        }

        #endregion

        #endregion

    }

}
