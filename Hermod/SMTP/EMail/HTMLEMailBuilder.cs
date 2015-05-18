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

using org.GraphDefined.Vanaheimr.Illias;

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
                if (value != null && value.Trim().IsNotNullOrEmpty())
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
                if (value != null && value.Trim().IsNotNullOrEmpty())
                    _HTMLTextBodypart = value;
            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTML e-mail builder.
        /// </summary>
        public HTMLEMailBuilder()
        {

            this.ContentType  = new MailContentType(this, MailContentTypes.multipart_alternative) { CharSet = "utf-8" };

            this.PlainText    = "";
            this.HTMLText     = "";

        }

        #endregion


        #region (protected, override) _EncodeBodyparts()

        /// <summary>
        /// Encode all information to a valid e-mail body.
        /// </summary>
        protected override EMailBodypart _EncodeBodyparts()
        {

            return new EMailBodypart(EMailBuilder:     this,
                                     Content:          new String[] { "This is a multi-part message in MIME format." },
                                     NestedBodyparts:  new EMailBodypart[] {

                                                           new EMailBodypart(ContentTypeBuilder:       AMail => new MailContentType(AMail, MailContentTypes.text_plain) { CharSet = "utf-8" },
                                                                             ContentTransferEncoding:  this.ContentTransferEncoding,
                                                                             ContentLanguage:          this.ContentLanguage,
                                                                             Content:                  PlainText.Split(TextLineSplitter, StringSplitOptions.None)),

                                                           new EMailBodypart(ContentTypeBuilder:       AMail => new MailContentType(AMail, MailContentTypes.text_html)  { CharSet = "utf-8" },
                                                                             ContentTransferEncoding:  this.ContentTransferEncoding,
                                                                             ContentLanguage:          this.ContentLanguage,
                                                                             Content:                  HTMLText.Split(TextLineSplitter, StringSplitOptions.None))

                                                       });

        }

        #endregion

    }

}
