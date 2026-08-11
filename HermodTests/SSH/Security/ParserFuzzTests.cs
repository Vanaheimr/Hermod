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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Fuzz-light: every parser that touches bytes from a peer is fed malformed, truncated and
    /// adversarial input and must fail <i>cleanly</i>.
    ///
    /// <para>
    /// The contract under test is not "rejects bad input" — a mutation may well produce something
    /// legitimately parseable, and that is fine. It is that the parser must never lose memory safety or
    /// liveness: no <see cref="NullReferenceException"/>, no <see cref="IndexOutOfRangeException"/>, no
    /// <see cref="ArgumentOutOfRangeException"/> from unchecked arithmetic, no
    /// <see cref="OverflowException"/>, and above all no unbounded allocation driven by an
    /// attacker-supplied length field. A typed rejection (<see cref="SshWireException"/> and friends) is
    /// the desired outcome; a crash of that kind is a bug.
    /// </para>
    ///
    /// <para>
    /// Most of these parsers run <b>before authentication</b> — the identification string, KEXINIT,
    /// host keys, host certificates, signatures — so they are reachable by any unauthenticated peer.
    /// Everything here is deterministic (fixed seed, systematic truncation) so a failure reproduces
    /// exactly, and the offending input is printed as hex.
    /// </para>
    /// </summary>
    [TestFixture]
    [Category("Fuzz")]
    public class ParserFuzzTests
    {

        #region Data

        private const Int32 Seed = 20260724;

        /// <summary>
        /// Exceptions that indicate a parser lost memory safety or did unchecked arithmetic, rather than
        /// rejecting bad input on purpose.
        /// </summary>
        private static readonly HashSet<Type> ForbiddenExceptions = [
            typeof(NullReferenceException),
            typeof(IndexOutOfRangeException),
            typeof(ArgumentOutOfRangeException),
            typeof(OverflowException),
            typeof(OutOfMemoryException),
            typeof(InvalidCastException),
            typeof(KeyNotFoundException),
            typeof(DivideByZeroException),
            typeof(ArrayTypeMismatchException),
            typeof(RankException)
        ];

        #endregion

        #region (private) the contract: succeed, or fail cleanly — never crash

        // How many inputs the current test actually pushed through a parser. A fuzz suite that silently
        // iterates an empty corpus passes just as green as one that does its job, so every test asserts
        // its own volume via ExercisedAtLeast.
        private static Int32 exercised;

        [SetUp]
        public void ResetCounter()
            => exercised = 0;

        private static void ExercisedAtLeast(Int32 Expected)
            => Assert.That(exercised, Is.GreaterThanOrEqualTo(Expected),
                           $"the fuzz corpus collapsed to {exercised} input(s) — this test is not actually fuzzing anything");

        private static void MustNotCrash(String Subject, ReadOnlySpan<Byte> Input, Action Parse)
        {

            exercised++;


            var hex = Convert.ToHexString(Input.Length <= 96 ? Input : Input[..96]).ToLowerInvariant();

            try
            {
                Parse();
            }
            catch (Exception e) when (!ForbiddenExceptions.Contains(e.GetType()))
            {
                // A typed rejection is exactly what we want from malformed input.
            }
            catch (Exception e)
            {
                Assert.Fail($"{Subject}: {e.GetType().Name} — the parser must reject malformed input, not crash.\n"
                            + $"  input ({Input.Length} byte(s)): {hex}{(Input.Length > 96 ? "…" : "")}\n"
                            + $"  {e.Message}");
            }

        }

        private static void MustNotCrash(String Subject, String Input, Action Parse)
            => MustNotCrash(Subject, Encoding.UTF8.GetBytes(Input), Parse);

        #endregion

        #region The harness itself must not be permissive

        /// <summary>
        /// A fuzz harness that accepts everything is worse than none — it reports green forever. This
        /// pins both directions of the contract: a memory-safety crash fails, a typed rejection passes.
        /// </summary>
        [Test]
        public void FuzzHarness_FailsOnACrash_AndAcceptsACleanRejection()
        {

            Byte[] input = [ 0x01, 0x02, 0x03 ];

            Assert.Multiple(() => {

                Assert.Throws<AssertionException>(
                    () => MustNotCrash("simulated overrun", input, () => throw new IndexOutOfRangeException()),
                    "a parser that reads past the end must be reported");

                Assert.Throws<AssertionException>(
                    () => MustNotCrash("simulated null deref", input, () => throw new NullReferenceException()),
                    "a null dereference must be reported");

                Assert.DoesNotThrow(
                    () => MustNotCrash("clean rejection", input, () => throw new SshWireException("malformed")),
                    "a typed rejection is the desired outcome, not a failure");

                Assert.DoesNotThrow(
                    () => MustNotCrash("parses fine", input, () => { }),
                    "successfully parsing a mutation is allowed");

            });

        }

        #endregion

        #region (private) mutation strategies

        /// <summary>
        /// Every prefix of a valid sample. Systematic truncation is the single most effective way to
        /// find "read past the end" bugs, and it is exhaustive rather than lucky.
        /// </summary>
        private static IEnumerable<Byte[]> Truncations(Byte[] Sample)
        {
            for (var length = 0; length < Sample.Length; length++)
                yield return Sample[..length];
        }

        /// <summary>Single-bit flips and byte substitutions at pseudo-random positions.</summary>
        private static IEnumerable<Byte[]> BitFlips(Byte[] Sample, Random Random, Int32 Count)
        {
            for (var i = 0; i < Count && Sample.Length > 0; i++)
            {
                var mutated = (Byte[]) Sample.Clone();
                var index   = Random.Next(mutated.Length);
                mutated[index] ^= (Byte) (1 << Random.Next(8));
                yield return mutated;
            }
        }

        /// <summary>
        /// Overwrite each 4-byte-aligned position with a hostile length prefix. SSH strings are
        /// length-prefixed, so this is the shape of the classic "allocate 4 GiB" attack.
        /// </summary>
        private static IEnumerable<Byte[]> HostileLengths(Byte[] Sample)
        {

            UInt32[] lengths = [ UInt32.MaxValue, 0x7FFFFFFF, 0x40000000, 0x00FFFFFF, 0x0000FFFF ];

            for (var offset = 0; offset + 4 <= Sample.Length; offset += 4)
                foreach (var length in lengths)
                {
                    var mutated = (Byte[]) Sample.Clone();
                    mutated[offset    ] = (Byte) (length >> 24);
                    mutated[offset + 1] = (Byte) (length >> 16);
                    mutated[offset + 2] = (Byte) (length >>  8);
                    mutated[offset + 3] = (Byte)  length;
                    yield return mutated;
                }

        }

        /// <summary>Pure noise, plus a few degenerate shapes.</summary>
        private static IEnumerable<Byte[]> Noise(Random Random, Int32 Count)
        {

            yield return [];
            yield return [ 0x00 ];
            yield return [ 0xFF, 0xFF, 0xFF, 0xFF ];
            yield return new Byte[64];                    // all zeroes

            for (var i = 0; i < Count; i++)
            {
                var buffer = new Byte[Random.Next(1, 256)];
                Random.NextBytes(buffer);
                yield return buffer;
            }

        }

        /// <summary>The full mutation corpus for one valid sample.</summary>
        private static IEnumerable<Byte[]> Corpus(Byte[] Sample, Random Random)
            => Truncations(Sample)
                   .Concat(BitFlips(Sample, Random, 64))
                   .Concat(HostileLengths(Sample))
                   .Concat(Noise(Random, 32));

        #endregion


        #region (private) valid samples to mutate

        private static Byte[] ValidKexInit()
            => KexInitMessage.CreateLocal(IsServer: true).Encode();

        private static Byte[] ValidExtInfo()
            => new ExtInfoMessage(("server-sig-algs", "ssh-ed25519,rsa-sha2-512")).Encode();

        private static Byte[] ValidCertificate()
        {

            var caKey   = SshHostKey.GenerateEd25519();
            var subject = SshHostKey.GenerateEd25519();

            var builder = new OpenSshCertificateBuilder {
                Serial       = 42,
                Type         = SshCertType.User,
                KeyId        = "fuzz",
                Principals   = [ "alice" ],
                ValidAfter   = DateTimeOffset.UtcNow.AddDays(-1),
                ValidBefore  = DateTimeOffset.UtcNow.AddDays(1)
            };

            return builder.Sign(subject.PublicKeyBlob, caKey).Blob;

        }

        #endregion


        #region Wire primitives (SshPacketReader)

        /// <summary>
        /// The reader underneath every other parser. A length prefix is attacker-controlled, so it must
        /// be validated against what is actually available before a single byte is allocated.
        /// </summary>
        [Test]
        [CancelAfter(60000)]
        public void Fuzz_SshPacketReader_Primitives()
        {

            var random = new Random(Seed);

            foreach (var input in Noise(random, 512).Concat(HostileLengths(new Byte[64])))
            {

                MustNotCrash("ReadBinaryString", input, () => {
                    var reader = new SshPacketReader(input);
                    while (reader.HasMoreData) _ = reader.ReadBinaryString();
                });

                MustNotCrash("ReadString", input, () => {
                    var reader = new SshPacketReader(input);
                    while (reader.HasMoreData) _ = reader.ReadString();
                });

                MustNotCrash("ReadNameList", input, () => {
                    var reader = new SshPacketReader(input);
                    while (reader.HasMoreData) _ = reader.ReadNameList();
                });

                MustNotCrash("ReadMPInt", input, () => {
                    var reader = new SshPacketReader(input);
                    while (reader.HasMoreData) _ = reader.ReadMPInt();
                });

                MustNotCrash("mixed scalars", input, () => {
                    var reader = new SshPacketReader(input);
                    while (reader.HasMoreData)
                    {
                        _ = reader.ReadByte();
                        if (reader.Remaining >= 4)  _ = reader.ReadUInt32();
                        if (reader.Remaining >= 8)  _ = reader.ReadUInt64();
                        if (reader.HasMoreData)     _ = reader.ReadBoolean();
                    }
                });

            }

            ExercisedAtLeast(2000);

        }

        /// <summary>
        /// A declared length far beyond the packet must be refused outright — never used as an
        /// allocation size. This is the specific bug class the length cap exists to prevent.
        /// </summary>
        [Test]
        public void Fuzz_HugeDeclaredLength_IsRefusedWithoutAllocating()
        {

            // A 4-byte length of 0xFFFFFFFF followed by nothing.
            Byte[] input = [ 0xFF, 0xFF, 0xFF, 0xFF ];

            var before = GC.GetTotalAllocatedBytes(precise: true);

            Assert.Throws<SshWireException>(() => {
                var reader = new SshPacketReader(input);
                _ = reader.ReadBinaryString();
            });

            var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

            Assert.That(allocated, Is.LessThan(1_000_000),
                        $"rejecting a 4 GiB length claim must not allocate ({allocated} bytes allocated)");

        }

        #endregion

        #region Pre-auth transport parsers

        [Test]
        [CancelAfter(60000)]
        public void Fuzz_KexInitMessage_Decode()
        {
            var random = new Random(Seed);
            foreach (var input in Corpus(ValidKexInit(), random))
                MustNotCrash("KexInitMessage.Decode", input, () => _ = KexInitMessage.Decode(input));
            ExercisedAtLeast(200);

        }

        [Test]
        [CancelAfter(60000)]
        public void Fuzz_ExtInfoMessage_Decode()
        {
            var random = new Random(Seed);
            foreach (var input in Corpus(ValidExtInfo(), random))
                MustNotCrash("ExtInfoMessage.Decode", input, () => _ = ExtInfoMessage.Decode(input));
            ExercisedAtLeast(100);

        }

        // Note: the ECDH init/reply parsers (SshKexCore) are internal, so they are exercised indirectly
        // through the handshake tests rather than here — widening their visibility just to fuzz them
        // would be the wrong trade.

        /// <summary>The very first bytes of a connection, from a completely unauthenticated peer.</summary>
        [Test]
        [CancelAfter(60000)]
        public void Fuzz_IdentificationString()
        {

            var random = new Random(Seed);

            String[] seeds = [
                "SSH-2.0-OpenSSH_10.2",
                "SSH-2.0-",
                "SSH-1.99-x",
                "SSH-2.0-" + new String('A', 5000),      // overlong
                "not-an-ssh-banner",
                "SSH-2.0-x\0\0\0",
                "\u0000\u0001\u0002"   // raw control bytes, written as escapes so the file stays text
            ];

            foreach (var seed in seeds)
                MustNotCrash("SshIdentificationString.TryParse", seed,
                             () => _ = SshIdentificationString.TryParse(seed, out _, out _));

            foreach (var input in Noise(random, 256))
                MustNotCrash("SshIdentificationString.TryParse(bytes)", input,
                             () => _ = SshIdentificationString.TryParse(Encoding.UTF8.GetString(input), out _, out _));

            ExercisedAtLeast(200);

        }

        #endregion

        #region Key, certificate and signature parsers (pre-auth)

        [Test]
        [CancelAfter(60000)]
        public void Fuzz_PublicKeyBlob_And_Signature()
        {

            var random = new Random(Seed);
            var key    = SshHostKey.GenerateEd25519();
            var data   = Encoding.UTF8.GetBytes("fuzzed exchange hash");
            var good   = key.Sign(key.AlgorithmNames[0], data);

            // A corrupt public-key blob, verified against a good signature and vice versa: both blobs
            // come off the wire, so both are attacker-controlled.
            foreach (var input in Corpus(key.PublicKeyBlob, random))
            {
                MustNotCrash("SshSignature.Verify(blob)",      input, () => _ = SshSignature.Verify(input, data, good));
                MustNotCrash("SshfpRecord.FromBlob",           input, () => _ = SshfpRecord.FromBlob(input));
                MustNotCrash("SshHostKeyRotation.DecodeKeyList", input, () => _ = SshHostKeyRotation.DecodeKeyList(input));
            }

            foreach (var input in Corpus(good, random))
                MustNotCrash("SshSignature.Verify(signature)", input, () => _ = SshSignature.Verify(key.PublicKeyBlob, data, input));

            ExercisedAtLeast(500);

        }

        /// <summary>Host certificates are parsed before authentication completes.</summary>
        [Test]
        [CancelAfter(120000)]
        public void Fuzz_Certificate_Parsing()
        {
            var random = new Random(Seed);
            foreach (var input in Corpus(ValidCertificate(), random))
            {
                MustNotCrash("SshCertificate.TryParse", input, () => _ = SshCertificate.TryParse(input, out _));
                MustNotCrash("SshSignature.Verify(cert)", input, () => _ = SshSignature.Verify(input, [ 1, 2, 3 ], [ 4, 5, 6 ]));
            }
            ExercisedAtLeast(500);

        }

        /// <summary>The proof payload a peer returns for hostkeys-prove — new, and fully attacker-supplied.</summary>
        [Test]
        [CancelAfter(60000)]
        public void Fuzz_HostKeyRotation_Proofs()
        {

            var random    = new Random(Seed);
            var key       = SshHostKey.GenerateEd25519();
            var sessionId = new Byte[32];
            random.NextBytes(sessionId);

            var proofs = SshHostKeyRotation.SignProofs([ key.PublicKeyBlob ], [ key ], sessionId)!;

            foreach (var input in Corpus(proofs, random))
                MustNotCrash("SshHostKeyRotation.VerifyProofs", input,
                             () => _ = SshHostKeyRotation.VerifyProofs([ key.PublicKeyBlob ], input, sessionId));

            ExercisedAtLeast(200);

        }

        #endregion

        #region Text-format parsers (keys, trust files, recordings)

        [Test]
        [CancelAfter(60000)]
        public void Fuzz_TextFormats()
        {

            var random = new Random(Seed);
            var key    = SshHostKey.GenerateEd25519();

            var authorizedKeyLine = SshPublicKey.FromHostKey(key, "fuzz@host").ToAuthorizedKeyLine();
            var rfc4716           = SshPublicKey.FromHostKey(key, "fuzz@host").ToRfc4716();
            var privatePem        = OpenSshPrivateKey.Format(key, "fuzz");

            String[] seeds = [
                authorizedKeyLine,
                rfc4716,
                privatePem,
                "",
                "ssh-ed25519",
                "ssh-ed25519 not-base64!!",
                "ssh-ed25519 " + Convert.ToBase64String(new Byte[4]),
                "-----BEGIN OPENSSH PRIVATE KEY-----\nAAAA\n-----END OPENSSH PRIVATE KEY-----\n",
                "|1|" + Convert.ToBase64String(new Byte[20]) + "|" + Convert.ToBase64String(new Byte[20]) + " ssh-ed25519 AAAA",
                "@cert-authority *.example.com ssh-ed25519 AAAA",
                new String('A', 100_000)
            ];

            foreach (var seed in seeds.Concat(TextMutations(seeds, random)))
            {
                MustNotCrash("SshPublicKey.TryParse",      seed, () => _ = SshPublicKey.TryParse(seed, out _));
                MustNotCrash("SshPublicKey.ParseRfc4716",  seed, () => _ = SshPublicKey.ParseRfc4716(seed));
                MustNotCrash("OpenSshPrivateKey.Parse",    seed, () => _ = OpenSshPrivateKey.Parse(seed));
                MustNotCrash("SshKeyGenerator.LoadPrivateKey", seed, () => _ = SshKeyGenerator.LoadPrivateKey(seed));
                MustNotCrash("AuthorizedKeysFile.Parse",   seed, () => _ = AuthorizedKeysFile.Parse(seed));
                MustNotCrash("KnownHostsFile.Parse",       seed, () => _ = KnownHostsFile.Parse(seed));
                MustNotCrash("AsciicastReader.Parse",      seed, () => _ = AsciicastReader.Parse(seed));
            }

            ExercisedAtLeast(500);

        }

        /// <summary>Truncations and character corruption of the text seeds.</summary>
        private static IEnumerable<String> TextMutations(IEnumerable<String> Seeds, Random Random)
        {

            foreach (var seed in Seeds)
            {

                if (seed.Length == 0)
                    continue;

                // A handful of truncations (full systematic truncation would be needlessly slow on text).
                for (var i = 0; i < 12; i++)
                    yield return seed[..Random.Next(seed.Length)];

                for (var i = 0; i < 12; i++)
                {
                    var chars = seed.ToCharArray();
                    chars[Random.Next(chars.Length)] = (Char) Random.Next(32, 127);
                    yield return new String(chars);
                }

            }

        }

        #endregion

        #region Asciicast (recording replay reads attacker-influenced session output)

        [Test]
        [CancelAfter(60000)]
        public void Fuzz_AsciicastReader()
        {

            var random = new Random(Seed);

            String[] seeds = [
                "{\"version\":2,\"width\":80,\"height\":24}\n[0.1,\"o\",\"hello\"]\n",
                "{\"version\":2}\n[not json\n",
                "[1,2,3]\n",
                "{\"version\":2,\"width\":-1,\"height\":-1}\n",
                "[1e400,\"o\",\"x\"]\n",                      // overflowing double
                "{\"version\":2}\n[0.1,\"o\"]\n",             // too few elements
                "{\"version\":2}\n[0.1,\"o\",null]\n"
            ];

            foreach (var seed in seeds.Concat(TextMutations(seeds, random)))
                MustNotCrash("AsciicastReader.Parse", seed, () => _ = AsciicastReader.Parse(seed));

            ExercisedAtLeast(100);

        }

        #endregion

    }

}
