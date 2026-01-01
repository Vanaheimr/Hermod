/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// An abstract E-Mail.
    /// </summary>
    public abstract partial class AbstractEMail
    {

        /// <summary>
        /// An abstract e-mail builder.
        /// </summary>
        public abstract class Builder
        {

            #region Data

            protected static readonly String[] TextLineSplitter = [ "\r\n", "\r", "\n" ];

            #endregion

            #region Properties

            /// <summary>
            /// The E-Mail as enumeration of strings.
            /// </summary>
            public List<String>                          MailText                   { get; } = [];

            /// <summary>
            /// The E-Mail header as enumeration of strings.
            /// </summary>
            public ConcurrentDictionary<String, String>  MailHeaders                { get; } = [];

            /// <summary>
            /// The E-Mail body as enumeration of strings.
            /// </summary>
            public List<String>                          MailBody                   { get; } = [];



            /// <summary>
            /// The content type of the e-mail.
            /// </summary>
            public MailContentType?                      ContentType                { get; set; }

            /// <summary>
            /// The optional content transfer encoding of the e-mail.
            /// </summary>
            public String?                               ContentTransferEncoding    { get; set; }

            /// <summary>
            /// The optional content language of the e-mail.
            /// </summary>
            public String?                               ContentLanguage            { get; set; }

            /// <summary>
            /// The optional content description of the e-mail.
            /// </summary>
            public String?                               ContentDescription         { get; set; }

            /// <summary>
            /// The optional content disposition of the e-mail.
            /// </summary>
            public String?                               ContentDisposition         { get; set; }





            /// <summary>
            /// The sender of this e-mail.
            /// </summary>
            public EMailAddress?                         From                       { get; set; }

            /// <summary>
            /// The receivers of this e-mail.
            /// </summary>
            public EMailAddressListBuilder               To                         { get; set; } = EMailAddressListBuilder.Empty;

            /// <summary>
            /// Additional receivers of this e-mail.
            /// </summary>
            public EMailAddressListBuilder               Cc                         { get; set; } = EMailAddressListBuilder.Empty;

            /// <summary>
            /// Additional but hidden receivers of this e-mail.
            /// </summary>
            public EMailAddressListBuilder               Bcc                        { get; set; } = EMailAddressListBuilder.Empty;

            /// <summary>
            /// The options receivers of any reply on this e-mail.
            /// </summary>
            public EMailAddressListBuilder               ReplyTo                    { get; set; } = EMailAddressListBuilder.Empty;

            /// <summary>
            /// The subject of this e-mail.
            /// </summary>
            public String?                               Subject                    { get; set; }

            /// <summary>
            /// The sending timestamp of this e-mail.
            /// </summary>
            public DateTimeOffset?                       Date                       { get; set; }

            /// <summary>
            /// The unique message identification of the e-mail.
            /// </summary>
            public Message_Id?                           MessageId                  { get; set; }

            /// <summary>
            /// The optional message identification this e-mail is a response to.
            /// </summary>
            public Message_Id?                           Reference                  { get; set; }

            /// <summary>
            /// The optional message identifications this e-mail is a response to.
            /// </summary>
            public List<Message_Id>                      References                 { get; set; } = [];




            /// <summary>
            /// The unique identification of the mailing list.
            /// </summary>
            public ListId?                               ListId                    { get; set; }

            /// <summary>
            /// The e-mail address of the mailing list for posting new e-mails.
            /// </summary>
            public SimpleEMailAddress?                   ListPost                  { get; set; }





            //ToDo: "resentSender", "resentDate", "resentMessageId", "Resent-From", 
            //      "Resent-Reply-To", "Resent-To", "Resent-Cc", "Resent-Bcc",



            /// <summary>
            /// The security level of the e-mail.
            /// </summary>
            public EMailSecurity             SecurityLevel            { get; set; }

            /// <summary>
            /// The symmetric key algorithm to use.
            /// </summary>
            public SymmetricKeyAlgorithmTag  SymmetricKeyAlgorithm    { get; set; }

            /// <summary>
            /// The hash algorithm to use.
            /// </summary>
            public HashAlgorithmTag          HashAlgorithm            { get; set; }

            /// <summary>
            /// The compression algorithm to use.
            /// </summary>
            public CompressionAlgorithmTag   CompressionAlgorithm     { get; set; }

            /// <summary>
            /// The passphrase.
            /// </summary>
            public String?                   Passphrase               { get; set; }


            #region Body

            private EMailBodypart? body;

            /// <summary>
            /// The e-mail body.
            /// </summary>
            public EMailBodypart? Body
            {

                get
                {
                    return body;
                }

                set
                {
                    if (value is not null)
                        body = value;
                }

            }

            #endregion

            public List<EMailBodypart>       Attachments              { get; } = [];

            #endregion

            #region Constructor(s)

            #region Builder()

            /// <summary>
            /// Create a new e-mail builder.
            /// </summary>
            public Builder()
            {

                this.To                     = EMailAddressListBuilder.Empty;
                this.ReplyTo                = EMailAddressListBuilder.Empty;
                this.Cc                     = EMailAddressListBuilder.Empty;
                this.Bcc                    = EMailAddressListBuilder.Empty;
                this.Subject                = "";
                this.Date                   = Timestamp.Now;
                this.References             = [];
                this.Attachments            = [];

                this.SecurityLevel          = EMailSecurity.auto;
                this.SymmetricKeyAlgorithm  = SymmetricKeyAlgorithmTag.Aes256;
                this.HashAlgorithm          = HashAlgorithmTag.Sha512;
                this.CompressionAlgorithm   = CompressionAlgorithmTag.Zip;

                MailHeaders.TryAdd("MIME-Version", "1.0");

            }

            #endregion

            #region Builder(EMail)

            /// <summary>
            /// Parse the e-mail from the given e-mail.
            /// </summary>
            /// <param name="EMail">An e-mail.</param>
            public Builder(EMail EMail)
                : this()
            {

                this.To                     = EMail.To;
                this.ReplyTo                = EMail.ReplyTo;
                this.Cc                     = EMail.Cc;
                this.Bcc                    = EMail.Bcc;
                this.Subject                = EMail.Subject;
                this.Date                   = EMail.Date;
                //this.References             = EMail.References.ToList();
                //this.Attachments            = ;

                this.body  = new EMailBodypart(EMail.ToText());

            }

            #endregion

            //#region Builder(MailText)

            ///// <summary>
            ///// Parse the e-mail from the given text lines.
            ///// </summary>
            ///// <param name="MailText">The E-Mail as an enumeration of strings.</param>
            //public Builder(IEnumerable<String> MailText)
            //{

            //    this.To                     = EMailAddressListBuilder.Empty;
            //    this.ReplyTo                = EMailAddressListBuilder.Empty;
            //    this.Cc                     = EMailAddressListBuilder.Empty;
            //    this.Bcc                    = EMailAddressListBuilder.Empty;
            //    this.Subject                = "";
            //    this.Date                   = Timestamp.Now;
            //    this.References             = [];
            //    this.Attachments            = [];

            //    this.SecurityLevel          = EMailSecurity.auto;
            //    this.SymmetricKeyAlgorithm  = SymmetricKeyAlgorithmTag.Aes256;
            //    this.HashAlgorithm          = HashAlgorithmTag.Sha512;
            //    this.CompressionAlgorithm   = CompressionAlgorithmTag.Zip;

            //    this.body                   = new EMailBodypart(MailText);

            //}

            //#endregion

            #endregion


            #region GetEMailHeader(Key)

            /// <summary>
            /// Get the E-Mail header value for the given key.
            /// </summary>
            /// <param name="Key">An E-Mail header key.</param>
            public String? GetEMailHeader(String Key)
            {

                if (MailHeaders.TryGetValue(Key, out var value))
                    return value;

                return null;

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

            #region SetEMailHeader(Key, Value)

            public void SetEMailHeader(String Key, String Value)
            {
                if (!MailHeaders.TryAdd(Key, Value))
                    MailHeaders[Key] = Value;
            }

            #endregion


            #region AddAttachment(EMailBodypart)

            /// <summary>
            /// Add an attachment to this e-mail.
            /// </summary>
            /// <param name="EMailBodypart">An attachment.</param>
            public T AddAttachment<T>(EMailBodypart EMailBodypart)
                where T : Builder
            {

                Attachments.Add(EMailBodypart);

                return (T) this;

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

                    if (From is null                       ||
                        From.Address.Value.IsNullOrEmpty() ||
                        !To.Any()                          ||
                        Subject.IsNullOrEmpty())
                    {
                        throw new Exception("Invalid email!");
                    }

                    if (From is not null)
                        SetEMailHeader("From",                       From.ToString());

                    if (To. Any())
                        SetEMailHeader("To",                         To.  ToString());

                    if (Cc. Any())
                        SetEMailHeader("Cc",                         Cc.  ToString());

                    if (Bcc.Any())
                        SetEMailHeader("Bcc",                        Bcc. ToString());

                    if (ReplyTo.Any())
                        SetEMailHeader("Reply-To",                   ReplyTo.ToString());

                    if (Subject.IsNotNullOrEmpty())
                        SetEMailHeader("Subject",                    Subject);

                    if (Date.HasValue)
                        SetEMailHeader("Date",                       Date.Value.ToString("r"));

                    if (MessageId.HasValue)
                        SetEMailHeader("Message-ID",                 MessageId.Value.ToString());

                    if (Reference.HasValue)
                        SetEMailHeader("In-Reply-To",                Reference.Value.ToString());

                    if (References.Count > 0)
                        SetEMailHeader("References",                 References.AggregateWith(" "));

                    if (ListId             is not null)
                        SetEMailHeader("List-ID",                    ListId.Value.ToString());

                    if (ListPost           is not null)
                        SetEMailHeader("List-Post",                  $"<mailto:{ListPost}>");



                    if (ContentType        is not null)
                        SetEMailHeader("Content-Type",               ContentType.ToString());

                    if (ContentTransferEncoding.IsNotNullOrEmpty())
                        SetEMailHeader("Content-Transfer-Encoding",  ContentTransferEncoding);

                    if (ContentLanguage    is not null)
                        SetEMailHeader("Content-Language",           ContentLanguage);

                    if (ContentDescription is not null)
                        SetEMailHeader("Content-Description",        ContentDescription);


                    return new EMail(this);

                }
            }

            #endregion

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
            internal Builder EncodeBodyparts()
            {

                var SignTheMail     = false;
                var EncryptTheMail  = false;

                EMailBodypart? bodypartToBeSecured = null;

                #region Add attachments, if available...

                if (Attachments       is null ||
                    Attachments.Count == 0)
                    bodypartToBeSecured  = _EncodeBodyparts();

                else
                    bodypartToBeSecured  = new EMailBodypart(
                                               ContentTypeBuilder:       AEMail => new MailContentType(MailContentTypes.multipart_mixed) { CharSet = "utf-8" }.GenerateMIMEBoundary(),
                                               ContentTransferEncoding:  "8bit",
                                               Content:                  [ "This is a multi-part message in MIME format." ],
                                               NestedBodyparts:          new EMailBodypart[] {
                                                                             _EncodeBodyparts()
                                                                         }.Concat(Attachments)
                                           );

                #endregion


                #region Check security settings

                switch (SecurityLevel)
                {

                    case EMailSecurity.autosign:

                        if (From?.SecretKeyRing is not null &&
                            From. SecretKeyRing.GetSecretKeys().Cast<PgpSecretKey>().ToList().Count != 0 &&
                            Passphrase.IsNotNullOrEmpty())
                        {
                            SignTheMail = true;
                        }

                        break;


                    case EMailSecurity.sign:

                        if (From?.SecretKeyRing is null ||
                            From. SecretKeyRing.GetSecretKeys().Cast<PgpSecretKey>().ToList().Count == 0 ||
                            Passphrase.IsNullOrEmpty())
                        {
                            throw new ApplicationException("Can not sign the e-mail!");
                        }

                        SignTheMail = true;

                        break;


                    case EMailSecurity.auto:

                        if (From?.SecretKeyRing is not null &&
                            From. SecretKeyRing.GetSecretKeys().Cast<PgpSecretKey>().ToList().Count != 0 &&
                            Passphrase.IsNotNullOrEmpty())
                        {
                            SignTheMail    = true;
                        }

                        if (SignTheMail &&
                            (!To.Any() | To.Any(v => v.PublicKeyRing is not null && v.PublicKeyRing.GetPublicKeys().Cast<PgpPublicKey>().ToList().Count != 0)) &&
                            (!Cc.Any() | Cc.Any(v => v.PublicKeyRing is not null && v.PublicKeyRing.GetPublicKeys().Cast<PgpPublicKey>().ToList().Count != 0)))
                        {
                            EncryptTheMail = true;
                        }

                        break;


                    case EMailSecurity.encrypt:

                        if (From?.SecretKeyRing is null  ||
                            From. SecretKeyRing.GetSecretKeys().Cast<PgpSecretKey>().ToList().Count == 0 ||
                            Passphrase.IsNullOrEmpty()  ||
                            To.Any(v => v.PublicKeyRing is null || v.PublicKeyRing.GetPublicKeys().Cast<PgpPublicKey>().ToList().Count == 0) ||
                            Cc.Any(v => v.PublicKeyRing is null || v.PublicKeyRing.GetPublicKeys().Cast<PgpPublicKey>().ToList().Count == 0))
                        {
                            throw new ApplicationException("Can not sign and encrypt the e-mail!");
                        }

                        EncryptTheMail = true;

                        break;

                }

                #endregion

                #region Sign the e-mail

                if (SignTheMail & !EncryptTheMail)
                {

                    var dataToBeSigned      = bodypartToBeSecured.

                                                  // Include headers of this MIME body
                                                  // https://tools.ietf.org/html/rfc1847 Security Multiparts for MIME:
                                                  ToText().

                                                  // Any trailing whitespace MUST then be removed from the signed material
                                                  Select(line => line.TrimEnd()).

                                                  // Canonical text format with <CR><LF> line endings
                                                  // https://tools.ietf.org/html/rfc3156 5. OpenPGP signed data
                                                  Aggregate((a, b) => a + "\r\n" + b)

                                                  //ToDo: Apply Content-Transfer-Encoding
                                                  ;

                    // MIME Security with OpenPGP (rfc3156, https://tools.ietf.org/html/rfc3156)
                    // OpenPGP Message Format     (rfc4880, https://tools.ietf.org/html/rfc4880)
                    body = new EMailBodypart(ContentTypeBuilder:          AMail => new MailContentType(MailContentTypes.multipart_signed) {
                                                                               MicAlg    = "pgp-sha512",
                                                                               Protocol  = "application/pgp-signature",
                                                                               CharSet   = "utf-8",
                                                                           },
                                              ContentTransferEncoding:     "8bit",
                                              NestedBodyparts:             [

                                                                               bodypartToBeSecured,

                                                                               new EMailBodypart(ContentTypeBuilder:       AMail => new MailContentType(MailContentTypes.application_pgp__signature) { CharSet = "utf-8" },
                                                                                             //    ContentTransferEncoding:  "8bit",
                                                                                                 ContentDescription:       "OpenPGP digital signature",
                                                                                                 ContentDisposition:       ContentDispositions.attachment.ToString() + "; filename=\"signature.asc\"",
                                                                                                 Content:                  [

                                                                                                                               OpenPGP.CreateSignature(new MemoryStream(dataToBeSigned.ToUTF8Bytes()),
                                                                                                                                                       From.SecretKeyRing?.GetSecretKeys().Cast<PgpSecretKey>().ToList().First(),
                                                                                                                                                       Passphrase,
                                                                                                                                                       HashAlgorithm: HashAlgorithm).

                                                                                                                                       WriteTo(new MemoryStream(), CloseOutputStream: false).
                                                                                                                                           ToUTF8String()

                                                                                                                           ])

                                                                           ]
                                             );

                }

                #endregion

                #region Encrypt the e-mail

                else if (SignTheMail & EncryptTheMail)
                {

                    var Plaintext   = bodypartToBeSecured.ToText().Aggregate((a, b) => a + "\r\n" + b).ToUTF8Bytes();
                    var Ciphertext  = new MemoryStream();

                    OpenPGP.EncryptSignAndZip(InputStream:            new MemoryStream(Plaintext),
                                              Length:                 (UInt64) Plaintext.Length,
                                              SecretKey:              From.SecretKeyRing?.GetSecretKeys().Cast<PgpSecretKey>().ToList().First(),
                                              Passphrase:             Passphrase,
                                              PublicKey:              To.First().PublicKeyRing.GetPublicKeys().Cast<PgpPublicKey>().ToList().First(),
                                              OutputStream:           Ciphertext,
                                              SymmetricKeyAlgorithm:  SymmetricKeyAlgorithm,
                                              HashAlgorithm:          HashAlgorithm,
                                              CompressionAlgorithm:   CompressionAlgorithm,
                                              ArmoredOutput:          true,
                                              Filename:               "encrypted.asc",
                                              LastModificationTime:   Timestamp.Now);

                    // MIME Security with OpenPGP (rfc3156, https://tools.ietf.org/html/rfc3156)
                    // OpenPGP Message Format     (rfc4880, https://tools.ietf.org/html/rfc4880)
                    body = new EMailBodypart(ContentTypeBuilder:          AMail => new MailContentType(MailContentTypes.multipart_encrypted) {
                                                                               Protocol = "application/pgp-encrypted",
                                                                               CharSet  = "utf-8",
                                                                           },
                                              ContentTransferEncoding:     "8bit",
                                              NestedBodyparts:             [

                                                                               new EMailBodypart(ContentTypeBuilder:   AMail => new MailContentType(MailContentTypes.application_pgp__encrypted) { CharSet = "utf-8" },
                                                                                                 ContentDescription:   "PGP/MIME version identification",
                                                                                                 ContentDisposition:   ContentDispositions.attachment.ToString() + "; filename=\"signature.asc\"",
                                                                                                 Content:              [ "Version: 1" ]),

                                                                               new EMailBodypart(ContentTypeBuilder:   AMail => new MailContentType(MailContentTypes.application_octet__stream) { CharSet = "utf-8" },
                                                                                                 ContentDescription:   "OpenPGP encrypted message",
                                                                                                 ContentDisposition:   ContentDispositions.inline.ToString() + "; filename=\"encrypted.asc\"",
                                                                                                 Content:              [ Ciphertext.ToArray().ToUTF8String() ]),

                                                                           ]
                                             );

                }

                #endregion


                else
                    this.body = bodypartToBeSecured;

                return this;

            }

            #endregion

        }

    }

}
