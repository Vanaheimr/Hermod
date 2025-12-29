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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    public static class Tools
    {

        #region TryParseMail(MailText, out MailHeaders, out MailBody)

        public static Boolean TryParseMail(String                                    MailText,
                                           out ConcurrentDictionary<String, String>  MailHeaders,
                                           out List<String>                          MailBody)

            => Tools.TryParseMail(
                   MailText.Split([ "\r\n", "\r", "\n" ], StringSplitOptions.None),
                   out MailHeaders,
                   out MailBody
               );

        #endregion

        #region TryParseMail(MailText, out MailHeaders, out MailBody)

        public static Boolean TryParseMail(IEnumerable<String>                       MailText,
                                           out ConcurrentDictionary<String, String>  MailHeaders,
                                           out List<String>                          MailBody)
        {

            try
            {

                MailHeaders     = [];
                var MailHeader  = MailText.TakeWhile(line => line.IsNeitherNullNorEmpty()).ToList();

                var Key         = "";
                var Value       = "";
                var space       = ' ';
                var tab         = '\t';
                var splitter    = new Char[1] { ':' };
                String[] Splitted;

                foreach (var mailHeaderLine in MailHeader)
                {

                    if (mailHeaderLine.StartsWith(space) ||
                        mailHeaderLine.StartsWith(tab))
                    {
                        Value += " " + mailHeaderLine.Trim();
                    }

                    else
                    {

                        if (Key != "")
                        {
                            MailHeaders.TryAdd(Key, Value);
                            Key   = "";
                            Value = "";
                        }

                        Splitted = mailHeaderLine.Split(splitter, 2);
                        Key      = Splitted[0].Trim();
                        Value    = Splitted[1].Trim();

                    }

                }

                if (Key != "")
                    MailHeaders.TryAdd(Key, Value);

                MailBody = [.. MailText.SkipWhile(line => line.IsNeitherNullNorEmpty()).Skip(1)];

            }
            catch
            {
                MailHeaders = [];
                MailBody    = [];
                return false;
            }

            return true;

        }

        #endregion


        public static T SetEMailHeader<T>(this T EMail, String Key, String Value)
            where T : AbstractEMail
        {

            EMail.RemoveEMailHeader(Key);
            EMail.AddEMailHeader(Key, Value);

            return EMail;

        }

    }


    /// <summary>
    /// An abstract E-Mail.
    /// </summary>
    public abstract partial class AbstractEMail
    {

        #region Properties

        #region MailText

        /// <summary>
        /// The E-Mail as enumeration of strings.
        /// </summary>
        public IEnumerable<String>                   MailText       { get; } = [];

        #endregion

        #region MailHeaders

        /// <summary>
        /// The E-Mail header as enumeration of strings.
        /// </summary>
        public ConcurrentDictionary<String, String>  MailHeaders    { get; } = [];

        #endregion

        #region MailBody

        /// <summary>
        /// The E-Mail body as enumeration of strings.
        /// </summary>
        public IEnumerable<String>                   MailBody       { get; protected set; } = [];

        #endregion


        #region ContentType

        private MailContentType? contentType;

        /// <summary>
        /// The content type of the e-mail.
        /// </summary>
        public MailContentType? ContentType
        {

            get
            {

                contentType ??= MailContentType.Parse(this.GetEMailHeader("Content-Type"));

                return contentType;

            }

            set
            {
                if (value is not null)
                {
                    contentType = value;
                    this.SetEMailHeader("Content-Type", contentType.ToString());
                }
            }

        }

        #endregion

        #region ContentTransferEncoding

        public String? ContentTransferEncoding
        {

            get
            {
                return this.GetEMailHeader("Content-Transfer-Encoding");
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                    this.SetEMailHeader("Content-Transfer-Encoding", value);
            }

        }

        #endregion

        #region ContentLanguage

        public String? ContentLanguage
        {

            get
            {
                return this.GetEMailHeader("Content-Language");
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                    this.SetEMailHeader("Content-Language", value);
            }

        }

        #endregion

        #region ContentDescription

        /// <summary>
        /// A short text to decripte the content of the e-mail body(part).
        /// </summary>
        public String? ContentDescription
        {

            get
            {
                return this.GetEMailHeader("Content-Description");
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                    this.SetEMailHeader("Content-Description", value);
            }

        }

        #endregion

        #region ContentDisposition

        /// <summary>
        /// Content-Disposition
        /// </summary>
        public String? ContentDisposition
        {

            get
            {
                return this.GetEMailHeader("Content-Disposition");
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                    this.SetEMailHeader("Content-Disposition", value);
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
        { }

        #endregion

        #region AbstractEMail(EMail)

        /// <summary>
        /// Parse the e-mail from the given e-mail.
        /// </summary>
        /// <param name="EMail">An e-mail.</param>
        /// <param name="MailTextFilter">A filter delegate for filtering e-mail headers.</param>
        public AbstractEMail(EMail                   EMail,
                             Func<String, Boolean>?  MailTextFilter   = null)
            : this()
        {

            if (EMail.ToText() is not null)
            {

                this.MailText = EMail.ToText();

                if (Tools.TryParseMail(this.MailText, out var mailHeaders, out var mailBody))
                {
                    this.MailHeaders  = mailHeaders;
                    this.MailBody     = mailBody;
                }

            }

            if (MailTextFilter is not null)
                MailHeaders = new ConcurrentDictionary<String, String>(
                                  MailHeaders.Where(header => MailTextFilter(header.Key.ToLower()))
                              );

        }

        #endregion

        #region AbstractEMail(MailText, MailTextFilter = null)

        /// <summary>
        /// Parse the e-mail from the given text lines.
        /// </summary>
        /// <param name="MailText">The E-Mail as an enumeration of strings.</param>
        /// <param name="MailTextFilter">A filter delegate for filtering e-mail headers.</param>
        public AbstractEMail(IEnumerable<String>     MailText,
                             Func<String, Boolean>?  MailTextFilter   = null)
            : this()
        {

            if (MailText is not null)
            {

                this.MailText = MailText;

                if (Tools.TryParseMail(this.MailText, out var mailHeaders, out var mailBody))
                {
                    this.MailHeaders  = mailHeaders;
                    this.MailBody     = mailBody;
                }

            }

            if (MailTextFilter is not null)
                MailHeaders = new ConcurrentDictionary<String, String>(
                                  MailHeaders.Where(header => MailTextFilter(header.Key.ToLower()))
                              );

        }

        #endregion

        #region AbstractEMail(MailHeaders)

        /// <summary>
        /// Create a new e-mail.
        /// </summary>
        /// <param name="MailHeaders">The E-Mail headers.</param>
        public AbstractEMail(IEnumerable<KeyValuePair<String, String>> MailHeaders)
            : this()
        {

            foreach (var mailHeader in MailHeaders)
                this.MailHeaders.TryAdd(mailHeader.Key, mailHeader.Value);

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
                    if (SimpleEMailAddress.TryParse(Value, out _))
                        this.MailHeaders.TryAdd(Key, Value);
                    break;

                case "to":
                case "cc":
                    if (EMailAddressList.Parse(Value) is not null)
                        this.MailHeaders.TryAdd(Key, Value);
                    break;

                case "subject":
                    this.MailHeaders.TryAdd(Key, Value);
                    break;

                default: MailHeaders.TryAdd(Key, Value);
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

            var kvp = MailHeaders.
                          Where(kvp => kvp.Key.Equals(Key, StringComparison.CurrentCultureIgnoreCase)).
                          FirstOrDefault();

            if (kvp.Key.IsNotNullOrEmpty())
                return kvp.Value;

            return String.Empty;

        }

        #endregion

        #region TryGetEMailHeader(Key, out Value)

        /// <summary>
        /// Get the E-Mail header value for the given key.
        /// </summary>
        /// <param name="Key">An E-Mail header key.</param>
        /// <param name="Value">The E-Mail header value.</param>
        public Boolean TryGetEMailHeader(String                           Key,
                                         [NotNullWhen(true)] out String?  Value)
        {

            if (MailHeaders.TryGetValue(Key, out var value))
            {
                Value = value;
                return true;
            }

            Value = null;
            return false;

        }

        #endregion

        #region RemoveEMailHeader(Key)

        /// <summary>
        /// Removes a key-value pair from the e-mail header.
        /// </summary>
        /// <param name="Key">The property key.</param>
        public AbstractEMail RemoveEMailHeader(String Key)
        {

            MailHeaders.TryRemove(Key, out _);

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

            MailHeaders.TryRemove(new KeyValuePair<String, String>(Key, Value));

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

            foreach (var kvp in MailHeaders)
            {
                if (IncludeKeyValuePairs(kvp))
                    MailHeaders.TryRemove(kvp.Key, out _);
            }

            return this;

        }

        #endregion


    }

}
