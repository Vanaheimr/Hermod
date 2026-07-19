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

using Org.BouncyCastle.Bcpg.OpenPgp;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// A e-mail.
    /// </summary>
    public class EMail
    {

        #region Well-known e-mail headers

        /// <summary>
        /// The sender of the e-mail.
        /// </summary>
        public EMailAddress        From         { get; }

        /// <summary>
        /// The receivers of the e-mail.
        /// </summary>
        public EMailAddressList    To           { get; }

        /// <summary>
        /// Additional receivers of the e-mail.
        /// </summary>
        public EMailAddressList    Cc           { get; }

        /// <summary>
        /// Hidden receivers of the e-mail.
        /// </summary>
        public EMailAddressList    Bcc          { get; }

        /// <summary>
        /// The receivers of any reply on this e-mail.
        /// </summary>
        public EMailAddressList    ReplyTo      { get; }

        /// <summary>
        /// The subject of the e-mail.
        /// </summary>
        public String              Subject      { get; }

        /// <summary>
        /// The sending timestamp of the e-mail.
        /// </summary>
        public DateTimeOffset      Date         { get; }

        /// <summary>
        /// The unique message identification of the e-mail.
        /// </summary>
        public Message_Id?         MessageId    { get; }

        #endregion

        #region Properties

        // As the order of the headers is important,
        // do not replace this list by a dictionary!
        private readonly ConcurrentDictionary<String, String> headers = [];

        /// <summary>
        /// The E-Mail header as enumeration of strings.
        /// </summary>
        public IEnumerable<KeyValuePair<String, String>>  Headers
            => headers;

        /// <summary>
        /// The e-mail body.
        /// </summary>
        public EMailBodypart                              Body      { get; }

        #endregion

        #region Constructor(s)

        #region (private)  EMail(MailHeaders)

        private EMail(IEnumerable<KeyValuePair<String, String>> MailHeaders)
        {

            foreach (var kvp in MailHeaders)
            {

                headers.TryAdd(kvp.Key, kvp.Value);

                switch (kvp.Key.ToLower())
                {

                    case "from":       this.From       = EMailAddress.    Parse(kvp.Value); break;
                    case "to":         this.To         = EMailAddressList.Parse(kvp.Value); break;
                    case "cc":         this.Cc         = EMailAddressList.Parse(kvp.Value); break;
                    case "bcc":        this.Bcc        = EMailAddressList.Parse(kvp.Value); break;
                    case "replyto":    this.ReplyTo    = EMailAddressList.Parse(kvp.Value); break;
                    case "subject":    this.Subject    =                        kvp.Value ; break;
                    case "date":       this.Date       = DateTime.        Parse(kvp.Value); break;
                    case "message-id": this.MessageId  = Message_Id.      Parse(kvp.Value); break;

                }

            }

        }

        #endregion

        #region (private)  EMail(MailText)

        /// <summary>
        /// Parse an e-mail from the given enumeration of strings.
        /// </summary>
        /// <param name="MailText">An enumeration of strings.</param>
        private EMail(IEnumerable<String> MailText)

            : this(MailText.TakeWhile(line => line.IsNotNullOrEmpty()).
                            AggregateIndentedLines().
                            ToKeyValuePairs(':'))

        {

            Body  = new EMailBodypart(MailText);

        }

        #endregion

        #region (internal) EMail(MailBuilder)

        /// <summary>
        /// Create a new e-mail based on the given e-mail builder.
        /// </summary>
        /// <param name="MailBuilder">An e-mail builder.</param>
        internal EMail(AbstractEMail.Builder MailBuilder)

            : this(MailBuilder.
                       EncodeBodyparts().
                       // Copy only everything which is not related to the e-mail body!
                       MailHeaders.Where(header => !header.Key.StartsWith("content", StringComparison.CurrentCultureIgnoreCase)).
                       Concat(MailBuilder.Body?.MailHeaders ?? []))

        {

            //ToDo: Do a real deep-copy here!
                 Body       = MailBuilder.Body;

            if (MailBuilder.From is null)
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentNullException(nameof(MailBuilder.From), "The 'From' e-mail address must not be null!");
#pragma warning restore CA2208 // Instantiate argument exceptions correctly

            //ToDo: Work-around for PGP/GPG!
            this.From       = MailBuilder.From;
            this.To         = MailBuilder.To;
            this.Cc         = MailBuilder.Cc;
            this.Bcc        = MailBuilder.Bcc;
            this.ReplyTo    = MailBuilder.ReplyTo;
            this.Subject    = MailBuilder.Subject;
            this.Date       = MailBuilder.Date.Value;
            this.MessageId  = MailBuilder.MessageId;

        }

        #endregion

        #endregion


        #region (static) Parse(MailText)

        /// <summary>
        /// Parse an e-mail from the given enumeration of strings.
        /// </summary>
        /// <param name="MailText">An enumeration of strings.</param>
        public static EMail Parse(IEnumerable<String> MailText)

            => new (MailText);

        #endregion


        #region GetEMailHeader(Key)

        /// <summary>
        /// Get the e-mail header value for the given key.
        /// </summary>
        /// <param name="Key">An e-mail header key.</param>
        public String GetEMailHeader(String Key)
        {

            var Property = headers.
                               Where(kvp => kvp.Key.Equals(Key, StringComparison.CurrentCultureIgnoreCase)).
                               FirstOrDefault();

            if (Property.Key.IsNotNullOrEmpty())
                return Property.Value;

            return String.Empty;

        }

        #endregion


        #region OpenPGP (RFC 3156)

        /// <summary>
        /// Whether this e-mail is an OpenPGP/MIME signed message (multipart/signed).
        /// Reflects the MIME structure only — call <see cref="VerifyPgpSignature(PgpPublicKeyRing)"/>
        /// to check whether the signature is actually valid.
        /// </summary>
        public Boolean IsPgpSigned
            => Body?.IsPgpSigned == true;

        /// <summary>
        /// Whether this e-mail is an OpenPGP/MIME encrypted message (multipart/encrypted).
        /// </summary>
        public Boolean IsPgpEncrypted
            => Body?.IsPgpEncrypted == true;


        #region VerifyPgpSignature(SenderPublicKey/s)

        /// <summary>
        /// Verify the OpenPGP/MIME signature of this (multipart/signed) e-mail against the given
        /// sender public key.
        /// </summary>
        /// <param name="SenderPublicKey">The purported sender's public key ring.</param>
        public PgpSignatureVerification VerifyPgpSignature(PgpPublicKeyRing SenderPublicKey)
            => VerifyPgpSignature(new PgpPublicKeyRingBundle(SenderPublicKey.GetEncoded()));

        /// <summary>
        /// Verify the OpenPGP/MIME signature of this (multipart/signed) e-mail against the given
        /// bundle of candidate sender public keys.
        /// </summary>
        /// <param name="SenderPublicKeys">Candidate sender public keys.</param>
        public PgpSignatureVerification VerifyPgpSignature(PgpPublicKeyRingBundle SenderPublicKeys)
        {

            if (Body is null || !Body.IsPgpSigned)
                return PgpSignatureVerification.NoSignature;

            var parts = Body.NestedBodyparts.ToList();

            if (parts.Count < 2)
                return PgpSignatureVerification.NoSignature;

            var signedPart     = parts[0];
            var signaturePart  = parts[1];

            // The signature covers the first MIME part exactly as it appeared on the wire (RFC 1847 /
            // RFC 3156). Prefer the preserved raw bytes: joining the verbatim lines with CRLF (and no
            // trailing CRLF — the CRLF before the boundary belongs to the boundary) reproduces the
            // signed octets byte-for-byte, regardless of header order/whitespace, which a re-serialization
            // might not. Fall back to reconstruction only for a part that was built rather than parsed.
            var signedData = (signedPart.RawContent is not null
                                  ? String.Join("\r\n", signedPart.RawContent)
                                  : signedPart.ToText().
                                        Select(line => line.TrimEnd()).
                                        Aggregate((a, b) => a + "\r\n" + b)
                             ).ToUTF8Bytes();

            var armoredSignature = String.Join("\r\n", signaturePart.MailBody);

            using var signatureStream = new MemoryStream(armoredSignature.ToUTF8Bytes());

            return OpenPGP.VerifyDetachedSignature(signedData, signatureStream, SenderPublicKeys);

        }

        #endregion

        #region DecryptPgp(RecipientKey/s, Passphrase)

        /// <summary>
        /// Decrypt this (multipart/encrypted) e-mail with the given recipient secret key and return
        /// the recovered inner MIME body part.
        /// </summary>
        /// <param name="RecipientKey">The recipient's secret key ring.</param>
        /// <param name="Passphrase">The passphrase protecting the secret key.</param>
        public EMailBodypart DecryptPgp(PgpSecretKeyRing RecipientKey, String Passphrase)
            => DecryptPgp(new PgpSecretKeyRingBundle(RecipientKey.GetEncoded()), Passphrase);

        /// <summary>
        /// Decrypt this (multipart/encrypted) e-mail with whichever of the given recipient secret keys
        /// the message was encrypted to, and return the recovered inner MIME body part.
        /// </summary>
        /// <param name="RecipientKeys">The recipient's secret key ring bundle.</param>
        /// <param name="Passphrase">The passphrase protecting the matching secret key.</param>
        public EMailBodypart DecryptPgp(PgpSecretKeyRingBundle RecipientKeys, String Passphrase)
        {

            using var input      = new MemoryStream(ExtractPgpCiphertext().ToUTF8Bytes());
            var       plaintext  = OpenPGP.Decrypt(input, RecipientKeys, Passphrase);

            // The decrypted payload is itself a MIME body part — parse it back.
            return new EMailBodypart(plaintext.ToUTF8String().Split(["\r\n"], StringSplitOptions.None));

        }

        #endregion

        #region DecryptAndVerifyPgp(RecipientKey/s, Passphrase, SenderPublicKey/s)

        /// <summary>
        /// Decrypt this (multipart/encrypted) e-mail with the given recipient secret key AND verify the
        /// embedded signature over the plaintext against the given sender public key. Since Hermod always
        /// signs when encrypting, this recovers both the content and its authenticity in one step.
        /// </summary>
        /// <param name="RecipientKey">The recipient's secret key ring.</param>
        /// <param name="Passphrase">The passphrase protecting the secret key.</param>
        /// <param name="SenderPublicKey">The purported sender's public key ring.</param>
        public PgpDecryptionResult DecryptAndVerifyPgp(PgpSecretKeyRing  RecipientKey,
                                                       String            Passphrase,
                                                       PgpPublicKeyRing  SenderPublicKey)

            => DecryptAndVerifyPgp(new PgpSecretKeyRingBundle(RecipientKey.   GetEncoded()),
                                   Passphrase,
                                   new PgpPublicKeyRingBundle(SenderPublicKey.GetEncoded()));

        /// <summary>
        /// Decrypt this (multipart/encrypted) e-mail with whichever recipient secret key it was encrypted
        /// to, AND verify the embedded signature over the plaintext against the given candidate sender keys.
        /// </summary>
        /// <param name="RecipientKeys">The recipient's secret key ring bundle.</param>
        /// <param name="Passphrase">The passphrase protecting the matching secret key.</param>
        /// <param name="SenderPublicKeys">Candidate sender public keys.</param>
        public PgpDecryptionResult DecryptAndVerifyPgp(PgpSecretKeyRingBundle  RecipientKeys,
                                                       String                  Passphrase,
                                                       PgpPublicKeyRingBundle  SenderPublicKeys)
        {

            using var input               = new MemoryStream(ExtractPgpCiphertext().ToUTF8Bytes());
            var (plaintext, signature)    = OpenPGP.DecryptAndVerify(input, RecipientKeys, Passphrase, SenderPublicKeys);

            return new PgpDecryptionResult(
                       new EMailBodypart(plaintext.ToUTF8String().Split(["\r\n"], StringSplitOptions.None)),
                       signature
                   );

        }

        #endregion

        #region (private) ExtractPgpCiphertext()

        /// <summary>
        /// Return the armored OpenPGP message from this (multipart/encrypted) e-mail.
        /// </summary>
        private String ExtractPgpCiphertext()
        {

            if (Body is null || !Body.IsPgpEncrypted)
                throw new InvalidOperationException("This e-mail is not an OpenPGP/MIME encrypted message!");

            // The ciphertext lives in the application/octet-stream part (RFC 3156 §4).
            var ciphertextPart = Body.NestedBodyparts.
                                     FirstOrDefault(part => part.MailBody.Any(line => line.Contains("BEGIN PGP MESSAGE")))
                                 ?? throw new InvalidOperationException("No OpenPGP message part found in the encrypted e-mail!");

            return String.Join("\r\n", ciphertextPart.MailBody);

        }

        #endregion

        #endregion


        #region Importance (RFC 2156)

        /// <summary>
        /// The importance of this e-mail, read from its headers with the precedence
        /// Importance → X-Priority → X-MSMail-Priority. Defaults to <see cref="MailImportance.Normal"/>.
        /// </summary>
        public MailImportance Importance
        {
            get
            {

                var importance = GetEMailHeader("Importance");
                if (importance.IsNotNullOrEmpty())
                {
                    if (importance.Trim().Equals("high", StringComparison.OrdinalIgnoreCase)) return MailImportance.High;
                    if (importance.Trim().Equals("low",  StringComparison.OrdinalIgnoreCase)) return MailImportance.Low;
                    return MailImportance.Normal;
                }

                // X-Priority: "1 (Highest)" … "5 (Lowest)" — the leading digit is what matters.
                var xPriority = GetEMailHeader("X-Priority").TrimStart();
                if (xPriority.Length > 0 && Char.IsDigit(xPriority[0]))
                {
                    if (xPriority[0] is '1' or '2') return MailImportance.High;
                    if (xPriority[0] is '4' or '5') return MailImportance.Low;
                    return MailImportance.Normal;
                }

                var msPriority = GetEMailHeader("X-MSMail-Priority");
                if (msPriority.IsNotNullOrEmpty())
                {
                    if (msPriority.Trim().Equals("high", StringComparison.OrdinalIgnoreCase)) return MailImportance.High;
                    if (msPriority.Trim().Equals("low",  StringComparison.OrdinalIgnoreCase)) return MailImportance.Low;
                }

                return MailImportance.Normal;

            }
        }

        #endregion

        #region Message Disposition Notifications (read receipts, RFC 8098)

        /// <summary>
        /// The address that requested a read receipt via the "Disposition-Notification-To" header,
        /// or null if none was requested.
        /// </summary>
        public String? DispositionNotificationTo
        {
            get
            {
                var value = GetEMailHeader("Disposition-Notification-To");
                return value.IsNotNullOrEmpty() ? value : null;
            }
        }

        /// <summary>
        /// Whether this e-mail requested a read receipt (RFC 8098).
        /// </summary>
        public Boolean IsReadReceiptRequested
            => DispositionNotificationTo is not null;

        /// <summary>
        /// Create a Message Disposition Notification (read receipt, RFC 8098) for this e-mail, addressed
        /// to its "Disposition-Notification-To" header. Returns null if no read receipt was requested.
        /// A mail client calls this when the user displays (or otherwise disposes of) the message and
        /// then sends the result like any other e-mail.
        /// </summary>
        /// <param name="ReportingAgent">The recipient generating the receipt (becomes the MDN's From).</param>
        /// <param name="Disposition">What happened to the message; defaults to displayed/automatic.</param>
        /// <param name="ReportingUAProduct">The product name for the Reporting-UA field.</param>
        /// <param name="IncludeOriginalHeaders">Whether to append the original message headers.</param>
        public EMail? CreateReadReceipt(EMailAddress         ReportingAgent,
                                        MessageDisposition?  Disposition             = null,
                                        String?              ReportingUAProduct      = null,
                                        Boolean              IncludeOriginalHeaders  = true)
        {

            var notifyTo = DispositionNotificationTo;
            if (notifyTo is null)
                return null;

            var disposition     = Disposition ?? MessageDisposition.DisplayedAutomatically;
            var reporter        = ReportingAgent.Address.ToString();
            var reporterDomain  = reporter.Contains('@') ? reporter[(reporter.IndexOf('@') + 1)..] : "localhost";
            var product         = ReportingUAProduct.IsNotNullOrEmpty() ? ReportingUAProduct! : "Hermod";
            var boundary        = "=_mdn_" + Guid.NewGuid().ToString("N");

            var lines = new List<String>
            {
                $"From: {ReportingAgent}",
                $"To: {notifyTo}",
                $"Subject: Read receipt: {Subject}",
                $"Date: {DateTime.UtcNow:R}",
                $"Message-ID: <mdn.{Guid.NewGuid():N}@{reporterDomain}>",
                "MIME-Version: 1.0",
                "Auto-Submitted: auto-replied",   // RFC 3834 — this is an automatic reply; do not reply to it
                "Content-Type: multipart/report; report-type=disposition-notification;",
                $"    boundary=\"{boundary}\"",
                "",

                // Part 1 — human-readable
                "--" + boundary,
                "Content-Type: text/plain; charset=utf-8",
                "",
                $"This is a return receipt for the mail you sent to {notifyTo}.",
                $"It was {disposition.Type.ToString().ToLowerInvariant()}.",
                "",

                // Part 2 — machine-readable disposition-notification (RFC 8098 §3.1.2)
                "--" + boundary,
                "Content-Type: message/disposition-notification",
                "",
                $"Reporting-UA: {reporterDomain}; {product}",
                $"Final-Recipient: rfc822; {reporter}"
            };

            var originalMessageId = GetEMailHeader("Message-ID");
            if (originalMessageId.IsNotNullOrEmpty())
                lines.Add($"Original-Message-ID: {originalMessageId}");

            lines.Add($"Disposition: {disposition}");
            lines.Add("");

            // Part 3 — original headers (optional)
            if (IncludeOriginalHeaders)
            {
                lines.Add("--" + boundary);
                lines.Add("Content-Type: text/rfc822-headers");
                lines.Add("");
                lines.AddRange(ToText().TakeWhile(line => line.IsNotNullOrEmpty()));
                lines.Add("");
            }

            lines.Add("--" + boundary + "--");

            return Parse(lines);

        }

        #endregion


        /// <summary>
        /// Return a string representation of this e-mail.
        /// </summary>
        public IEnumerable<String> ToText()

            => headers.
                   Select(kvp => kvp.Key + ": " + kvp.Value).
                   Concat([ "" ]).
                   Concat(Body.ToText(false));


        public override String ToString()

            => String.Join(Environment.NewLine, ToText());

    }

}
