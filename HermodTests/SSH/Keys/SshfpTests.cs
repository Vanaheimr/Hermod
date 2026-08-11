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

using System.Security.Cryptography;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>M8 SSHFP (RFC 4255): record generation (ssh-keygen -r equivalent) and trust-mode verification.</summary>
    [TestFixture]
    public class SshfpTests
    {

        #region FromHostKey_ProducesSha1AndSha256_WithCorrectAlgorithm

        [Test]
        public void FromHostKey_ProducesSha1AndSha256_WithCorrectAlgorithm()
        {

            var key     = SshHostKey.GenerateEd25519();
            var records = SshfpRecord.FromHostKey(key);

            var sha1   = records.Single(r => r.FingerprintType == SshfpFingerprintType.Sha1);
            var sha256 = records.Single(r => r.FingerprintType == SshfpFingerprintType.Sha256);

            Assert.Multiple(() => {
                Assert.That(records,               Has.Count.EqualTo(2));
                Assert.That(sha256.Algorithm,      Is.EqualTo(SshfpAlgorithm.Ed25519));
                Assert.That(sha1.FingerprintHex,   Is.EqualTo(Convert.ToHexStringLower(SHA1.HashData(key.PublicKeyBlob))));
                Assert.That(sha256.FingerprintHex, Is.EqualTo(Convert.ToHexStringLower(SHA256.HashData(key.PublicKeyBlob))));
                Assert.That(sha256.ToZoneLine("host.example."), Is.EqualTo($"host.example. IN SSHFP 4 2 {sha256.FingerprintHex}"));
                Assert.That(sha256.Matches(key.PublicKeyBlob),  Is.True);
            });

        }

        #endregion

        #region Algorithm_MappingPerKeyType

        [Test]
        public void Algorithm_MappingPerKeyType()
        {
            Assert.Multiple(() => {
                Assert.That(SshfpRecord.FromHostKey(SshHostKey.GenerateEd25519())[0].Algorithm,          Is.EqualTo(SshfpAlgorithm.Ed25519));
                Assert.That(SshfpRecord.FromHostKey(SshHostKey.GenerateEcdsa("ecdsa-sha2-nistp256"))[0].Algorithm, Is.EqualTo(SshfpAlgorithm.Ecdsa));
                Assert.That(SshfpRecord.FromHostKey(SshHostKey.GenerateRsa(2048))[0].Algorithm,          Is.EqualTo(SshfpAlgorithm.Rsa));
            });
        }

        #endregion

        #region Verifier_TrustModes

        [Test]
        [CancelAfter(15000)]
        public async Task Verifier_TrustModes(CancellationToken CancellationToken)
        {

            var key   = SshHostKey.GenerateEd25519();
            var other = SshHostKey.GenerateEd25519();

            // RequireDnssec + validated + matching → auto-accept.
            var secure = new SshfpVerifier(new InMemorySshfpResolver().Add("host", key, DnssecValidated: true), SshfpTrust.RequireDnssec);
            // RequireDnssec + unvalidated → advisory only.
            var insecure = new SshfpVerifier(new InMemorySshfpResolver().Add("host", key, DnssecValidated: false), SshfpTrust.RequireDnssec);
            // Records for a different key → mismatch.
            var wrong = new SshfpVerifier(new InMemorySshfpResolver().Add("host", other, DnssecValidated: true), SshfpTrust.RequireDnssec);
            // Advisory never auto-accepts, even validated.
            var advisory = new SshfpVerifier(new InMemorySshfpResolver().Add("host", key, DnssecValidated: true), SshfpTrust.Advisory);
            // Off ignores everything.
            var off = new SshfpVerifier(new InMemorySshfpResolver().Add("host", key, DnssecValidated: true), SshfpTrust.Off);

            Assert.Multiple(() => {
                Assert.That(secure.VerifyAsync("host", key.PublicKeyBlob, CancellationToken).Result,     Is.EqualTo(SshfpVerdict.SecureMatch));
                Assert.That(insecure.VerifyAsync("host", key.PublicKeyBlob, CancellationToken).Result,   Is.EqualTo(SshfpVerdict.InsecureMatch));
                Assert.That(wrong.VerifyAsync("host", key.PublicKeyBlob, CancellationToken).Result,      Is.EqualTo(SshfpVerdict.Mismatch));
                Assert.That(advisory.VerifyAsync("host", key.PublicKeyBlob, CancellationToken).Result,   Is.EqualTo(SshfpVerdict.InsecureMatch));
                Assert.That(off.VerifyAsync("host", key.PublicKeyBlob, CancellationToken).Result,        Is.EqualTo(SshfpVerdict.NoRecords));
                Assert.That(secure.VerifyAsync("unknown", key.PublicKeyBlob, CancellationToken).Result,  Is.EqualTo(SshfpVerdict.NoRecords));
            });

            await Task.CompletedTask;

        }

        #endregion

    }

}
