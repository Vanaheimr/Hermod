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

    /// <summary>
    /// An e-mail builder.
    /// </summary>
    public abstract class AbstractEMailBuilder
    {

        #region Data

        protected internal Dictionary<String, String>  _AdditionalHeaders;
        protected readonly List<EMailBodypart>         _Attachments;

        #endregion

        #region Properties

        //ToDo: "resentSender", "resentDate", "resentMessageId", "Resent-From", 
        //      "Resent-Reply-To", "Resent-To", "Resent-Cc", "Resent-Bcc",

        #region From

        private EMailAddress _From;

        /// <summary>
        /// The sender of this e-mail.
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
        /// The receivers of this e-mail.
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
        /// Additional receivers of this e-mail.
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
        /// Additional but hidden receivers of this e-mail.
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
        /// The subject of this e-mail.
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

        /// <summary>
        /// The sending timestamp of this e-mail.
        /// </summary>
        public DateTime Date { get; set; }

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

        private readonly MessageId _Reference;

        private readonly List<MessageId> _References;

        #region SecurityLevel

        /// <summary>
        /// The security level of the e-mail.
        /// </summary>
        public EMailSecurity SecurityLevel { get; set; }

        #endregion

        #region SymmetricKeyAlgorithm

        /// <summary>
        /// The symmetric key algorithm to use.
        /// </summary>
        public SymmetricKeyAlgorithms SymmetricKeyAlgorithm { get; set; }

        #endregion

        #region HashAlgorithm

        /// <summary>
        /// The hash algorithm to use.
        /// </summary>
        public HashAlgorithms HashAlgorithm { get; set; }

        #endregion

        #region CompressionAlgorithm

        /// <summary>
        /// The compression algorithm to use.
        /// </summary>
        public CompressionAlgorithms CompressionAlgorithm { get; set; }

        #endregion

        #region Passphrase

        public String Passphrase
        {
            get;
            set;
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

            this._To                    = new EMailAddressList();
            this._ReplyTo               = new EMailAddressList();
            this._Cc                    = new EMailAddressList();
            this._Bcc                   = new EMailAddressList();
            this._Subject               = "";
            this. Date                  = DateTime.Now;
            this._References            = new List<MessageId>();
            this._AdditionalHeaders     = new Dictionary<String, String>();
            this._Attachments           = new List<EMailBodypart>();

            this.SecurityLevel          = EMailSecurity.auto;
            this.SymmetricKeyAlgorithm  = SymmetricKeyAlgorithms.Aes256;
            this.HashAlgorithm          = HashAlgorithms.Sha512;
            this.CompressionAlgorithm   = CompressionAlgorithms.Zip;

        }

        #endregion

        #region AbstractEMailBuilder(TextLines)

        /// <summary>
        /// Parse the given text lines.
        /// </summary>
        /// <param name="TextLines">An enumeration of strings.</param>
        public AbstractEMailBuilder(IEnumerable<String> TextLines)
        {

            var Body         = new List<String>();
            var ReadBody     = false;

            String Property  = null;
            String Value     = null;

            foreach (var Line in TextLines)
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


        #region AddHeaderValues(Key, Value)

        public AbstractEMailBuilder AddHeaderValues(String Key, String Value)
        {

            //FixMe!
            switch (Key.ToLower())
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

        #endregion

        #region AddAttachment(EMailBodypart)

        /// <summary>
        /// Add an attachment to this e-mail.
        /// </summary>
        /// <param name="EMailBodypart">An attachment.</param>
        public AbstractEMailBuilder AddAttachment(EMailBodypart EMailBodypart)
        {
            _Attachments.Add(EMailBodypart);
            return this;
        }

        #endregion



        public static T Parse<T>(String MailText)
            where T : AbstractEMailBuilder, new()
        {

            T Mail;
            if (TryParse<T>(MailText, out Mail))
                return Mail;

            return null;

        }


        public static Boolean TryParse<T>(String MailText, out T Mail)
            where T : AbstractEMailBuilder, new()
        {

            #region Parse MailText

            String[] MailHeaderLine;

            var MailHeaders          = new List<KeyValuePair<String, String>>();
            var Key                  = "";
            var Value                = "";
            var CopyBody             = false;
            var MailBody             = new List<String>();
            var SplitMailHeaderLine  = new Char[1] { ':' };

            foreach (var MailLine in MailText.Split(new String[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
            {

                if (MailLine == "")
                    CopyBody = true;

                if (!CopyBody)
                {

                    if (!MailLine.StartsWith(" "))
                    {

                        if (Key != "")
                        {
                            MailHeaders.Add(new KeyValuePair<String, String>(Key, Value));
                            Key   = "";
                            Value = "";
                        }

                        MailHeaderLine = MailLine.Split(SplitMailHeaderLine, 2);

                        if (MailHeaderLine.Length == 2)
                        {
                            Key   = MailHeaderLine[0].Trim();
                            Value = MailHeaderLine[1].Trim();
                        }

                    }

                    else
                        Value += " " + MailLine.Trim();

                }

                else
                    MailBody.Add(MailLine);

            }

            #endregion

            var _From       = EMailAddress.Parse(MailHeaders.
                                                     Where(kvp => kvp.Key.ToLower() == "from").
                                                     FirstOrDefault().
                                                     Value);

            var _Tos        = new EMailAddressList(MailHeaders.
                                                       Where(kvp => kvp.Key.ToLower() == "to").
                                                       FirstOrDefault().
                                                       Value.
                                                       Split(new Char[] { ',' }).
                                                       Select(v => EMailAddress.Parse(v.Trim())));

            //this._ReplyTo            = new EMailAddressList(MailBuilder.ReplyTo);
            //this._Cc                 = new EMailAddressList(MailBuilder.Cc);
            //this._Bcc                = new EMailAddressList(MailBuilder.Bcc);

            var _Subject    = MailHeaders.
                                  Where(kvp => kvp.Key.ToLower() == "subject").
                                  FirstOrDefault().
                                  Value;

            // this._Date               = MailBuilder.Date;
            // this._MessageId          = MailBuilder.MessageId;
            // this._AdditionalHeaders  = new Dictionary<String, String>();

            //MailBuilder._AdditionalHeaders.ForEach(v => { this._AdditionalHeaders.Add(v.Key, v.Value); });
            //this._Bodypart           = MailBuilder.Body;


            var _MessageId  = MessageId.Parse(MailHeaders.
                                      Where(kvp => kvp.Key.ToLower() == "message-id").
                                      FirstOrDefault().
                                      Value);




            Mail = new T() {
                               From       = _From,
                               To         = _Tos,
                               Subject    = _Subject,
                               MessageId  = _MessageId,
                           //    Text       = MailBody
                           };

            return true;

        }


        #region (protected, abstract) EncodeBodyparts()

        /// <summary>
        /// Encode all nested e-mail body parts.
        /// </summary>
        protected abstract EMailBodypart _EncodeBodyparts();

        #endregion

        #region (internal) EncodeBodyparts()

        /// <summary>
        /// Encode this and all nested e-mail body parts.
        /// </summary>
        internal void EncodeBodyparts()
        {

            var SignTheMail     = false;
            var EncryptTheMail  = false;

            EMailBodypart BodypartToBeSecured = null;

            #region Add attachments, if available...

            if (_Attachments.Count == 0)
                BodypartToBeSecured  = _EncodeBodyparts();

            else
                BodypartToBeSecured  = new EMailBodypart(ContentType:              MailContentTypes.multipart_mixed,
                                                         ContentTransferEncoding:  "8bit",
                                                         Charset:                  "utf-8",
                                                         NestedBodyparts:          new EMailBodypart[] { _EncodeBodyparts() }.
                                                                                       Concat(_Attachments));

            #endregion

            //var ma = new EMailAddress(this.To.First().OwnerName, this.To.First().Address,
            //                          PublicKeyRing: OpenPGP.ReadPublicKeyRing(File.OpenRead("achim_at_ahzf.de.asc")));
            //this.To.Clear();
            //this.To.Add(ma);

            #region Check security settings

            switch (SecurityLevel)
            {

                case EMailSecurity.autosign:

                    if (From.SecretKeyRing != null &&
                        From.SecretKeyRing.Any()   &&
                        Passphrase.IsNotNullOrEmpty())
                        SignTheMail = true;

                    break;


                case EMailSecurity.sign:

                    if (From.SecretKeyRing == null ||
                       !From.SecretKeyRing.Any()   ||
                        Passphrase.IsNullOrEmpty())
                        throw new ApplicationException("Can not sign the e-mail!");

                    SignTheMail = true;

                    break;


                case EMailSecurity.auto:

                    if (From.SecretKeyRing != null &&
                        From.SecretKeyRing.Any()   &&
                        Passphrase.IsNotNullOrEmpty())
                        SignTheMail = true;

                    if (SignTheMail &&
                        (!To.Any() | To.Any(v => v.PublicKeyRing != null && v.PublicKeyRing.Any() )) &&
                        (!Cc.Any() | Cc.Any(v => v.PublicKeyRing != null && v.PublicKeyRing.Any() )))
                        EncryptTheMail = true;

                    break;


                case EMailSecurity.encrypt:

                    if (From.SecretKeyRing == null  ||
                       !From.SecretKeyRing.Any()    ||
                        Passphrase.IsNullOrEmpty()  ||
                        To.Any(v => v.PublicKeyRing == null || !v.PublicKeyRing.Any() ) ||
                        Cc.Any(v => v.PublicKeyRing == null || !v.PublicKeyRing.Any() ))
                        throw new ApplicationException("Can not sign and encrypt the e-mail!");

                    EncryptTheMail = true;

                    break;

            }

            #endregion

            #region Sign the e-mail

            if (SignTheMail & !EncryptTheMail)
            {

                var DataToBeSigned      = BodypartToBeSecured.

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

                                                                           BodypartToBeSecured,

                                                                           new EMailBodypart(ContentType:              MailContentTypes.application_pgp__signature,
                                                                                         //    ContentTransferEncoding:  "8bit",
                                                                                             Charset:                  "utf-8",
                                                                                             ContentDescription:       "OpenPGP digital signature",
                                                                                             ContentDisposition:       ContentDispositions.attachment.ToString() + "; filename=\"signature.asc\"",
                                                                                             Content:                  new MailBodyString(

                                                                                                                           OpenPGP.CreateSignature(new MemoryStream(DataToBeSigned.ToUTF8Bytes()),
                                                                                                                                                   From.SecretKeyRing.First(),
                                                                                                                                                   Passphrase,
                                                                                                                                                   HashAlgorithm: HashAlgorithm).

                                                                                                                                   WriteTo(new MemoryStream(), CloseOutputStream: false).
                                                                                                                                       ToUTF8String())

                                                                                                                       )

                                                                       }
                                         );

            }

            #endregion

            #region Encrypt the e-mail

            else if (SignTheMail & EncryptTheMail)
            {

                var Plaintext   = BodypartToBeSecured.ToText().Aggregate((a, b) => a + "\r\n" + b).ToUTF8Bytes();
                var Ciphertext  = new MemoryStream();

                OpenPGP.EncryptSignAndZip(InputStream:            new MemoryStream(Plaintext),
                                          Length:                 (UInt64) Plaintext.Length,
                                          SecretKey:              From.SecretKeyRing.First(),
                                          Passphrase:             Passphrase,
                                          PublicKey:              To.First().PublicKeyRing.First(),
                                          OutputStream:           Ciphertext,
                                          SymmetricKeyAlgorithm:  SymmetricKeyAlgorithm,
                                          HashAlgorithm:          HashAlgorithm,
                                          CompressionAlgorithm:   CompressionAlgorithm,
                                          ArmoredOutput:          true,
                                          Filename:               "encrypted.asc",
                                          LastModificationTime:   DateTime.UtcNow);

                // MIME Security with OpenPGP (rfc3156, https://tools.ietf.org/html/rfc3156)
                // OpenPGP Message Format     (rfc4880, https://tools.ietf.org/html/rfc4880)
                _Body = new EMailBodypart(ContentType:                 MailContentTypes.multipart_encrypted,
                                          AdditionalContentTypeInfos:  new List<KeyValuePair<String, String>>() {
                                                                           new KeyValuePair<String, String>("protocol", "application/pgp-encrypted"),
                                                                       },
                                          ContentTransferEncoding:     "8bit",
                                          Charset:                     "utf-8",
                                          NestedBodyparts:             new EMailBodypart[] {

                                                                           new EMailBodypart(ContentType:          MailContentTypes.application_pgp__encrypted,
                                                                                             Charset:              "utf-8",
                                                                                             ContentDescription:   "PGP/MIME version identification",
                                                                                             ContentDisposition:   ContentDispositions.attachment.ToString() + "; filename=\"signature.asc\"",
                                                                                             Content:              new MailBodyString("Version: 1")),

                                                                           new EMailBodypart(ContentType:          MailContentTypes.application_octet__stream,
                                                                                             Charset:              "utf-8",
                                                                                             ContentDescription:   "OpenPGP encrypted message",
                                                                                             ContentDisposition:   ContentDispositions.inline.ToString() + "; filename=\"encrypted.asc\"",
                                                                                             Content:              new MailBodyString(Ciphertext.ToArray().ToUTF8String())),

                                                                       }
                                         );

            }

            #endregion


            else
                this._Body = _EncodeBodyparts();

        }

        #endregion

    }

}
