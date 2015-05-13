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
using System.IO;
using System.Linq;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.BouncyCastle;
using Org.BouncyCastle.Bcpg;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Services.Mail
{

    public static class Tools
    {

        #region ParseMail(MailText, out MailHeaders, out MailBody)

        public static void ParseMail(String                                  MailText,
                                     out List<KeyValuePair<String, String>>  MailHeaders,
                                     out List<String>                        MailBody)
        {

            Tools.ParseMail(MailText.Split(new String[] { "\r\n", "\r", "\n" }, StringSplitOptions.None),
                            out MailHeaders,
                            out MailBody);

        }

        #endregion

        #region ParseMail(MailText, out MailHeaders, out MailBody)

        public static void ParseMail(IEnumerable<String>                     MailText,
                                     out List<KeyValuePair<String, String>>  MailHeaders,
                                     out List<String>                        MailBody)
        {

            MailHeaders     = new List<KeyValuePair<String, String>>();
            var MailHeader  = MailText.TakeWhile(line => line.IsNeitherNullNorEmpty()).ToList();

            var Key         = "";
            var Value       = "";
            var Splitter    = new Char[1] { ':' };
            String[] Splitted;

            foreach (var MailHeaderLine in MailHeader)
            {

                if (MailHeaderLine.StartsWith(" ") ||
                    MailHeaderLine.StartsWith("\t"))
                    Value += " " + MailHeaderLine.Trim();

                else
                {

                    if (Key != "")
                    {
                        MailHeaders.Add(new KeyValuePair<String, String>(Key, Value));
                        Key   = "";
                        Value = "";
                    }

                    Splitted = MailHeaderLine.Split(Splitter, 2);
                    Key      = Splitted[0].Trim();
                    Value    = Splitted[1].Trim();

                }

            }

            if (Key != "")
                MailHeaders.Add(new KeyValuePair<String, String>(Key, Value));

            MailBody = MailText.SkipWhile(line => line.IsNeitherNullNorEmpty()).Skip(1).ToList();

        }

        #endregion


        public static T SetEMailHeader<T>(this T Thing, String Key, String Value)
            where T : AbstractEMail
        {

            Thing.RemoveEMailHeader(Key);
            Thing.AddEMailHeader(Key, Value);

            return Thing;

        }

    }


    /// <summary>
    /// An E-Mail builder.
    /// </summary>
    public abstract class AbstractEMail
    {

        #region Properties

        #region MailText

        private readonly IEnumerable<String> _MailText;

        /// <summary>
        /// The E-Mail as enumeration of strings.
        /// </summary>
        public IEnumerable<String> MailText
        {
            get
            {
                return _MailText;
            }
        }

        #endregion

        #region MailHeaders

        private readonly List<KeyValuePair<String, String>> _MailHeaders;

        /// <summary>
        /// The E-Mail header as enumeration of strings.
        /// </summary>
        public IEnumerable<KeyValuePair<String, String>> MailHeaders
        {
            get
            {
                return _MailHeaders.Where(v => v.Key.IsNotNullOrEmpty());
            }
        }

        #endregion

        #region MailBody

        private readonly List<String> _MailBody;

        /// <summary>
        /// The E-Mail body as enumeration of strings.
        /// </summary>
        public IEnumerable<String> MailBody
        {
            get
            {
                return _MailBody;
            }
        }

        #endregion


        #region ContentType

        protected MailContentType _ContentType;

        public MailContentType ContentType
        {

            get
            {
                return _ContentType;
            }

            set
            {
                if (value != null)
                {
                    _ContentType = value;
                    this.SetEMailHeader("Content-Type", _ContentType.ToString());
                }
            }

        }

        #endregion

        #region ContentTransferEncoding

        protected String _ContentTransferEncoding;

        public String ContentTransferEncoding
        {
            get
            {
                return _ContentTransferEncoding;
            }
        }

        #endregion

        #region ContentLanguage

        protected String _ContentLanguage;

        public String ContentLanguage
        {
            get
            {
                return _ContentLanguage;
            }
        }

        #endregion

        #region ContentDescription

        protected String _ContentDescription;

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

        protected String _ContentDisposition;

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

        #endregion

        #region Constructor(s)

        #region AbstractEMail()

        /// <summary>
        /// Parse the e-mail from the given text lines.
        /// </summary>
        public AbstractEMail()
            : this(new String[0])
        { }

        #endregion

        #region AbstractEMail(MailText)

        /// <summary>
        /// Parse the e-mail from the given text lines.
        /// </summary>
        /// <param name="MailText">The E-Mail as an enumeration of strings.</param>
        public AbstractEMail(IEnumerable<String> MailText)
        {

            this._MailText      = MailText != null ? MailText : new String[0];
            this._MailHeaders   = new List<KeyValuePair<String, String>>();
            this._MailBody      = new List<String>();

            Tools.ParseMail(MailText, out _MailHeaders, out _MailBody);

        }

        #endregion

        #endregion


        #region AddEMailHeader(Key, Value)

        /// <summary>
        /// Adds a key-value pair to the e-mail header.
        /// For well-known headers, e.g. FROM, TO, ... simple validations checks are performed.
        /// </summary>
        /// <remarks>For an e-mail the order of the header lines is often important! Keys are NOT unique, e.g. RECEIVED!</remarks>
        /// <param name="Key">The property key.</param>
        /// <param name="Value">The property value.</param>
        public AbstractEMail AddEMailHeader(String Key, String Value)
        {

            switch (Key.ToLower())
            {

                case "from":
                    if (SimpleEMailAddress.IsValid(Value))
                        this._MailHeaders.Add(new KeyValuePair<String, String>(Key, Value));
                    break;

                case "to":
                    if (SimpleEMailAddress.IsValid(Value))
                        this._MailHeaders.Add(new KeyValuePair<String, String>(Key, Value));
                    break;

                case "cc":
                    if (SimpleEMailAddress.IsValid(Value))
                        this._MailHeaders.Add(new KeyValuePair<String, String>(Key, Value));
                    break;

                case "subject":
                    this._MailHeaders.Add(new KeyValuePair<String, String>(Key, Value));
                    break;

                default: _MailHeaders.Add(new KeyValuePair<String, String>(Key, Value));
                    break;

            }

            return this;

        }

        #endregion

        #region GetEMailHeader(Key)

        /// <summary>
        /// Get the E-Mail header value for the given key.
        /// </summary>
        /// <param name="Key">An E-Mail header key.</param>
        public String GetEMailHeader(String Key)
        {

            var Property = MailHeaders.
                               Where(kvp => kvp.Key.ToLower() == Key.ToLower()).
                               FirstOrDefault();

            if (Property.Key.IsNotNullOrEmpty())
                return Property.Value;

            return String.Empty;

        }

        #endregion

        #region RemoveEMailHeader(Key)

        /// <summary>
        /// Removes a key-value pair from the e-mail header.
        /// </summary>
        /// <param name="Key">The property key.</param>
        public AbstractEMail RemoveEMailHeader(String Key)
        {

            lock (_MailHeaders)
            {
                var ToRemove = _MailHeaders.Where(v => v.Key == Key).ToArray();
                ToRemove.ForEach(v => _MailHeaders.Remove(v));
            }

            return this;

        }

        #endregion

        #region RemoveEMailHeader(Key, Value)

        /// <summary>
        /// Removes a key-value pair from the e-mail header.
        /// </summary>
        /// <param name="Key">The property key.</param>
        /// <param name="Value">The property value.</param>
        public AbstractEMail RemoveEMailHeader(String Key, String Value)
        {

            lock (_MailHeaders)
            {
                var ToRemove = _MailHeaders.Where(v => v.Key == Key && v.Value == Value).ToArray();
                ToRemove.ForEach(v => _MailHeaders.Remove(v));
            }

            return this;

        }

        #endregion

        #region RemoveEMailHeader(IncludeKeyValuePairs)

        /// <summary>
        /// Removes a key-value pair from the e-mail header.
        /// </summary>
        /// <param name="IncludeKeyValuePairs">The e-mail headers to include.</param>
        public AbstractEMail RemoveEMailHeader(Func<KeyValuePair<String, String>, Boolean> IncludeKeyValuePairs)
        {

            lock (_MailHeaders)
            {
                var ToRemove = _MailHeaders.Where(IncludeKeyValuePairs).ToArray();
                ToRemove.ForEach(v => _MailHeaders.Remove(v));
            }

            return this;

        }

        #endregion


    }

}
