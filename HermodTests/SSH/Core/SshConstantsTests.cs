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

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Sanity checks that the protocol constants carry their RFC-assigned numeric values.
    /// A wrong value here would silently break interoperability with every other implementation.
    /// </summary>
    [TestFixture]
    public class SshConstantsTests
    {

        [Test]
        public void MessageNumbers_HaveTheirAssignedValues()
        {
            Assert.Multiple(() => {
                Assert.That((Byte) SshMessageNumber.Disconnect,              Is.EqualTo(1));
                Assert.That((Byte) SshMessageNumber.KexInit,                 Is.EqualTo(20));
                Assert.That((Byte) SshMessageNumber.NewKeys,                 Is.EqualTo(21));
                Assert.That((Byte) SshMessageNumber.KexEcdhInit,             Is.EqualTo(30));
                Assert.That((Byte) SshMessageNumber.KexEcdhReply,            Is.EqualTo(31));
                Assert.That((Byte) SshMessageNumber.UserAuthRequest,         Is.EqualTo(50));
                Assert.That((Byte) SshMessageNumber.UserAuthSuccess,         Is.EqualTo(52));
                Assert.That((Byte) SshMessageNumber.UserAuthBanner,          Is.EqualTo(53));
                Assert.That((Byte) SshMessageNumber.GlobalRequest,           Is.EqualTo(80));
                Assert.That((Byte) SshMessageNumber.ChannelOpen,             Is.EqualTo(90));
                Assert.That((Byte) SshMessageNumber.ChannelData,             Is.EqualTo(94));
                Assert.That((Byte) SshMessageNumber.ChannelRequest,          Is.EqualTo(98));
                Assert.That((Byte) SshMessageNumber.Ping,                    Is.EqualTo(192));
                Assert.That((Byte) SshMessageNumber.Pong,                    Is.EqualTo(193));
            });
        }

        [Test]
        public void DisconnectReasons_HaveTheirAssignedValues()
        {
            Assert.Multiple(() => {
                Assert.That((UInt32) DisconnectReason.HostNotAllowedToConnect,      Is.EqualTo(1));
                Assert.That((UInt32) DisconnectReason.KeyExchangeFailed,            Is.EqualTo(3));
                Assert.That((UInt32) DisconnectReason.MacError,                     Is.EqualTo(5));
                Assert.That((UInt32) DisconnectReason.ProtocolVersionNotSupported,  Is.EqualTo(8));
                Assert.That((UInt32) DisconnectReason.ByApplication,                Is.EqualTo(11));
                Assert.That((UInt32) DisconnectReason.IllegalUserName,              Is.EqualTo(15));
            });
        }

    }

}
