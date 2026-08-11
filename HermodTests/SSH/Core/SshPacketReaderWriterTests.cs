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
using System.Buffers;
using System.Numerics;
using System.Globalization;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Unit tests for the SSH binary wire format reader/writer (RFC 4251, section 5).
    /// </summary>
    [TestFixture]
    public class SshPacketReaderWriterTests
    {

        #region (private) Write(Action)

        /// <summary>
        /// Run the given write action against a fresh buffer and return the assembled bytes.
        /// </summary>
        private static Byte[] Write(WriteDelegate Action)
        {
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            Action(ref writer);
            return abw.WrittenSpan.ToArray();
        }

        private delegate void WriteDelegate(ref SshPacketWriter Writer);

        #endregion


        #region Byte / Boolean

        [Test]
        public void Byte_RoundTrip()
        {

            var bytes = Write((ref SshPacketWriter w) => { w.WriteByte(0x00); w.WriteByte(0x7F); w.WriteByte(0xFF); });

            Assert.That(bytes, Is.EqualTo(new Byte[] { 0x00, 0x7F, 0xFF }));

            var reader  = new SshPacketReader(bytes);
            var b0      = reader.ReadByte();
            var b1      = reader.ReadByte();
            var b2      = reader.ReadByte();
            var hasMore = reader.HasMoreData;

            Assert.Multiple(() => {
                Assert.That(b0,      Is.EqualTo(0x00));
                Assert.That(b1,      Is.EqualTo(0x7F));
                Assert.That(b2,      Is.EqualTo(0xFF));
                Assert.That(hasMore, Is.False);
            });

        }

        [Test]
        public void Boolean_RoundTrip()
        {

            var bytes = Write((ref SshPacketWriter w) => { w.WriteBoolean(true); w.WriteBoolean(false); });

            Assert.That(bytes, Is.EqualTo(new Byte[] { 0x01, 0x00 }));

            var reader = new SshPacketReader(bytes);
            var first  = reader.ReadBoolean();
            var second = reader.ReadBoolean();

            Assert.Multiple(() => {
                Assert.That(first,  Is.True);
                Assert.That(second, Is.False);
            });

        }

        [Test]
        public void Boolean_AnyNonZeroIsTrue()
        {
            // RFC 4251: "All non-zero values MUST be interpreted as TRUE".
            var reader = new SshPacketReader(new Byte[] { 0x02 });
            Assert.That(reader.ReadBoolean(), Is.True);
        }

        #endregion

        #region UInt32 / UInt64

        [Test]
        public void UInt32_IsBigEndian()
        {

            var bytes = Write((ref SshPacketWriter w) => w.WriteUInt32(0x01020304u));

            Assert.That(bytes, Is.EqualTo(new Byte[] { 0x01, 0x02, 0x03, 0x04 }));
            Assert.That(new SshPacketReader(bytes).ReadUInt32(), Is.EqualTo(0x01020304u));

        }

        [Test]
        public void UInt64_IsBigEndian()
        {

            var bytes = Write((ref SshPacketWriter w) => w.WriteUInt64(0x0102030405060708ul));

            Assert.That(bytes, Is.EqualTo(new Byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 }));
            Assert.That(new SshPacketReader(bytes).ReadUInt64(), Is.EqualTo(0x0102030405060708ul));

        }

        [TestCase(0u)]
        [TestCase(1u)]
        [TestCase(255u)]
        [TestCase(65536u)]
        [TestCase(UInt32.MaxValue)]
        public void UInt32_RoundTrip(UInt32 Value)
        {
            var bytes = Write((ref SshPacketWriter w) => w.WriteUInt32(Value));
            Assert.That(new SshPacketReader(bytes).ReadUInt32(), Is.EqualTo(Value));
        }

        #endregion

        #region String (binary + UTF-8)

        [Test]
        public void BinaryString_RoundTrip()
        {

            var payload = new Byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            var bytes   = Write((ref SshPacketWriter w) => w.WriteBinaryString(payload));

            // uint32 length prefix + data
            Assert.That(bytes, Is.EqualTo(new Byte[] { 0x00, 0x00, 0x00, 0x04, 0xDE, 0xAD, 0xBE, 0xEF }));
            Assert.That(new SshPacketReader(bytes).ReadBinaryString(), Is.EqualTo(payload));

        }

        [Test]
        public void EmptyString_RoundTrip()
        {

            var bytes = Write((ref SshPacketWriter w) => w.WriteBinaryString(ReadOnlySpan<Byte>.Empty));

            Assert.That(bytes, Is.EqualTo(new Byte[] { 0x00, 0x00, 0x00, 0x00 }));
            Assert.That(new SshPacketReader(bytes).ReadBinaryString(), Is.Empty);

        }

        [Test]
        public void Utf8String_RoundTrip()
        {

            const String text = "Schöne Grüße — SSH ☺";
            var bytes  = Write((ref SshPacketWriter w) => w.WriteString(text));
            var reader = new SshPacketReader(bytes);

            Assert.That(reader.ReadString(), Is.EqualTo(text));

        }

        [Test]
        public void Utf8String_LengthIsByteCountNotCharCount()
        {

            const String text = "ä";                            // 1 char, 2 UTF-8 bytes
            var bytes = Write((ref SshPacketWriter w) => w.WriteString(text));

            Assert.That(bytes, Is.EqualTo(new Byte[] { 0x00, 0x00, 0x00, 0x02, 0xC3, 0xA4 }));

        }

        #endregion

        #region Name-list

        [Test]
        public void NameList_RoundTrip()
        {

            String[] names = ["curve25519-sha256", "ecdh-sha2-nistp256", "diffie-hellman-group14-sha256"];
            var bytes      = Write((ref SshPacketWriter w) => w.WriteNameList(names));
            var reader     = new SshPacketReader(bytes);

            Assert.That(reader.ReadNameList(), Is.EqualTo(names));

        }

        [Test]
        public void NameList_WireLayout()
        {

            // "zlib,none" => length 9, then the ASCII bytes.
            var bytes = Write((ref SshPacketWriter w) => w.WriteNameList("zlib", "none"));

            Assert.That(bytes, Is.EqualTo(new Byte[] {
                0x00, 0x00, 0x00, 0x09,
                (Byte) 'z', (Byte) 'l', (Byte) 'i', (Byte) 'b', (Byte) ',',
                (Byte) 'n', (Byte) 'o', (Byte) 'n', (Byte) 'e'
            }));

        }

        [Test]
        public void EmptyNameList_RoundTrip()
        {

            var bytes = Write((ref SshPacketWriter w) => w.WriteNameList());

            Assert.That(bytes,                                 Is.EqualTo(new Byte[] { 0x00, 0x00, 0x00, 0x00 }));
            Assert.That(new SshPacketReader(bytes).ReadNameList(), Is.Empty);

        }

        [Test]
        public void NameList_RejectsCommaInName()
        {
            Assert.Throws<SshWireException>(() => Write((ref SshPacketWriter w) => w.WriteNameList("a,b")));
        }

        [Test]
        public void NameList_RejectsEmptyName()
        {
            Assert.Throws<SshWireException>(() => Write((ref SshPacketWriter w) => w.WriteNameList("ok", "")));
        }

        #endregion

        #region MPInt (RFC 4251, section 5 test vectors)

        // value (hex)      => wire representation (hex)
        //   0                 00 00 00 00
        //   9a378f9b2e332a7   00 00 00 08 09 a3 78 f9 b2 e3 32 a7
        //   80                00 00 00 02 00 80
        //  -1234              00 00 00 02 ed cc
        //  -deadbeef          00 00 00 05 ff 21 52 41 11
        public static System.Collections.Generic.IEnumerable<TestCaseData> MPIntVectors()
        {
            yield return new TestCaseData(BigInteger.Zero,
                                          new Byte[] { 0x00, 0x00, 0x00, 0x00 }).SetName("MPInt_Zero");
            yield return new TestCaseData(BigInteger.Parse("09a378f9b2e332a7", NumberStyles.HexNumber),
                                          new Byte[] { 0x00, 0x00, 0x00, 0x08, 0x09, 0xa3, 0x78, 0xf9, 0xb2, 0xe3, 0x32, 0xa7 }).SetName("MPInt_Positive");
            yield return new TestCaseData(new BigInteger(0x80),
                                          new Byte[] { 0x00, 0x00, 0x00, 0x02, 0x00, 0x80 }).SetName("MPInt_LeadingZeroForSignBit");
            yield return new TestCaseData(new BigInteger(-0x1234),
                                          new Byte[] { 0x00, 0x00, 0x00, 0x02, 0xed, 0xcc }).SetName("MPInt_NegativeSmall");
            yield return new TestCaseData(new BigInteger(-0xdeadbeefL),
                                          new Byte[] { 0x00, 0x00, 0x00, 0x05, 0xff, 0x21, 0x52, 0x41, 0x11 }).SetName("MPInt_NegativeLarge");
        }

        [TestCaseSource(nameof(MPIntVectors))]
        public void MPInt_Encode(BigInteger Value, Byte[] Expected)
        {
            var bytes = Write((ref SshPacketWriter w) => w.WriteMPInt(Value));
            Assert.That(bytes, Is.EqualTo(Expected));
        }

        [TestCaseSource(nameof(MPIntVectors))]
        public void MPInt_Decode(BigInteger Expected, Byte[] Wire)
        {
            var reader = new SshPacketReader(Wire);
            Assert.That(reader.ReadMPInt(), Is.EqualTo(Expected));
        }

        [Test]
        public void MPInt_RoundTrip_LargeModulus()
        {
            // A 2048-bit positive value, as would appear in an RSA key.
            var value  = BigInteger.Pow(2, 2048) - 1;
            var bytes  = Write((ref SshPacketWriter w) => w.WriteMPInt(value));
            var reader = new SshPacketReader(bytes);

            Assert.That(reader.ReadMPInt(), Is.EqualTo(value));
        }

        #endregion

        #region Composite / cursor behaviour

        [Test]
        public void MixedSequence_RoundTrip()
        {

            var bytes = Write((ref SshPacketWriter w) => {
                w.WriteByte((Byte) SshMessageNumber.KexInit);
                w.WriteBoolean(true);
                w.WriteUInt32(0xCAFEBABEu);
                w.WriteString("ssh-connection");
                w.WriteNameList("aes256-gcm@openssh.com", "chacha20-poly1305@openssh.com");
            });

            var reader   = new SshPacketReader(bytes);
            var msg      = (SshMessageNumber) reader.ReadByte();
            var flag     = reader.ReadBoolean();
            var number   = reader.ReadUInt32();
            var service  = reader.ReadString();
            var ciphers  = reader.ReadNameList();
            var hasMore  = reader.HasMoreData;

            Assert.Multiple(() => {
                Assert.That(msg,     Is.EqualTo(SshMessageNumber.KexInit));
                Assert.That(flag,    Is.True);
                Assert.That(number,  Is.EqualTo(0xCAFEBABEu));
                Assert.That(service, Is.EqualTo("ssh-connection"));
                Assert.That(ciphers, Is.EqualTo(new[] { "aes256-gcm@openssh.com", "chacha20-poly1305@openssh.com" }));
                Assert.That(hasMore, Is.False);
            });

        }

        [Test]
        public void Position_And_Remaining_Track()
        {

            var reader = new SshPacketReader(new Byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });

            Assert.That(reader.Position,  Is.EqualTo(0));
            Assert.That(reader.Remaining, Is.EqualTo(5));

            reader.ReadByte();
            reader.ReadUInt32();

            Assert.That(reader.Position,    Is.EqualTo(5));
            Assert.That(reader.Remaining,   Is.EqualTo(0));
            Assert.That(reader.HasMoreData, Is.False);

        }

        #endregion

        #region Malformed input

        [Test]
        public void Read_PastEnd_Throws()
        {
            Assert.Throws<SshWireException>(() => {
                var reader = new SshPacketReader(new Byte[] { 0x01, 0x02 });
                reader.ReadUInt32();
            });
        }

        [Test]
        public void ReadString_LengthExceedsRemaining_Throws()
        {
            // Declares a length of 16, but only 2 payload bytes follow.
            var wire = new Byte[] { 0x00, 0x00, 0x00, 0x10, 0xAA, 0xBB };
            Assert.Throws<SshWireException>(() => {
                var reader = new SshPacketReader(wire);
                reader.ReadBinaryString();
            });
        }

        [Test]
        public void ReadString_ExceedsMaxLength_Throws()
        {
            // A well-formed 8-byte string, but the caller caps at 4.
            var wire = Write((ref SshPacketWriter w) => w.WriteBinaryString(new Byte[8]));
            Assert.Throws<SshWireException>(() => {
                var reader = new SshPacketReader(wire);
                reader.ReadBinaryString(MaxLength: 4);
            });
        }

        [Test]
        public void ReadString_ExceedsMaxLength_DoesNotOverAllocate()
        {
            // A truncated packet claiming a 2 GiB length must fail on the remaining-bytes check,
            // never attempt the allocation.
            var wire = new Byte[] { 0x7F, 0xFF, 0xFF, 0xFF };
            Assert.Throws<SshWireException>(() => {
                var reader = new SshPacketReader(wire);
                reader.ReadBinaryString();
            });
        }

        #endregion

    }

}
