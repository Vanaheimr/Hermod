/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System;
using System.Linq;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A part of an e-mail body.
    /// </summary>
    /// <remarks>More or less an embedded e-mail for its own.</remarks>
    public class EMailBodypart : AbstractEMail
    {

        #region Properties

        /// <summary>
        /// The content of this e-mail body.
        /// </summary>
        public IEnumerable<String>         Content            { get; }

        /// <summary>
        /// The nested e-mail body parts.
        /// </summary>
        public IEnumerable<EMailBodypart>  NestedBodyparts    { get; }

        #endregion

        #region Constructor(s)

        #region EMailBodypart(EMailBuilder, ...)

        /// <summary>
        /// Create a new e-mail bodypart.
        /// </summary>
        public EMailBodypart(AbstractEMailBuilder         EMailBuilder,
                             IEnumerable<String>?         Content           = null,
                             IEnumerable<EMailBodypart>?  NestedBodyparts   = null)

        {

            // Only copy all e-mail headers starting with "content"...
            base._MailHeaders.AddRange(EMailBuilder.MailHeaders.Where(header => header.Key.ToLower().StartsWith("content")));

            if (Content is not null && Content.Any())
                this._MailBody.AddRange(Content);

            this.NestedBodyparts  = NestedBodyparts is not null
                                        ? new List<EMailBodypart>(NestedBodyparts)
                                        : Array.Empty<EMailBodypart>();

            if (this.NestedBodyparts.Any())
                this.ContentType.GenerateMIMEBoundary();

        }

        #endregion

        #region EMailBodypart(...)

        /// <summary>
        /// Create a new e-mail bodypart.
        /// </summary>
        public EMailBodypart(Func<AbstractEMail, MailContentType>  ContentTypeBuilder,
                             String?                               ContentTransferEncoding   = null,
                             String?                               ContentLanguage           = null,
                             String?                               ContentDescription        = null,
                             String?                               ContentDisposition        = null,
                             String?                               MIMEBoundary              = null,
                             IEnumerable<EMailBodypart>?           NestedBodyparts           = null,
                             IEnumerable<String>?                  Content                   = null)

        {

            this.ContentType              = ContentTypeBuilder(this);
            this.ContentTransferEncoding  = ContentTransferEncoding;
            this.ContentLanguage          = ContentLanguage;
            this.ContentDescription       = ContentDescription;
            this.ContentDisposition       = ContentDisposition;

            if (Content is not null)
                this._MailBody.AddRange(Content);

            this.NestedBodyparts          = NestedBodyparts is not null
                                                ? new List<EMailBodypart>(NestedBodyparts)
                                                : new List<EMailBodypart>();

            if (this.NestedBodyparts.Any())
                this.ContentType.GenerateMIMEBoundary();

        }

        #endregion

        #region EMailBodypart(MailText)

        /// <summary>
        /// Parse the e-mail from the given text lines.
        /// </summary>
        /// <param name="MailText">The E-Mail as an enumeration of strings.</param>
        public EMailBodypart(IEnumerable<String> MailText)

            : base(MailText,
                   header => header.ToLower().StartsWith("content"))

        {

            ContentType ??= new MailContentType(this, GetEMailHeader("Content-Type"));

            if (ContentType.Text.Contains("boundary="))
            {

                var mimeBoundaryCheck     = "--" + ContentType.MIMEBoundary;
                var mimeBoundaryCheckEnd  = "--" + ContentType.MIMEBoundary + "--";
                var listOfList            = new List<List<String>>();
                var list                  = new List<String>();

                foreach (var line in MailBody)
                {

                    if (line == mimeBoundaryCheck ||
                        line == mimeBoundaryCheckEnd)
                    {
                        listOfList.Add(list);
                        list = new List<String>();
                    }

                    else
                        list.Add(line);

                }

                this._MailBody.Clear();
                this._MailBody.AddRange(listOfList[0]);
                this.NestedBodyparts = listOfList.Skip(1).Select(list => new EMailBodypart(list)).ToList();

            }

            else
            {

                //this._MailBody.Clear();
                //this._MailBody.AddRange(MailBody);
                this.NestedBodyparts = Array.Empty<EMailBodypart>();

            }

        }

        #endregion

        #endregion


        #region ToText(IncludeHeaders = true)

        /// <summary>
        /// Return a text-serialization of this e-mail body part.
        /// </summary>
        /// <param name="IncludeHeaders">Whether to include the e-mail headers.</param>
        public IEnumerable<String> ToText(Boolean IncludeHeaders = true)

            => (IncludeHeaders
                    ? MailHeaders.
                          Select(v => v.Key + ": " + v.Value).
                          Concat(new String[] { "" })
                    : Array.Empty<String>()).

                Concat(_MailBody ?? (IEnumerable<String>) Array.Empty<String>()).

                Concat(NestedBodyparts.
                           SelectMany(bodypart => new String[] { "--" + ContentType.MIMEBoundary }.
                           Concat(bodypart.ToText(true)))).

                Concat(NestedBodyparts.Count() > 0 ? new String[] { "--" + ContentType.MIMEBoundary + "--" } : Array.Empty<String>());

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(MailHeaders.    Count(), " header lines / ",
                             MailBody.       Count() + " body lines; Content-type: ",
                             ContentType, "; ",
                             NestedBodyparts.Count(), " nested mail bodies!");

        #endregion

    }

}
