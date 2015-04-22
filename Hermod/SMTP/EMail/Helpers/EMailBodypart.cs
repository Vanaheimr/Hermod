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
    /// A part of an e-mail body.
    /// </summary>
    public class EMailBodypart
    {

        #region Data

        private   static   readonly Random                      _Random = new Random();

        #endregion

        #region Properties

        #region ContentType

        private MailContentTypes _ContentType;

        public MailContentTypes ContentType
        {
            get
            {
                return _ContentType;
            }
        }

        #endregion

        #region ContentTransferEncoding

        private String _ContentTransferEncoding;

        public String ContentTransferEncoding
        {
            get
            {
                return _ContentTransferEncoding;
            }
        }

        #endregion

        #region Charset

        private String _Charset;

        public String Charset
        {
            get
            {
                return _Charset;
            }
        }

        #endregion

        #region ContentLanguage

        private String _ContentLanguage;

        public String ContentLanguage
        {
            get
            {
                return _ContentLanguage;
            }
        }

        #endregion

        #region ContentDescription

        private String _ContentDescription;

        /// <summary>
        /// Content-Description
        /// </summary>
        public String ContentDescription
        {
            get
            {
                return _ContentDescription;
            }
        }

        #endregion

        #region ContentDisposition

        private String _ContentDisposition;

        /// <summary>
        /// Content-Disposition
        /// </summary>
        public String ContentDisposition
        {
            get
            {
                return _ContentDisposition;
            }
        }

        #endregion

        #region MIMEBoundary

        private String _MIMEBoundary;

        public String MIMEBoundary
        {
            get
            {
                return _MIMEBoundary;
            }
        }

        #endregion

        #region NestedBodyparts

        internal readonly List<EMailBodypart> _NestedBodyparts;

        public IEnumerable<EMailBodypart> NestedBodyparts
        {
            get
            {
                return _NestedBodyparts;
            }
        }

        #endregion

        protected internal readonly Dictionary<String, String> _AdditionalContentTypeInfos;

        protected internal readonly Dictionary<String, String> _AdditionalHeaders;


        #region Headers

        public IEnumerable<String> Headers
        {

            get
            {

                var _AdditionalContentTypes = _AdditionalContentTypeInfos.
                                                  Select(kvp => "; " + kvp.Key + "=\"" + kvp.Value + "\"").
                                                  Aggregate();

                return new String[] {

                       // Content-Type:  multipart/alternative; charset=utf-8; boundary="--FRONTEX--"
                       "Content-Type: "              + _ContentType.ToString().Replace("__", "-").Replace("_", "/") +
                                                       "; charset=" + _Charset +
                                                       (_NestedBodyparts.Any() ? "; boundary=\"" + MIMEBoundary + "\"" : "") +
                                                       _AdditionalContentTypes,

                       _ContentTransferEncoding != null ? "Content-Transfer-Encoding: " + _ContentTransferEncoding : null,
                       _ContentLanguage.   IsNotNullOrEmpty() ? "Content-Language: "    + _ContentLanguage    : null,
                       _ContentDescription.IsNotNullOrEmpty() ? "Content-Description: " + _ContentDescription : null,
                       _ContentDisposition.IsNotNullOrEmpty() ? "Content-Disposition: " + _ContentDisposition : null

                }.
                Where(line => line.IsNotNullOrEmpty()).

                Concat(_AdditionalHeaders.
                           Where     (kvp => kvp.Key.  IsNotNullOrEmpty() &&
                                             kvp.Value.IsNotNullOrEmpty()).
                           SafeSelect(kvp => kvp.Key + ": " + kvp.Value));

            }

        }

        #endregion

        #region Content

        private MailBodyString _Content;

        public IEnumerable<String> Content
        {
            get
            {
                return _Content.Lines;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new e-mail bodypart.
        /// </summary>
        public EMailBodypart(MailContentTypes                           ContentType,
                             String                                     ContentTransferEncoding     = null,
                             String                                     Charset                     = "utf-8",
                             String                                     ContentLanguage             = null,
                             String                                     ContentDescription          = null,
                             String                                     ContentDisposition          = null,
                             String                                     MIMEBoundary                = null,
                             IEnumerable<KeyValuePair<String, String>>  AdditionalContentTypeInfos  = null,
                             IEnumerable<KeyValuePair<String, String>>  AdditionalHeaders           = null,
                             IEnumerable<EMailBodypart>                 NestedBodyparts             = null,
                             MailBodyString                             Content                     = null)

        {

            this._ContentType                 = ContentType;
            this._ContentTransferEncoding     = ContentTransferEncoding;//.IsNotNullOrEmpty() ? ContentTransferEncoding : "8bit";
            this._Charset                     = Charset.                IsNotNullOrEmpty() ? Charset                 : "utf-8";
            this._ContentLanguage             = ContentLanguage;
            this._ContentDescription          = ContentDescription;
            this._ContentDisposition          = ContentDisposition;
            this._MIMEBoundary                = MIMEBoundary.           IsNotNullOrEmpty() ? MIMEBoundary            : "-8<--" +
                                                    _ContentType.ToString().Replace("_", "/") + "--8<--" +
                                                    _Random.GetBytes(12).ToHexString() + "--8<-";

            this._AdditionalContentTypeInfos  = new Dictionary<String, String>();
            if (AdditionalContentTypeInfos != null)
                AdditionalContentTypeInfos.ForEach(kvp => this._AdditionalContentTypeInfos.Add(kvp.Key, kvp.Value));

            this._AdditionalHeaders           = new Dictionary<String, String>();
            if (AdditionalHeaders != null)
                AdditionalHeaders.ForEach(kvp => this._AdditionalHeaders.Add(kvp.Key, kvp.Value));

            this._NestedBodyparts             = NestedBodyparts != null
                                                   ? new List<EMailBodypart>(NestedBodyparts)
                                                   : new List<EMailBodypart>();

            this._Content                     = Content;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return MIMEBoundary;
        }

        #endregion


        #region ToText(IncludeHeaders = true)

        public IEnumerable<String> ToText(Boolean IncludeHeaders = true)
        {

            var AllHeaders = IncludeHeaders   ? Headers      : new String[0];
            var Boundary   = _Content != null ? String.Empty : "--" + MIMEBoundary;

            if (_Content != null)
                AllHeaders = AllHeaders.
                                 Concat(new String[] { Boundary }).
                                 Concat(_Content.Lines).
                                 Concat(new String[] { "" });

            else
                AllHeaders = AllHeaders.
                                 Concat(_NestedBodyparts.
                                            SelectMany(bodypart => new String[] { "", Boundary }.
                                                                   Concat(bodypart.ToText(true)))).
                                 Concat(new String[] { "", "--" + MIMEBoundary + "--" });

            return AllHeaders;

        }

        #endregion

    }

}
