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

using org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC;

using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.TLS;

[TestFixture]
public class ServerHelloTests
{
    [Test]
    public void Parse_MinimalServerHello_ExtractsKeyScheduleFields()
    {
        // Hand-built ServerHello: cipher=0x1301, supported_versions=0x0304,
        // key_share = secp256r1 (0x0017) with a 2-byte dummy key aabb.
        //   Body: legacy_version(0303) random(32×00) sessid(00) cipher(1301) comp(00)
        //         extlen(0010) [supported_versions 002b0002 0304][key_share 00330006 0017 0002 aabb]
        string supportedVersions = "002b" + "0002" + "0304";
        string keyShare = "0033" + "0006" + "0017" + "0002" + "aabb";
        string extensions = supportedVersions + keyShare;
        string body =
            "0303" + new string('0', 64) + "00" + "1301" + "00" +
            (extensions.Length / 2).ToString("x4") + extensions;
        int bodyLen = body.Length / 2;
        string hex = "02" + bodyLen.ToString("x6") + body;

        bool ok = ServerHello.TryParse(Hex.Parse(hex), out ServerHelloInfo? info);

        Assert.That(ok, Is.True);
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.IsHelloRetryRequest, Is.False);
        Assert.That(info.CipherSuite, Is.EqualTo(CipherSuite.Aes128GcmSha256));
        Assert.That(info.SelectedVersion, Is.EqualTo((ushort?)0x0304));
        Assert.That(info.KeyShareGroup, Is.EqualTo(NamedGroup.Secp256r1));
        Assert.That(Hex.ToHex(info.KeySharePublicKey!), Is.EqualTo("aabb"));
    }

    [Test]
    public void Parse_DetectsHelloRetryRequest()
    {
        // Random == SHA-256("HelloRetryRequest"); minimal remainder with empty extensions.
        string hrrRandom = "cf21ad74e59a6111be1d8c021e65b891c2a211167abb8c5e079e09e2c8a8339c";
        string body = "0303" + hrrRandom + "00" + "1301" + "00" + "0000";
        int bodyLen = body.Length / 2;
        string hex = "02" + bodyLen.ToString("x6") + body;

        Assert.That(ServerHello.TryParse(Hex.Parse(hex), out ServerHelloInfo? info), Is.True);
        Assert.That(info!.IsHelloRetryRequest, Is.True);
    }

    [Test]
    public void Parse_RejectsWrongHandshakeType()
    {
        Assert.That(ServerHello.TryParse(Hex.Parse("01000000"), out _), Is.False); // 01 = ClientHello
    }
}
