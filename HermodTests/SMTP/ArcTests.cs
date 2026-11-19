/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Security.Cryptography;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// The Authenticated Received Chain (RFC 8617): chains sealed by <see cref="ArcSealer"/>,
    /// or by hand where a chain has to be broken in a way the sealer never breaks it, and
    /// validated by <see cref="ArcValidator"/> with the key of the test.
    /// </summary>
    [TestFixture]
    public class ArcTests
    {

        #region Data

        private const String AAR = "ARC-Authentication-Results";
        private const String AMS = "ARC-Message-Signature";
        private const String AS  = "ARC-Seal";

        private sealed class QuietLogger : ILogger
        {
            public void Log(LogLevel level, String message) { }
        }

        private RSA    key  = null!;
        private String spki = null!;

        [OneTimeSetUp]
        public void MakeKey()
        {
            key  = RSA.Create(2048);
            spki = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        }

        [OneTimeTearDown]
        public void DisposeKey()
            => key.Dispose();

        #endregion

        #region (private) helpers

        private static String Message()

            => "From: <news@origin.example>\r\n" +
               "To: <list@forwarder.example>\r\n" +
               "Subject: sealed\r\n" +
               "Date: Thu, 19 Nov 2026 10:00:00 +0000\r\n" +
               $"Message-ID: <{Guid.NewGuid():N}@origin.example>\r\n" +
               "\r\n" +
               "Hello through a list.\r\n";

        private ArcSealer Sealer(String? SignedHeaders = null)

            => new (SignedHeaders is null
                        ? new ArcConfig { Domain = "forwarder.example", Selector = "arc", PrivateKeyPem = key.ExportPkcs8PrivateKeyPem() }
                        : new ArcConfig { Domain = "forwarder.example", Selector = "arc", PrivateKeyPem = key.ExportPkcs8PrivateKeyPem(), SignedHeaders = SignedHeaders },
                    new QuietLogger());

        /// <summary>
        /// The message with one more set sealed by <see cref="ArcSealer"/>, the chain as received
        /// having passed (or, for the first set, there being none).
        /// </summary>
        private String Seal(String Message)

            => Sealer().Seal(Message, "forwarder.example", "spf=pass smtp.mailfrom=origin.example",
                             ArcChain.Parse(DkimCanonicalization.ParseFields(DkimCanonicalization.Split(Message).Headers)) is null
                                 ? ArcResult.None
                                 : ArcResult.Pass);

        private String Sealed(Int32 Sets)
        {
            var message = Message();
            for (var i = 1; i <= Sets; i++)
                message = Seal(message);
            return message;
        }

        private Task<ArcResult> Validate(String Message)

            => new ArcValidator((domain, selector, ct) => Task.FromResult<String?>(spki), new QuietLogger()).
                   ValidateAsync(Message);

        private static String Relaxed(String Name, String Value)
            => DkimCanonicalization.CanonicalizeHeader(new DkimHeaderField(Name, " " + Value, $"{Name}: {Value}"), "relaxed");

        private static String Place(String Tags, String Signature, Boolean First)
            => First ? $"b={Signature}; {Tags}" : $"{Tags}; b={Signature}";

        private String Sign(String Input, HashAlgorithmName Hash)
            => Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(Input), Hash, RSASignaturePadding.Pkcs1));

        /// <summary>
        /// One more set sealed by hand after RFC 8617 §4.1 and §5.1.1 - without Hermod's sealer,
        /// and without its removal of the b= value, so that it can carry what a sealer must
        /// not write: any instance and cv= as given, another algorithm, more tags in the AS,
        /// b= as the first tag.
        /// </summary>
        private String HandSeal(String   Message,
                                Int32    Instance,
                                String   Cv,
                                String   Algorithm       = "rsa-sha256",
                                String?  ExtraSealTags   = null,
                                Boolean  SignatureFirst  = false)
        {

            var (head, body) = DkimCanonicalization.Split(Message);
            var fields       = DkimCanonicalization.ParseFields(head);
            var hash         = Algorithm == "rsa-sha1" ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256;

            // §4.1.1
            var aar          = $"i={Instance}; forwarder.example; spf=pass smtp.mailfrom=origin.example";

            // §4.1.2: a DKIM signature over From, To and Subject.
            var bodyOctets   = Encoding.UTF8.GetBytes(DkimCanonicalization.CanonicalizeBody(body, "relaxed"));
            var bh           = Convert.ToBase64String(hash == HashAlgorithmName.SHA1 ? SHA1.HashData(bodyOctets) : SHA256.HashData(bodyOctets));
            var amsTags      = $"i={Instance}; a={Algorithm}; c=relaxed/relaxed; d=forwarder.example; s=arc; t=1763546400; h=from:to:subject; bh={bh}";
            var amsInput     = new StringBuilder();

            foreach (var name in new[] { "from", "to", "subject" })
                amsInput.Append(DkimCanonicalization.CanonicalizeHeader(fields.Last(field => field.Name.Equals(name, StringComparison.OrdinalIgnoreCase)), "relaxed")).Append("\r\n");

            amsInput.Append(Relaxed(AMS, Place(amsTags, "", SignatureFirst)));
            var ams          = Place(amsTags, Sign(amsInput.ToString(), hash), SignatureFirst);

            // §4.1.3, §5.1.1: the sets 1..i, each AAR, AMS, AS - the new AS with b= empty and
            // without its final CRLF.
            var asTags       = $"i={Instance}; a={Algorithm}; cv={Cv}; d=forwarder.example; s=arc; t=1763546400" +
                               (ExtraSealTags is null ? "" : "; " + ExtraSealTags);
            var asInput      = new StringBuilder();

            for (var i = 1; i < Instance; i++)
                foreach (var name in new[] { AAR, AMS, AS })
                    foreach (var field in fields.Where(field => field.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && ArcChain.InstanceOf(field.RawValue) == i).Take(1))
                        asInput.Append(DkimCanonicalization.CanonicalizeHeader(field, "relaxed")).Append("\r\n");

            asInput.Append(Relaxed(AAR, aar)).Append("\r\n");
            asInput.Append(Relaxed(AMS, ams)).Append("\r\n");
            asInput.Append(Relaxed(AS,  Place(asTags, "", SignatureFirst)));

            var seal         = Place(asTags, Sign(asInput.ToString(), hash), SignatureFirst);

            return $"{AS}: {seal}\r\n{AMS}: {ams}\r\n{AAR}: {aar}\r\n{Message}";

        }

        /// <summary>
        /// The values of the header fields of that name, top to bottom.
        /// </summary>
        private static List<DkimHeaderField> Fields(String Message, String Name)

            => [ .. DkimCanonicalization.ParseFields(DkimCanonicalization.Split(Message).Headers).
                                         Where(field => field.Name.Equals(Name, StringComparison.OrdinalIgnoreCase)) ];

        /// <summary>
        /// A message whose three ARC header fields all claim the instance 2147483647.
        /// </summary>
        private static String HighestInstanceMessage()

            => "ARC-Seal: i=2147483647; a=rsa-sha256; cv=none; d=forwarder.example; s=arc; t=1763546400; b=AAAA\r\n" +
               "ARC-Message-Signature: i=2147483647; a=rsa-sha256; c=relaxed/relaxed; d=forwarder.example; s=arc; h=from; bh=AAAA; b=AAAA\r\n" +
               "ARC-Authentication-Results: i=2147483647; forwarder.example; spf=pass\r\n" +
               Message();

        #endregion


        #region Guards

        [Test(Description = "RFC 8617 §5.2: one set sealed by ArcSealer, cv=none - the chain passes")]
        public async Task A_chain_of_one_set_passes()
            => Assert.That(await Validate(Sealed(1)), Is.EqualTo(ArcResult.Pass));

        [Test(Description = "RFC 8617 §5.2: three sets sealed by ArcSealer, cv=none, pass, pass - the chain passes")]
        public async Task A_chain_of_three_sets_passes()
            => Assert.That(await Validate(Sealed(3)), Is.EqualTo(ArcResult.Pass));

        [Test(Description = "RFC 8617 §5.2: a set sealed by hand on a set sealed by ArcSealer - the chain passes; a guard for the hand sealer")]
        public async Task A_set_sealed_by_hand_passes()
            => Assert.That(await Validate(HandSeal(Sealed(1), 2, "pass")), Is.EqualTo(ArcResult.Pass));

        [Test(Description = "RFC 8617 §5.2 step 4: the body changed after the newest AMS - the chain fails")]
        public async Task A_chain_over_a_changed_body_fails()
            => Assert.That(await Validate(Sealed(2).Replace("Hello through a list.", "Hello from somebody else.")), Is.EqualTo(ArcResult.Fail));

        #endregion

        #region The limit of 50 sets

        [Test(Description = "RFC 8617 §5.2 step 1: \"The maximum number of ARC Sets that can be attached to a message is 50\" - 50 sets pass")]
        public async Task A_chain_of_50_sets_passes()
            => Assert.That(await Validate(Sealed(50)), Is.EqualTo(ArcResult.Pass));

        [Test(Description = "RFC 8617 §5.2 step 1: \"If more than the maximum number exist, the Chain Validation Status is \"fail\"\" - 51 sets")]
        public async Task A_chain_of_51_sets_fails()
            => Assert.That(await Validate(HandSeal(Sealed(50), 51, "pass")), Is.EqualTo(ArcResult.Fail));

        [Test(Description = "RFC 8617 §4.2.1: \"Instance tag values can range from 1-50 (inclusive)\" - i=2147483647 fails, and at once: no loop counts up to an instance the sender chose")]
        public async Task An_instance_number_of_2147483647_fails_at_once()
        {

            // Run elsewhere: the validator parses the chain before its first await.
            var validation = Task.Run(() => Validate(HighestInstanceMessage()));

            Assert.That(await Task.WhenAny(validation, Task.Delay(TimeSpan.FromSeconds(5))), Is.SameAs(validation),
                        "the validator is still counting instances after 5 s");

            Assert.That(await validation, Is.EqualTo(ArcResult.Fail));

        }

        [Test(Description = "RFC 8617 §4.2.1: \"Instance tag values can range from 1-50 (inclusive)\" - the sealer adds no 51st set")]
        public void A_chain_of_50_sets_is_not_sealed_again()
        {

            var received = Sealed(50);

            Assert.That(Sealer().Seal(received, "relay.example", "arc=pass", ArcResult.Pass), Is.EqualTo(received));

        }

        [Test(Description = "RFC 8617 §4.2.1: \"Instance tag values can range from 1-50 (inclusive)\" - the sealer adds no set to i=2147483647, and does not count up to it")]
        public async Task An_instance_number_of_2147483647_is_not_sealed_again()
        {

            var received = HighestInstanceMessage();
            var sealing  = Task.Run(() => Sealer().Seal(received, "relay.example", "arc=fail", ArcResult.Fail));

            Assert.That(await Task.WhenAny(sealing, Task.Delay(TimeSpan.FromSeconds(5))), Is.SameAs(sealing),
                        "the sealer is still counting instances after 5 s");

            Assert.That(await sealing, Is.EqualTo(received));

        }

        [Test(Description = "RFC 8617 §5.1 step 3: the instance \"is 1 more than the highest instance number found\" - sets 1 and 3 get a set 4, and §5.1.2: a failed chain is sealed \"as if this newest ARC Set was the only set present\"")]
        public void A_chain_with_a_gap_is_sealed_over_the_new_set_alone()
        {

            var sealed_  = Sealer().Seal(HandSeal(Sealed(1), 3, "pass"), "relay.example", "arc=fail", ArcResult.Fail);
            var newest   = Fields(sealed_, AS).First();
            var tags     = ArcChain.ParseTags(newest.RawValue);
            var set      = new ArcSet(4, newest, Fields(sealed_, AMS).First(), Fields(sealed_, AAR).First());

            Assert.Multiple(() => {
                Assert.That(tags["i"],  Is.EqualTo("4"));
                Assert.That(tags["cv"], Is.EqualTo("fail"));
                Assert.That(key.VerifyData(Encoding.UTF8.GetBytes(ArcChain.BuildSealSigningInput([ set ], 1)),
                                           Convert.FromBase64String(tags["b"].Replace(" ", "").Replace("\t", "")),
                                           HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
                            Is.True, "the seal signs its own set alone");
            });

        }

        [Test(Description = "RFC 8617 §5.1.2: \"In the case of a failed Authenticated Received Chain\", the AS signs the new set \"as if this newest ARC Set was the only set present\" - a chain whose body changed")]
        public void A_chain_that_failed_validation_is_sealed_over_the_new_set_alone()
        {

            var sealed_  = Sealer().Seal(Sealed(1).Replace("Hello through a list.", "Hello from somebody else."), "relay.example", "arc=fail", ArcResult.Fail);
            var newest   = Fields(sealed_, AS).First();
            var tags     = ArcChain.ParseTags(newest.RawValue);
            var set      = new ArcSet(2, newest, Fields(sealed_, AMS).First(), Fields(sealed_, AAR).First());

            Assert.Multiple(() => {
                Assert.That(tags["i"],  Is.EqualTo("2"));
                Assert.That(tags["cv"], Is.EqualTo("fail"));
                Assert.That(key.VerifyData(Encoding.UTF8.GetBytes(ArcChain.BuildSealSigningInput([ set ], 1)),
                                           Convert.FromBase64String(tags["b"].Replace(" ", "").Replace("\t", "")),
                                           HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
                            Is.True, "the seal signs its own set alone");
            });

        }

        #endregion

        #region The tags of an ARC-Seal

        [Test(Description = "RFC 8617 §4.1.3: \"the DKIM \"h\" tag is NOT allowed and, if found, MUST result in a cv status of \"fail\"\" - in the first set and in the second")]
        public async Task An_ARC_Seal_with_an_h_tag_fails()
        {

            var first  = await Validate(HandSeal(Message(), 1, "none", ExtraSealTags: "h=from:to"));
            var second = await Validate(HandSeal(Sealed(1), 2, "pass", ExtraSealTags: "h=from:to"));

            Assert.Multiple(() => {
                Assert.That(first,  Is.EqualTo(ArcResult.Fail), "h= in the AS of instance 1");
                Assert.That(second, Is.EqualTo(ArcResult.Fail), "h= in the AS of instance 2");
            });

        }

        #endregion

    }

}
