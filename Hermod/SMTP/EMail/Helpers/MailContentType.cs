/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A e-mail content type with all of its sub-information.
    /// </summary>
    public class MailContentType
    {

        #region Data

        private        readonly  AbstractEMail   _EMailHeader;
        private static readonly  Random          _Random         = new Random(DateTime.UtcNow.Millisecond);

        #endregion

        #region Properties

        #region ContentType

        private MailContentTypes _ContentType;

        /// <summary>
        /// The content type.
        /// </summary>
        public MailContentTypes ContentType
        {

            get
            {
                return _ContentType;
            }

            set
            {
                _ContentType = value;
            }

        }

        #endregion

        #region CharSet

        private String _CharSet;

        /// <summary>
        /// The character set.
        /// </summary>
        public String CharSet
        {

            get
            {
                return _CharSet;
            }

            set
            {
                _CharSet = value;
            }

        }

        #endregion

        #region MIMEBoundary

        private String _MIMEBoundary;

        /// <summary>
        /// The MIME boundary.
        /// </summary>
        public String MIMEBoundary
        {

            get
            {
                return _MIMEBoundary;
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                    _MIMEBoundary = value;
            }

        }

        #endregion

        #region MicAlg

        private String _MicAlg;

        /// <summary>
        /// MicAlg part. Used e.g. for PGP/GPG.
        /// </summary>
        public String MicAlg
        {

            get
            {
                return _MicAlg;
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                    _MicAlg = value;
            }

        }

        #endregion

        #region Protocol

        private String _Protocol;

        /// <summary>
        /// Protocol part. Used e.g. for PGP/GPG.
        /// </summary>
        public String Protocol
        {

            get
            {
                return _Protocol;
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                    _Protocol = value;
            }

        }

        #endregion


        #region Text

        private String _Text;

        public String Text
        {
            get
            {
                return _Text;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region MailContentType(EMailHeader, ContentType = MailContentTypes.unknown)

        /// <summary>
        /// Create a new e-mail content type.
        /// </summary>
        /// <param name="ContentType">The content type.</param>
        public MailContentType(AbstractEMail     EMailHeader,
                               MailContentTypes  ContentType = MailContentTypes.unknown)
        {

            this._EMailHeader  = EMailHeader;
            this._ContentType  = ContentType;

        }

        #endregion

        #region MailContentType(EMailHeader, ContentTypeString)

        /// <summary>
        /// Create a new e-mail content type by parsing the given string.
        /// </summary>
        /// <param name="ContentTypeString"></param>
        public MailContentType(AbstractEMail  EMailHeader,
                               String         ContentTypeString)
        {

            this._EMailHeader  = EMailHeader;
            this._Text         = ContentTypeString;

            var Splitted = ContentTypeString.
                               Split(new String[] { ";" }, StringSplitOptions.RemoveEmptyEntries).
                               Select(v => v.Trim()).
                               ToArray();

            if (Splitted.Length == 0)
                this._ContentType = MailContentTypes.text_plain;

            else if (!Enum.TryParse<MailContentTypes>(Splitted[0].Replace("/", "_").Replace("+", "__"), out this._ContentType))
                this._ContentType = MailContentTypes.text_plain;


            else
                foreach (var SubInformation in Splitted.Skip(1))
                {

                    if (SubInformation.ToLower().StartsWith("charset="))
                    {

                        _CharSet = SubInformation.Substring("charset=".Length).Trim().ToLower();

                        if (_CharSet.StartsWith(@""""))
                            _CharSet = _CharSet.Remove(0, 1);

                        if (_CharSet.EndsWith(@""""))
                            _CharSet = _CharSet.Substring(0, _CharSet.Length - 1);

                        continue;

                    }

                    if (SubInformation.ToLower().StartsWith("boundary="))
                    {

                        _MIMEBoundary = SubInformation.Substring("boundary=".Length).Trim();

                        if (_MIMEBoundary.StartsWith(@""""))
                            _MIMEBoundary = _MIMEBoundary.Remove(0, 1);

                        if (_MIMEBoundary.EndsWith(@""""))
                            _MIMEBoundary = _MIMEBoundary.Substring(0, MIMEBoundary.Length - 1);

                        continue;

                    }

                }

            // Update text-version within the e-mail header
            if (_EMailHeader != null)
                _EMailHeader.SetEMailHeader("Content-Type", this.ToString());

        }

        #endregion

        #endregion


        #region GenerateMIMEBoundary()

        /// <summary>
        /// Generate a valid MIME boundary, if it does not exist.
        /// </summary>
        public MailContentType GenerateMIMEBoundary()
        {

            if (_MIMEBoundary == null)
            {

                _MIMEBoundary = "-8<--" + _ContentType.ToString().Replace("_", "/") + "--8<--" + _Random.GetBytes(12).ToHexString() + "--8<-";

                // Update text-version within the e-mail header
                if (_EMailHeader != null)
                    _EMailHeader.SetEMailHeader("Content-Type", this.ToString());

            }

            return this;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override String ToString()
        {

            return _ContentType.ToString().Replace("__", "-").Replace("_", "/") +
                   (CharSet.     IsNotNullOrEmpty() ?        "; charset=\""  + CharSet      + "\"" : "") +
                   (MIMEBoundary.IsNotNullOrEmpty() ? ";\r\n    boundary=\"" + MIMEBoundary + "\"" : "") +
                   (MicAlg.      IsNotNullOrEmpty() ? ";\r\n    micalg=\""   + MicAlg       + "\"" : "") +
                   (Protocol.    IsNotNullOrEmpty() ? ";\r\n    protocol=\"" + Protocol     + "\"" : "");

        }

        #endregion

    }

}
