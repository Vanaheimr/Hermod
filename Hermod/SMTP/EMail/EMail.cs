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
    /// A e-mail.
    /// </summary>
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

        #region Header

        private readonly List<KeyValuePair<String, String>> _Header;

        /// <summary>
        /// The E-Mail header as enumeration of strings.
        /// </summary>
        public IEnumerable<KeyValuePair<String, String>> Header
        {
            get
            {
                return _Header;
            }
        }

        #endregion

        #region Body

        /// <summary>
        /// The e-mail body.
        /// </summary>
        public readonly EMailBodypart Body;

        #endregion

        #region ToText

        /// <summary>
        /// Return a string representation of this e-mail.
        /// </summary>
        public IEnumerable<String> ToText
        {
            get
            {

                return _Header.
                            Select(line => line.Key + ": " + line.Value).
                            Concat(new String[] { "" }).
                            Concat(Body.ToText(false));

            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region (private) EMail(MailHeader)

        private EMail(IEnumerable<KeyValuePair<String, String>> MailHeader)
        {

            _Header = new List<KeyValuePair<String, String>>(MailHeader);

            foreach (var KVP in _Header)
            {

                switch (KVP.Key.ToLower())
                {

                    case "from":       this.From       = EMailAddress.    Parse(KVP.Value); break;
                    case "to":         this.To         = EMailAddressList.Parse(KVP.Value); break;
                    case "cc":         this.Cc         = EMailAddressList.Parse(KVP.Value); break;
                    case "bcc":        this.Bcc        = EMailAddressList.Parse(KVP.Value); break;
                    case "replyto":    this.ReplyTo    = EMailAddressList.Parse(KVP.Value); break;
                    case "subject":    this.Subject    =                        KVP.Value ; break;
                    case "date":       this.Date       = DateTime.        Parse(KVP.Value); break;
                    case "message-id": this.MessageId  = MessageId.       Parse(KVP.Value); break;

                }

            }

        }

        #endregion

        #region (private) EMail(MailText)

        /// <summary>
        /// Parse an e-mail from the given enumeration of strings.
        /// </summary>
        /// <param name="MailText">An enumeration of strings.</param>
        private EMail(IEnumerable<String> MailText)

            : this(MailText.TakeWhile(line => line.IsNotNullOrEmpty()).
                            AggregateIndentedLines().
                            ToKeyValuePairs(':'))

        {

            Body  = new EMailBodypart(MailText);

        }

        #endregion

        #region EMail(MailBuilder)

        /// <summary>
        /// Create a new e-mail based on the given e-mail builder.
        /// </summary>
        /// <param name="MailBuilder">An e-mail builder.</param>
        public EMail(AbstractEMailBuilder MailBuilder)

            : this(MailBuilder.
                       EncodeBodyparts().
                       // Copy only everything which is not related to the e-mail body!
                       MailHeaders.Where(header => !header.Key.ToLower().StartsWith("content")).
                       Concat(MailBuilder.Body.MailHeaders))

        {

            //ToDo: Do a real deep-copy here!
            Body  = MailBuilder.Body;

        }

        #endregion

        #endregion


        #region GetEMailHeader(Key)

        /// <summary>
        /// Get the e-mail header value for the given key.
        /// </summary>
        /// <param name="Key">An e-mail header key.</param>
        public String GetEMailHeader(String Key)
        {

            var Property = _Header.
                               Where(kvp => kvp.Key.ToLower() == Key.ToLower()).
                               FirstOrDefault();

            if (Property.Key.IsNotNullOrEmpty())
                return Property.Value;

            return String.Empty;

        }

        #endregion

        #region (static) Parse(MailText)

        /// <summary>
        /// Parse an e-mail from the given enumeration of strings.
        /// </summary>
        /// <param name="MailText">An enumeration of strings.</param>
        public static EMail Parse(IEnumerable<String> MailText)
        {
            return new EMail(MailText);
        }

        #endregion

    }

}
