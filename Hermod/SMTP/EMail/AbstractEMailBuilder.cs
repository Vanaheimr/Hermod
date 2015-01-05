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

    public class MailBodyString
    {

        private readonly String[] _Lines;

        public IEnumerable<String> Lines
        {
            get
            {
                return _Lines;
            }
        }


        public MailBodyString(String Lines)
        {
            this._Lines = Lines.Replace("\r\n", "\n").Split(new Char[] { '\n' }, StringSplitOptions.None);
        }

        public MailBodyString(IEnumerable<String> Lines)
        {
            this._Lines = Lines.ToArray();
        }

    }


    /// <summary>
    /// An e-mail builder.
    /// </summary>
    public abstract class AbstractEMailBuilder
    {

        //ToDo: "Resent-From", "Resent-Reply-To", "Resent-To", "Resent-Cc", "Resent-Bcc",
        // readonly MessageIdList   references;
        // MailboxAddress           resentSender;
        // DateTimeOffset           resentDate;
        // string                   resentMessageId;

        #region Data

        protected internal Dictionary<String, String>  _AdditionalHeaders;
        protected readonly List<EMailBodypart>         _Attachments;

        #endregion

        #region Properties

        #region From

        private EMailAddress _From;

        /// <summary>
        /// The sender of the e-mail.
        /// </summary>
        public EMailAddress From
        {

            get
            {
                return _From;
            }

            set
            {
                if (value != null)
                    _From = value;
            }

        }

        #endregion

        #region To

        private readonly EMailAddressList _To;

        /// <summary>
        /// The receivers of the e-mail.
        /// </summary>
        public EMailAddressList To
        {

            get
            {
                return _To;
            }

            set
            {
                if (value != null)
                    _To.Add(value);
            }

        }

        #endregion

        #region ReplyTo

        private readonly EMailAddressList _ReplyTo;

        /// <summary>
        /// The receivers of any reply on this e-mail.
        /// </summary>
        public EMailAddressList ReplyTo
        {

            get
            {
                return _ReplyTo;
            }

            set
            {
                if (value != null)
                    _ReplyTo.Add(value);
            }

        }

        #endregion

        #region Cc

        private readonly EMailAddressList _Cc;

        /// <summary>
        /// Additional receivers of the e-mail.
        /// </summary>
        public EMailAddressList Cc
        {

            get
            {
                return _Cc;
            }

            set
            {
                if (value != null)
                    _Cc.Add(value);
            }

        }

        #endregion

        #region Bcc

        private readonly EMailAddressList _Bcc;

        /// <summary>
        /// Hidden receivers of the e-mail.
        /// </summary>
        public EMailAddressList Bcc
        {

            get
            {
                return _Bcc;
            }

            set
            {
                if (value != null)
                    _Bcc.Add(value);
            }

        }

        #endregion

        #region Subject

        private String _Subject;

        /// <summary>
        /// The subject of the e-mail.
        /// </summary>
        public String Subject
        {

            get
            {
                return _Subject;
            }

            set
            {
                if (value != null && value != String.Empty && value.Trim() != "")
                    _Subject = value.Trim();
            }

        }

        #endregion

        #region Date

        private DateTime _Date;

        public DateTime Date
        {

            get
            {
                return _Date;
            }

            set
            {
                _Date = value;
            }

        }

        #endregion

        #region MessageId

        private MessageId _MessageId;

        /// <summary>
        /// The unique message identification of the e-mail.
        /// </summary>
        public MessageId MessageId
        {

            get
            {
                return _MessageId;
            }

            set
            {
                if (value != null)
                    _MessageId = value;
            }

        }

        #endregion

        #region SecurityLevel

        private EMailSecurity _SecurityLevel;

        /// <summary>
        /// The security level of the e-mail.
        /// </summary>
        public EMailSecurity SecurityLevel
        {

            get
            {
                return _SecurityLevel;
            }

            set
            {
                _SecurityLevel = value;
            }

        }

        #endregion


        #region Body

        protected EMailBodypart _Body;

        /// <summary>
        /// The e-mail body.
        /// </summary>
        public EMailBodypart Body
        {
            get
            {
                return _Body;
            }
        }

        #endregion


        #region AsImmutable

        /// <summary>
        /// Convert this e-mail builder to an immutable e-mail.
        /// </summary>
        public EMail AsImmutable
        {
            get
            {

                if (From.Address.Value.IsNullOrEmpty() ||
                    To.Count() < 1 ||
                    Subject.IsNullOrEmpty())

                    throw new Exception("Invalid email!");

                return new EMail(this);

            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region AbstractEMailBuilder()

        /// <summary>
        /// Create a new e-mail builder.
        /// </summary>
        public AbstractEMailBuilder()
        {

            this._SecurityLevel      = EMailSecurity.auto;

            this._To                 = new EMailAddressList();
            this._ReplyTo            = new EMailAddressList();
            this._Cc                 = new EMailAddressList();
            this._Bcc                = new EMailAddressList();
            this._Subject            = "";
            this._Date               = DateTime.Now;
            this._AdditionalHeaders  = new Dictionary<String, String>();
            this._Attachments        = new List<EMailBodypart>();

        }

        #endregion

        #region AbstractEMailBuilder(Lines)

        public AbstractEMailBuilder(IEnumerable<String> Lines)
        {

            var Body         = new List<String>();
            var ReadBody     = false;

            String Property  = null;
            String Value     = null;

            foreach (var Line in Lines)
            {

                if (ReadBody)
                    Body.Add(Line);

                else if (Line.IsNullOrEmpty())
                {

                    ReadBody = true;

                    if (Property != null)
                        AddHeaderValues(Property, Value);

                    Property = null;
                    Value    = null;

                }

                // The current line is part of a previous line
                else if (Line.StartsWith(" ") ||
                         Line.StartsWith("\t"))
                {

                    // Only if this is the first line ever read!
                    if (Property.IsNullOrEmpty())
                        throw new Exception("Invalid headers found!");

                    Value += " " + Line.Trim();

                }

                else
                {

                    if (Property != null)
                        AddHeaderValues(Property, Value);

                    var Splitted = Line.Split(new Char[] { ':' }, 2, StringSplitOptions.None);

                    Property = Splitted[0].Trim();
                    Value    = Splitted[1].Trim();

                }

            }

        }

        #endregion

        #endregion



        public AbstractEMailBuilder AddHeaderValues(String Key, String Value)
        {

            switch (Key) //.ToLower())
            {

                case "from":
                    this._From = new EMailAddress(SimpleEMailAddress.Parse(Value));
                    break;

                case "to":
                    this._To.Add(new EMailAddress(SimpleEMailAddress.Parse(Value)));
                    break;

                case "cc":
                    this._To.Add(new EMailAddress(SimpleEMailAddress.Parse(Value)));
                    break;

                case "subject":
                    this._Subject = Value;
                    break;

                default: _AdditionalHeaders.Add(Key, Value);
                    break;

            }

            return this;

        }

        #region AddAttachment(EMailBodypart)

        public AbstractEMailBuilder AddAttachment(EMailBodypart EMailBodypart)
        {
            _Attachments.Add(EMailBodypart);
            return this;
        }

        #endregion


        #region (protected, abstract) EncodeBodyparts()

        protected abstract EMailBodypart _EncodeBodyparts();

        #endregion

        #region (internal) EncodeBodyparts()

        internal void EncodeBodyparts()
        {

            if (_Attachments.Count == 0)
            {

                if (SecurityLevel == EMailSecurity.auto & From.PublicKey != null)
                {

                    var BodypartToBeSigned  = _EncodeBodyparts();
                    var aaaa = BodypartToBeSigned.ToText();
                    var DataToBeSigned      = BodypartToBeSigned.

                                                  // Include headers of this MIME body
                                                  // https://tools.ietf.org/html/rfc1847 Security Multiparts for MIME:
                                                  ToText().

                                                  // Any trailing whitespace MUST then be removed from the signed material
                                                  Select(line => line.TrimEnd()).

                                                  // Canonical text format with <CR><LF> line endings
                                                  // https://tools.ietf.org/html/rfc3156 5. OpenPGP signed data
                                                  Aggregate((a, b) => a + "\r\n" + b)

                                                  // Apply Content-Transfer-Encoding

                                                  // Additional new line
                                                  + "\r\n";

                    var sig = OpenPGP.CreateSignature(new MemoryStream(DataToBeSigned.ToUTF8Bytes()),
                                                      From.SecretKey, "jenaopendata2305?!",
                                                      HashAlgorithm: HashAlgorithms.Sha512);

                    // MIME Security with OpenPGP (rfc3156, https://tools.ietf.org/html/rfc3156)
                    // OpenPGP Message Format     (rfc4880, https://tools.ietf.org/html/rfc4880)
                    _Body = new EMailBodypart(ContentType:                 MailContentTypes.multipart_signed,
                                              AdditionalContentTypeInfos:  new List<KeyValuePair<String, String>>() {
                                                                               new KeyValuePair<String, String>("micalg",   "pgp-sha512"),
                                                                               new KeyValuePair<String, String>("protocol", "application/pgp-signature"),
                                                                           },
                                              ContentTransferEncoding:     "8bit",
                                              Charset:                     "utf-8",
                                              NestedBodyparts:             new EMailBodypart[] {
                                                                               BodypartToBeSigned,
                                                                               new EMailBodypart(ContentType:              MailContentTypes.application_pgp__signature,
                                                                                             //    ContentTransferEncoding:  "8bit",
                                                                                                 Charset:                  "utf-8",
                                                                                                 ContentDescription:       "OpenPGP digital signature",
                                                                                                 AdditionalHeaders:        new List<KeyValuePair<String, String>>() {
                                                                                                                               new KeyValuePair<String, String>("Content-Disposition", ContentDispositions.attachment.ToString() + "; filename=\"signature.asc\""),
                                                                                                                           },
                                                                                                 Content:                  new MailBodyString(sig.WriteTo(new MemoryStream(), CloseOutputStream: false).ToUTF8String()))
                                                                           }
                                             );

                }

                else
                    this._Body = _EncodeBodyparts();

            }

            else
            {

                _Body = new EMailBodypart(ContentType:              MailContentTypes.multipart_mixed,
                                          ContentTransferEncoding:  "8bit",
                                          Charset:                  "utf-8",
                                          NestedBodyparts:          new EMailBodypart[] { _EncodeBodyparts() }.
                                                                        Concat(_Attachments)
                                         );

            }

        }

        #endregion

    }

}
