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

using org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC;

using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.TLS;

/// <summary>
/// Verifies the TLS 1.3 key schedule byte for byte against the example traces from RFC 8448
/// ("Simple 1-RTT Handshake"). This is the touchstone from phase 2 of the plan.
/// </summary>
[TestFixture]
public class KeyScheduleTests
{
    // ClientHello (196 bytes) and ServerHello (90 bytes) from RFC 8448 §3.
    private const string ClientHelloHex = """
        01 00 00 c0 03 03 cb 34 ec b1 e7 81 63 ba 1c 38 c6 da cb 19 6a 6d ff a2 1a 8d 99 12 ec 18 a2 ef 62 83
        02 4d ec e7 00 00 06 13 01 13 03 13 02 01 00 00 91 00 00 00 0b
        00 09 00 00 06 73 65 72 76 65 72 ff 01 00 01 00 00 0a 00 14 00
        12 00 1d 00 17 00 18 00 19 01 00 01 01 01 02 01 03 01 04 00 23
        00 00 00 33 00 26 00 24 00 1d 00 20 99 38 1d e5 60 e4 bd 43 d2
        3d 8e 43 5a 7d ba fe b3 c0 6e 51 c1 3c ae 4d 54 13 69 1e 52 9a
        af 2c 00 2b 00 03 02 03 04 00 0d 00 20 00 1e 04 03 05 03 06 03
        02 03 08 04 08 05 08 06 04 01 05 01 06 01 02 01 04 02 05 02 06
        02 02 02 00 2d 00 02 01 01 00 1c 00 02 40 01
        """;

    private const string ServerHelloHex = """
        02 00 00 56 03 03 a6 af 06 a4 12 18 60
        dc 5e 6e 60 24 9c d3 4c 95 93 0c 8a c5 cb 14 34 da c1 55 77 2e
        d3 e2 69 28 00 13 01 00 00 2e 00 33 00 24 00 1d 00 20 c9 82 88
        76 11 20 95 fe 66 76 2b db f7 c6 72 e1 56 d6 cc 25 3b 83 3d f1
        dd 69 b1 b0 4e 75 1f 0f 00 2b 00 02 03 04
        """;

    private const string EcdheSharedSecret = "8bd4054fb55b9d63fdfbacf9f04b9f0d35e6d63f537563efd46272900f89492d";

    private static byte[] TranscriptHash()
    {
        byte[] transcript = [.. Hex.Parse(ClientHelloHex), .. Hex.Parse(ServerHelloHex)];
        return new KeySchedule(CipherSuite.Aes128GcmSha256).TranscriptHash(transcript);
    }

    [Test]
    public void TranscriptHash_ClientHelloServerHello_MatchesRfc8448()
    {
        Assert.That(Hex.ToHex(TranscriptHash()), Is.EqualTo("860c06edc07858ee8e78f0e7428c58edd6b43f2ca3e6e95f02ed063cf0e1cad8"));
    }

    [Test]
    public void EarlySecret_NoPsk_MatchesRfc8448()
    {
        var ks = new KeySchedule(CipherSuite.Aes128GcmSha256);
        Assert.That(Hex.ToHex(ks.EarlySecret()), Is.EqualTo("33ad0a1c607ec03b09e6cd9893680ce210adf300aa1f2660e1b22e10f170f92a"));
    }

    [Test]
    public void HandshakeSecret_MatchesRfc8448()
    {
        var ks = new KeySchedule(CipherSuite.Aes128GcmSha256);
        byte[] hs = ks.HandshakeSecret(ks.EarlySecret(), Hex.Parse(EcdheSharedSecret));
        Assert.That(Hex.ToHex(hs), Is.EqualTo("1dc826e93606aa6fdc0aadc12f741b01046aa6b99f691ed221a9f0ca043fbeac"));
    }

    [Test]
    public void HandshakeTrafficSecrets_MatchRfc8448()
    {
        var ks = new KeySchedule(CipherSuite.Aes128GcmSha256);
        HandshakeTrafficSecrets s = ks.DeriveHandshakeSecrets(Hex.Parse(EcdheSharedSecret), TranscriptHash());

        Assert.That(Hex.ToHex(s.ClientHandshakeTrafficSecret), Is.EqualTo("b3eddb126e067f35a780b3abf45e2d8f3b1a950738f52e9600746a0e27a55a21"));
        Assert.That(Hex.ToHex(s.ServerHandshakeTrafficSecret), Is.EqualTo("b67b7d690cc16c4e75e54213cb2d37b4e9c912bcded9105d42befd59d391ad38"));
    }

    [Test]
    public void TrafficKeyDerivation_FromServerHsSecret_MatchesRfc8448()
    {
        // RFC 8448 uses the TLS labels "key"/"iv"; this checks HKDF-Expand-Label directly.
        // (QUIC uses "quic key"/"quic iv" — same mechanics, different labels, verified in RFC 9001.)
        byte[] sHsSecret = Hex.Parse("b67b7d690cc16c4e75e54213cb2d37b4e9c912bcded9105d42befd59d391ad38");
        byte[] key = TlsHkdf.ExpandLabel(System.Security.Cryptography.HashAlgorithmName.SHA256, sHsSecret, "key", 16);
        byte[] iv = TlsHkdf.ExpandLabel(System.Security.Cryptography.HashAlgorithmName.SHA256, sHsSecret, "iv", 12);

        Assert.That(Hex.ToHex(key), Is.EqualTo("3fce516009c21727d0f2e4e86ee403bc"));
        Assert.That(Hex.ToHex(iv), Is.EqualTo("5d313eb2671276ee13000b30"));
    }
}
