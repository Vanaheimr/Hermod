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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.Mail
{

    /// <summary>
    /// A HTML e-mail builder.
    /// </summary>
    public class HTMLEMailBuilder : AbstractEMailBuilder
    {

        #region Properties

        #region PlainText

        private String _PlainTextBodypart;

        /// <summary>
        /// The plaintext body of the HTML e-mail.
        /// </summary>
        public String PlainText
        {

            get
            {
                return _PlainTextBodypart;
            }

            set
            {
                if (value != null && value != String.Empty && value.Trim() != "")
                    _PlainTextBodypart = value;
            }

        }

        #endregion

        #region HTMLText

        private String _HTMLTextBodypart;

        /// <summary>
        /// The HTML body of the HTML e-mail.
        /// </summary>
        public String HTMLText
        {

            get
            {
                return _HTMLTextBodypart;
            }

            set
            {
                if (value != null && value != String.Empty && value.Trim() != "")
                    _HTMLTextBodypart = value;
            }

        }

        #endregion

        #region ContentLanguage

        private String _ContentLanguage;

        /// <summary>
        /// The language of the e-mail body.
        /// </summary>
        public String ContentLanguage
        {
            get
            {
                return _ContentLanguage;
            }

            set
            {
                if (value != null && value != String.Empty && value.Trim() != "")
                    _ContentLanguage = value;
            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTML e-mail builder.
        /// </summary>
        /// <param name="ContentLanguage">The language of the e-mail body.</param>
        public HTMLEMailBuilder(String ContentLanguage = null)
        {
            this._ContentLanguage  = ContentLanguage;
            this.PlainText         = "";
            this.HTMLText          = "";
        }

        #endregion


        #region (protected, override) _EncodeBodyparts()

        protected override EMailBodypart _EncodeBodyparts()
        {

            return new EMailBodypart(ContentType:              MailContentTypes.multipart_alternative,
                                     ContentTransferEncoding:  "8bit",
                                     Charset:                  "utf-8",
                                     NestedBodyparts:          new EMailBodypart[] {

                                                                   new EMailBodypart(ContentType:              MailContentTypes.text_plain,
                                                                                     ContentTransferEncoding:  "8bit",
                                                                                     Charset:                  "utf-8",
                                                                                     ContentLanguage:          ContentLanguage,
                                                                                     Content:                  new MailBodyString(PlainText)),

                                                                   new EMailBodypart(ContentType:              MailContentTypes.text_html,
                                                                                     ContentTransferEncoding:  "8bit",
                                                                                     Charset:                  "utf-8",
                                                                                     ContentLanguage:          ContentLanguage,
                                                                                     Content:                  new MailBodyString(HTMLText))

                                                               });

        }

        #endregion

    }

}
