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
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.DNSSEC
{

    /// <summary>
    /// A root zone signed with NSEC3: the one zone whose closest encloser for a
    /// top-level domain is the root, and whose wildcard is "*." — the asterisk
    /// label and nothing after it.
    /// </summary>
    /// <remarks>
    /// No root on the internet is signed this way; a test bed or a private root
    /// is. Findings 76 and 77 of the conformance suite: the parser refused "*.",
    /// and the signer wrote the root's NSEC3 owner names with two dots.
    /// </remarks>
    [TestFixture]
    public class RootZoneNSEC3_Tests
    {

        #region (private) Root()

        /// <summary>
        /// A root zone: its apex, a delegation of <c>test.</c>, and an address at
        /// <c>host.tld.</c>, which makes <c>tld.</c> an empty non-terminal.
        /// </summary>
        private static InMemoryDNSZone Root()

            => new InMemoryDNSZone().
                   AddZoneFile(
                       """
                       $ORIGIN .
                       $TTL 3600
                       @          IN SOA  a.root-servers.test. hostmaster.root-servers.test. 1 7200 3600 1209600 3600
                       @          IN NS   a.root-servers.test.
                       test.      IN NS   ns.test.
                       host.tld.  IN A    192.0.2.31
                       """
                   );

        #endregion


        #region TheRootsWildcard_IsAName()

        /// <summary>
        /// RFC 4592 §2.1.1 defines a wildcard domain name by its leftmost label,
        /// and the root's is "*.".
        /// </summary>
        [Test]
        public void TheRootsWildcard_IsAName()
        {

            var wildcard = DomainName.ParseLenient("*.");

            Assert.Multiple(() => {
                Assert.That(wildcard.FullName,  Is.EqualTo("*."));
                Assert.That(wildcard.Labels,    Is.EqualTo(new[] { "*" }));
            });

        }

        #endregion

        #region TheChain_IsOneLabelBelowTheRoot()

        /// <summary>
        /// RFC 5155 §3: an NSEC3 owner name is the hash "prepended as a single label
        /// to the name of the zone", and §7.1 gives every empty non-terminal one.
        /// </summary>
        [Test]
        public void TheChain_IsOneLabelBelowTheRoot()
        {

            using var key = DNSSECSigningKey.Generate(DomainName.Parse("."), 13, KeySigningKey: true);

            var root   = DomainName.Parse(".");

            List<IDNSResourceRecord> records = [
                new SOA(root, DNSQueryClasses.IN, TimeSpan.FromHours(1),
                        DomainName.Parse("a.root-servers.test."),
                        SimpleEMailAddress.Parse("hostmaster@root-servers.test"),
                        1, TimeSpan.FromHours(2), TimeSpan.FromHours(1), TimeSpan.FromDays(14), TimeSpan.FromHours(1)),
                new NS (root,                           DNSQueryClasses.IN, TimeSpan.FromHours(1), DomainName.Parse("a.root-servers.test.")),
                new A  (DomainName.Parse("host.tld."),  DNSQueryClasses.IN, TimeSpan.FromHours(1), IPv4Address.Parse("192.0.2.31"))
            ];

            var owners = DNSSECZoneSigner.Sign(records, root, [ key ], NSEC3: NSEC3Parameters.Recommended).
                                          OfType<NSEC3>().
                                          Select(nsec3 => nsec3.DomainName.FullName).
                                          ToList();

            var ent    = NSEC3.ComputeHashedOwnerName(DomainName.Parse("tld."), DomainName.Parse("."), 0, []).FullName;

            Assert.Multiple(() => {
                Assert.That(owners,                                   Is.Not.Empty);
                Assert.That(owners.Select(o => o.TrimEnd('.').Split('.').Length), Is.All.EqualTo(1));
                Assert.That(owners, Does.Contain(ent).IgnoreCase,     "tld. exists because host.tld. does");
            });

        }

        #endregion

        #region ANameError_IsProvenWithTheRootsWildcard()

        /// <summary>
        /// RFC 5155 §7.2.2 and §8.4: a name error at the root carries the root's
        /// record, a cover of the name, and a cover of "*." — and the proof the
        /// server sends is one the validator accepts.
        /// </summary>
        [Test]
        public async Task ANameError_IsProvenWithTheRootsWildcard()
        {

            using var key = DNSSECSigningKey.Generate(DomainName.Parse("."), 13, KeySigningKey: true);

            var zone   = Root().Sign([ key ], NSEC3: NSEC3Parameters.Recommended);

            var result = await zone.Lookup(new DNSQuestion(DNSServiceName.Parse("zz."), DNSResourceRecordTypes.A, DNSQueryClasses.IN),
                                           DNSSECOK: true);

            Assert.Multiple(() => {

                Assert.That(result.Status, Is.EqualTo(DNSZoneLookupStatus.NameError));

                Assert.That(DenialOfExistenceValidator.Verify(DomainName.Parse("zz."),
                                                              DNSResourceRecordTypes.A,
                                                              result.AuthorityRRs),
                            Is.EqualTo(DenialOfExistence.NameDoesNotExist));

            });

        }

        #endregion

    }

}
