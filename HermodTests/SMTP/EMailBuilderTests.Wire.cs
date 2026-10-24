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

using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;

using Org.BouncyCastle.Bcpg;

using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// What the builders put on the wire, and what they got wrong there: recipients
    /// shared by every builder, blind copies in a header and missing from the
    /// envelope, names and subjects that are not ASCII written raw, ids without their
    /// angle brackets, text labelled quoted-printable and not encoded, an HTML part
    /// without its Content-Type, a whole attachment in one line, micalg pgp-sha512
    /// whatever the hash, and mail encrypted for recipients who had no key.
    /// </summary>
    public partial class EMailBuilderTests
    {

        #region (private) A queue that keeps what it gets

        private sealed class KeepingQueue : IMailQueue
        {
            public readonly List<QueuedMail> Queued = [];
            public Task EnqueueAsync(QueuedMail mail, CancellationToken ct = default) { Queued.Add(mail); return Task.CompletedTask; }
            public Task<IReadOnlyList<QueuedMail>> GetPendingAsync(Int32 maxItems = 50, CancellationToken ct = default)  => Task.FromResult<IReadOnlyList<QueuedMail>>(Queued);
            public Task<QueuedMail?> GetByIdAsync(String id, CancellationToken ct = default)                           => Task.FromResult(Queued.FirstOrDefault(mail => mail.Id == id));
            public Task UpdateAsync(QueuedMail mail, CancellationToken ct = default)                                    => Task.CompletedTask;
            public Task RemoveAsync(String id, CancellationToken ct = default)                                          => Task.CompletedTask;
            public Task<Int32> GetQueueLengthAsync(CancellationToken ct = default)                                      => Task.FromResult(Queued.Count);
            public Task<IReadOnlyList<QueuedMail>> GetFailedAsync(Int32 maxItems = 100, CancellationToken ct = default)  => Task.FromResult<IReadOnlyList<QueuedMail>>([]);
            public ChannelReader<QueuedMail> NewMailReader    { get; } = Channel.CreateUnbounded<QueuedMail>().Reader;
            public void SignalRetryCheck() { }
            public ChannelReader<Boolean>    RetryCheckReader { get; } = Channel.CreateUnbounded<Boolean>().Reader;
        }

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        private static EMailAddress Address(String Address, String? Name = null)
            => new (SimpleEMailAddress.Parse(Address), Name);

        private static String Text(EMail Mail)
            => String.Join("\r\n", Mail.ToText());

        /// <summary>
        /// The value of the first header line of that name, unfolded (RFC 5322 §2.2.3).
        /// </summary>
        private static String Header(String Text, String Name)

            => Regex.Replace(Regex.Match(Text, $@"^{Name}: ([^\r\n]*(?:\r\n[ \t][^\r\n]*)*)", RegexOptions.Multiline | RegexOptions.IgnoreCase).Groups[1].Value,
                             @"\r\n(?=[ \t])",
                             "");

        #endregion


        [Test]
        public void The_recipients_of_one_builder_are_not_those_of_the_next()
        {

            var first  = new TextEMailBuilder();
            first.To.Add(Address("bob@example.org"));
            first.Bcc.Add(Address("secret@example.org"));

            var second = new TextEMailBuilder();

            // Asked twice, Empty is two lists: one each, not one for both.
            var oneEmpty    = EMailAddressListBuilder.Empty;
            var otherEmpty  = EMailAddressListBuilder.Empty;

            Assert.Multiple(() => {
                Assert.That(second.To,   Is.Empty, "a list shared by every builder had sent bob@ the next mail too");
                Assert.That(second.Bcc,  Is.Empty);
                Assert.That(otherEmpty,  Is.Not.SameAs(oneEmpty));
            });

        }

        [Test]
        public async Task Blind_copies_are_recipients_of_the_envelope_and_in_no_header()
        {

            var builder = new TextEMailBuilder {
                              From     = Address("alice@example.com", "Alice"),
                              Subject  = "Blind",
                              Text     = $"Hallo {Marker}"
                          };

            builder.To. Add(Address("bob@example.org"));
            builder.Cc. Add(Address("carol@example.net"));
            builder.Bcc.Add(Address("secret@example.org"));

            var envelope = new EMailEnvelop((AbstractEMail.Builder) builder);
            var queue    = new KeepingQueue();
            await new MailSender(queue, new QuietLogger()).SendAsync(envelope);

            // A message read with a Bcc: header - a draft, say - leaves without it as well.
            var parsed   = new EMailEnvelop(EMail.Parse([ "From: alice@example.com",
                                                          "To: bob@example.org",
                                                          "Bcc: Secret <secret@example.org>",
                                                          "Subject: parsed",
                                                          "",
                                                          "hello" ]));
            var queue2   = new KeepingQueue();
            await new MailSender(queue2, new QuietLogger()).SendAsync(parsed);

            Assert.Multiple(() => {

                Assert.That(envelope.RcptTo.Select(rcpt => rcpt.Address.ToString()),
                            Is.EquivalentTo(new[] { "bob@example.org", "carol@example.net", "secret@example.org" }),
                            "To, Cc and Bcc get it - before, only To did");

                Assert.That(Text(envelope.Mail),            Does.Not.Contain("Bcc:").And.Not.Contain("secret@"));
                Assert.That(Text(envelope.Mail),            Does.Contain("From: Alice <alice@example.com>").And.Contain("Subject: Blind"),
                            "an envelope of a builder has the headers of its mail");
                Assert.That(queue.Queued.Select(mail => mail.MessageContent), Has.All.Not.Contain("secret@"));

                Assert.That(parsed.RcptTo.Select(rcpt => rcpt.Address.ToString()), Is.EquivalentTo(new[] { "bob@example.org", "secret@example.org" }));
                Assert.That(queue2.Queued.Select(mail => mail.MessageContent),     Has.All.Not.Contain("Bcc:"));
                Assert.That(Text(parsed.Mail),                                      Does.Contain("Bcc:"), "the copy the sender keeps still says who got one");

            });

        }

        [Test]
        public void Names_and_subjects_that_are_not_ASCII_are_encoded_and_long_lines_folded()
        {

            var builder = new TextEMailBuilder {
                              From     = Address("joerg@example.com", "Müller, Jörg"),
                              Subject  = "Grüße aus Köln – und eine Betreffzeile, die länger ist als eine Zeile sein darf",
                              Text     = "Hallo"
                          };

            builder.To.Add(Address("doe@example.org", "Doe, John"));
            for (var i = 0; i < 12; i++)
                builder.Cc.Add(Address($"colleague{i}@example.net", $"Colleague {i}"));

            var text    = Text(builder);
            var header  = text[..text.IndexOf("\r\n\r\n")];
            var parsed  = EMail.Parse(text.Split("\r\n"));

            Assert.Multiple(() => {

                Assert.That(header.All(c => c < 0x80),                                 Is.True,  "no raw UTF-8 in the header");
                Assert.That(header.Split("\r\n").Max(line => line.Length),            Is.LessThanOrEqualTo(78), "folded (RFC 5322 §2.1.1)");
                Assert.That(Header(text, "From"),                                     Does.StartWith("=?UTF-8?Q?M=C3=BCller=2C_J=C3=B6rg?="));
                Assert.That(Header(text, "To"),                                       Is.EqualTo("\"Doe, John\" <doe@example.org>"), "a name with a comma quoted - else it had been two addresses");

                Assert.That(parsed.Subject,                                           Is.EqualTo(builder.Subject));
                Assert.That(parsed.From.OwnerName,                                    Is.EqualTo("Müller, Jörg"));
                Assert.That(parsed.To.Select(to => to.Address.ToString()),            Is.EqualTo(new[] { "doe@example.org" }));
                Assert.That(parsed.Cc.Count(),                                        Is.EqualTo(12));

            });

        }

        [Test]
        public void Ids_and_the_date_are_written_as_RFC_5322_has_them()
        {

            var date    = new DateTimeOffset(2026, 10, 16, 9, 30, 0, TimeSpan.FromHours(2));
            var builder = new TextEMailBuilder {
                              From        = Address("alice@example.com"),
                              Subject     = "Ids",
                              Text        = "Hallo",
                              Date        = date,
                              Reference   = Message_Id.Parse("parent@example.org"),
                              References  = [ Message_Id.Parse("root@example.org"), Message_Id.Parse("parent@example.org") ]
                          };

            builder.To.Add(Address("bob@example.org"));

            EMail mail  = builder;
            var text    = Text(mail);
            var parsed  = EMail.Parse(text.Split("\r\n"));

            Assert.Multiple(() => {

                Assert.That(Header(text, "Message-ID"),   Does.Match(@"^<[0-9a-f]{32}@example\.com>$"), "made when there was none, in angle brackets");
                Assert.That(Header(text, "In-Reply-To"),  Is.EqualTo("<parent@example.org>"));
                Assert.That(Header(text, "References"),   Is.EqualTo("<root@example.org> <parent@example.org>"));
                Assert.That(Header(text, "Date"),         Is.EqualTo("Fri, 16 Oct 2026 09:30:00 +0200"), "the zone as a number, not GMT");

                Assert.That(parsed.Date,                  Is.EqualTo(date));
                Assert.That(parsed.MessageId,             Is.EqualTo(mail.MessageId));

            });

        }

        [Test]
        public void Text_that_is_not_ASCII_is_quoted_printable_and_ASCII_stays_7bit()
        {

            EMail Mail(String Body)
            {
                var builder = new TextEMailBuilder { From = Address("alice@example.com"), Subject = "QP", Text = Body };
                builder.To.Add(Address("bob@example.org"));
                return builder;
            }

            var umlauts = Text(Mail("Grüße aus Köln\n1 + 1 = 2\n" + new String('x', 100)));
            var ascii   = Text(Mail("Hello from Cologne"));
            var body    = umlauts[(umlauts.IndexOf("\r\n\r\n") + 4)..];

            Assert.Multiple(() => {

                Assert.That(umlauts,                                   Does.Contain("Content-Transfer-Encoding: quoted-printable"));
                Assert.That(body,                                      Does.Contain("Gr=C3=BC=C3=9Fe aus K=C3=B6ln"), "encoded - not only labelled so");
                Assert.That(body,                                      Does.Contain("1 + 1 =3D 2"), "an \"=\" that had been read as an escape");
                Assert.That(body.Split("\r\n").Max(line => line.Length), Is.LessThanOrEqualTo(76));
                Assert.That(umlauts.All(c => c < 0x80),                Is.True, "7-bit on the wire");

                Assert.That(ascii,                                     Does.Contain("Content-Transfer-Encoding: 7bit"));
                Assert.That(ascii,                                     Does.Contain("Hello from Cologne"));

            });

        }

        [Test]
        public void An_HTML_part_has_its_Content_Type()
        {

            EMail Mail(String Html, String? Plain)
            {
                var builder = new HTMLEMailBuilder(Html, Plain) { From = Address("alice@example.com"), Subject = "HTML" };
                builder.To.Add(Address("bob@example.org"));
                return builder;
            }

            var htmlOnly    = Mail("<p>Grüße</p>", null);
            var alternative = Mail("<p>Grüße</p>", "Grüße");

            Assert.Multiple(() => {

                Assert.That(Header(Text(htmlOnly), "Content-Type"),         Does.StartWith("text/html; charset="), "not shown as source text");
                Assert.That(Text(htmlOnly),                                 Does.Contain("<p>Gr=C3=BC=C3=9Fe</p>"));

                Assert.That(Header(Text(alternative), "Content-Type"),      Does.StartWith("multipart/alternative;").And.Contain("boundary="));
                Assert.That(alternative.Body.NestedBodyparts.Select(part => part.ContentType!.ContentType),
                            Is.EqualTo(new[] { MailContentTypes.text_plain, MailContentTypes.text_html }));
                Assert.That(EMail.Parse(Text(alternative).Split("\r\n")).Body.NestedBodyparts.Count(), Is.EqualTo(2));

            });

        }

        [Test]
        public void An_attachment_is_base64_in_lines_and_its_name_encoded()
        {

            var octets  = Enumerable.Range(0, 3000).Select(i => (Byte) (i * 7)).ToArray();
            var builder = new TextEMailBuilder { From = Address("alice@example.com"), Subject = "Anhang", Text = "Anbei." };
            builder.To.Add(Address("bob@example.org"));
            builder.AddAttachment(octets, "Bericht März \"final\".pdf", MailContentTypes.application_octet__stream);

            EMail mail      = builder;
            var attachment  = mail.Body.NestedBodyparts.Last();
            var lines       = attachment.MailBody.ToArray();

            Assert.Multiple(() => {

                Assert.That(lines.Max(line => line.Length),                  Is.LessThanOrEqualTo(76), "not the whole file in one line");
                Assert.That(Convert.FromBase64String(String.Concat(lines)),  Is.EqualTo(octets));
                Assert.That(attachment.ContentDisposition,                   Is.EqualTo("attachment; filename*=utf-8''Bericht%20M%C3%A4rz%20%22final%22.pdf"));

            });

        }

        [Test]
        public void A_signature_names_its_hash_and_verifies_over_text_that_is_not_ASCII()
        {

            var builder = new TextEMailBuilder {
                              From           = new EMailAddress("Alice", SimpleEMailAddress.Parse("alice@example.com"), secretKeyRing, publicKeyRing),
                              Subject        = "Signiert",
                              Text           = $"Grüße {Marker}   \nFrom here on",
                              SecurityLevel  = EMailSecurity.sign,
                              HashAlgorithm  = HashAlgorithmTag.Sha256,
                              Passphrase     = Passphrase
                          };

            builder.To.Add(Address("bob@example.org"));

            EMail mail   = builder;
            var text     = Text(mail);
            var verified = EMail.Parse(text.Split("\r\n")).VerifyPgpSignature(publicKeyRing);

            Assert.Multiple(() => {
                Assert.That(Header(text, "Content-Type"), Does.Contain("micalg=\"pgp-sha256\""), "the hash that was used - not pgp-sha512");
                Assert.That(verified.IsValid,             Is.True, verified.Status.ToString());
            });

        }

        [Test]
        public void An_encrypted_mail_is_for_recipients_who_can_read_it()
        {

            EMailAddress Alice() => new ("Alice", SimpleEMailAddress.Parse("alice@example.com"), secretKeyRing, publicKeyRing);
            EMailAddress Bob()   => new ("Bob",   SimpleEMailAddress.Parse("bob@example.org"),   null,          recipientPublicRing);

            TextEMailBuilder Builder(EMailSecurity Security)
                => new () { From = Alice(), Subject = "Vertraulich", Text = $"Hallo {Marker}", SecurityLevel = Security, Passphrase = Passphrase };

            // auto: Carol has no key - encrypted, she could not have read it.
            var withoutKey = Builder(EMailSecurity.auto);
            withoutKey.To.Add(Bob());
            withoutKey.Cc.Add(Address("carol@example.net"));

            // auto: a blind copy - its key had been named to everybody in the ciphertext.
            var withBlind  = Builder(EMailSecurity.auto);
            withBlind.To. Add(Bob());
            withBlind.Bcc.Add(new EMailAddress("Carol", SimpleEMailAddress.Parse("carol@example.net"), null, carolPublicRing));

            var encrypted  = Builder(EMailSecurity.encrypt);
            encrypted.To.Add(Bob());

            var blindEncrypt = Builder(EMailSecurity.encrypt);
            blindEncrypt.To. Add(Bob());
            blindEncrypt.Bcc.Add(new EMailAddress("Carol", SimpleEMailAddress.Parse("carol@example.net"), null, carolPublicRing));

            Assert.Multiple(() => {

                AssertSigned(Text(withoutKey));
                AssertSigned(Text(withBlind));

                Assert.That(Text(encrypted), Does.Contain("multipart/encrypted").And.Not.Contain("signature.asc"),
                            "the control part \"Version: 1\" is no signature");

                Assert.That(() => { EMail _ = blindEncrypt; }, Throws.InstanceOf<ApplicationException>());

            });

        }

    }

}
