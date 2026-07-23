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

using System.Net.Sockets;

using NUnit.Framework;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.IP
{

    /// <summary>
    /// IPSocket tests — in particular the conversions to and from the .NET IPEndPoint, which must
    /// preserve the address family for both IPv4 and IPv6 (including the "any" and loopback addresses).
    /// </summary>
    [TestFixture]
    public class IPSocketTests
    {

        #region ToIPEndPoint_IPv4Any()

        [Test]
        public void ToIPEndPoint_IPv4Any()
        {

            var endPoint = new IPSocket(IPv4Address.Any, IPPort.Parse(22)).ToIPEndPoint();

            Assert.Multiple(() => {
                Assert.That(endPoint.AddressFamily, Is.EqualTo(AddressFamily.InterNetwork));
                Assert.That(endPoint.Address,       Is.EqualTo(System.Net.IPAddress.Any));
                Assert.That(endPoint.Port,          Is.EqualTo(22));
            });

        }

        #endregion

        #region ToIPEndPoint_IPv6Any()

        [Test]
        public void ToIPEndPoint_IPv6Any()
        {

            // Regression test: an IPv6 "any" socket must convert to the IPv6 any endpoint (::), not to
            // the IPv4 any endpoint (0.0.0.0). Getting this wrong silently breaks dual-stack binding.
            var endPoint = new IPSocket(IPv6Address.Any, IPPort.Parse(22)).ToIPEndPoint();

            Assert.Multiple(() => {
                Assert.That(endPoint.AddressFamily, Is.EqualTo(AddressFamily.InterNetworkV6));
                Assert.That(endPoint.Address,       Is.EqualTo(System.Net.IPAddress.IPv6Any));
                Assert.That(endPoint.Port,          Is.EqualTo(22));
            });

        }

        #endregion

        #region ToIPEndPoint_IPv4Localhost()

        [Test]
        public void ToIPEndPoint_IPv4Localhost()
        {

            var endPoint = new IPSocket(IPv4Address.Localhost, IPPort.Parse(2222)).ToIPEndPoint();

            Assert.Multiple(() => {
                Assert.That(endPoint.AddressFamily, Is.EqualTo(AddressFamily.InterNetwork));
                Assert.That(endPoint.Address,       Is.EqualTo(System.Net.IPAddress.Loopback));
                Assert.That(endPoint.Port,          Is.EqualTo(2222));
            });

        }

        #endregion

        #region ToIPEndPoint_IPv6Localhost()

        [Test]
        public void ToIPEndPoint_IPv6Localhost()
        {

            var endPoint = new IPSocket(IPv6Address.Localhost, IPPort.Parse(2222)).ToIPEndPoint();

            Assert.Multiple(() => {
                Assert.That(endPoint.AddressFamily, Is.EqualTo(AddressFamily.InterNetworkV6));
                Assert.That(endPoint.Address,       Is.EqualTo(System.Net.IPAddress.IPv6Loopback));
                Assert.That(endPoint.Port,          Is.EqualTo(2222));
            });

        }

        #endregion

        #region ToIPEndPoint_NormalIPv4()

        [Test]
        public void ToIPEndPoint_NormalIPv4()
        {

            var endPoint = new IPSocket(IPv4Address.Parse("192.168.1.5"), IPPort.Parse(443)).ToIPEndPoint();

            Assert.Multiple(() => {
                Assert.That(endPoint.AddressFamily, Is.EqualTo(AddressFamily.InterNetwork));
                Assert.That(endPoint.Address,       Is.EqualTo(System.Net.IPAddress.Parse("192.168.1.5")));
                Assert.That(endPoint.Port,          Is.EqualTo(443));
            });

        }

        #endregion

        #region ToIPEndPoint_NormalIPv6()

        [Test]
        public void ToIPEndPoint_NormalIPv6()
        {

            var endPoint = new IPSocket(IPv6Address.Parse("2001:db8::1"), IPPort.Parse(443)).ToIPEndPoint();

            Assert.Multiple(() => {
                Assert.That(endPoint.AddressFamily, Is.EqualTo(AddressFamily.InterNetworkV6));
                Assert.That(endPoint.Address,       Is.EqualTo(System.Net.IPAddress.Parse("2001:db8::1")));
                Assert.That(endPoint.Port,          Is.EqualTo(443));
            });

        }

        #endregion

        #region RoundTrip_ThroughIPEndPoint([TestCaseSource])

        public static System.Collections.Generic.IEnumerable<IPSocket> RoundTripSockets()
        {
            yield return new IPSocket(IPv4Address.Any,       IPPort.Parse(22));
            yield return new IPSocket(IPv6Address.Any,       IPPort.Parse(22));
            yield return new IPSocket(IPv4Address.Localhost, IPPort.Parse(22));
            yield return new IPSocket(IPv6Address.Localhost, IPPort.Parse(22));
            yield return new IPSocket(IPv4Address.Parse("10.20.30.40"),  IPPort.Parse(8080));
            yield return new IPSocket(IPv6Address.Parse("2001:db8::42"), IPPort.Parse(8080));
        }

        [Test]
        [TestCaseSource(nameof(RoundTripSockets))]
        public void RoundTrip_ThroughIPEndPoint(IPSocket Original)
        {
            Assert.That(IPSocket.FromIPEndPoint(Original.ToIPEndPoint()), Is.EqualTo(Original));
        }

        #endregion

    }

}
