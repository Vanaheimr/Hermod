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

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    using IPAddress = System.Net.IPAddress;

    /// <summary>
    /// M8: the NetworkAcl rule engine — CIDR matching, port sets, first-match ordering, presets and DNS-rebinding safety.
    /// </summary>
    [TestFixture]
    public class NetworkAclTests
    {

        private static IPAddress IP(String s) => IPAddress.Parse(s);


        #region Cidr_IPv4_EdgePrefixes

        [Test]
        public void Cidr_IPv4_EdgePrefixes()
        {
            Assert.Multiple(() => {
                var slash16 = IpCidr.Parse("10.20.0.0/16");
                Assert.That(slash16.Contains(IP("10.20.5.7")),  Is.True);
                Assert.That(slash16.Contains(IP("10.20.255.1")),Is.True);
                Assert.That(slash16.Contains(IP("10.21.0.1")),  Is.False);

                var host = IpCidr.Parse("192.168.1.50");   // bare = /32
                Assert.That(host.Contains(IP("192.168.1.50")), Is.True);
                Assert.That(host.Contains(IP("192.168.1.51")), Is.False);

                var all = IpCidr.Parse("0.0.0.0/0");
                Assert.That(all.Contains(IP("8.8.8.8")),   Is.True);
                Assert.That(all.Contains(IP("10.0.0.1")),  Is.True);
                Assert.That(all.Contains(IP("::1")),       Is.False, "a v4 /0 must not match a v6 address");
            });
        }

        #endregion

        #region Cidr_IPv6_PrefixMatching

        [Test]
        public void Cidr_IPv6_PrefixMatching()
        {
            Assert.Multiple(() => {
                var ula = IpCidr.Parse("fc00::/7");
                Assert.That(ula.Contains(IP("fc00::1")), Is.True);
                Assert.That(ula.Contains(IP("fd00::1")), Is.True,  "fd.. is inside fc00::/7");
                Assert.That(ula.Contains(IP("fe80::1")), Is.False, "link-local is outside fc00::/7");

                var loop = IpCidr.Parse("::1/128");
                Assert.That(loop.Contains(IP("::1")),  Is.True);
                Assert.That(loop.Contains(IP("::2")),  Is.False);
                Assert.That(loop.Contains(IP("127.0.0.1")), Is.False, "family mismatch");
            });
        }

        #endregion

        #region Ports_RangesAndSets

        [Test]
        public void Ports_RangesAndSets()
        {
            var set = PortSet.Parse("80, 443, 8000-8999");
            Assert.Multiple(() => {
                Assert.That(set.Contains(80),   Is.True);
                Assert.That(set.Contains(443),  Is.True);
                Assert.That(set.Contains(8000), Is.True);
                Assert.That(set.Contains(8999), Is.True);
                Assert.That(set.Contains(79),   Is.False);
                Assert.That(set.Contains(9000), Is.False);
            });
        }

        #endregion

        #region FirstMatchWins

        [Test]
        public void FirstMatchWins()
        {
            // Deny one host inside an otherwise-allowed subnet — order matters.
            var acl = NetworkAcl.DenyByDefault()
                          .Deny(Cidr:  "10.20.0.13")
                          .Allow(Cidr: "10.20.0.0/16");

            Assert.Multiple(() => {
                Assert.That(acl.Allows(IP("10.20.0.13"), 5432), Is.False, "the earlier deny wins");
                Assert.That(acl.Allows(IP("10.20.0.14"), 5432), Is.True);
                Assert.That(acl.Allows(IP("10.99.0.1"),  5432), Is.False, "default deny");
            });
        }

        #endregion

        #region PortScopedRule

        [Test]
        public void PortScopedRule()
        {
            var acl = NetworkAcl.DenyByDefault().Allow(Cidr: "10.20.0.0/16", Ports: "5432,6379");
            Assert.Multiple(() => {
                Assert.That(acl.Allows(IP("10.20.0.5"), 5432), Is.True);
                Assert.That(acl.Allows(IP("10.20.0.5"), 6379), Is.True);
                Assert.That(acl.Allows(IP("10.20.0.5"), 22),   Is.False, "port not permitted");
            });
        }

        #endregion

        #region Presets_LoopbackAndPrivate

        [Test]
        public void Presets_LoopbackAndPrivate()
        {
            var loopback = NetworkAcl.LoopbackOnly;
            var priv     = NetworkAcl.PrivateNetworksOnly;

            Assert.Multiple(() => {
                Assert.That(loopback.Allows(IP("127.0.0.1"), 5432), Is.True);
                Assert.That(loopback.Allows(IP("::1"),       5432), Is.True);
                Assert.That(loopback.Allows(IP("10.0.0.1"),  5432), Is.False);
                Assert.That(loopback.Allows(IP("8.8.8.8"),   443),  Is.False);

                Assert.That(priv.Allows(IP("10.1.2.3"),      5432), Is.True);
                Assert.That(priv.Allows(IP("172.16.5.5"),    5432), Is.True);
                Assert.That(priv.Allows(IP("192.168.1.10"),  5432), Is.True);
                Assert.That(priv.Allows(IP("fc00::1234"),    5432), Is.True);
                Assert.That(priv.Allows(IP("8.8.8.8"),       443),  Is.False, "public address denied");
            });
        }

        #endregion

        #region DnsRebinding_AllResolvedAddressesMustPass

        [Test]
        public void DnsRebinding_AllResolvedAddressesMustPass()
        {
            var acl = NetworkAcl.Subnet("10.20.0.0/16");

            // A name that resolves to one allowed + one disallowed address must be refused (rebinding attack).
            var mixed = new IIPAddress[] { IPv4Address.Parse("10.20.0.5"), IPv4Address.Parse("8.8.8.8") };
            var clean = new IIPAddress[] { IPv4Address.Parse("10.20.0.5"), IPv4Address.Parse("10.20.9.9") };

            Assert.Multiple(() => {
                Assert.That(acl.AllowsAll(mixed, 5432), Is.False, "one disallowed address poisons the whole name");
                Assert.That(acl.AllowsAll(clean, 5432), Is.True);
                Assert.That(acl.AllowsAll(System.Array.Empty<IIPAddress>(), 5432), Is.False, "an empty resolution is denied");
            });
        }

        #endregion

        #region Rules_WrittenAsText

        [Test]
        public void Rules_WrittenAsText()
        {

            var acl = NetworkAcl.AllowByDefault();

            Assert.Multiple(() => {

                Assert.That(acl.TryAdd("192.168.7.0/24 => ALLOW", out var e1), Is.True, e1);
                Assert.That(acl.TryAdd(" 10.0.0.0/8=>deny ",      out var e2), Is.True, e2);
                Assert.That(acl.TryAdd("fd00::/8 => Deny",        out var e3), Is.True, e3);

                Assert.That(acl.TryAdd("10.0.0.0/8 DENY",         out var noArrow),   Is.False);
                Assert.That(noArrow,                                                    Does.Contain("ALLOW").And.Contain("DENY"));
                Assert.That(acl.TryAdd("10.0.0.0/8 => MAYBE",     out var noAction),  Is.False);
                Assert.That(noAction,                                                   Does.Contain("MAYBE"));
                Assert.That(acl.TryAdd("10.0.0.300/8 => DENY",    out var noPrefix),  Is.False);
                Assert.That(noPrefix,                                                   Does.Contain("no address prefix"));

                Assert.That(acl.Rules,                                  Has.Count.EqualTo(3), "a rule that is none is not added");
                Assert.That(acl.Allows(IP("192.168.7.9"),   443),       Is.True);
                Assert.That(acl.Allows(IP("10.1.2.3"),      443),       Is.False);
                Assert.That(acl.Allows(IP("fd12::1"),       443),       Is.False);
                Assert.That(acl.Allows(IP("8.8.8.8"),       443),       Is.True);

            });

        }

        #endregion

        #region MappedIPv4_IsMatchedAsIPv4

        [Test]
        public void MappedIPv4_IsMatchedAsIPv4()
        {

            var acl = NetworkAcl.AllowByDefault().Deny(Cidr: "10.0.0.0/8");

            Assert.Multiple(() => {
                Assert.That(acl.Allows(IP("::ffff:10.0.0.1"), 80), Is.False, "::ffff:10.0.0.1 reaches 10.0.0.1");
                Assert.That(acl.Allows(IP("::ffff:8.8.8.8"),  80), Is.True);
            });

        }

        #endregion

        #region DenyNonPublicNetworks_LeavesThePublicInternet

        [Test]
        public void DenyNonPublicNetworks_LeavesThePublicInternet()
        {

            var publicOnly  = NetworkAcl.AllowByDefault().DenyNonPublicNetworks();
            var oneLAN      = NetworkAcl.AllowByDefault();

            Assert.That(oneLAN.TryAdd("192.168.7.0/24 => ALLOW", out var error), Is.True, error);
            oneLAN.DenyNonPublicNetworks();

            Assert.Multiple(() => {

                foreach (var denied in new[] { "127.0.0.1", "10.1.2.3", "172.20.0.1", "192.168.1.1", "169.254.169.254", "100.64.0.1",
                                               "0.0.0.0", "224.0.0.1", "::1", "::", "fd00::1", "fe80::1", "64:ff9b::a00:1", "::ffff:127.0.0.1" })
                    Assert.That(publicOnly.Allows(IP(denied), 443), Is.False, denied);

                foreach (var allowed in new[] { "8.8.8.8", "1.1.1.1", "2606:4700:4700::1111" })
                    Assert.That(publicOnly.Allows(IP(allowed), 443), Is.True, allowed);

                Assert.That(oneLAN.Allows(IP("192.168.7.20"), 443), Is.True,  "a rule before the presets wins");
                Assert.That(oneLAN.Allows(IP("192.168.8.20"), 443), Is.False);

            });

        }

        #endregion

    }

}
