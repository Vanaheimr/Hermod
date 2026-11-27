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

using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// RFC 9440, Client-Cert and Client-Cert-Chain: how a TLS-terminating
    /// reverse proxy tells the server behind it which certificate the client
    /// authenticated with.
    ///
    /// Two things can go wrong, and they are on opposite sides. The server can
    /// read more into a value than is there: a byte sequence that is not
    /// exactly one certificate, or a chain with a link that does not read, taken
    /// for a shorter chain. And the proxy can pass on what the client sent,
    /// which is the whole of the attack RFC 9440 has to defend against: a client
    /// that writes Client-Cert itself names any certificate it likes.
    ///
    /// The values of Appendix A are the RFC's own; the rest are certificates
    /// made here, a root, an intermediate and a client, and a stranger's.
    /// </summary>
    [TestFixture]
    public class ClientCertificateFieldsTests
    {

        #region RFC 9440, Appendix A

        // Figure 1: the client's certificate, its intermediate and the root, as
        // PEM, without the BEGIN and END lines.
        private const String LeafPEM          = "MIIBqDCCAU6gAwIBAgIBBzAKBggqhkjOPQQDAjA6MRswGQYDVQQKDBJMZXQncyBB" +
                                                "dXRoZW50aWNhdGUxGzAZBgNVBAMMEkxBIEludGVybWVkaWF0ZSBDQTAeFw0yMDAx" +
                                                "MTQyMjU1MzNaFw0yMTAxMjMyMjU1MzNaMA0xCzAJBgNVBAMMAkJDMFkwEwYHKoZI" +
                                                "zj0CAQYIKoZIzj0DAQcDQgAE8YnXXfaUgmnMtOXU/IncWalRhebrXmckC8vdgJ1p" +
                                                "5Be5F/3YC8OthxM4+k1M6aEAEFcGzkJiNy6J84y7uzo9M6NyMHAwCQYDVR0TBAIw" +
                                                "ADAfBgNVHSMEGDAWgBRm3WjLa38lbEYCuiCPct0ZaSED2DAOBgNVHQ8BAf8EBAMC" +
                                                "BsAwEwYDVR0lBAwwCgYIKwYBBQUHAwIwHQYDVR0RAQH/BBMwEYEPYmRjQGV4YW1w" +
                                                "bGUuY29tMAoGCCqGSM49BAMCA0gAMEUCIBHda/r1vaL6G3VliL4/Di6YK0Q6bMje" +
                                                "SkC3dFCOOB8TAiEAx/kHSB4urmiZ0NX5r5XarmPk0wmuydBVoU4hBVZ1yhk=";

        private const String IntermediatePEM  = "MIIB5jCCAYugAwIBAgIBFjAKBggqhkjOPQQDAjBWMQswCQYDVQQGEwJVUzEbMBkG" +
                                                "A1UECgwSTGV0J3MgQXV0aGVudGljYXRlMSowKAYDVQQDDCFMZXQncyBBdXRoZW50" +
                                                "aWNhdGUgUm9vdCBBdXRob3JpdHkwHhcNMjAwMTE0MjEzMjMwWhcNMzAwMTExMjEz" +
                                                "MjMwWjA6MRswGQYDVQQKDBJMZXQncyBBdXRoZW50aWNhdGUxGzAZBgNVBAMMEkxB" +
                                                "IEludGVybWVkaWF0ZSBDQTBZMBMGByqGSM49AgEGCCqGSM49AwEHA0IABJf+aA54" +
                                                "RC5pyLAR5yfXVYmNpgd+CGUTDp2KOGhc0gK91zxhHesEYkdXkpS2UN8Kati+yHtW" +
                                                "CV3kkhCngGyv7RqjZjBkMB0GA1UdDgQWBBRm3WjLa38lbEYCuiCPct0ZaSED2DAf" +
                                                "BgNVHSMEGDAWgBTEA2Q6eecKu9g9yb5glbkhhVINGDASBgNVHRMBAf8ECDAGAQH/" +
                                                "AgEAMA4GA1UdDwEB/wQEAwIBhjAKBggqhkjOPQQDAgNJADBGAiEA5pLvaFwRRkxo" +
                                                "mIAtDIwg9D7gC1xzxBl4r28EzmSO1pcCIQCJUShpSXO9HDIQMUgH69fNDEMHXD3R" +
                                                "RX5gP7kuu2KGMg==";

        private const String RootPEM          = "MIICBjCCAaygAwIBAgIJAKS0yiqKtlhoMAoGCCqGSM49BAMCMFYxCzAJBgNVBAYT" +
                                                "AlVTMRswGQYDVQQKDBJMZXQncyBBdXRoZW50aWNhdGUxKjAoBgNVBAMMIUxldCdz" +
                                                "IEF1dGhlbnRpY2F0ZSBSb290IEF1dGhvcml0eTAeFw0yMDAxMTQyMTI1NDVaFw00" +
                                                "MDAxMDkyMTI1NDVaMFYxCzAJBgNVBAYTAlVTMRswGQYDVQQKDBJMZXQncyBBdXRo" +
                                                "ZW50aWNhdGUxKjAoBgNVBAMMIUxldCdzIEF1dGhlbnRpY2F0ZSBSb290IEF1dGhv" +
                                                "cml0eTBZMBMGByqGSM49AgEGCCqGSM49AwEHA0IABFoaHU+Z5bPKmGzlYXtCf+E6" +
                                                "HYj62fORaHDOrt+yyh3H/rTcs7ynFfGn+gyFsrSP3Ez88rajv+U2NfD0o0uZ4Pmj" +
                                                "YzBhMB0GA1UdDgQWBBTEA2Q6eecKu9g9yb5glbkhhVINGDAfBgNVHSMEGDAWgBTE" +
                                                "A2Q6eecKu9g9yb5glbkhhVINGDAPBgNVHRMBAf8EBTADAQH/MA4GA1UdDwEB/wQE" +
                                                "AwIBhjAKBggqhkjOPQQDAgNIADBFAiEAmAeg1ycKHriqHnaD4M/UDBpQRpkmdcRF" +
                                                "YGMg1Qyrkx4CIB4ivz3wQcQkGhcsUZ1SOImd/lq1Q0FLf09rGfLQPWDc";

        // Figure 2: the Client-Cert field value, its display line breaks removed.
        private const String Figure2          = ":MIIBqDCCAU6gAwIBAgIBBzAKBggqhkjOPQQDAjA6MRswGQYDVQQKDBJ" +
                                                "MZXQncyBBdXRoZW50aWNhdGUxGzAZBgNVBAMMEkxBIEludGVybWVkaWF0ZSBDQTAeFw0" +
                                                "yMDAxMTQyMjU1MzNaFw0yMTAxMjMyMjU1MzNaMA0xCzAJBgNVBAMMAkJDMFkwEwYHKoZ" +
                                                "Izj0CAQYIKoZIzj0DAQcDQgAE8YnXXfaUgmnMtOXU/IncWalRhebrXmckC8vdgJ1p5Be" +
                                                "5F/3YC8OthxM4+k1M6aEAEFcGzkJiNy6J84y7uzo9M6NyMHAwCQYDVR0TBAIwADAfBgN" +
                                                "VHSMEGDAWgBRm3WjLa38lbEYCuiCPct0ZaSED2DAOBgNVHQ8BAf8EBAMCBsAwEwYDVR0" +
                                                "lBAwwCgYIKwYBBQUHAwIwHQYDVR0RAQH/BBMwEYEPYmRjQGV4YW1wbGUuY29tMAoGCCq" +
                                                "GSM49BAMCA0gAMEUCIBHda/r1vaL6G3VliL4/Di6YK0Q6bMjeSkC3dFCOOB8TAiEAx/k" +
                                                "HSB4urmiZ0NX5r5XarmPk0wmuydBVoU4hBVZ1yhk=:";

        // Figure 3: the Client-Cert-Chain field value, the intermediate and the
        // root, its display line breaks removed.
        private const String Figure3          = ":MIIB5jCCAYugAwIBAgIBFjAKBggqhkjOPQQDAjBWMQsw" +
                                                "CQYDVQQGEwJVUzEbMBkGA1UECgwSTGV0J3MgQXV0aGVudGljYXRlMSowKAYDVQQ" +
                                                "DDCFMZXQncyBBdXRoZW50aWNhdGUgUm9vdCBBdXRob3JpdHkwHhcNMjAwMTE0Mj" +
                                                "EzMjMwWhcNMzAwMTExMjEzMjMwWjA6MRswGQYDVQQKDBJMZXQncyBBdXRoZW50a" +
                                                "WNhdGUxGzAZBgNVBAMMEkxBIEludGVybWVkaWF0ZSBDQTBZMBMGByqGSM49AgEG" +
                                                "CCqGSM49AwEHA0IABJf+aA54RC5pyLAR5yfXVYmNpgd+CGUTDp2KOGhc0gK91zx" +
                                                "hHesEYkdXkpS2UN8Kati+yHtWCV3kkhCngGyv7RqjZjBkMB0GA1UdDgQWBBRm3W" +
                                                "jLa38lbEYCuiCPct0ZaSED2DAfBgNVHSMEGDAWgBTEA2Q6eecKu9g9yb5glbkhh" +
                                                "VINGDASBgNVHRMBAf8ECDAGAQH/AgEAMA4GA1UdDwEB/wQEAwIBhjAKBggqhkjO" +
                                                "PQQDAgNJADBGAiEA5pLvaFwRRkxomIAtDIwg9D7gC1xzxBl4r28EzmSO1pcCIQC" +
                                                "JUShpSXO9HDIQMUgH69fNDEMHXD3RRX5gP7kuu2KGMg==:, :MIICBjCCAaygAw" +
                                                "IBAgIJAKS0yiqKtlhoMAoGCCqGSM49BAMCMFYxCzAJBgNVBAYTAlVTMRswGQYDV" +
                                                "QQKDBJMZXQncyBBdXRoZW50aWNhdGUxKjAoBgNVBAMMIUxldCdzIEF1dGhlbnRp" +
                                                "Y2F0ZSBSb290IEF1dGhvcml0eTAeFw0yMDAxMTQyMTI1NDVaFw00MDAxMDkyMTI" +
                                                "1NDVaMFYxCzAJBgNVBAYTAlVTMRswGQYDVQQKDBJMZXQncyBBdXRoZW50aWNhdG" +
                                                "UxKjAoBgNVBAMMIUxldCdzIEF1dGhlbnRpY2F0ZSBSb290IEF1dGhvcml0eTBZM" +
                                                "BMGByqGSM49AgEGCCqGSM49AwEHA0IABFoaHU+Z5bPKmGzlYXtCf+E6HYj62fOR" +
                                                "aHDOrt+yyh3H/rTcs7ynFfGn+gyFsrSP3Ez88rajv+U2NfD0o0uZ4PmjYzBhMB0" +
                                                "GA1UdDgQWBBTEA2Q6eecKu9g9yb5glbkhhVINGDAfBgNVHSMEGDAWgBTEA2Q6ee" +
                                                "cKu9g9yb5glbkhhVINGDAPBgNVHRMBAf8EBTADAQH/MA4GA1UdDwEB/wQEAwIBh" +
                                                "jAKBggqhkjOPQQDAgNIADBFAiEAmAeg1ycKHriqHnaD4M/UDBpQRpkmdcRFYGMg" +
                                                "1Qyrkx4CIB4ivz3wQcQkGhcsUZ1SOImd/lq1Q0FLf09rGfLQPWDc:";

        private static X509Certificate2 FromPEM(String Base64)
            => X509CertificateLoader.LoadCertificate(Convert.FromBase64String(Base64));

        #endregion

        #region Data

        private HTTPServer?        httpServer;
        private HTTPAPI?           httpAPI;

        private X509Certificate2?  root;
        private X509Certificate2?  intermediate;
        private X509Certificate2?  client;
        private X509Certificate2?  stranger;

        private X509Certificate2   Root          => root!;
        private X509Certificate2   Intermediate  => intermediate!;
        private X509Certificate2   Client        => client!;
        private X509Certificate2   Stranger      => stranger!;

        private URL                URL
            => URL.Parse($"http://127.0.0.1:{httpServer!.TCPPort}");

        #endregion

        #region Setup / Teardown

        [OneTimeSetUp]
        public void Init()
        {

            root          = Issue("Client-Cert Test Root",          null,          IsCA: true);
            intermediate  = Issue("Client-Cert Test Intermediate",  root,          IsCA: true);
            client        = Issue("Client-Cert Test Client",        intermediate,  IsCA: false);
            stranger      = Issue("Client-Cert Test Stranger",      null,          IsCA: false);

            httpServer    = new HTTPServer(
                                TCPPort:    IPPort.Zero,
                                AutoStart:  true
                            );

            httpAPI       = new HTTPAPI(httpServer);

            // Reports what the server made of the two fields, and of the
            // connection, over which no certificate came.
            httpAPI.AddHandler(HTTPPath.Root + "whoami",
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => Task.FromResult(
                                                  new HTTPResponse.Builder(request) {
                                                      HTTPStatusCode  = HTTPStatusCode.OK,
                                                      ContentType     = HTTPContentType.Text.PLAIN,
                                                      Content         = String.Join(
                                                                            "\n",
                                                                            $"cert={request.ForwardedClientCertificate?.Subject ?? "-"}",
                                                                            $"chain={String.Join("|", request.ForwardedClientCertificateChain.Select(certificate => certificate.Subject))}",
                                                                            $"tls={request.ClientCertificate?.Subject ?? "-"}"
                                                                        ).ToUTF8Bytes(),
                                                      Connection      = ConnectionType.Close
                                                  }.AsImmutable));

        }

        [OneTimeTearDown]
        public async Task Shutdown()
        {

            if (httpServer is not null)
                await httpServer.DisposeAsync();

            root?.        Dispose();
            intermediate?.Dispose();
            client?.      Dispose();
            stranger?.    Dispose();

        }

        #endregion

        #region (private static) Issue(Name, Issuer, IsCA)

        private static X509Certificate2 Issue(String             Name,
                                              X509Certificate2?  Issuer,
                                              Boolean            IsCA)
        {

            var key      = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request  = new CertificateRequest($"CN={Name}", key, HashAlgorithmName.SHA256);

            if (IsCA)
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));

            var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
            var notAfter  = DateTimeOffset.UtcNow.AddDays(30);

            if (Issuer is null)
                return request.CreateSelfSigned(notBefore, notAfter);

            using var certificate = request.Create(Issuer, notBefore, notAfter, RandomNumberGenerator.GetBytes(8));

            return certificate.CopyWithPrivateKey(key);

        }

        #endregion

        #region (private) WhoAmI   (Fields)

        private async Task<Dictionary<String, String>> WhoAmI(params (String Name, String Value)[] Fields)

            => Report(await SendRaw(Fields));

        #endregion

        #region (private) SendRaw  (Fields)

        /// <summary>
        /// A request on the wire, each field a field line of its own, in the order
        /// given: the only way to send one field twice. Returns the status line and
        /// the body.
        /// </summary>
        private async Task<(String StatusLine, String Body)> SendRaw(params (String Name, String Value)[] Fields)
        {

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(System.Net.IPAddress.Loopback, httpServer!.TCPPort.ToUInt16());

            var stream  = tcp.GetStream();
            var request = new StringBuilder("GET /whoami HTTP/1.1\r\nHost: 127.0.0.1\r\n");

            foreach (var (name, value) in Fields)
                request.Append($"{name}: {value}\r\n");

            request.Append("Connection: close\r\n\r\n");

            await stream.WriteAsync(Encoding.ASCII.GetBytes(request.ToString()));

            using var reader   = new StreamReader(stream, Encoding.UTF8);
            var       readTask = reader.ReadToEndAsync();

            if (await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(10))) != readTask)
                Assert.Fail("No response within ten seconds.");

            var response   = await readTask;
            var endOfHead  = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);

            return (response[..response.IndexOf("\r\n", StringComparison.Ordinal)],
                    endOfHead < 0 ? "" : response[(endOfHead + 4)..]);

        }

        #endregion

        #region (private static) Report(Response)

        private static Dictionary<String, String> Report((String StatusLine, String Body) Response)
        {

            Assert.That(Response.StatusLine, Does.StartWith("HTTP/1.1 200"), Response.Body);

            return Response.Body.
                       Split('\n').
                       Select(line => line.Split('=', 2)).
                       ToDictionary(parts => parts[0], parts => parts[1]);

        }

        #endregion

        #region (private static) Field(Certificate)

        private static String Field(X509Certificate2 Certificate)
            => $":{Convert.ToBase64String(Certificate.RawData)}:";

        #endregion


        #region TheRFCsExample_IsReadAsTheClientsCertificate()

        /// <summary>
        /// Figure 2 is the certificate of Figure 1 that the client presented.
        /// </summary>
        [Test]
        public void TheRFCsExample_IsReadAsTheClientsCertificate()
        {

            using var leaf = FromPEM(LeafPEM);

            Assert.That(HTTPClientCertificateFields.TryParse(Figure2, out var certificate), Is.True);

            using (certificate)
            {
                Assert.That(certificate!.RawData, Is.EqualTo(leaf.RawData));
                Assert.That(certificate.Subject,  Is.EqualTo("CN=BC"));
                Assert.That(certificate.Issuer,   Does.Contain("CN=LA Intermediate CA"));
            }

        }

        #endregion

        #region TheRFCsExampleChain_IsReadInTLSOrder()

        /// <summary>
        /// Figure 3 is the rest of Figure 1, in its order: the client's issuer
        /// first, then the root.
        /// </summary>
        [Test]
        public void TheRFCsExampleChain_IsReadInTLSOrder()
        {

            using var leaf          = FromPEM(LeafPEM);
            using var intermediate  = FromPEM(IntermediatePEM);
            using var root          = FromPEM(RootPEM);

            Assert.That(HTTPClientCertificateFields.TryParseChain(Figure3, out var chain), Is.True);

            Assert.Multiple(() => {
                Assert.That(chain!.Select(certificate => certificate.RawData), Is.EqualTo(new[] { intermediate.RawData, root.RawData }));
                Assert.That(chain![0].Subject,  Is.EqualTo(leaf.Issuer),     "the first is the client's issuer");
                Assert.That(chain![1].Subject,  Is.EqualTo(chain[0].Issuer), "the second is the first's issuer");
            });

            foreach (var certificate in chain!)
                certificate.Dispose();

        }

        #endregion

        #region TheRFCsExample_IsWrittenToTheOctet()

        /// <summary>
        /// What the proxy writes for Figure 1's certificates is Figures 2 and 3.
        /// The certificates come from the PEM of Figure 1, the expected values from
        /// the field values of Figures 2 and 3, so neither side is derived from the
        /// other.
        /// </summary>
        [Test]
        public void TheRFCsExample_IsWrittenToTheOctet()
        {

            using var leaf          = FromPEM(LeafPEM);
            using var intermediate  = FromPEM(IntermediatePEM);
            using var root          = FromPEM(RootPEM);

            Assert.Multiple(() => {
                Assert.That(HTTPClientCertificateFields.FieldValue(leaf),                           Is.EqualTo(Figure2));
                Assert.That(HTTPClientCertificateFields.ChainFieldValue([ intermediate, root ]),    Is.EqualTo(Figure3));
            });

        }

        #endregion


        #region AParameter_IsIgnored()

        /// <summary>
        /// RFC 9440 defines no parameters; one that comes along changes nothing.
        /// </summary>
        [Test]
        public void AParameter_IsIgnored()
        {

            Assert.Multiple(() => {

                Assert.That(HTTPClientCertificateFields.TryParse($"{Field(Client)};x=1", out var certificate), Is.True);
                Assert.That(certificate!.RawData, Is.EqualTo(Client.RawData));

                Assert.That(HTTPClientCertificateFields.TryParseChain($"{Field(Intermediate)};x=1, {Field(Root)};y", out var chain), Is.True);
                Assert.That(chain!.Select(link => link.RawData), Is.EqualTo(new[] { Intermediate.RawData, Root.RawData }));

            });

        }

        #endregion

        #region AnInvalidClientCert_IsNoCertificate(Case)

        /// <summary>
        /// A value is a certificate only when it is a single byte sequence whose
        /// octets are one DER-encoded certificate and nothing else.
        /// </summary>
        [TestCase("absent")]
        [TestCase("empty")]
        [TestCase("not a byte sequence but its base64")]
        [TestCase("a string")]
        [TestCase("an integer")]
        [TestCase("an inner list")]
        [TestCase("two certificates")]
        [TestCase("a byte sequence of nothing")]
        [TestCase("a byte sequence of something else")]
        [TestCase("the DER cut short")]
        [TestCase("the DER and one more octet")]
        [TestCase("the DER twice")]
        [TestCase("the certificate as PEM")]
        [TestCase("an unterminated byte sequence")]
        public void AnInvalidClientCert_IsNoCertificate(String Case)
        {

            var der    = Client.RawData;
            var base64 = Convert.ToBase64String(der);

            String? value = Case switch {
                "absent"                               => null,
                "empty"                                => "",
                "not a byte sequence but its base64"   => base64,
                "a string"                             => $"\"{base64}\"",
                "an integer"                           => "42",
                "an inner list"                        => $"({Field(Client)})",
                "two certificates"                     => $"{Field(Client)}, {Field(Stranger)}",
                "a byte sequence of nothing"           => "::",
                "a byte sequence of something else"    => ":SGVybW9k:",
                "the DER cut short"                    => $":{Convert.ToBase64String(der[..^1])}:",
                "the DER and one more octet"           => $":{Convert.ToBase64String([.. der, 0])}:",
                "the DER twice"                        => $":{Convert.ToBase64String([.. der, .. der])}:",
                "the certificate as PEM"               => $":{Convert.ToBase64String(Encoding.ASCII.GetBytes(Client.ExportCertificatePem()))}:",
                "an unterminated byte sequence"        => $":{base64}",
                _                                      => throw new ArgumentException(Case)
            };

            Assert.That(HTTPClientCertificateFields.TryParse(value, out var certificate), Is.False, Case);
            Assert.That(certificate, Is.Null);

        }

        #endregion

        #region AnInvalidChain_IsNoChain(Case)

        /// <summary>
        /// A chain with one link that does not read is no chain, not a shorter one.
        /// </summary>
        [TestCase("absent")]
        [TestCase("a link that is not a byte sequence")]
        [TestCase("a link that is not a certificate")]
        [TestCase("a link that is an inner list")]
        [TestCase("a link cut short")]
        [TestCase("not a list")]
        public void AnInvalidChain_IsNoChain(String Case)
        {

            var value = Case switch {
                "absent"                              => null,
                "a link that is not a byte sequence"  => $"{Field(Intermediate)}, 42",
                "a link that is not a certificate"    => $"{Field(Intermediate)}, :SGVybW9k:",
                "a link that is an inner list"        => $"({Field(Intermediate)}), {Field(Root)}",
                "a link cut short"                    => $"{Field(Intermediate)}, :{Convert.ToBase64String(Root.RawData[..^1])}:",
                "not a list"                          => $"{Field(Intermediate)},, {Field(Root)}",
                _                                     => throw new ArgumentException(Case)
            };

            Assert.That(HTTPClientCertificateFields.TryParseChain(value, out var chain), Is.False, Case);
            Assert.That(chain, Is.Null);

        }

        #endregion


        #region TheServerReadsBothFields()

        /// <summary>
        /// What the proxy wrote is what the handler reads, the connection's own
        /// certificate untouched: none came over it.
        /// </summary>
        [Test]
        public async Task TheServerReadsBothFields()
        {

            var report = await WhoAmI(("Client-Cert",        Field(Client)),
                                      ("Client-Cert-Chain",  $"{Field(Intermediate)}, {Field(Root)}"));

            Assert.Multiple(() => {
                Assert.That(report["cert"],   Is.EqualTo(Client.Subject));
                Assert.That(report["chain"],  Is.EqualTo($"{Intermediate.Subject}|{Root.Subject}"));
                Assert.That(report["tls"],    Is.EqualTo("-"));
            });

        }

        #endregion

        #region AChainOverSeveralFieldLines_IsOneChain()

        /// <summary>
        /// "Client-Cert-Chain MAY have a list of values or occur multiple times in
        /// a request" (Section 2.3). The parser refused any field twice that it
        /// did not know to be a list, and the whole request with it.
        /// </summary>
        [Test]
        public async Task AChainOverSeveralFieldLines_IsOneChain()
        {

            var report = await WhoAmI(("Client-Cert",        Field(Client)),
                                      ("Client-Cert-Chain",  Field(Intermediate)),
                                      ("client-cert-chain",  Field(Root)));

            Assert.That(report["chain"], Is.EqualTo($"{Intermediate.Subject}|{Root.Subject}"));

        }

        #endregion

        #region ARepeatedClientCert_IsRefused()

        /// <summary>
        /// Client-Cert is a singleton, which "MUST NOT have a list of values or
        /// occur multiple times in a request" (Section 2.2), and a request with
        /// two is refused rather than read for either.
        /// </summary>
        [Test]
        public async Task ARepeatedClientCert_IsRefused()
        {

            var (statusLine, _) = await SendRaw(("Client-Cert",  Field(Client)),
                                                ("Client-Cert",  Field(Stranger)));

            Assert.That(statusLine, Does.StartWith("HTTP/1.1 400"));

        }

        #endregion

        #region AParsedRequest_WithTwoClientCerts_HasNone()

        /// <summary>
        /// HTTPRequest.TryParse keeps a field it sees twice as several values
        /// rather than refusing the request as the server does. Client-Cert is a
        /// singleton, so two of them are neither; a chain on two lines is still
        /// one chain.
        /// </summary>
        [Test]
        public void AParsedRequest_WithTwoClientCerts_HasNone()
        {

            Assert.That(HTTPRequest.TryParse("GET / HTTP/1.1\r\n" +
                                             "Host: 127.0.0.1\r\n" +
                                             $"Client-Cert: {Field(Client)}\r\n" +
                                             $"Client-Cert: {Field(Stranger)}\r\n" +
                                             $"Client-Cert-Chain: {Field(Intermediate)}\r\n" +
                                             "\r\n",
                                             out var twice),
                        Is.True);

            Assert.That(HTTPRequest.TryParse("GET / HTTP/1.1\r\n" +
                                             "Host: 127.0.0.1\r\n" +
                                             $"Client-Cert: {Field(Client)}\r\n" +
                                             $"Client-Cert-Chain: {Field(Intermediate)}\r\n" +
                                             $"Client-Cert-Chain: {Field(Root)}\r\n" +
                                             "\r\n",
                                             out var once),
                        Is.True);

            Assert.Multiple(() => {
                Assert.That(twice!.ForwardedClientCertificate,                                         Is.Null);
                Assert.That(twice!.ForwardedClientCertificateChain,                                    Is.Empty);
                Assert.That(once!. ForwardedClientCertificate?.RawData,                                Is.EqualTo(Client.RawData));
                Assert.That(once!. ForwardedClientCertificateChain.Select(link => link.RawData),       Is.EqualTo(new[] { Intermediate.RawData, Root.RawData }));
            });

        }

        #endregion

        #region AChainWithoutClientCert_IsNoChain()

        /// <summary>
        /// "Client-Cert-Chain MUST NOT appear unless Client-Cert is also present"
        /// (Section 2.3): one that does is the chain of nothing.
        /// </summary>
        [Test]
        public async Task AChainWithoutClientCert_IsNoChain()
        {

            var report = await WhoAmI(("Client-Cert-Chain",  $"{Field(Intermediate)}, {Field(Root)}"));

            Assert.Multiple(() => {
                Assert.That(report["cert"],   Is.EqualTo("-"));
                Assert.That(report["chain"],  Is.Empty);
            });

        }

        #endregion

        #region AnInvalidClientCert_LeavesTheRequestAlone()

        /// <summary>
        /// A value that is no certificate is no certificate, and no reason to
        /// refuse a request: a server that does not trust the sender has to
        /// ignore the field anyway, and whether this one does is the handler's.
        /// Its chain goes with it.
        /// </summary>
        [Test]
        public async Task AnInvalidClientCert_LeavesTheRequestAlone()
        {

            var report = await WhoAmI(("Client-Cert",        ":SGVybW9k:"),
                                      ("Client-Cert-Chain",  $"{Field(Intermediate)}, {Field(Root)}"));

            Assert.Multiple(() => {
                Assert.That(report["cert"],   Is.EqualTo("-"));
                Assert.That(report["chain"],  Is.Empty);
            });

        }

        #endregion

        #region AnInvalidChain_LeavesTheCertificate()

        /// <summary>
        /// A chain that does not read is no chain; the certificate stands on its
        /// own, as one sent without a chain does.
        /// </summary>
        [Test]
        public async Task AnInvalidChain_LeavesTheCertificate()
        {

            var report = await WhoAmI(("Client-Cert",        Field(Client)),
                                      ("Client-Cert-Chain",  $"{Field(Intermediate)}, :SGVybW9k:"));

            Assert.Multiple(() => {
                Assert.That(report["cert"],   Is.EqualTo(Client.Subject));
                Assert.That(report["chain"],  Is.Empty);
            });

        }

        #endregion


        #region TheProxy_ReplacesWhatTheClientSent()

        /// <summary>
        /// The attack RFC 9440 exists to stop: the client writes Client-Cert
        /// itself, naming a certificate it never authenticated with. The proxy
        /// declares the one it did, and only that one reaches the server, in
        /// whatever case the client spelled the fields.
        /// </summary>
        [Test]
        public async Task TheProxy_ReplacesWhatTheClientSent()
        {

            using var http = new HTTPClient(URL);

            var response = await http.RunRequest(
                                     HTTPMethod.GET,
                                     HTTPPath.Root + "whoami",
                                     RequestBuilder:  builder => {
                                         builder.SetHeaderField("client-cert",        Field(Stranger));
                                         builder.SetHeaderField("CLIENT-CERT-CHAIN",  Field(Stranger));
                                         builder.ForwardClientCertificate(Client, [ Intermediate ]);
                                     },
                                     RequestTimeout:  TimeSpan.FromSeconds(10)
                                 );

            var report = Report(($"HTTP/1.1 {response.HTTPStatusCode.Code}", response.HTTPBodyAsUTF8String!));

            Assert.Multiple(() => {
                Assert.That(report["cert"],   Is.EqualTo(Client.Subject));
                Assert.That(report["chain"],  Is.EqualTo(Intermediate.Subject));
            });

        }

        #endregion

        #region TheProxy_WithoutACertificate_RemovesBoth()

        /// <summary>
        /// A request on a connection without a client certificate "MUST be
        /// sanitized by removing any and all occurrences of the Client-Cert and
        /// Client-Cert-Chain header fields" (Section 2.4): what the client wrote
        /// does not reach the server, and nothing is written in its place.
        /// </summary>
        [Test]
        public void TheProxy_WithoutACertificate_RemovesBoth()
        {

            Assert.That(HTTPRequest.TryParse("GET /whoami HTTP/1.1\r\n" +
                                             "Host: 127.0.0.1\r\n" +
                                             $"Client-Cert: {Field(Stranger)}\r\n" +
                                             $"Client-Cert-Chain: {Field(Stranger)}\r\n" +
                                             "\r\n",
                                             out var received),
                        Is.True);

            Assert.That(received!.ForwardedClientCertificate?.RawData, Is.EqualTo(Stranger.RawData), "the client's own, before");

            var forwarded = new HTTPRequest.Builder(received).
                                    ForwardClientCertificate(received.ClientCertificate).
                                    AsImmutable;

            Assert.Multiple(() => {
                Assert.That(forwarded.ForwardedClientCertificate,                                            Is.Null);
                Assert.That(forwarded.ForwardedClientCertificateChain,                                       Is.Empty);
                Assert.That(forwarded.TryGetHeaderField(HTTPClientCertificateFields.ClientCert,      out _), Is.False);
                Assert.That(forwarded.TryGetHeaderField(HTTPClientCertificateFields.ClientCertChain, out _), Is.False);
                Assert.That(forwarded.EntirePDU,                                                             Does.Not.Contain("Client-Cert").IgnoreCase);
            });

        }

        #endregion

        #region TheProxy_LeavesTheClientsCertificateOutOfTheChain()

        /// <summary>
        /// X509Chain puts the client's certificate first; Client-Cert-Chain "does
        /// not itself include the end-entity certificate" (Section 2.3). The trust
        /// anchor is kept, as given.
        /// </summary>
        [Test]
        public void TheProxy_LeavesTheClientsCertificateOutOfTheChain()
        {

            Assert.That(HTTPRequest.TryParse("GET / HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n", out var received), Is.True);

            var forwarded = new HTTPRequest.Builder(received!).
                                    ForwardClientCertificate(Client, [ Client, Intermediate, Root ]).
                                    AsImmutable;

            Assert.Multiple(() => {
                Assert.That(forwarded.TryGetHeaderField(HTTPClientCertificateFields.ClientCertChain, out var chain), Is.True);
                Assert.That(chain,                                                                                  Is.EqualTo($"{Field(Intermediate)}, {Field(Root)}"));
                Assert.That(forwarded.ForwardedClientCertificate?.RawData,                                          Is.EqualTo(Client.RawData));
            });

        }

        #endregion

        #region TheProxy_RefusesAChainWithoutACertificate()

        /// <summary>
        /// A chain without the certificate it validated is a call that would write
        /// what Section 2.3 forbids.
        /// </summary>
        [Test]
        public void TheProxy_RefusesAChainWithoutACertificate()
        {

            Assert.That(HTTPRequest.TryParse("GET / HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n", out var received), Is.True);

            Assert.Throws<ArgumentException>(() => new HTTPRequest.Builder(received!).ForwardClientCertificate(null, [ Intermediate ]));

        }

        #endregion

    }

}
