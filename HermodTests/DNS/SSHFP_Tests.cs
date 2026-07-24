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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS
{

    /// <summary>
    /// Offline tests for the SSHFP (RFC 4255) resource record, across every algorithm and fingerprint
    /// type and every code path a record travels: written by a server (<c>Serialize</c>), read by a
    /// client (the <c>(DomainName, Stream)</c> constructor the wire parser picks), rendered to and
    /// parsed from a zone file, and parsed from the DoH/JSON representation.
    /// </summary>
    [TestFixture]
    public class SSHFP_Tests
    {

        #region Data / test sources

        // RFC 4255 (1,2), RFC 6594 (3), RFC 7479 (4), RFC 8709 (6).
        private static readonly SSHFP_Algorithm[] algorithms = [
            SSHFP_Algorithm.RSA,
            SSHFP_Algorithm.DSS,
            SSHFP_Algorithm.ECDSA,
            SSHFP_Algorithm.Ed25519,
            SSHFP_Algorithm.Ed448
        ];

        private static readonly SSHFP_FingerprintType[] fingerprintTypes = [
            SSHFP_FingerprintType.SHA1,
            SSHFP_FingerprintType.SHA256
        ];

        /// <summary>Every algorithm × fingerprint-type combination.</summary>
        public static IEnumerable<TestCaseData> AllCombinations()
        {
            foreach (var algorithm in algorithms)
                foreach (var type in fingerprintTypes)
                    yield return new TestCaseData(algorithm, type).SetName($"{{m}}({algorithm},{type})");
        }

        /// <summary>SHA-1 fingerprints are 20 bytes, SHA-256 are 32 — in BYTES, not hex characters.</summary>
        private static Int32 LengthOf(SSHFP_FingerprintType Type)
            => Type == SSHFP_FingerprintType.SHA1 ? 20 : 32;

        private static Byte[] FingerprintFor(SSHFP_FingerprintType Type)
        {
            var fingerprint = new Byte[LengthOf(Type)];
            for (var i = 0; i < fingerprint.Length; i++)
                fingerprint[i] = (Byte) (i + 1);
            return fingerprint;
        }

        private static SSHFP Record(SSHFP_Algorithm Algorithm, SSHFP_FingerprintType Type)
            => new (DomainName.Parse("host.example.com."),
                    DNSQueryClasses.IN,
                    TimeSpan.FromSeconds(3600),
                    Algorithm,
                    Type,
                    FingerprintFor(Type));

        // The layout SSHFP(DomainName, Stream) expects once the name and type have been consumed:
        // class, TTL, RDLENGTH, then the RDATA.
        private static MemoryStream WireStream(Byte Algorithm, Byte FingerprintType, Byte[] Fingerprint)
        {

            var stream = new MemoryStream();

            stream.WriteUInt16BE((UInt16) DNSQueryClasses.IN);
            stream.WriteUInt32BE(3600);
            stream.WriteUInt16BE((UInt16) (2 + Fingerprint.Length));
            stream.WriteByte(Algorithm);
            stream.WriteByte(FingerprintType);
            stream.Write(Fingerprint, 0, Fingerprint.Length);

            stream.Seek(0, SeekOrigin.Begin);

            return stream;

        }

        #endregion


        #region Algorithm numbers

        /// <summary>
        /// Ed25519 (4) is the most common modern SSH host-key type and Ed448 (6) its larger sibling;
        /// both must be known, or every Ed25519 SSHFP record on the wire is rejected as undefined.
        /// </summary>
        [Test]
        public void AlgorithmNumbers_MatchTheRFCs()
        {
            Assert.Multiple(() => {
                Assert.That((Byte) SSHFP_Algorithm.RSA,           Is.EqualTo(1));
                Assert.That((Byte) SSHFP_Algorithm.DSS,           Is.EqualTo(2));
                Assert.That((Byte) SSHFP_Algorithm.ECDSA,         Is.EqualTo(3));
                Assert.That((Byte) SSHFP_Algorithm.Ed25519,       Is.EqualTo(4));
                Assert.That((Byte) SSHFP_Algorithm.Ed448,         Is.EqualTo(6));
                Assert.That((Byte) SSHFP_FingerprintType.SHA1,    Is.EqualTo(1));
                Assert.That((Byte) SSHFP_FingerprintType.SHA256,  Is.EqualTo(2));
            });
        }

        #endregion

        #region (client) Wire parsing

        /// <summary>
        /// A fingerprint is 20 bytes (SHA-1) or 32 bytes (SHA-256) — not 40/64, which are the lengths of
        /// its *hex* rendering. Comparing the two made every wire-parsed SSHFP record throw.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(AllCombinations))]
        public void Wire_Parse_AllAlgorithmsAndTypes(SSHFP_Algorithm Algorithm, SSHFP_FingerprintType Type)
        {

            var fingerprint = FingerprintFor(Type);

            using var stream = WireStream((Byte) Algorithm, (Byte) Type, fingerprint);

            var sshfp = new SSHFP(DomainName.Parse("host.example.com."), stream);

            Assert.Multiple(() => {
                Assert.That(sshfp.FingerprintAlgorithm,  Is.EqualTo(Algorithm));
                Assert.That(sshfp.FingerprintType,       Is.EqualTo(Type));
                Assert.That(sshfp.Fingerprint,           Is.EqualTo(fingerprint));
                Assert.That(sshfp.Type,                  Is.EqualTo(DNSResourceRecordTypes.SSHFP));
                Assert.That(sshfp.Class,                 Is.EqualTo(DNSQueryClasses.IN));
                Assert.That(sshfp.TimeToLive,            Is.EqualTo(TimeSpan.FromSeconds(3600)));
            });

        }

        #endregion

        #region (server → client) Serialize / parse round-trip

        /// <summary>
        /// What a server writes must be exactly what a client reads back: serialize the record, then
        /// consume the name and type the way the response parser does and re-parse the remainder.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(AllCombinations))]
        public void Wire_SerializeThenParse_RoundTrips(SSHFP_Algorithm Algorithm, SSHFP_FingerprintType Type)
        {

            var original = Record(Algorithm, Type);

            using var stream = new MemoryStream();
            original.Serialize(stream, UseCompression: false);
            stream.Seek(0, SeekOrigin.Begin);

            // The response parser consumes the owner name and the record type before handing the
            // stream to the (DomainName, Stream) constructor.
            var name = DNSTools.ExtractDNSServiceName(stream);
            var type = (DNSResourceRecordTypes) stream.ReadUInt16BE();

            Assert.That(type, Is.EqualTo(DNSResourceRecordTypes.SSHFP));

            var parsed = new SSHFP(DomainName.Parse(name.FullName), stream);

            Assert.Multiple(() => {
                Assert.That(parsed.FingerprintAlgorithm,  Is.EqualTo(original.FingerprintAlgorithm));
                Assert.That(parsed.FingerprintType,       Is.EqualTo(original.FingerprintType));
                Assert.That(parsed.Fingerprint,           Is.EqualTo(original.Fingerprint));
                Assert.That(parsed.Class,                 Is.EqualTo(original.Class));
                Assert.That(parsed.TimeToLive,            Is.EqualTo(original.TimeToLive));
            });

        }

        #endregion

        #region (zone file) Render / parse round-trip

        [Test]
        [TestCaseSource(nameof(AllCombinations))]
        public void ZoneFile_RoundTrips(SSHFP_Algorithm Algorithm, SSHFP_FingerprintType Type)
        {

            var original  = Record(Algorithm, Type);
            var zoneLine  = original.ToZoneFileString();

            Assert.That(ADNSResourceRecord.TryParseZoneFileString(zoneLine, out var parsed, out var error),
                        Is.True, $"{error} — line: {zoneLine}");

            Assert.That(parsed, Is.TypeOf<SSHFP>());

            var sshfp = (SSHFP) parsed!;

            Assert.Multiple(() => {
                Assert.That(sshfp.FingerprintAlgorithm,  Is.EqualTo(Algorithm));
                Assert.That(sshfp.FingerprintType,       Is.EqualTo(Type));
                Assert.That(sshfp.Fingerprint,           Is.EqualTo(original.Fingerprint));
            });

        }

        #endregion

        #region (DoH / JSON) Parsing

        /// <summary>The DoH client parses RDATA from JSON text: "&lt;algorithm&gt; &lt;type&gt; &lt;hex&gt;".</summary>
        [Test]
        [TestCaseSource(nameof(AllCombinations))]
        public void Json_Parse_AllAlgorithmsAndTypes(SSHFP_Algorithm Algorithm, SSHFP_FingerprintType Type)
        {

            var fingerprint = FingerprintFor(Type);
            var data        = $"{(Byte) Algorithm} {(Byte) Type} {Convert.ToHexString(fingerprint).ToLowerInvariant()}";

            var sshfp = SSHFP.TryParseFromJSON(DomainName.Parse("host.example.com."), TimeSpan.FromSeconds(3600), data);

            Assert.That(sshfp, Is.Not.Null, $"failed to parse JSON RDATA: {data}");

            Assert.Multiple(() => {
                Assert.That(sshfp!.FingerprintAlgorithm,  Is.EqualTo(Algorithm));
                Assert.That(sshfp!.FingerprintType,       Is.EqualTo(Type));
                Assert.That(sshfp!.Fingerprint,           Is.EqualTo(fingerprint));
            });

        }

        #endregion

        #region Rejection of malformed records

        /// <summary>The length guard must still reject a genuinely malformed fingerprint.</summary>
        [Test]
        [TestCase(SSHFP_FingerprintType.SHA1,   19)]
        [TestCase(SSHFP_FingerprintType.SHA1,   32)]
        [TestCase(SSHFP_FingerprintType.SHA256, 20)]
        [TestCase(SSHFP_FingerprintType.SHA256, 33)]
        public void Rejects_WrongFingerprintLength(SSHFP_FingerprintType Type, Int32 WrongLength)
        {
            Assert.Throws<ArgumentException>(() =>
                _ = new SSHFP(DomainName.Parse("host.example.com."),
                              DNSQueryClasses.IN,
                              TimeSpan.FromSeconds(3600),
                              SSHFP_Algorithm.Ed25519,
                              Type,
                              new Byte[WrongLength]));
        }

        /// <summary>An unassigned fingerprint type cannot be length-checked, so it must be refused.</summary>
        [Test]
        public void Rejects_UnknownFingerprintTypeOnTheWire()
        {
            using var stream = WireStream((Byte) SSHFP_Algorithm.Ed25519, 99, new Byte[32]);
            Assert.That(() => _ = new SSHFP(DomainName.Parse("host.example.com."), stream),
                        Throws.Exception);
        }

        /// <summary>An algorithm number that is not assigned is refused rather than silently mis-typed.</summary>
        [Test]
        public void Rejects_UnknownAlgorithmOnTheWire()
        {
            using var stream = WireStream(99, (Byte) SSHFP_FingerprintType.SHA256, new Byte[32]);
            Assert.That(() => _ = new SSHFP(DomainName.Parse("host.example.com."), stream),
                        Throws.Exception);
        }

        #endregion

        #region Rendering

        /// <summary>RText and the zone-file RDATA must show the fingerprint in hex, not "System.Byte[]".</summary>
        [Test]
        public void Rendering_ShowsTheFingerprintAsHex()
        {

            var sshfp = Record(SSHFP_Algorithm.Ed25519, SSHFP_FingerprintType.SHA256);
            var hex   = Convert.ToHexString(sshfp.Fingerprint).ToLowerInvariant();

            Assert.Multiple(() => {
                Assert.That(sshfp.RText,               Does.Contain(hex));
                Assert.That(sshfp.RText,               Does.Not.Contain("System.Byte[]"));
                Assert.That(sshfp.ToZoneFileString(),  Does.Contain(hex));
                Assert.That(sshfp.ToZoneFileString(),  Does.Not.Contain("System.Byte[]"));
            });

        }

        #endregion

        #region Cache extension

        /// <summary>The CacheSSHFP helper must accept every algorithm, Ed25519 included.</summary>
        [Test]
        [TestCaseSource(nameof(AllCombinations))]
        public void CacheSSHFP_AcceptsEveryAlgorithm(SSHFP_Algorithm Algorithm, SSHFP_FingerprintType Type)
        {

            var dnsClient = new DNSClient();

            Assert.DoesNotThrow(() =>
                dnsClient.CacheSSHFP(DomainName.Parse("cached.example.com."),
                                     Algorithm,
                                     Type,
                                     FingerprintFor(Type)));

        }

        #endregion

    }

}
