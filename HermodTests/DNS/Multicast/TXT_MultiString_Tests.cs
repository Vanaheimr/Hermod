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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Multicast
{

    /// <summary>
    /// The multi-string TXT resource record (RFC 1035 §3.3.14) as DNS-Based Service
    /// Discovery needs it (RFC 6763 §6): one character-string per key/value pair,
    /// at most 255 bytes of UTF-8 each, and the key/value rules of §6.4.
    /// </summary>
    [TestFixture]
    public class TXT_MultiString_Tests
    {

        #region Helpers

        private static readonly TimeSpan        TimeToLive    = TimeSpan.FromSeconds(4500);
        private static readonly DNSServiceName  InstanceName  = DNSServiceName.Parse("myhost._test._tcp.local.");
        private static readonly DomainName      HostName      = DomainName.    Parse("myhost.local.");

        private static KeyValuePair<String, String?> KV(String Key, String? Value)
            => new (Key, Value);

        /// <summary>
        /// The RDATA of a record as it is on the (uncompressed) wire: the owner
        /// name is walked label by label, then TYPE, CLASS, TTL and RDLENGTH are
        /// skipped, and exactly RDLENGTH bytes are returned.
        /// </summary>
        private static Byte[] RDataOf(IDNSResourceRecord Record)
        {

            var wire    = Record.ToWireFormat();
            var offset  = 0;

            while (wire[offset] != 0)
                offset += 1 + wire[offset];

            offset += 1;            // root label
            offset += 2 + 2 + 4;    // TYPE, CLASS, TTL

            var rdLength = (wire[offset] << 8) | wire[offset + 1];
            offset += 2;

            Assert.That(wire.Length, Is.EqualTo(offset + rdLength), "RDLENGTH must delimit the record exactly");

            return wire[offset..(offset + rdLength)];

        }

        private static TXT RoundTrip(TXT Record)
        {

            var back = DNSInfo.ReadResourceRecord(new MemoryStream(Record.ToWireFormat())) as TXT;

            Assert.That(back, Is.Not.Null, "the wire format must read back as a TXT record");

            return back!;

        }

        #endregion


        #region A single text is split on the wire

        #region SingleText_600ASCIIBytes_IsSplitIntoThreeCharacterStrings()

        [Test]
        public void SingleText_600ASCIIBytes_IsSplitIntoThreeCharacterStrings()
        {

            var text   = new String('a', 600);
            var txt    = new TXT(HostName, DNSQueryClasses.IN, TimeToLive, text);
            var rdata  = RDataOf(txt);

            Assert.Multiple(() => {

                Assert.That(txt.Text,                Is.EqualTo(text));
                Assert.That(txt.Strings,             Has.Count.EqualTo(3));
                Assert.That(txt.Strings[0].Length,   Is.EqualTo(255));
                Assert.That(txt.Strings[1].Length,   Is.EqualTo(255));
                Assert.That(txt.Strings[2].Length,   Is.EqualTo(90));

                // 3 length bytes + 600 bytes of text
                Assert.That(rdata.Length,            Is.EqualTo(603));
                Assert.That(rdata[0],                Is.EqualTo(255));
                Assert.That(rdata[256],              Is.EqualTo(255));
                Assert.That(rdata[512],              Is.EqualTo(90));

            });

            var back = RoundTrip(txt);

            Assert.Multiple(() => {
                Assert.That(back.Strings,  Is.EqualTo(txt.Strings));
                Assert.That(back.Text,     Is.EqualTo(text));
            });

        }

        #endregion

        #region SingleText_TwoByteCharacters_AreNeverSplitAndRoundTrip()

        [Test]
        public void SingleText_TwoByteCharacters_AreNeverSplitAndRoundTrip()
        {

            // 300 × 'ä' = 600 bytes of UTF-8; 127 characters fit into 254 bytes,
            // so the split must happen after 127, not 255, characters.
            var text = new String('ä', 300);
            var txt  = new TXT(HostName, DNSQueryClasses.IN, TimeToLive, text);

            Assert.Multiple(() => {

                Assert.That(txt.Strings,                                                     Has.Count.EqualTo(3));
                Assert.That(txt.Strings.Select(s => s.Length),                               Is.EqualTo(new[] { 127, 127, 46 }));
                Assert.That(txt.Strings.All(s => Encoding.UTF8.GetByteCount(s) <= TXT.MaxCharacterStringLength), Is.True);
                Assert.That(txt.Strings.All(s => Encoding.UTF8.GetByteCount(s) % 2 == 0),    Is.True, "an odd byte count would mean a character was cut in half");
                Assert.That(String.Concat(txt.Strings),                                      Is.EqualTo(text));

            });

            var back = RoundTrip(txt);

            Assert.Multiple(() => {
                Assert.That(back.Text,     Is.EqualTo(text));
                Assert.That(back.Strings,  Is.EqualTo(txt.Strings));
                Assert.That(back.Text,     Does.Not.Contain("�"), "a replacement character would mean a UTF-8 sequence was split");
            });

        }

        #endregion

        #region SingleText_SurrogatePairs_AreNeverSplit()

        [Test]
        public void SingleText_SurrogatePairs_AreNeverSplit()
        {

            // 100 × U+1F600 (4 bytes of UTF-8, two UTF-16 code units each) = 400 bytes;
            // 63 of them fit into 252 bytes.
            var text = String.Concat(Enumerable.Repeat("\U0001F600", 100));
            var txt  = new TXT(InstanceName, DNSQueryClasses.IN, TimeToLive, text);

            Assert.Multiple(() => {

                Assert.That(txt.Strings,                                              Has.Count.EqualTo(2));
                Assert.That(txt.Strings.Select(s => Encoding.UTF8.GetByteCount(s)),   Is.EqualTo(new[] { 252, 148 }));
                Assert.That(txt.Strings.All(s => Char.IsSurrogatePair(s, 0)),         Is.True, "every string must start with a complete character");

            });

            Assert.That(RoundTrip(txt).Text, Is.EqualTo(text));

        }

        #endregion

        #region SingleText_Short_IsOneCharacterString()

        [Test]
        public void SingleText_Short_IsOneCharacterString()
        {

            var txt    = new TXT(HostName, DNSQueryClasses.IN, TimeToLive, "v=spf1 -all");
            var rdata  = RDataOf(txt);

            Assert.Multiple(() => {

                Assert.That(txt.Strings,     Is.EqualTo(new[] { "v=spf1 -all" }));
                Assert.That(txt.Text,        Is.EqualTo("v=spf1 -all"));
                Assert.That(rdata,           Is.EqualTo(new Byte[] { 11 }.Concat(Encoding.ASCII.GetBytes("v=spf1 -all")).ToArray()));

                // An empty text is still "one or more" character-strings on the wire (RFC 1035 §3.3.14).
                var empty = new TXT(HostName, DNSQueryClasses.IN, TimeToLive, String.Empty);
                Assert.That(empty.Strings,   Is.EqualTo(new[] { String.Empty }));
                Assert.That(RDataOf(empty),  Is.EqualTo(new Byte[] { 0x00 }));

            });

        }

        #endregion

        #endregion

        #region Character-strings are validated

        #region Strings_LongerThan255Bytes_AreRejected()

        [Test]
        public void Strings_LongerThan255Bytes_AreRejected()
        {

            Assert.Multiple(() => {

                // 256 ASCII characters = 256 bytes
                Assert.That(() => new TXT(InstanceName, DNSQueryClasses.IN, TimeToLive, [ new String('a', 256) ]),
                            Throws.InstanceOf<ArgumentException>());

                // 128 × 'ä' = 128 characters, but 256 bytes of UTF-8: the limit is bytes, not characters.
                Assert.That(() => new TXT(InstanceName, DNSQueryClasses.IN, TimeToLive, [ new String('ä', 128) ]),
                            Throws.InstanceOf<ArgumentException>());

                // One bad string spoils the record, wherever it is.
                Assert.That(() => new TXT(InstanceName, DNSQueryClasses.IN, TimeToLive, [ "txtver=1", new String('b', 300) ]),
                            Throws.InstanceOf<ArgumentException>());

            });

        }

        #endregion

        #region Strings_Exactly255Bytes_AreAccepted()

        [Test]
        public void Strings_Exactly255Bytes_AreAccepted()
        {

            var ascii  = new String('a', 255);                       // 255 bytes
            var umlaut = new String('ä', 127) + "x";                 // 254 + 1 = 255 bytes
            var txt    = new TXT(InstanceName, DNSQueryClasses.IN, TimeToLive, [ ascii, umlaut ]);

            Assert.Multiple(() => {
                Assert.That(txt.Strings,                  Is.EqualTo(new[] { ascii, umlaut }));
                Assert.That(RDataOf(txt).Length,          Is.EqualTo(2 * (1 + 255)));
                Assert.That(RoundTrip(txt).Strings,       Is.EqualTo(new[] { ascii, umlaut }));
            });

        }

        #endregion

        #region Strings_NullEntries_AreRejected()

        [Test]
        public void Strings_NullEntries_AreRejected()
        {

            Assert.Multiple(() => {

                Assert.That(() => new TXT(InstanceName, DNSQueryClasses.IN, TimeToLive, new String[] { "txtver=1", null! }),
                            Throws.InstanceOf<ArgumentException>());

                Assert.That(() => new TXT(HostName,     DNSQueryClasses.IN, TimeToLive, new String[] { null! }),
                            Throws.InstanceOf<ArgumentException>());

                Assert.That(() => new TXT(InstanceName, DNSQueryClasses.IN, TimeToLive, (IEnumerable<String>) null!),
                            Throws.InstanceOf<ArgumentNullException>());

            });

        }

        #endregion

        #region Strings_Empty_BecomesOneEmptyStringOnTheWire()

        [Test]
        public void Strings_Empty_BecomesOneEmptyStringOnTheWire()
        {

            var txt    = new TXT(InstanceName, DNSQueryClasses.IN, TimeToLive, Array.Empty<String>());
            var rdata  = RDataOf(txt);

            Assert.Multiple(() => {

                // RFC 1035 §3.3.14: "one or more <character-string>s" — an empty TXT record
                // is a single zero-length character-string, i.e. RDLENGTH 1.
                Assert.That(txt.Strings,     Is.EqualTo(new[] { String.Empty }));
                Assert.That(txt.Text,        Is.EqualTo(String.Empty));
                Assert.That(rdata,           Is.EqualTo(new Byte[] { 0x00 }));
                Assert.That(txt.KeyValues,   Is.Empty);

            });

            var back = RoundTrip(txt);

            Assert.Multiple(() => {
                Assert.That(back.Strings,    Is.EqualTo(new[] { String.Empty }));
                Assert.That(back.Text,       Is.EqualTo(String.Empty));
            });

        }

        #endregion

        #endregion

        #region The key/value view (RFC 6763 §6.4)

        #region KeyValues_FirstOccurrenceWins_AndKeysAreCaseInsensitive()

        [Test]
        public void KeyValues_FirstOccurrenceWins_AndKeysAreCaseInsensitive()
        {

            var txt = new TXT(InstanceName, DNSQueryClasses.IN, TimeToLive, [ "txtver=1", "TXTVER=2", "TxtVer=3" ]);

            Assert.Multiple(() => {

                // "the client MUST silently ignore all but the first occurrence of that attribute"
                Assert.That(txt.KeyValues,                            Has.Count.EqualTo(1));
                Assert.That(txt.KeyValues["txtver"],                  Is.EqualTo("1"));
                Assert.That(txt.KeyValues["TXTVER"],                  Is.EqualTo("1"));
                Assert.That(txt.KeyValues["TxTvEr"],                  Is.EqualTo("1"));

                Assert.That(txt.TryGetValue("TXTVER", out var value), Is.True);
                Assert.That(value,                                    Is.EqualTo("1"));

                // All three strings are nevertheless still on the wire.
                Assert.That(txt.Strings,                              Has.Count.EqualTo(3));

            });

        }

        #endregion

        #region KeyValues_IgnoresEmptyStringsAndStringsStartingWithEquals()

        [Test]
        public void KeyValues_IgnoresEmptyStringsAndStringsStartingWithEquals()
        {

            var txt = new TXT(InstanceName, DNSQueryClasses.IN, TimeToLive, [ "", "=ignored", "=", "txtver=1" ]);

            Assert.Multiple(() => {

                Assert.That(txt.KeyValues,                     Has.Count.EqualTo(1));
                Assert.That(txt.KeyValues.ContainsKey(""),     Is.False);
                Assert.That(txt.KeyValues["txtver"],           Is.EqualTo("1"));
                Assert.That(txt.TryGetValue("", out _),        Is.False);
                Assert.That(txt.TryGetValue("missing", out _), Is.False);

                // The wire is untouched by the view.
                Assert.That(txt.Strings,                       Is.EqualTo(new[] { "", "=ignored", "=", "txtver=1" }));

            });

        }

        #endregion

        #region KeyValues_BooleanAttributesEmptyValuesAndEmbeddedEquals()

        [Test]
        public void KeyValues_BooleanAttributesEmptyValuesAndEmbeddedEquals()
        {

            var txt = new TXT(InstanceName, DNSQueryClasses.IN, TimeToLive, [ "flag", "empty=", "path=/a=b", "url=https://x/?q=1" ]);

            Assert.Multiple(() => {

                Assert.That(txt.KeyValues,                            Has.Count.EqualTo(4));

                // "key" without '=' is a boolean attribute: present, value null
                Assert.That(txt.KeyValues.ContainsKey("flag"),        Is.True);
                Assert.That(txt.KeyValues["flag"],                    Is.Null);
                Assert.That(txt.TryGetValue("flag", out var flag),    Is.True);
                Assert.That(flag,                                     Is.Null);

                // "key=" is an attribute with an empty value
                Assert.That(txt.TryGetValue("empty", out var empty),  Is.True);
                Assert.That(empty,                                    Is.EqualTo(String.Empty));

                // Only the first '=' separates key and value
                Assert.That(txt.KeyValues["path"],                    Is.EqualTo("/a=b"));
                Assert.That(txt.KeyValues["url"],                     Is.EqualTo("https://x/?q=1"));

            });

        }

        #endregion

        #region FromKeyValues_BuildsKeyEqualsValueStrings()

        [Test]
        public void FromKeyValues_BuildsKeyEqualsValueStrings()
        {

            var txt = TXT.FromKeyValues(
                          InstanceName,
                          DNSQueryClasses.IN,
                          TimeToLive,
                          [
                              KV("txtver", "1"),
                              KV("flag",   null),
                              KV("url",    "https://myhost.local.:8443/"),
                              KV("empty",  ""),
                              KV("a b",    "space in key is printable ASCII")
                          ]
                      );

            Assert.Multiple(() => {

                Assert.That(txt.Strings,                      Is.EqualTo(new[] { "txtver=1", "flag", "url=https://myhost.local.:8443/", "empty=", "a b=space in key is printable ASCII" }));
                Assert.That(txt.DomainName.FullName,          Is.EqualTo("myhost._test._tcp.local."));
                Assert.That(txt.Type,                         Is.EqualTo(DNSResourceRecordTypes.TXT));
                Assert.That(txt.TimeToLive,                   Is.EqualTo(TimeToLive));

                Assert.That(txt.KeyValues["txtver"],          Is.EqualTo("1"));
                Assert.That(txt.KeyValues["flag"],            Is.Null);
                Assert.That(txt.KeyValues["url"],             Is.EqualTo("https://myhost.local.:8443/"));
                Assert.That(txt.KeyValues["empty"],           Is.EqualTo(String.Empty));

            });

            var back = RoundTrip(txt);

            Assert.That(back.Strings, Is.EqualTo(txt.Strings));

        }

        #endregion

        #region FromKeyValues_RejectsInvalidKeys(Key)

        [TestCase("",        TestName = "FromKeyValues_RejectsInvalidKeys(empty)")]
        [TestCase("a=b",     TestName = "FromKeyValues_RejectsInvalidKeys(equals sign)")]
        [TestCase("=",       TestName = "FromKeyValues_RejectsInvalidKeys(only an equals sign)")]
        [TestCase("ä",       TestName = "FromKeyValues_RejectsInvalidKeys(non-ASCII)")]
        [TestCase("a\tb",    TestName = "FromKeyValues_RejectsInvalidKeys(control character)")]
        [TestCase("a\u007Fb", TestName = "FromKeyValues_RejectsInvalidKeys(DEL)")]
        public void FromKeyValues_RejectsInvalidKeys(String Key)
        {

            Assert.That(() => TXT.FromKeyValues(InstanceName, DNSQueryClasses.IN, TimeToLive, [ KV("txtver", "1"), KV(Key, "value") ]),
                        Throws.InstanceOf<ArgumentException>(),
                        $"RFC 6763 §6.4: keys are printable US-ASCII (0x20-0x7E) without '=', but '{Key}' was accepted");

        }

        #endregion

        #endregion

        #region JSON and zone-file presentation

        #region TryParseFromJSON_KeepsQuotedStringsSeparate()

        [Test]
        public void TryParseFromJSON_KeepsQuotedStringsSeparate()
        {

            var name      = DomainName.Parse("example.com.");

            var two       = TXT.TryParseFromJSON(name, TimeToLive, "\"a\" \"b\"");
            var single    = TXT.TryParseFromJSON(name, TimeToLive, "\"single\"");
            var unquoted  = TXT.TryParseFromJSON(name, TimeToLive, "v=spf1 include:_spf.example.com -all");

            Assert.Multiple(() => {

                Assert.That(two,                Is.Not.Null);
                Assert.That(two!.Strings,       Is.EqualTo(new[] { "a", "b" }));
                Assert.That(two.Text,           Is.EqualTo("ab"));

                Assert.That(single,             Is.Not.Null);
                Assert.That(single!.Strings,    Is.EqualTo(new[] { "single" }));

                Assert.That(unquoted,           Is.Not.Null);
                Assert.That(unquoted!.Strings,  Is.EqualTo(new[] { "v=spf1 include:_spf.example.com -all" }));
                Assert.That(unquoted.Text,      Is.EqualTo("v=spf1 include:_spf.example.com -all"));

            });

        }

        #endregion

        #region ToZoneFileString_QuotesAndEscapesEachString()

        [Test]
        public void ToZoneFileString_QuotesAndEscapesEachString()
        {

            // a"b  and  c\d
            var txt       = new TXT(InstanceName, DNSQueryClasses.IN, TimeToLive, [ "a\"b", "c\\d", "plain" ]);
            var zoneFile  = txt.ToZoneFileString();

            Assert.Multiple(() => {

                Assert.That(zoneFile, Does.Contain("myhost._test._tcp.local."));
                Assert.That(zoneFile, Does.Contain("TXT"));

                // "a\"b"  — the quote is escaped
                Assert.That(zoneFile, Does.Contain("\"a\\\"b\""));

                // "c\\d"  — the backslash is escaped
                Assert.That(zoneFile, Does.Contain("\"c\\\\d\""));

                // every string is quoted and separated by a space
                Assert.That(zoneFile, Does.Contain("\"a\\\"b\" \"c\\\\d\" \"plain\""));

            });

        }

        #endregion

        #endregion

        #region Wire format

        #region DNSServiceNameOwner_RoundTripsThroughTheWire()

        [Test]
        public void DNSServiceNameOwner_RoundTripsThroughTheWire()
        {

            var txt   = new TXT(InstanceName, DNSQueryClasses.IN, TimeToLive, [ "txtver=1", "e_name=Wärmepumpe Küche", "flag" ]);
            var back  = RoundTrip(txt);

            Assert.Multiple(() => {

                Assert.That(back.DomainName.FullName,   Is.EqualTo("myhost._test._tcp.local."));
                Assert.That(back.DomainName,            Is.EqualTo(InstanceName));
                Assert.That(back.Type,                  Is.EqualTo(DNSResourceRecordTypes.TXT));
                Assert.That(back.Class,                 Is.EqualTo(DNSQueryClasses.IN));
                Assert.That(back.TimeToLive,            Is.EqualTo(TimeToLive));
                Assert.That(back.Strings,               Is.EqualTo(new[] { "txtver=1", "e_name=Wärmepumpe Küche", "flag" }));
                Assert.That(back.KeyValues["e_name"],   Is.EqualTo("Wärmepumpe Küche"));

                // and the wire format of the copy is identical to the original one
                Assert.That(back.ToWireFormat(),        Is.EqualTo(txt.ToWireFormat()));

            });

        }

        #endregion

        #region StreamConstructor_ReadsClassTimeToLiveAndCharacterStrings()

        [Test]
        public void StreamConstructor_ReadsClassTimeToLiveAndCharacterStrings()
        {

            // The stream starts at the CLASS field: the owner name and TYPE were
            // already consumed by whoever dispatched to this constructor.
            using var stream = new MemoryStream();

            stream.WriteUInt16BE((UInt16) DNSQueryClasses.IN);   // CLASS
            stream.WriteUInt32BE(4500);                          // TTL
            stream.WriteUInt16BE((UInt16) 7);                    // RDLENGTH

            var rdata = new Byte[] { 2, (Byte) 'h', (Byte) 'i', 3, (Byte) 'y', (Byte) 'o', (Byte) 'u' };
            stream.Write(rdata, 0, rdata.Length);

            stream.Position = 0;

            var txt = new TXT(InstanceName, stream);

            Assert.Multiple(() => {

                Assert.That(txt.DomainName.FullName,   Is.EqualTo("myhost._test._tcp.local."));
                Assert.That(txt.Type,                  Is.EqualTo(DNSResourceRecordTypes.TXT));
                Assert.That(txt.Class,                 Is.EqualTo(DNSQueryClasses.IN));
                Assert.That(txt.TimeToLive,            Is.EqualTo(TimeSpan.FromSeconds(4500)));
                Assert.That(txt.Strings,               Is.EqualTo(new[] { "hi", "you" }));
                Assert.That(txt.Text,                  Is.EqualTo("hiyou"));

                // RDLENGTH delimits the RDATA: nothing more, nothing less, was consumed.
                Assert.That(stream.Position,           Is.EqualTo(stream.Length));

                // and serializing it again yields the same RDATA
                Assert.That(RDataOf(txt),              Is.EqualTo(rdata));

            });

        }

        #endregion

        #endregion

    }

}
