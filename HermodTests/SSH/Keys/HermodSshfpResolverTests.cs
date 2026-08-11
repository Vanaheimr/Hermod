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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// The Hermod-DNS <see cref="ISshfpResolver"/> adapter, driven entirely off the DNS client's cache
    /// so no network is involved: records published for a host must come back mapped onto
    /// <see cref="SshfpRecord"/> and must verify the host key they were derived from.
    /// </summary>
    [TestFixture]
    public class HermodSshfpResolverTests
    {

        #region (private) helpers

        private static SSHFP_Algorithm ToHermod(SshfpAlgorithm Algorithm)
            => (SSHFP_Algorithm) (Byte) Algorithm;

        private static SSHFP_FingerprintType ToHermod(SshfpFingerprintType Type)
            => (SSHFP_FingerprintType) (Byte) Type;

        // Publish the SSHFP records of a host key into the DNS client's cache, exactly as a zone would.
        private static void Publish(DNSClient DNSClient, String Host, ISshHostKey HostKey)
        {
            foreach (var record in SshfpRecord.FromHostKey(HostKey))
                DNSClient.CacheSSHFP(DomainName.Parse(Host),
                                     ToHermod(record.Algorithm),
                                     ToHermod(record.FingerprintType),
                                     record.Fingerprint);
        }

        #endregion


        #region Resolver_ReturnsPublishedRecords_AndVerifiesTheHostKey

        /// <summary>
        /// Ed25519 is the case that used to be impossible: Hermod refused algorithm 4 outright, so this
        /// covers both the adapter and the record mapping underneath it.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        [TestCase("ssh-ed25519")]
        [TestCase("ecdsa-sha2-nistp256")]
        [TestCase("ssh-rsa")]
        public async Task Resolver_ReturnsPublishedRecords_AndVerifiesTheHostKey(String KeyType, CancellationToken CancellationToken)
        {

            var hostKey   = SshKeyGenerator.Generate(KeyType);
            var dnsClient = new DNSClient();
            const String host = "sshfp-resolver-test.example.";

            Publish(dnsClient, host, hostKey);

            var resolver = new HermodSshfpResolver(dnsClient);
            var result   = await resolver.QueryAsync(host, CancellationToken);

            Assert.Multiple(() => {

                Assert.That(result.Records, Is.Not.Empty, "the published SSHFP records must come back");

                Assert.That(result.Records.Any(r => r.FingerprintType == SshfpFingerprintType.Sha256), Is.True,
                            "the SHA-256 record must survive the mapping");

                Assert.That(result.Records.Any(r => r.Matches(hostKey.PublicKeyBlob)), Is.True,
                            "a returned record must verify the host key it was derived from");

                Assert.That(result.DnssecValidated, Is.False,
                            "without a DNSSEC validator the answer must never be reported as validated");

            });

        }

        #endregion

        #region Resolver_FeedsTheSshfpVerifier

        /// <summary>The adapter must slot into SshfpVerifier and produce a match under Advisory trust.</summary>
        [Test]
        [CancelAfter(20000)]
        public async Task Resolver_FeedsTheSshfpVerifier(CancellationToken CancellationToken)
        {

            var hostKey   = SshHostKey.GenerateEd25519();
            var other     = SshHostKey.GenerateEd25519();
            var dnsClient = new DNSClient();
            const String host = "sshfp-verifier-test.example.";

            Publish(dnsClient, host, hostKey);

            var verifier = new SshfpVerifier(new HermodSshfpResolver(dnsClient), SshfpTrust.Advisory);

            var matched  = await verifier.VerifyAsync(host, hostKey.PublicKeyBlob, CancellationToken);
            var mismatch = await verifier.VerifyAsync(host, other.PublicKeyBlob,   CancellationToken);

            Assert.Multiple(() => {
                Assert.That(matched,  Is.Not.EqualTo(SshfpVerdict.NoRecords), "the published key must be found");
                Assert.That(mismatch, Is.Not.EqualTo(matched),                "a different key must not verify the same way");
            });

        }

        #endregion

        #region Resolver_UnknownHost_IsEmpty

        /// <summary>An unresolvable name yields no records rather than throwing — SSHFP must never break a login.</summary>
        [Test]
        [CancelAfter(20000)]
        public async Task Resolver_UnknownHost_IsEmpty(CancellationToken CancellationToken)
        {

            var resolver = new HermodSshfpResolver(new DNSClient(), QueryTimeout: TimeSpan.FromMilliseconds(250));

            var result = await resolver.QueryAsync("no-such-host.invalid.", CancellationToken);

            Assert.That(result.Records, Is.Empty);
            Assert.That(result.DnssecValidated, Is.False);

        }

        #endregion

        #region Resolver_MalformedHostname_IsEmpty

        [Test]
        [CancelAfter(20000)]
        public async Task Resolver_MalformedHostname_IsEmpty(CancellationToken CancellationToken)
        {
            var resolver = new HermodSshfpResolver(new DNSClient(), QueryTimeout: TimeSpan.FromMilliseconds(250));
            var result   = await resolver.QueryAsync("", CancellationToken);
            Assert.That(result.Records, Is.Empty);
        }

        #endregion

    }

}
