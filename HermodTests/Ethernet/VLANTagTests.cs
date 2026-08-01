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

using org.GraphDefined.Vanaheimr.Hermod.Ethernet;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Ethernet
{

    /// <summary>
    /// VLANId and VLANTag tests.
    /// </summary>
    [TestFixture]
    public class VLANTagTests
    {

        #region VLANIdRangeAndSpecialValues()

        /// <summary>
        /// A VLAN identifier is a 12-bit value with three reserved values.
        /// </summary>
        [Test]
        public void VLANIdRangeAndSpecialValues()
        {

            Assert.That(VLANId.MaxValue,                Is.EqualTo(4095));
            Assert.That(VLANId.Bits,                    Is.EqualTo(12));

            Assert.That(VLANId.Null.    Value,          Is.EqualTo(0));
            Assert.That(VLANId.Null.    IsNullVLAN,     Is.True);
            Assert.That(VLANId.Null.    IsAssignable,   Is.False);

            Assert.That(VLANId.Default. Value,          Is.EqualTo(1));
            Assert.That(VLANId.Default. IsDefaultVLAN,  Is.True);
            Assert.That(VLANId.Default. IsAssignable,   Is.True);

            Assert.That(VLANId.Reserved.Value,          Is.EqualTo(4095));
            Assert.That(VLANId.Reserved.IsReserved,     Is.True);
            Assert.That(VLANId.Reserved.IsAssignable,   Is.False);

            Assert.That(VLANId.From(4094).IsAssignable, Is.True);

            Assert.Throws<ArgumentOutOfRangeException>(() => VLANId.From(4096));
            Assert.That(VLANId.TryFrom(4096),           Is.Null);
            Assert.That(VLANId.TryFrom(4095),           Is.EqualTo(VLANId.Reserved));

        }

        #endregion

        #region VLANIdParsingAndFormatting()

        /// <summary>
        /// VLAN identifiers are parsed and formatted as decimal numbers.
        /// </summary>
        [Test]
        public void VLANIdParsingAndFormatting()
        {

            Assert.That(VLANId.Parse("100").Value,        Is.EqualTo(100));
            Assert.That(VLANId.Parse(" 4094 ").Value,     Is.EqualTo(4094));

            Assert.That(VLANId.TryParse("4096", out _),   Is.False);
            Assert.That(VLANId.TryParse("-1",   out _),   Is.False);
            Assert.That(VLANId.TryParse("",     out _),   Is.False);
            Assert.That(VLANId.TryParse(null,   out _),   Is.False);
            Assert.That(VLANId.TryParse("abc",  out _),   Is.False);
            Assert.That(VLANId.TryParse("4096"),          Is.Null);

            Assert.Throws<FormatException>(() => VLANId.Parse("4096"));

            var vlanId = VLANId.From(100);

            Assert.That(vlanId.ToString(),      Is.EqualTo("100"));
            Assert.That(vlanId.ToString("D"),   Is.EqualTo("100"));
            Assert.That(vlanId.ToString("X"),   Is.EqualTo("0x064"));
            Assert.That(vlanId.ToString("x"),   Is.EqualTo("0x064"));

            Assert.Throws<FormatException>(() => vlanId.ToString("Q"));

        }

        #endregion

        #region VLANIdEqualityAndOrdering()

        /// <summary>
        /// Checks equality, ordering and hash codes of VLAN identifiers.
        /// </summary>
        [Test]
        public void VLANIdEqualityAndOrdering()
        {

            Assert.That(VLANId.From(100) == VLANId.From(100), Is.True);
            Assert.That(VLANId.From(100) != VLANId.From(200), Is.True);
            Assert.That(VLANId.From(100) <  VLANId.From(200), Is.True);
            Assert.That(VLANId.From(200) >  VLANId.From(100), Is.True);
            Assert.That(VLANId.From(100) <= VLANId.From(100), Is.True);
            Assert.That(VLANId.From(100) >= VLANId.From(100), Is.True);

            Assert.That(VLANId.From(100).GetHashCode(), Is.EqualTo(VLANId.From(100).GetHashCode()));
            Assert.That(VLANId.From(100).Equals((Object) VLANId.From(100)), Is.True);
            Assert.That(VLANId.From(100).Equals((Object) 100),              Is.False);

            Assert.Throws<ArgumentException>(() => VLANId.From(100).CompareTo((Object) 100));

            VLANId? nothing = null;
            VLANId? nullVLAN = VLANId.Null;
            VLANId? hundred  = VLANId.From(100);

            Assert.That(nothing. IsNullOrNullVLAN(),     Is.True);
            Assert.That(nullVLAN.IsNullOrNullVLAN(),     Is.True);
            Assert.That(hundred. IsNullOrNullVLAN(),     Is.False);
            Assert.That(hundred. IsNotNullOrNullVLAN(),  Is.True);

        }

        #endregion


        #region VLANTagPacksTagControlInformation()

        /// <summary>
        /// The Tag Control Information packs PCP, DEI and VID into 16 bits.
        /// </summary>
        [Test]
        public void VLANTagPacksTagControlInformation()
        {

            //  PCP = 1, DEI = 0, VID = 100  =>  001 0 0000 0110 0100  =  0x2064
            var vlanTag = VLANTag.CustomerTag(100, 1);

            Assert.That(vlanTag.TCI,          Is.EqualTo(0x2064));
            Assert.That(vlanTag.PCP,          Is.EqualTo(1));
            Assert.That(vlanTag.DEI,          Is.False);
            Assert.That(vlanTag.VID.Value,    Is.EqualTo(100));
            Assert.That(vlanTag.Priority,     Is.EqualTo(PCPPriorities.Background));
            Assert.That(vlanTag.TPID,         Is.EqualTo(EtherType.VLAN));
            Assert.That(vlanTag.IsCustomerTag, Is.True);
            Assert.That(vlanTag.IsServiceTag,  Is.False);

            //  PCP = 5, DEI = 1, VID = 100  =>  101 1 0000 0110 0100  =  0xB064
            var voice = VLANTag.CustomerTag(100, (Byte) PCPPriorities.Voice, DEI: true);

            Assert.That(voice.TCI,            Is.EqualTo(0xB064));
            Assert.That(voice.DEI,            Is.True);
            Assert.That(voice.Priority,       Is.EqualTo(PCPPriorities.Voice));

            //  The highest possible Tag Control Information.
            var max = new VLANTag(VLANId.Reserved, 7, true);

            Assert.That(max.TCI,              Is.EqualTo(0xFFFF));

            Assert.That(VLANTag.Length,       Is.EqualTo(4));

        }

        #endregion

        #region VLANTagPriorityCodePoints()

        /// <summary>
        /// The numeric order of the priority code points is not the priority order:
        /// background (1) ranks below best effort (0).
        /// </summary>
        [Test]
        public void VLANTagPriorityCodePoints()
        {

            Assert.That((Byte) PCPPriorities.BestEffort,           Is.EqualTo(0));
            Assert.That((Byte) PCPPriorities.Background,           Is.EqualTo(1));
            Assert.That((Byte) PCPPriorities.ExcellentEffort,      Is.EqualTo(2));
            Assert.That((Byte) PCPPriorities.CriticalApplications, Is.EqualTo(3));
            Assert.That((Byte) PCPPriorities.Video,                Is.EqualTo(4));
            Assert.That((Byte) PCPPriorities.Voice,                Is.EqualTo(5));
            Assert.That((Byte) PCPPriorities.InternetworkControl,  Is.EqualTo(6));
            Assert.That((Byte) PCPPriorities.NetworkControl,       Is.EqualTo(7));

            var networkControl = new VLANTag(VLANId.From(1), PCPPriorities.NetworkControl);

            Assert.That(networkControl.PCP,      Is.EqualTo(7));
            Assert.That(networkControl.Priority, Is.EqualTo(PCPPriorities.NetworkControl));

        }

        #endregion

        #region VLANTagByteRoundtrip()

        /// <summary>
        /// A VLAN tag is 4 bytes: 2 bytes TPID and 2 bytes TCI, in network byte order.
        /// </summary>
        [Test]
        public void VLANTagByteRoundtrip()
        {

            var customerTag = VLANTag.CustomerTag(100, 1);
            Assert.That(customerTag.GetBytes(), Is.EqualTo(new Byte[] { 0x81, 0x00, 0x20, 0x64 }));

            var serviceTag  = VLANTag.ServiceTag(200);
            Assert.That(serviceTag. GetBytes(), Is.EqualTo(new Byte[] { 0x88, 0xA8, 0x00, 0xC8 }));
            Assert.That(serviceTag. IsServiceTag,  Is.True);
            Assert.That(serviceTag. IsCustomerTag, Is.False);

            Assert.That(VLANTag.TryParse(new Byte[] { 0x81, 0x00, 0x20, 0x64 }, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(customerTag));

            Assert.That(VLANTag.From(new Byte[] { 0x88, 0xA8, 0x00, 0xC8 }), Is.EqualTo(serviceTag));

            // Trailing bytes are ignored, only the first 4 bytes are read.
            Assert.That(VLANTag.TryParse(new Byte[] { 0x81, 0x00, 0x20, 0x64, 0x08, 0x00 }, out var prefix), Is.True);
            Assert.That(prefix, Is.EqualTo(customerTag));

            var destination = new Byte[4];
            customerTag.WriteTo(destination);
            Assert.That(destination, Is.EqualTo(new Byte[] { 0x81, 0x00, 0x20, 0x64 }));

            Assert.Throws<ArgumentException>(() => customerTag.WriteTo(new Byte[3]));

        }

        #endregion

        #region VLANTagRejectsInvalidInput()

        /// <summary>
        /// A VLAN tag needs a known Tag Protocol Identifier and a 3-bit priority.
        /// </summary>
        [Test]
        public void VLANTagRejectsInvalidInput()
        {

            // Too short.
            Assert.That(VLANTag.TryParse(new Byte[] { 0x81, 0x00, 0x20 },       out _), Is.False);

            // 0x0800 is not a Tag Protocol Identifier.
            Assert.That(VLANTag.TryParse(new Byte[] { 0x08, 0x00, 0x20, 0x64 }, out _), Is.False);

            Assert.Throws<ArgumentException>          (() => VLANTag.From(new Byte[] { 0x08, 0x00, 0x20, 0x64 }));
            Assert.Throws<ArgumentException>          (() => new VLANTag(VLANId.From(100), 1, false, EtherType.IPv4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VLANTag(VLANId.From(100), 8));

            Assert.That(VLANTag.KnownTPIDs.Count, Is.EqualTo(5));
            Assert.That(VLANTag.KnownTPIDs,       Does.Contain(EtherType.VLAN));
            Assert.That(VLANTag.KnownTPIDs,       Does.Contain(EtherType.ProviderBridging));

        }

        #endregion

        #region VLANTagPriorityTaggedOnly()

        /// <summary>
        /// A VLAN tag with VID 0 only conveys a priority, it does not assign a VLAN.
        /// </summary>
        [Test]
        public void VLANTagPriorityTaggedOnly()
        {

            var priorityTag = VLANTag.CustomerTag(0, (Byte) PCPPriorities.Voice);

            Assert.That(priorityTag.IsPriorityTagOnly, Is.True);
            Assert.That(priorityTag.VID,               Is.EqualTo(VLANId.Null));
            Assert.That(priorityTag.TCI,               Is.EqualTo(0xA000));

            Assert.That(VLANTag.CustomerTag(1).IsPriorityTagOnly, Is.False);

        }

        #endregion

        #region VLANTagEqualityAndOrdering()

        /// <summary>
        /// Checks equality, ordering and hash codes of VLAN tags.
        /// </summary>
        [Test]
        public void VLANTagEqualityAndOrdering()
        {

            var tag100  = VLANTag.CustomerTag(100);
            var tag200  = VLANTag.CustomerTag(200);
            var sTag100 = VLANTag.ServiceTag (100);

            Assert.That(tag100 == VLANTag.CustomerTag(100), Is.True);
            Assert.That(tag100 != tag200,                   Is.True);
            Assert.That(tag100 != sTag100,                  Is.True);
            Assert.That(tag100 <  tag200,                   Is.True);
            Assert.That(tag200 >  tag100,                   Is.True);
            Assert.That(tag100 <= VLANTag.CustomerTag(100), Is.True);
            Assert.That(tag100 >= VLANTag.CustomerTag(100), Is.True);

            // The customer tag (0x8100) sorts before the service tag (0x88A8).
            Assert.That(tag200 < sTag100,                   Is.True);

            Assert.That(tag100.GetHashCode(), Is.EqualTo(VLANTag.CustomerTag(100).GetHashCode()));
            Assert.That(tag100.Equals((Object) VLANTag.CustomerTag(100)), Is.True);
            Assert.That(tag100.Equals((Object) 100),                      Is.False);

            Assert.Throws<ArgumentException>(() => tag100.CompareTo((Object) 100));

        }

        #endregion

        #region VLANTagToString()

        /// <summary>
        /// Checks the text representation of VLAN tags.
        /// </summary>
        [Test]
        public void VLANTagToString()
        {

            Assert.That(VLANTag.CustomerTag(100, 1).ToString(),
                        Is.EqualTo("C-Tag VID 100, PCP 1 (Background) [TPID 0x8100]"));

            Assert.That(VLANTag.ServiceTag(200, 5, DEI: true).ToString(),
                        Is.EqualTo("S-Tag VID 200, PCP 5 (Voice), DEI [TPID 0x88A8]"));

        }

        #endregion

    }

}
