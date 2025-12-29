/*
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

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A TEXT e-mail builder.
    /// </summary>
    public class TextEMailBuilder : AbstractEMail.Builder
    {

        #region Properties

        /// <summary>
        /// The body of the text e-mail.
        /// </summary>
        public String  Text    { get; set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new TEXT e-mail builder.
        /// </summary>
        public TextEMailBuilder()
        {

            this.ContentType  = new MailContentType(MailContentTypes.text_plain) { CharSet = "utf-8" };
            this.Text         = "";

        }

        #endregion


        #region (protected, override) _EncodeBodyparts()

        protected override EMailBodypart _EncodeBodyparts()

            => new (
                   ContentTypeBuilder:       mail => new MailContentType(MailContentTypes.text_plain) { CharSet = "utf-8" },//"ISO-8859-15",
                   ContentTransferEncoding:  "quoted-printable",//"8bit",
                   ContentLanguage:          ContentLanguage,
                   Content:                  Text.Split(TextLineSplitter, StringSplitOptions.None)
               );

        #endregion


        public static implicit operator EMail(TextEMailBuilder TextEMailBuilder)
            => TextEMailBuilder.AsImmutable;

    }

}
