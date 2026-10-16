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
using System.Text;

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

            /// <summary>
            /// Request a read receipt (message disposition notification, RFC 8098) to this address.
            /// </summary>
            public EMailAddress?                         DispositionNotificationTo { get; set; }

            /// <summary>
            /// The importance of this e-mail (RFC 2156). Only High/Low emit headers; Normal is the default.
            /// </summary>
            public MailImportance                        Importance                { get; set; } = MailImportance.Normal;





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

                    // The headers as RFC 5322 and RFC 2047 have them: display names and a subject that
                    // are not ASCII as encoded words, a display name with a comma or a quote in quotes,
                    // long lines folded; message ids in angle brackets, the date with its numeric zone.
                    // No Bcc: the blind copies are recipients of the envelope (EMailEnvelop) - a header
                    // would have shown them to everybody.
                    SetEMailHeader("From",                           AddressHeader("From", [ From ]));

                    if (To. Any())
                        SetEMailHeader("To",                         AddressHeader("To", To));

                    if (Cc. Any())
                        SetEMailHeader("Cc",                         AddressHeader("Cc", Cc));

                    MailHeaders.TryRemove("Bcc", out _);

                    if (ReplyTo.Any())
                        SetEMailHeader("Reply-To",                   AddressHeader("Reply-To", ReplyTo));

                    SetEMailHeader("Subject",                        HeaderValue("Subject", MimeEncoding.UnstructuredHeader("Subject", Subject!)));

                    if (Date.HasValue)
                        SetEMailHeader("Date",                       MimeEncoding.Date(Date.Value));

                    MessageId ??= Message_Id.Parse(MimeEncoding.NewMessageId(From.Address.Domain));
                    SetEMailHeader("Message-ID",                     $"<{MessageId}>");

                    if (Reference.HasValue)
                        SetEMailHeader("In-Reply-To",                $"<{Reference}>");

                    if (References.Count > 0)
                        SetEMailHeader("References",                 HeaderValue("References", MimeEncoding.Fold("References", References.Select(id => ($"<{id}>", false)))));

                    if (ListId             is not null)
                        SetEMailHeader("List-ID",                    ListId.Value.ToString());

                    if (ListPost           is not null)
                        SetEMailHeader("List-Post",                  $"<mailto:{ListPost}>");

                    if (DispositionNotificationTo is not null)
                        SetEMailHeader("Disposition-Notification-To", AddressHeader("Disposition-Notification-To", [ DispositionNotificationTo ]));

                    // Message importance (RFC 2156 + the de-facto X-* headers). Only stamp High/Low;
                    // Normal is the default and adds no headers.
                    if (Importance == MailImportance.High)
                    {
                        SetEMailHeader("Importance",          "High");
                        SetEMailHeader("Priority",            "urgent");
                        SetEMailHeader("X-Priority",          "1 (Highest)");
                        SetEMailHeader("X-MSMail-Priority",   "High");
                    }
                    else if (Importance == MailImportance.Low)
                    {
                        SetEMailHeader("Importance",          "Low");
                        SetEMailHeader("Priority",            "non-urgent");
                        SetEMailHeader("X-Priority",          "5 (Lowest)");
                        SetEMailHeader("X-MSMail-Priority",   "Low");
                    }



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

            #region (private) AddressHeader(Name, Addresses) / HeaderValue(Name, Line)

            /// <summary>
            /// The value of an address header, as the MIME writer writes it.
            /// </summary>
            private static String AddressHeader(String Name, IEnumerable<EMailAddress> Addresses)

                => HeaderValue(Name, MimeEncoding.MailboxListHeader(Name, Addresses.Select(address => new MailboxAddress(address.OwnerName, address.Address.ToString()))));

            /// <summary>
            /// The value of a header line the MIME writer wrote - "Name: value" - folded as it was.
            /// </summary>
            private static String HeaderValue(String Name, String Line)
                => Line[(Name.Length + 2)..];

            #endregion

            #region (protected) EncodeText(Text)

            /// <summary>
            /// Text as the lines of a body part, in UTF-8: as it is when it is 7-bit
            /// ASCII in short lines, quoted-printable when it is not - and labelled
            /// as what it is.
            /// </summary>
            protected static (String TransferEncoding, String[] Lines) EncodeText(String Text)
            {

                var octets = Encoding.UTF8.GetBytes(Text);

                return MimeEncoding.NeedsEncoding(octets)
                           ? ("quoted-printable", Encoding.ASCII.GetString(MimeEncoding.QuotedPrintable(octets)).Split("\r\n"))
                           : ("7bit",             Text.Split(TextLineSplitter, StringSplitOptions.None));

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
                                               ContentTypeBuilder:       AEMail => new MailContentType(MailContentTypes.multipart_mixed).GenerateMIMEBoundary(),
                                               ContentTransferEncoding:  "7bit",
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

                        // Encrypted only when every recipient can read it - one without a key had got
                        // a message nobody could open for them - and not with blind copies, whose keys
                        // the ciphertext had named to everybody (see EMailSecurity.encrypt).
                        if (SignTheMail &&
                            !Bcc.Any() &&
                            To.Concat(Cc).All(v => v.PublicKeyRing is not null && v.PublicKeyRing.GetPublicKeys().Cast<PgpPublicKey>().ToList().Count != 0))
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

                        // A ciphertext names the keys it is encrypted to: one for everybody had shown
                        // the blind copies to everybody. They need ciphertexts of their own - PgpMime
                        // and MimeMessageBuilder write those.
                        if (Bcc.Any())
                            throw new ApplicationException("Blind copies of an encrypted e-mail need a ciphertext of their own!");

                        // Note: the encryption path always signs as well (OpenPGP.EncryptSignAndZip), so
                        // "encrypt-only" is not a distinct case in this implementation — encrypting implies
                        // a signature. The dispatch below therefore checks EncryptTheMail first.
                        EncryptTheMail = true;

                        break;

                }

                #endregion

                // Order matters, and it is intentional: encryption is checked first because in this
                // implementation encrypting *always* signs as well (OpenPGP.EncryptSignAndZip). We
                // deliberately do NOT support encrypt-without-sign: an encrypted-but-unsigned mail gives
                // confidentiality but no authenticity — the recipient cannot verify the sender, and it
                // enables surreptitious-forwarding / tampering attacks (RFC 3156). Secure mail wants
                // authenticated encryption, so "encrypt" ⇒ sign+encrypt, and only if we are not
                // encrypting do we consider a plain (detached) signature. See EMailSecurity.encrypt.

                #region Encrypt the e-mail (this always signs as well)

                if (EncryptTheMail)
                {

                    var Plaintext   = bodypartToBeSecured.ToText().Aggregate((a, b) => a + "\r\n" + b).ToUTF8Bytes();
                    var Ciphertext  = new MemoryStream();

                    // Encrypt to EVERY recipient (To + Cc) that carries a public key, so each of them
                    // can decrypt the message with their own private key — not just the first To.
                    // Prefer each recipient's encryption-capable (sub)key, fall back to the primary key,
                    // and de-duplicate in case the same key is listed more than once.
                    var recipientKeys = To.Concat(Cc).
                                            Where (recipient => recipient.PublicKeyRing is not null).
                                            Select(recipient => recipient.PublicKeyRing!.GetPublicKeys().Cast<PgpPublicKey>().FirstOrDefault(key => key.IsEncryptionKey)
                                                                    ?? recipient.PublicKeyRing!.GetPublicKeys().Cast<PgpPublicKey>().First()).
                                            DistinctBy(key => key.KeyId).
                                            ToList();

                    OpenPGP.EncryptSignAndZip(InputStream:            new MemoryStream(Plaintext),
                                              Length:                 (UInt64) Plaintext.Length,
                                              SecretKey:              From.SecretKeyRing?.GetSecretKeys().Cast<PgpSecretKey>().ToList().First(),
                                              Passphrase:             Passphrase,
                                              PublicKeys:             recipientKeys,
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
                                                                               Protocol = "application/pgp-encrypted"
                                                                           },
                                              ContentTransferEncoding:     "7bit",
                                              NestedBodyparts:             [

                                                                               // The control part: "Version: 1" (RFC 3156 §4) - no file, and no signature.
                                                                               new EMailBodypart(ContentTypeBuilder:   AMail => new MailContentType(MailContentTypes.application_pgp__encrypted),
                                                                                                 ContentDescription:   "PGP/MIME version identification",
                                                                                                 Content:              [ "Version: 1" ]),

                                                                               new EMailBodypart(ContentTypeBuilder:   AMail => new MailContentType(MailContentTypes.application_octet__stream) { CharSet = "utf-8" },
                                                                                                 ContentDescription:   "OpenPGP encrypted message",
                                                                                                 ContentDisposition:   ContentDispositions.inline.ToString() + "; filename=\"encrypted.asc\"",
                                                                                                 Content:              [ Ciphertext.ToArray().ToUTF8String() ]),

                                                                           ]
                                             );

                }

                #endregion

                #region Sign the e-mail (only — encrypting, handled above, already signs)

                else if (SignTheMail)
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
                    // micalg names the hash the signature was made with (RFC 3156 §5): pgp-sha256 for
                    // SHA-256 - not pgp-sha512 whatever it was.
                    body = new EMailBodypart(ContentTypeBuilder:          AMail => new MailContentType(MailContentTypes.multipart_signed) {
                                                                               MicAlg    = "pgp-" + HashAlgorithm.ToString().ToLowerInvariant(),
                                                                               Protocol  = "application/pgp-signature"
                                                                           },
                                              ContentTransferEncoding:     "7bit",
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


                else
                    this.body = bodypartToBeSecured;

                return this;

            }

            #endregion

        }

    }

}
