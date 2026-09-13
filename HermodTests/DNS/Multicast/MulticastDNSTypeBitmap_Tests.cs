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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Multicast
{

    /// <summary>
    /// The NSEC type bitmap (RFC 4034 §4.1.2) as Multicast DNS uses it for
    /// negative responses (RFC 6762 §6.1).
    /// </summary>
    [TestFixture]
    public class MulticastDNSTypeBitmap_Tests
    {

        #region Encode_AAndAAAA_MatchesRFC4034()

        [Test]
        public void Encode_AAndAAAA_MatchesRFC4034()
        {

            Assert.Multiple(() => {

                // window 0, 4 bytes: A = bit 1 of byte 0 (0x40), AAAA = 28 = bit 4 of byte 3 (0x08)
                Assert.That(MulticastDNSTypeBitmap.Encode([ DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA ]),
                            Is.EqualTo(new Byte[] { 0x00, 0x04, 0x40, 0x00, 0x00, 0x08 }));

                // the bitmap is as short as the highest type needs
                Assert.That(MulticastDNSTypeBitmap.Encode([ DNSResourceRecordTypes.A ]),
                            Is.EqualTo(new Byte[] { 0x00, 0x01, 0x40 }));

                Assert.That(MulticastDNSTypeBitmap.Encode([ DNSResourceRecordTypes.AAAA ]),
                            Is.EqualTo(new Byte[] { 0x00, 0x04, 0x00, 0x00, 0x00, 0x08 }));

                // A (bit 1 of byte 0), TXT = 16 (bit 0 of byte 2), SRV = 33 (bit 1 of byte 4)
                Assert.That(MulticastDNSTypeBitmap.Encode([ DNSResourceRecordTypes.A, DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.TXT ]),
                            Is.EqualTo(new Byte[] { 0x00, 0x05, 0x40, 0x00, 0x80, 0x00, 0x40 }));

            });

        }

        #endregion

        #region Decode_RoundTripsTypesInHigherWindows()

        [Test]
        public void Decode_RoundTripsTypesInHigherWindows()
        {

            DNSResourceRecordTypes[] types = [ DNSResourceRecordTypes.A, DNSResourceRecordTypes.TXT, DNSResourceRecordTypes.URI, DNSResourceRecordTypes.CAA ];

            var bitmap = MulticastDNSTypeBitmap.Encode(types);

            Assert.Multiple(() => {

                // window 0 (A, TXT) and window 1 (URI = 256 = bit 0, CAA = 257 = bit 1)
                Assert.That(bitmap,                                             Is.EqualTo(new Byte[] { 0x00, 0x03, 0x40, 0x00, 0x80,  0x01, 0x01, 0xC0 }));
                Assert.That(MulticastDNSTypeBitmap.Decode(bitmap),              Is.EqualTo(types));

                Assert.That(MulticastDNSTypeBitmap.Encode([ DNSResourceRecordTypes.CAA ]),
                            Is.EqualTo(new Byte[] { 0x01, 0x01, 0x40 }));

                Assert.That(MulticastDNSTypeBitmap.Decode([ 0x01, 0x01, 0x40 ]),
                            Is.EqualTo(new[] { DNSResourceRecordTypes.CAA }));

                Assert.That(MulticastDNSTypeBitmap.Decode(MulticastDNSTypeBitmap.Encode([ DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA ])),
                            Is.EqualTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA }));

            });

        }

        #endregion

        #region Contains_ReportsOnlyTheEncodedTypes()

        [Test]
        public void Contains_ReportsOnlyTheEncodedTypes()
        {

            var bitmap = MulticastDNSTypeBitmap.Encode([ DNSResourceRecordTypes.A, DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.TXT, DNSResourceRecordTypes.CAA ]);

            Assert.Multiple(() => {

                Assert.That(MulticastDNSTypeBitmap.Contains(bitmap, DNSResourceRecordTypes.A),      Is.True);
                Assert.That(MulticastDNSTypeBitmap.Contains(bitmap, DNSResourceRecordTypes.SRV),    Is.True);
                Assert.That(MulticastDNSTypeBitmap.Contains(bitmap, DNSResourceRecordTypes.TXT),    Is.True);
                Assert.That(MulticastDNSTypeBitmap.Contains(bitmap, DNSResourceRecordTypes.CAA),    Is.True);

                Assert.That(MulticastDNSTypeBitmap.Contains(bitmap, DNSResourceRecordTypes.AAAA),   Is.False);
                Assert.That(MulticastDNSTypeBitmap.Contains(bitmap, DNSResourceRecordTypes.PTR),    Is.False);
                Assert.That(MulticastDNSTypeBitmap.Contains(bitmap, DNSResourceRecordTypes.NSEC),   Is.False);
                Assert.That(MulticastDNSTypeBitmap.Contains(bitmap, DNSResourceRecordTypes.URI),    Is.False, "a neighbour within the same window");
                Assert.That(MulticastDNSTypeBitmap.Contains(bitmap, DNSResourceRecordTypes.Any),    Is.False);

                Assert.That(MulticastDNSTypeBitmap.Contains([],     DNSResourceRecordTypes.A),      Is.False);

                // and it agrees with the DNSSEC reading of the same bytes
                Assert.That(ADNSResourceRecord.TypeBitMapContains(bitmap, DNSResourceRecordTypes.SRV),   Is.True);
                Assert.That(ADNSResourceRecord.TypeBitMapContains(bitmap, DNSResourceRecordTypes.AAAA),  Is.False);

            });

        }

        #endregion

        #region Encode_EmptySet_IsEmpty()

        [Test]
        public void Encode_EmptySet_IsEmpty()
        {

            Assert.Multiple(() => {

                Assert.That(MulticastDNSTypeBitmap.Encode([]),   Is.Empty);
                Assert.That(MulticastDNSTypeBitmap.Decode([]),   Is.Empty);

                Assert.That(() => MulticastDNSTypeBitmap.Encode(null!),  Throws.InstanceOf<ArgumentNullException>());
                Assert.That(() => MulticastDNSTypeBitmap.Decode(null!),  Throws.InstanceOf<ArgumentNullException>());

            });

        }

        #endregion

        #region Decode_Garbage_DoesNotThrowAndReturnsWhatItCan()

        [Test]
        public void Decode_Garbage_DoesNotThrowAndReturnsWhatItCan()
        {

            Assert.Multiple(() => {

                // a lone window byte
                Assert.That(MulticastDNSTypeBitmap.Decode([ 0x00 ]),                                   Is.Empty);

                // a window claiming four bytes, one present
                Assert.That(MulticastDNSTypeBitmap.Decode([ 0x00, 0x04, 0x40 ]),                       Is.Empty);

                // a zero-length bitmap (RFC 4034 §4.1.2 forbids it)
                Assert.That(MulticastDNSTypeBitmap.Decode([ 0x01, 0x00 ]),                             Is.Empty);

                // a bitmap longer than the 32 bytes a window can have
                Assert.That(MulticastDNSTypeBitmap.Decode([ 0x00, 0x21 ]),                             Is.Empty);

                // a good window followed by a truncated one: the good one is returned
                Assert.That(MulticastDNSTypeBitmap.Decode([ 0x00, 0x01, 0x40, 0x00, 0x05, 0x01 ]),     Is.EqualTo(new[] { DNSResourceRecordTypes.A }));

                // a good window followed by a dangling byte
                Assert.That(MulticastDNSTypeBitmap.Decode([ 0x00, 0x01, 0x40, 0x01 ]),                 Is.EqualTo(new[] { DNSResourceRecordTypes.A }));

                // a type this build has no name for is still a type
                Assert.That(MulticastDNSTypeBitmap.Decode([ 0xFF, 0x01, 0x80 ]),                       Is.EqualTo(new[] { (DNSResourceRecordTypes) 0xFF00 }));

                // Contains must not throw on garbage either
                Assert.That(MulticastDNSTypeBitmap.Contains([ 0x00, 0x04, 0x40 ], DNSResourceRecordTypes.A),  Is.False);

            });

        }

        #endregion

        #region Encode_IgnoresDuplicatesAndSortsWindows()

        [Test]
        public void Encode_IgnoresDuplicatesAndSortsWindows()
        {

            var unordered = MulticastDNSTypeBitmap.Encode([ DNSResourceRecordTypes.CAA, DNSResourceRecordTypes.AAAA, DNSResourceRecordTypes.A, DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA ]);
            var ordered   = MulticastDNSTypeBitmap.Encode([ DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA, DNSResourceRecordTypes.CAA ]);

            Assert.Multiple(() => {

                Assert.That(unordered,                                  Is.EqualTo(ordered));
                Assert.That(unordered,                                  Is.EqualTo(new Byte[] { 0x00, 0x04, 0x40, 0x00, 0x00, 0x08,  0x01, 0x01, 0x40 }));
                Assert.That(MulticastDNSTypeBitmap.Decode(unordered),   Is.EqualTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA, DNSResourceRecordTypes.CAA }));

            });

        }

        #endregion

        #region Encode_LastBitOfAWindow_UsesTheFullBitmapLength()

        [Test]
        public void Encode_LastBitOfAWindow_UsesTheFullBitmapLength()
        {

            // ANY = 255 is bit 7 of byte 31: the last bit of window 0.
            var bitmap = MulticastDNSTypeBitmap.Encode([ DNSResourceRecordTypes.Any ]);

            Assert.Multiple(() => {

                Assert.That(bitmap,                                                             Has.Length.EqualTo(2 + 32));
                Assert.That(bitmap[0],                                                          Is.EqualTo(0x00));
                Assert.That(bitmap[1],                                                          Is.EqualTo(32));
                Assert.That(bitmap[2..33],                                                      Is.All.EqualTo(0x00));
                Assert.That(bitmap[33],                                                         Is.EqualTo(0x01));

                Assert.That(MulticastDNSTypeBitmap.Decode(bitmap),                              Is.EqualTo(new[] { DNSResourceRecordTypes.Any }));
                Assert.That(MulticastDNSTypeBitmap.Contains(bitmap, DNSResourceRecordTypes.Any), Is.True);
                Assert.That(MulticastDNSTypeBitmap.Contains(bitmap, DNSResourceRecordTypes.A),   Is.False);

            });

        }

        #endregion

        #region Bitmap_RoundTripsThroughAnNSECRecord()

        [Test]
        public void Bitmap_RoundTripsThroughAnNSECRecord()
        {

            // RFC 6762 §6.1: an IPv4-only host answers an AAAA query with an NSEC record
            // whose bitmap lists the types it does own (and whose next name is its own name).
            var name    = DomainName.Parse("myhost.local.");
            var bitmap  = MulticastDNSTypeBitmap.Encode([ DNSResourceRecordTypes.A, DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.TXT ]);
            var nsec    = new NSEC(name, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), name, bitmap);

            var back    = DNSInfo.ReadResourceRecord(new MemoryStream(nsec.ToWireFormat())) as NSEC;

            Assert.That(back, Is.Not.Null);

            Assert.Multiple(() => {

                Assert.That(back!.DomainName.FullName,                                              Is.EqualTo("myhost.local."));
                Assert.That(back.NextDomainName.FullName,                                           Is.EqualTo("myhost.local."));
                Assert.That(back.TypeBitMaps,                                                       Is.EqualTo(bitmap));

                Assert.That(MulticastDNSTypeBitmap.Decode(back.TypeBitMaps),                        Is.EquivalentTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.SRV, DNSResourceRecordTypes.TXT }));
                Assert.That(MulticastDNSTypeBitmap.Contains(back.TypeBitMaps, DNSResourceRecordTypes.A),     Is.True);
                Assert.That(MulticastDNSTypeBitmap.Contains(back.TypeBitMaps, DNSResourceRecordTypes.AAAA),  Is.False, "no AAAA record: the negative answer");

                Assert.That(back.ToZoneFileString(),                                                Does.Contain("A").And.Contain("SRV").And.Contain("TXT"));

            });

        }

        #endregion

    }

}
