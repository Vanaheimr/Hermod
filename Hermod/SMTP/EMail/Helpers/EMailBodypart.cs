/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim@graphdefined.org>
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

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A part of an e-mail body.
    /// </summary>
    /// <remarks>More or less an embedded e-mail for its own.</remarks>
    public class EMailBodypart : AbstractEMail
    {

        #region Properties

        #region Content

        //private readonly IEnumerable<String> _Content;

        /// <summary>
        /// The content of this e-mail body.
        /// </summary>
        public IEnumerable<String> Content
        {
            get
            {
                return _MailBody;
            }
        }

        #endregion

        #region NestedBodyparts

        private readonly IEnumerable<EMailBodypart> _NestedBodyparts;

        public IEnumerable<EMailBodypart> NestedBodyparts
        {
            get
            {
                return _NestedBodyparts;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region EMailBodypart(EMailBuilder, ...)

        /// <summary>
        /// Create a new e-mail bodypart.
        /// </summary>
        public EMailBodypart(AbstractEMailBuilder        EMailBuilder,
                             IEnumerable<String>         Content          = null,
                             IEnumerable<EMailBodypart>  NestedBodyparts  = null)

        {

            // Only copy all e-mail headers starting with "content"...
            base._MailHeaders.AddRange(EMailBuilder.MailHeaders.Where(header => header.Key.ToLower().StartsWith("content")));

            this._MailBody.AddRange(Content);
            this._NestedBodyparts         = NestedBodyparts != null
                                                ? new List<EMailBodypart>(NestedBodyparts)
                                                : (IEnumerable<EMailBodypart>) new EMailBodypart[0];

            if (_NestedBodyparts.Count() > 0)
                this.ContentType.GenerateMIMEBoundary();

        }

        #endregion

        #region EMailBodypart(...)

        /// <summary>
        /// Create a new e-mail bodypart.
        /// </summary>
        public EMailBodypart(Func<AbstractEMail, MailContentType>  ContentTypeBuilder,
                             String                                ContentTransferEncoding  = null,
                             String                                ContentLanguage          = null,
                             String                                ContentDescription       = null,
                             String                                ContentDisposition       = null,
                             String                                MIMEBoundary             = null,
                             IEnumerable<EMailBodypart>            NestedBodyparts          = null,
                             IEnumerable<String>                   Content                  = null)

        {

            this.ContentType              = ContentTypeBuilder(this);
            this.ContentTransferEncoding  = ContentTransferEncoding;
            this.ContentLanguage          = ContentLanguage;
            this.ContentDescription       = ContentDescription;
            this.ContentDisposition       = ContentDisposition;

            if (Content != null)
                this._MailBody.AddRange(Content);

            this._NestedBodyparts         = NestedBodyparts != null
                                                ? new List<EMailBodypart>(NestedBodyparts)
                                                : new List<EMailBodypart>();

            if (_NestedBodyparts.Count() > 0)
                ContentType.GenerateMIMEBoundary();

        }

        #endregion

        #region EMailBodypart(MailText)

        /// <summary>
        /// Parse the e-mail from the given text lines.
        /// </summary>
        /// <param name="MailText">The E-Mail as an enumeration of strings.</param>
        public EMailBodypart(IEnumerable<String> MailText)
            : base(MailText, header => header.ToLower().StartsWith("content"))
        {

            if (ContentType == null)
                ContentType = new MailContentType(this, GetEMailHeader("Content-Type"));

            if (ContentType.Text.Contains("boundary="))
            {

                var MIMEBoundaryCheck    = "--" + ContentType.MIMEBoundary;
                var MIMEBoundaryCheckEnd = "--" + ContentType.MIMEBoundary + "--";

                var ListOfList = new List<List<String>>();
                var List       = new List<String>();

                foreach (var line in MailBody)
                {

                    if (line == MIMEBoundaryCheck ||
                        line == MIMEBoundaryCheckEnd)
                    {
                        ListOfList.Add(List);
                        List = new List<String>();
                    }

                    else
                        List.Add(line);

                }

                this._MailBody.Clear();
                this._MailBody.AddRange(ListOfList[0]);
                _NestedBodyparts = ListOfList.Skip(1).Select(list => new EMailBodypart(list)).ToList();

            }

            else
            {

                //this._MailBody.Clear();
                //this._MailBody.AddRange(MailBody);
                _NestedBodyparts = new EMailBodypart[0];

            }

        }

        #endregion

        #endregion


        #region ToText(IncludeHeaders = true)

        public IEnumerable<String> ToText(Boolean IncludeHeaders = true)
        {

            var AllHeaders = (IncludeHeaders
                                 ? MailHeaders.
                                       Select(v => v.Key + ": " + v.Value).
                                       Concat(new String[] { "" })
                                 : new String[0]).

                             Concat(_MailBody != null ? _MailBody : (IEnumerable<String>) new String[0]).

                             Concat(_NestedBodyparts.
                                        SelectMany(bodypart => new String[] { "--" + ContentType.MIMEBoundary }.
                                        Concat(bodypart.ToText(true)))).

                             Concat(_NestedBodyparts.Count() > 0 ? new String[] { "--" + ContentType.MIMEBoundary + "--" } : new String[0]);

            return AllHeaders;

        }

        #endregion


        public override String ToString()
        {
            return  MailHeaders.Count() + " header lines / " + MailBody.Count() + " body lines; Content-type: " + ContentType + "; " + _NestedBodyparts.Count() + " nested mail bodies!";
        }

    }

}
