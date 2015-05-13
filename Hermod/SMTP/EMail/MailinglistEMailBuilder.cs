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

using org.GraphDefined.Vanaheimr.Illias;
using System.Collections.Generic;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.Mail
{

    /// <summary>
    /// A mailinglist e-mail builder.
    /// </summary>
    public class MailinglistEMailBuilder : AbstractEMailBuilder
    {

        #region Properties

        #region ListId

        private ListId _ListId;

        /// <summary>
        /// The unique identification of the mailinglist.
        /// </summary>
        public ListId ListId
        {

            get
            {

                if (_ListId != null)
                    return _ListId;

                var _ListIdString = MailHeaders.
                                        Where(kvp => kvp.Key.ToLower() == "list-id").
                                        FirstOrDefault();

                if (_ListIdString.Key != null)
                {
                    _ListId = ListId.Parse(_ListIdString.Value);
                    return _ListId;
                }

                return null;

            }

            set
            {

                if (value != null)
                {

                    _ListId = value;

                    this.SetEMailHeader("List-Id", value.ToString());

                }

            }

        }

        #endregion

        #region ListPost

        private SimpleEMailAddress _ListPost;

        /// <summary>
        /// The e-mail address of the mailinglist for posting new e-mails.
        /// </summary>
        public SimpleEMailAddress ListPost
        {

            get
            {

                if (_ListPost != null)
                    return _ListPost;

                var _ListPostString = MailHeaders.
                                      Where(kvp => kvp.Key.ToLower() == "list-post").
                                      First();

                if (_ListPostString.Key != "")
                {
                    _ListPost = SimpleEMailAddress.Parse(_ListPostString.Value.Replace("mailto:", ""));
                    return _ListPost;
                }

                return null;

            }

            set
            {

                if (value != null)
                {

                    _ListPost = value;

                    this.SetEMailHeader("List-Post", "<mailto:" + value.ToString() + ">");

                }

            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        #region AbstractEMailBuilder(MailText)

        /// <summary>
        /// Create a new mailinglist e-mail builder.
        /// </summary>
        public MailinglistEMailBuilder()
        { }

        #endregion

        #region MailinglistEMailBuilder(MailText)

        /// <summary>
        /// Parse the e-mail from the given text lines.
        /// </summary>
        /// <param name="MailText">The E-Mail as an enumeration of strings.</param>
        public MailinglistEMailBuilder(IEnumerable<String> MailText)
            : base(MailText)
        { }

        #endregion

        #endregion


        #region (protected, override) _EncodeBodyparts()

        protected override EMailBodypart _EncodeBodyparts()
        {

            //return new EMailBodypart(ContentType:              MailContentTypes.text_plain,
            //                         ContentTransferEncoding:  "quoted-printable",//"8bit",
            //                         Charset:                  "utf-8",//"ISO-8859-15",
            //                         ContentLanguage:          ContentLanguage,
            //                         Content:                  new String[] { Text });

            return this.Body;

        }

        #endregion

    }

}
