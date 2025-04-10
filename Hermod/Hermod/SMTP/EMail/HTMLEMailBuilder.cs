﻿/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
    /// A HTML e-mail builder.
    /// </summary>
    public class HTMLEMailBuilder : AbstractEMailBuilder
    {

        #region Properties

        #region HTMLText

        private String htmlTextBodypart;

        /// <summary>
        /// The HTML body of the e-mail.
        /// </summary>
        public String HTMLText
        {

            get
            {
                return htmlTextBodypart;
            }

            set
            {
                if (value is not null && value.Trim().IsNotNullOrEmpty())
                    htmlTextBodypart = value.Trim();
            }

        }

        #endregion

        #region PlainText

        private String? plainTextBodypart;

        /// <summary>
        /// An alternative plaintext body of the e-mail.
        /// </summary>
        public String? PlainText
        {

            get
            {
                return plainTextBodypart;
            }

            set
            {
                plainTextBodypart = value?.Trim();
            }

        }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTML e-mail builder.
        /// </summary>
        /// <param name="HTMLMail">The HTML mail.</param>
        /// <param name="PlainTextMail">An alternative plain text mail.</param>
        public HTMLEMailBuilder(String?  HTMLMail        = null,
                                String?  PlainTextMail   = null)
        {

            this.htmlTextBodypart   = HTMLMail ?? String.Empty;
            this.plainTextBodypart  = PlainTextMail;

        }

        #endregion


        #region (protected, override) _EncodeBodyparts()

        /// <summary>
        /// Encode all information to a valid e-mail body.
        /// </summary>
        protected override EMailBodypart _EncodeBodyparts()
        {

            #region HTML & PlainText e-mail...

            if (htmlTextBodypart. IsNotNullOrEmpty() &&
                plainTextBodypart.IsNotNullOrEmpty())
            {

                this.ContentType = new MailContentType(this,
                                                       MailContentTypes.multipart_alternative)
                {
                    CharSet = "utf-8"
                };

                return new EMailBodypart(EMailBuilder:     this,
                                         Content:          new String[] { "This is a multi-part message in MIME format." },
                                         NestedBodyparts:  new EMailBodypart[] {

                                                               new EMailBodypart(ContentTypeBuilder:       AMail => new MailContentType(AMail, MailContentTypes.text_plain) { CharSet = "utf-8" },
                                                                                 ContentTransferEncoding:  this.ContentTransferEncoding,
                                                                                 ContentLanguage:          this.ContentLanguage,
                                                                                 Content:                  plainTextBodypart.Split(TextLineSplitter, StringSplitOptions.None)),

                                                               new EMailBodypart(ContentTypeBuilder:       AMail => new MailContentType(AMail, MailContentTypes.text_html)  { CharSet = "utf-8" },
                                                                                 ContentTransferEncoding:  this.ContentTransferEncoding,
                                                                                 ContentLanguage:          this.ContentLanguage,
                                                                                 Content:                  htmlTextBodypart. Split(TextLineSplitter, StringSplitOptions.None))

                                                           });

            }

            #endregion

            #region HTML e-mail...

            else if (htmlTextBodypart.IsNotNullOrEmpty())
            {

                this.ContentType = new MailContentType(this,
                                                       MailContentTypes.text_html)
                {

                    CharSet = "utf-8"

                };

                return new EMailBodypart(EMailBuilder:     this,
                                         Content:          htmlTextBodypart.Split(TextLineSplitter, StringSplitOptions.None));

            }

            #endregion

            #region PlainText e-mail...

            else if (plainTextBodypart.IsNotNullOrEmpty())
            {

                this.ContentType = new MailContentType(this,
                                                       MailContentTypes.text_plain)
                {

                    CharSet = "utf-8"

                };

                return new EMailBodypart(EMailBuilder:     this,
                                         Content:          plainTextBodypart.Split(TextLineSplitter, StringSplitOptions.None));

            }

            #endregion

            #region Empty e-mail...

            else
            {

                this.ContentType = new MailContentType(this,
                                                       MailContentTypes.text_plain)
                {

                    CharSet = "utf-8"

                };

                return new EMailBodypart(EMailBuilder:     this,
                                         Content:          new String[0]);

            }

            #endregion

        }

        #endregion


        #region (implicit operator) HTMLEMailBuilder => EMail

        /// <summary>
        /// An implicit conversion of a HTTPEMailBuilder into an EMail.
        /// </summary>
        /// <param name="HTMLEMailBuilder">A HTML e-mail builder.</param>
        public static implicit operator EMail(HTMLEMailBuilder HTMLEMailBuilder)
            => HTMLEMailBuilder.AsImmutable;

        #endregion

    }

}
