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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Messages;

/// <summary>
/// The range of a response's <c>:status</c>. RFC 9110 §15 has a status code as three digits and
/// calls a value outside 100–599 invalid, and a client that receives one SHOULD process the
/// response as if it had a 5xx status: an invalid status code does not make the response
/// malformed. RFC 9114 §4.3.2 asks for exactly one <c>:status</c> and nothing more of it. So the
/// client reads any three-digit code from 100 to 999 as a response with that code, and refuses as
/// malformed only what is not three digits or lies below 100, where neither a final response nor
/// an interim one can be.
/// </summary>
[TestFixture]
public class Http3StatusCodeRangeTests
{

    [TestCase("100", 100)]
    [TestCase("199", 199)]
    [TestCase("200", 200)]
    [TestCase("599", 599)]
    [TestCase("600", 600)]
    [TestCase("999", 999)]
    public void Validator_ReadsEveryThreeDigitStatusFrom100To999(string value, int expected)
    {
        Assert.That(Http3MessageValidator.ValidateResponseHeaders([new(":status", value)], out int status), Is.Null);
        Assert.That(status, Is.EqualTo(expected));
    }

    [TestCase("000")]
    [TestCase("099")]
    [TestCase("20")]
    [TestCase("1000")]
    [TestCase("+20")]
    [TestCase(" 20")]
    [TestCase("")]
    public void Validator_RefusesAStatusThatIsNotThreeDigitsOrBelow100(string value)
        => Assert.That(Http3MessageValidator.ValidateResponseHeaders([new(":status", value)], out _), Is.Not.Null);

    [Test]
    public async Task Client_ReadsA999AsTheFinalResponse_WithItsCodeAndContent()
    {
        // Over real sockets: the server writes the handler's 999, and the client hands it on as
        // the response it is, with its content, rather than as a malformed one.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(cert,
            request => new Http3Response { Status = 999, Body = Encoding.UTF8.GetBytes("invalid, not malformed") },
            port: IPPort.Zero);
        server.Start();

        await using var client = new Http3Client("localhost", server.Port, validation);
        await client.ConnectAsync(TimeSpan.FromSeconds(10));
        Http3Response response = await client.GetAsync("/").WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(response.Status, Is.EqualTo(999));
        Assert.That(response.BodyText, Is.EqualTo("invalid, not malformed"));
    }

}
