/*
 * Copyright (c) 2010-2020, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A TEXT e-mail builder.
    /// </summary>
    public class TextEMailBuilder : AbstractEMailBuilder
    {

        #region Properties

        #region Text

        private String _TextBody;

        /// <summary>
        /// The body of the text e-mail.
        /// </summary>
        public String Text
        {

            get
            {
                return _TextBody;
            }

            set
            {
                if (value != null && value != String.Empty && value.Trim() != "")
                    _TextBody = value;
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
        /// Create a new TEXT e-mail builder.
        /// </summary>
        public TextEMailBuilder()
        {

            this.ContentType  = new MailContentType(this, MailContentTypes.text_plain) { CharSet = "utf-8" };

            this.Text         = "";

        }

        #endregion


        #region (protected, override) _EncodeBodyparts()

        protected override EMailBodypart _EncodeBodyparts()
        {

            return new EMailBodypart(ContentTypeBuilder:       AMail => new MailContentType(AMail, MailContentTypes.text_plain) { CharSet = "utf-8" },//"ISO-8859-15",
                                     ContentTransferEncoding:  "quoted-printable",//"8bit",
                                     ContentLanguage:          ContentLanguage,
                                     Content:                  Text.Split(TextLineSplitter, StringSplitOptions.None));

        }

        #endregion


        public static implicit operator EMail(TextEMailBuilder TextEMailBuilder)
            => TextEMailBuilder.AsImmutable;

    }

}
