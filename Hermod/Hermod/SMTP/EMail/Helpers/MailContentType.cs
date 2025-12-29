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

#region Usings

using org.GraphDefined.Vanaheimr.Illias;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A e-mail content type with all of its sub-information.
    /// </summary>
    public class MailContentType
    {

        #region Data

 //       private readonly  AbstractEMail   AbstractEMail;

        #endregion

        #region Properties

        /// <summary>
        /// The content type.
        /// </summary>
        public MailContentTypes  ContentType     { get; set; }

        /// <summary>
        /// The character set.
        /// </summary>
        public String?           CharSet         { get; set; }

        /// <summary>
        /// The MIME boundary.
        /// </summary>
        public String?           MIMEBoundary    { get; set; }

        /// <summary>
        /// MicAlg part. Used e.g. for PGP/GPG.
        /// </summary>
        public String?           MicAlg          { get; set; }

        /// <summary>
        /// Protocol part. Used e.g. for PGP/GPG.
        /// </summary>
        public String?           Protocol        { get; set; }


        public String            Text            { get; set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new e-mail content type.
        /// </summary>
        /// <param name="ContentType">The content type.</param>
        public MailContentType(MailContentTypes  ContentType,
                               String?           CharSet        = null,
                               String?           MIMEBoundary   = null,
                               String?           MicAlg         = null,
                               String?           Protocol       = null,
                               String?           Text           = null)
        {

            this.ContentType   = ContentType;
            this.CharSet       = CharSet;
            this.MIMEBoundary  = MIMEBoundary;
            this.MicAlg        = MicAlg;
            this.Protocol      = Protocol;
            this.Text          = Text ?? "";

        }

        #endregion


        #region Parse    (ContentTypeString)

        /// <summary>
        /// Parse the given string as a e-mail content type.
        /// </summary>
        /// <param name="ContentTypeString"></param>
        public static MailContentType? Parse(String ContentTypeString)
        {

            if (TryParse(ContentTypeString, out var mailContentType))
                return mailContentType;

            return null;

        }

        #endregion

        #region TryParse (ContentTypeString, out MailContentType)

        /// <summary>
        /// Try to parse the given string as a e-mail content type.
        /// </summary>
        /// <param name="ContentTypeString"></param>
        public static Boolean TryParse(String                                    ContentTypeString,
                                       [NotNullWhen(true)] out MailContentType?  MailContentType)
        {

            MailContentTypes? mailContentType = null;

            var splitted = ContentTypeString.
                               Split([";"], StringSplitOptions.RemoveEmptyEntries).
                               Select(v => v.Trim()).
                               ToArray();

            if (splitted.Length == 0)
            {

                MailContentType  = new MailContentType(
                                       MailContentTypes.text_plain,
                                       Text: ContentTypeString
                                   );

                return true;

            }

            if (Enum.TryParse<MailContentTypes>(splitted[0].Replace("/", "_").Replace("+", "__"), out var _mailContentType))
            {

                mailContentType = _mailContentType;

                var charSet       = "";
                var mimeBoundary  = "";

                foreach (var subInformation in splitted.Skip(1))
                {

                    if (subInformation.ToLower().StartsWith("charset="))
                    {

                        charSet = subInformation.Substring("charset=".Length).Trim().ToLower();

                        if (charSet.StartsWith(@""""))
                            charSet = charSet.Remove(0, 1);

                        if (charSet.EndsWith(@""""))
                            charSet = charSet.Substring(0, charSet.Length - 1);

                        continue;

                    }

                    if (subInformation.ToLower().StartsWith("boundary="))
                    {

                        mimeBoundary = subInformation.Substring("boundary=".Length).Trim();

                        if (mimeBoundary.StartsWith(@""""))
                            mimeBoundary = mimeBoundary.Remove(0, 1);

                        if (mimeBoundary.EndsWith(@""""))
                            mimeBoundary = mimeBoundary.Substring(0, mimeBoundary.Length - 1);

                        continue;

                    }

                    MailContentType  = new MailContentType(
                                           mailContentType.Value,
                                           CharSet:       charSet,
                                           MIMEBoundary:  mimeBoundary,
                                           Text:          ContentTypeString
                                       );

                    return true;

                }

                // Update text-version within the e-mail header
                //     if (this.AbstractEMail is not null)
                //         this.AbstractEMail.SetEMailHeader("Content-Type", this.ToString());

            }

            MailContentType = null;
            return false;

        }

        #endregion


        #region GenerateMIMEBoundary()

        /// <summary>
        /// Generate a valid MIME boundary, if it does not exist.
        /// </summary>
        public MailContentType GenerateMIMEBoundary()
        {

            if (MIMEBoundary is null)
            {

                MIMEBoundary = "-8<--" + ContentType.ToString().Replace("_", "/") + "--8<--" + RandomExtensions.RandomBytes(12).ToHexString() + "--8<-";

                //// Update text-version within the e-mail header
                //if (AbstractEMail is not null)
                //    AbstractEMail.SetEMailHeader("Content-Type", this.ToString());

            }

            return this;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()
        {

            return ContentType.ToString().Replace("__", "-").Replace("_", "/") +
                   (CharSet.     IsNotNullOrEmpty() ?        "; charset=\""  + CharSet      + "\"" : String.Empty) +
                   (MIMEBoundary.IsNotNullOrEmpty() ? ";\r\n    boundary=\"" + MIMEBoundary + "\"" : String.Empty) +
                   (MicAlg.      IsNotNullOrEmpty() ? ";\r\n    micalg=\""   + MicAlg       + "\"" : String.Empty) +
                   (Protocol.    IsNotNullOrEmpty() ? ";\r\n    protocol=\"" + Protocol     + "\"" : String.Empty);

        }

        #endregion

    }

}
