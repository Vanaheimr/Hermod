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

using System.Net;
using System.Net.Sockets;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// RFC 9530 digest fields over HTTP/1.1: Content-Digest, Repr-Digest and the
    /// two Want-… fields, typed on both directions, and the three places where
    /// the stack itself touches them.
    ///
    /// Each of those places is about the same sentence of the RFC: the digest is
    /// of the octets as they travel, after any content coding. So the
    /// compression filter has to compute a handler's digest again, the server's
    /// own digest comes after the compression, and the client checks before it
    /// decodes. And a field that is not a valid Dictionary is not there at all
    /// (RFC 9651, Section 4.2), which the HTTP/2 copy of the grammar did not
    /// know before it was moved here.
    /// </summary>
    [TestFixture]
    public class DigestFieldsTests
    {

        #region (private) RawServer

        /// <summary>
        /// Answers each connection with the next canned response and keeps the
        /// request heads it saw — for what the client does with octets no Hermod
        /// server would send.
        /// </summary>
        private sealed class RawServer : IDisposable
        {

            private readonly TcpListener              listener  = new (System.Net.IPAddress.Loopback, 0);
            private readonly CancellationTokenSource  cts       = new ();
            private readonly Queue<Byte[]>            responses;

            public List<String>  Requests  { get; } = [];

            public URL           URL
                => URL.Parse($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}");

            public Task          Served    { get; }


            public RawServer(params Byte[][] Responses)
            {

                responses = new (Responses);

                listener.Start();

                Served = Task.Run(Serve);

            }

            private async Task Serve()
            {
                try
                {

                    while (responses.Count > 0)
                    {

                        using var tcpClient = await listener.AcceptTcpClientAsync(cts.Token);
                        var stream = tcpClient.GetStream();

                        var buffer  = new Byte[8192];
                        var length  = 0;

                        while (length < buffer.Length)
                        {

                            var read = await stream.ReadAsync(buffer.AsMemory(length), cts.Token);

                            if (read == 0)
                                break;

                            length += read;

                            if (Encoding.ASCII.GetString(buffer, 0, length).Contains("\r\n\r\n", StringComparison.Ordinal))
                                break;

                        }

                        Requests.Add(Encoding.ASCII.GetString(buffer, 0, length));

                        await stream.WriteAsync(responses.Dequeue(), cts.Token);
                        await stream.FlushAsync(cts.Token);

                    }

                }
                catch (OperationCanceledException)
                { }
                catch (IOException)
                { }
                catch (SocketException)
                { }
            }

            public void Dispose()
            {
                cts.Cancel();
                listener.Stop();
                cts.Dispose();
            }

        }

        #endregion

        #region Data

        private static readonly String  compressibleText  = String.Join("\n", Enumerable.Range(0, 400).Select(i => $"line {i} of a very repetitive document"));
        private static readonly Byte[]  helloWorld        = "Hello World!".ToUTF8Bytes();

        private HTTPServer?  compressingServer;
        private HTTPServer?  digestingServer;

        #endregion

        #region Setup / Teardown

        [OneTimeSetUp]
        public void Init()
        {

            #region The compressing server: AutomaticContentCompression alone

            compressingServer = new HTTPServer(
                                    TCPPort:    IPPort.Zero,
                                    AutoStart:  true
                                ) {
                                    AutomaticContentCompression = true
                                };

            var compressingAPI = new HTTPAPI(compressingServer);

            // The handler's own digests, of the identity octets.
            compressingAPI.AddHandler(HTTPPath.Root + "digested",
                                      HTTPMethod:    HTTPMethod.GET,
                                      HTTPDelegate:  request => Task.FromResult(
                                                         new HTTPResponse.Builder(request) {
                                                             HTTPStatusCode  = HTTPStatusCode.OK,
                                                             ContentType     = HTTPContentType.Text.PLAIN,
                                                             Content         = compressibleText.ToUTF8Bytes(),
                                                             ContentDigest   = HTTPDigest.FieldValue(compressibleText.ToUTF8Bytes(), "sha-256") + ", " +
                                                                               HTTPDigest.FieldValue(compressibleText.ToUTF8Bytes(), "sha-512"),
                                                             ReprDigest      = HTTPDigest.FieldValue(compressibleText.ToUTF8Bytes(), "sha-512"),
                                                             Connection      = ConnectionType.Close
                                                         }.AsImmutable));

            // A digest in an algorithm this stack does not compute.
            compressingAPI.AddHandler(HTTPPath.Root + "md5",
                                      HTTPMethod:    HTTPMethod.GET,
                                      HTTPDelegate:  request => Task.FromResult(
                                                         new HTTPResponse.Builder(request) {
                                                             HTTPStatusCode  = HTTPStatusCode.OK,
                                                             ContentType     = HTTPContentType.Text.PLAIN,
                                                             Content         = compressibleText.ToUTF8Bytes(),
                                                             ContentDigest   = $"md5=:{Convert.ToBase64String(System.Security.Cryptography.MD5.HashData(compressibleText.ToUTF8Bytes()))}:",
                                                             Connection      = ConnectionType.Close
                                                         }.AsImmutable));

            // The four fields, both ways: what the request wanted comes back in
            // a header of the response, beside the digests.
            compressingAPI.AddHandler(HTTPPath.Root + "fields",
                                      HTTPMethod:    HTTPMethod.GET,
                                      HTTPDelegate:  request => Task.FromResult(
                                                         new HTTPResponse.Builder(request) {
                                                             HTTPStatusCode  = HTTPStatusCode.OK,
                                                             ContentType     = HTTPContentType.Text.PLAIN,
                                                             Content         = helloWorld,
                                                             ContentDigest   = HTTPDigest.FieldValue(helloWorld, HTTPDigest.SelectAlgorithm(request.WantContentDigest) ?? "sha-256"),
                                                             ReprDigest      = HTTPDigest.FieldValue(helloWorld, HTTPDigest.SelectAlgorithm(request.WantReprDigest)    ?? "sha-256"),
                                                             Connection      = ConnectionType.Close
                                                         }.SetHeaderField("X-Wanted", $"{request.WantContentDigest} | {request.WantReprDigest}").
                                                           AsImmutable));

            // A request's own Content-Digest, checked by the handler — as it
            // arrived, and after the handler undid its content coding.
            compressingAPI.AddHandler(HTTPPath.Root + "check",
                                      HTTPMethod:    HTTPMethod.POST,
                                      HTTPDelegate:  request => Task.FromResult(
                                                         new HTTPResponse.Builder(request) {
                                                             HTTPStatusCode  = HTTPStatusCode.OK,
                                                             ContentType     = HTTPContentType.Text.PLAIN,
                                                             Content         = request.VerifyContentDigest().ToString().ToUTF8Bytes(),
                                                             Connection      = ConnectionType.Close
                                                         }.AsImmutable));

            compressingAPI.AddHandler(HTTPPath.Root + "decoded-then-checked",
                                      HTTPMethod:    HTTPMethod.POST,
                                      HTTPDelegate:  request => {

                                          String verdict;

                                          try
                                          {

                                              // In place, so the body is read first.
                                              _ = request.HTTPBody;

                                              verdict = request.TryDecodeBodyInPlace(out var error)
                                                            ? request.VerifyContentDigest().ToString()
                                                            : $"not decoded: {error}";

                                          }
                                          catch (InvalidOperationException)
                                          {
                                              verdict = "refused";
                                          }

                                          return Task.FromResult(
                                                     new HTTPResponse.Builder(request) {
                                                         HTTPStatusCode  = HTTPStatusCode.OK,
                                                         ContentType     = HTTPContentType.Text.PLAIN,
                                                         Content         = verdict.ToUTF8Bytes(),
                                                         Connection      = ConnectionType.Close
                                                     }.AsImmutable);

                                      });

            #endregion

            #region The digesting server: AutomaticContentCompression and AutomaticContentDigests

            digestingServer = new HTTPServer(
                                  TCPPort:    IPPort.Zero,
                                  AutoStart:  true
                              ) {
                                  AutomaticContentCompression  = true,
                                  AutomaticContentDigests      = true
                              };

            var digestingAPI = new HTTPAPI(digestingServer);

            digestingAPI.AddHandler(HTTPPath.Root + "text",
                                    HTTPMethod:    HTTPMethod.GET,
                                    HTTPDelegate:  request => Task.FromResult(
                                                       new HTTPResponse.Builder(request) {
                                                           HTTPStatusCode  = HTTPStatusCode.OK,
                                                           ContentType     = HTTPContentType.Text.PLAIN,
                                                           Content         = compressibleText.ToUTF8Bytes(),
                                                           Connection      = ConnectionType.Close
                                                       }.AsImmutable));

            digestingAPI.AddHandler(HTTPPath.Root + "text",
                                    HTTPMethod:    HTTPMethod.HEAD,
                                    HTTPDelegate:  request => Task.FromResult(
                                                       new HTTPResponse.Builder(request) {
                                                           HTTPStatusCode  = HTTPStatusCode.OK,
                                                           ContentType     = HTTPContentType.Text.PLAIN,
                                                           Content         = compressibleText.ToUTF8Bytes(),
                                                           Connection      = ConnectionType.Close
                                                       }.AsImmutable));

            digestingAPI.AddHandler(HTTPPath.Root + "nothing",
                                    HTTPMethod:    HTTPMethod.GET,
                                    HTTPDelegate:  request => Task.FromResult(
                                                       new HTTPResponse.Builder(request) {
                                                           HTTPStatusCode  = HTTPStatusCode.NoContent,
                                                           Connection      = ConnectionType.Close
                                                       }.AsImmutable));

            digestingAPI.AddHandler(HTTPPath.Root + "own",
                                    HTTPMethod:    HTTPMethod.GET,
                                    HTTPDelegate:  request => Task.FromResult(
                                                       new HTTPResponse.Builder(request) {
                                                           HTTPStatusCode  = HTTPStatusCode.OK,
                                                           ContentType     = HTTPContentType.Text.PLAIN,
                                                           Content         = helloWorld,
                                                           ContentDigest   = HTTPDigest.FieldValue(helloWorld, "sha-512"),
                                                           Connection      = ConnectionType.Close
                                                       }.AsImmutable));

            #endregion

        }

        [OneTimeTearDown]
        public async Task Shutdown()
        {

            if (compressingServer is not null)
                await compressingServer.DisposeAsync();

            if (digestingServer is not null)
                await digestingServer.DisposeAsync();

        }

        #endregion

        #region (private) Get(Server, Path, AcceptEncoding = null, Want = null, Head = false)

        /// <summary>
        /// A client that neither decodes nor verifies, so that the assertions are
        /// about the wire.
        /// </summary>
        private static async Task<HTTPResponse> Get(HTTPServer  Server,
                                                    String      Path,
                                                    String?     AcceptEncoding   = null,
                                                    String?     Want             = null,
                                                    Boolean     Head             = false)
        {

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{Server.TCPPort}"));

            return await client.RunRequest(
                             Head ? HTTPMethod.HEAD : HTTPMethod.GET,
                             HTTPPath.Parse(Path),
                             RequestBuilder:  builder => {
                                                  if (AcceptEncoding is not null) builder.AcceptEncoding     = AcceptEncoding;
                                                  if (Want           is not null) builder.WantContentDigest  = Want;
                                              },
                             RequestTimeout:  TimeSpan.FromSeconds(10)
                         );

        }

        #endregion

        #region (private) Raw(Header, Body = null)

        private static Byte[] Raw(String   Header,
                                  Byte[]?  Body   = null)
        {

            var header = Encoding.ASCII.GetBytes(Header);

            return Body is null
                       ? header
                       : [.. header, .. Body];

        }

        #endregion


        #region The grammar (RFC 9651)

        #region AFieldThatDoesNotParseIsIgnoredWhole()

        /// <summary>
        /// RFC 9651, Section 4.2: a field that fails to parse is ignored as a
        /// whole, and the fields built on it may not loosen that. The grammar
        /// this was moved from kept the members it could read, so the md5 junk
        /// below was dropped and the sha-256 beside it verified the content —
        /// a Match on a field the RFC says is not there.
        /// </summary>
        [Test]
        public void AFieldThatDoesNotParseIsIgnoredWhole()
        {

            var field = "md5=:not-base64!:, " + HTTPDigest.FieldValue(helloWorld, "sha-256");

            Assert.Multiple(() => {
                Assert.That(HTTPDigest.Parse (field),              Is.Empty);
                Assert.That(HTTPDigest.Verify(field, helloWorld),  Is.EqualTo(HTTPDigestVerification.Unsupported));
            });

        }

        #endregion

        #region AMemberThatIsNotAByteSequenceVoidsTheField()

        /// <summary>
        /// RFC 9651, Section 2.2: a field that parses but breaks the constraints
        /// of its own definition is ignored the same way, and RFC 9530 has every
        /// member be a Byte Sequence. "?1" is a Boolean.
        /// </summary>
        [Test]
        public void AMemberThatIsNotAByteSequenceVoidsTheField()
        {

            var field = HTTPDigest.FieldValue(helloWorld, "sha-256") + ", sha-512=?1";

            Assert.That(HTTPDigest.Parse(field), Is.Empty);

        }

        #endregion

        #region AnUpperCaseKeyIsNotAKey()

        /// <summary>
        /// A Dictionary key is lower case (RFC 9651, Section 3.2). The grammar this
        /// was moved from lowered it and accepted the member.
        /// </summary>
        [Test]
        public void AnUpperCaseKeyIsNotAKey()
        {

            var field = HTTPDigest.FieldValue(helloWorld, "sha-256").Replace("sha-256", "SHA-256");

            Assert.That(HTTPDigest.Parse(field), Is.Empty);

        }

        #endregion

        #region ADigestWithoutPaddingIsADigest()

        /// <summary>
        /// RFC 9651, Section 4.2.7: parsers "SHOULD NOT fail when '=' padding is
        /// not present". A sha-256 digest is 32 octets, which base64 ends with
        /// one '='.
        /// </summary>
        [Test]
        public void ADigestWithoutPaddingIsADigest()
        {

            var field = $"sha-256=:{Convert.ToBase64String(HTTPDigest.Compute(helloWorld, "sha-256")).TrimEnd('=')}:";

            Assert.That(field, Does.Not.EndWith("=:"));
            Assert.That(HTTPDigest.Verify(field, helloWorld), Is.EqualTo(HTTPDigestVerification.Match));

        }

        #endregion

        #region APreferenceOutsideTheRangeVoidsTheWantField()

        /// <summary>
        /// RFC 9530, Section 4: a preference is an Integer from 0 to 10. The
        /// grammar this was moved from clamped an 11 to a 10, so that the field
        /// below — whose sha-256 is unacceptable and whose sha-512 is no valid
        /// preference at all — chose sha-512. Ignored whole, it states no
        /// preference, and that is sha-256. A bare key is a Boolean and no
        /// preference either.
        /// </summary>
        [Test]
        public void APreferenceOutsideTheRangeVoidsTheWantField()
        {

            Assert.Multiple(() => {
                Assert.That(HTTPDigest.ParseWant      ("sha-256=0, sha-512=11"),  Is.Empty);
                Assert.That(HTTPDigest.SelectAlgorithm("sha-256=0, sha-512=11"),  Is.EqualTo("sha-256"));
                Assert.That(HTTPDigest.SelectAlgorithm("sha-256=0, sha-512"),     Is.EqualTo("sha-256"));
                Assert.That(HTTPDigest.SelectAlgorithm("sha-256=0, sha-512=1"),   Is.EqualTo("sha-512"));
            });

        }

        #endregion

        #endregion

        #region The fields on HTTP/1.1

        #region TheFourFieldsTravelBothWays()

        [Test]
        public async Task TheFourFieldsTravelBothWays()
        {

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{compressingServer!.TCPPort}"));

            var response = await client.GET(
                                     HTTPPath.Root + "fields",
                                     RequestBuilder:  builder => {
                                                          builder.WantContentDigest  = "sha-512=10, sha-256=1";
                                                          builder.WantReprDigest     = "sha-256=3";
                                                      },
                                     RequestTimeout:  TimeSpan.FromSeconds(10)
                                 );

            Assert.Multiple(() => {
                Assert.That(response.GetHeaderField("X-Wanted"),  Is.EqualTo("sha-512=10, sha-256=1 | sha-256=3"));
                Assert.That(response.ContentDigest,               Is.EqualTo(HTTPDigest.FieldValue(helloWorld, "sha-512")));
                Assert.That(response.ReprDigest,                  Is.EqualTo(HTTPDigest.FieldValue(helloWorld, "sha-256")));
                Assert.That(response.VerifyContentDigest(),       Is.EqualTo(HTTPDigestVerification.Match));
            });

        }

        #endregion

        #region RepeatedFieldLinesAreOneDictionary()

        /// <summary>
        /// RFC 9651, Section 4.2: the field lines of a Dictionary are combined
        /// before it is parsed. A Content-Digest split over two lines is two
        /// claims, and the false one in the second line is found.
        /// </summary>
        [Test]
        public async Task RepeatedFieldLinesAreOneDictionary()
        {

            using var server = new RawServer(
                                   Raw("HTTP/1.1 200 OK\r\n" +
                                       "Content-Length: 12\r\n" +
                                      $"Content-Digest: {HTTPDigest.FieldValue(helloWorld, "sha-256")}\r\n" +
                                      $"Content-Digest: {HTTPDigest.FieldValue("something else"u8.ToArray(), "sha-512")}\r\n" +
                                       "Connection: close\r\n\r\n",
                                       helloWorld)
                               );

            using var client = new HTTPClient(server.URL, MaxNumberOfRetries: 1);

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(HTTPDigest.Parse(response.ContentDigest!).Keys,  Is.EquivalentTo(new[] { "sha-256", "sha-512" }));
                Assert.That(response.VerifyContentDigest(),                  Is.EqualTo(HTTPDigestVerification.Mismatch));
            });

        }

        #endregion

        #region ARequestsDigestIsCheckedAsItArrived()

        /// <summary>
        /// The request direction: a handler checks a gzipped request body against
        /// its Content-Digest, and has to do it before it undoes the coding.
        /// Afterwards the check is refused rather than reported as a mismatch the
        /// client did not cause.
        /// </summary>
        [Test]
        public async Task ARequestsDigestIsCheckedAsItArrived()
        {

            using var client  = new HTTPClient(URL.Parse($"http://127.0.0.1:{compressingServer!.TCPPort}"));

            var gzipped       = HTTPContentCoding.Encode(helloWorld, "gzip");

            async Task<String> Post(String Path, Byte[] Digested)
                => (await client.POST(
                              HTTPPath.Parse(Path),
                              gzipped,
                              HTTPContentType.Text.PLAIN,
                              RequestBuilder:  builder => {
                                                   builder.ContentEncoding  = [ "gzip" ];
                                                   builder.ContentDigest    = HTTPDigest.FieldValue(Digested, "sha-256");
                                               },
                              RequestTimeout:  TimeSpan.FromSeconds(10)
                          )).HTTPBodyAsUTF8String ?? "";

            var asItArrived     = await Post("/check",                gzipped);
            var ofTheIdentity   = await Post("/check",                helloWorld);
            var afterDecoding   = await Post("/decoded-then-checked", gzipped);

            Assert.Multiple(() => {
                Assert.That(asItArrived,    Is.EqualTo(nameof(HTTPDigestVerification.Match)));
                Assert.That(ofTheIdentity,  Is.EqualTo(nameof(HTTPDigestVerification.Mismatch)), "the identity octets are not what travelled");
                Assert.That(afterDecoding,  Is.EqualTo("refused"));
            });

        }

        #endregion

        #endregion

        #region The compression filter

        #region CompressingComputesTheHandlersDigestsAgain()

        /// <summary>
        /// The handler digested the identity octets; the filter then gzipped them.
        /// It used to copy both fields on verbatim, onto a response whose octets
        /// they no longer described — the integrity check failing on content
        /// nobody had corrupted. Both are computed again, in each algorithm the
        /// handler used.
        /// </summary>
        [Test]
        public async Task CompressingComputesTheHandlersDigestsAgain()
        {

            var response = await Get(compressingServer!, "/digested", AcceptEncoding: "gzip");

            Assert.Multiple(() => {
                Assert.That(response.ContentEncoding,                                 Is.EqualTo(new[] { "gzip" }), response.EntirePDU);
                Assert.That(HTTPDigest.Parse(response.ContentDigest!).Keys,           Is.EqualTo(new[] { "sha-256", "sha-512" }));
                Assert.That(HTTPDigest.Verify(response.ContentDigest, response.HTTPBody!), Is.EqualTo(HTTPDigestVerification.Match));
                Assert.That(HTTPDigest.Verify(response.ReprDigest,    response.HTTPBody!), Is.EqualTo(HTTPDigestVerification.Match));
            });

        }

        #endregion

        #region ADigestThatCannotBeComputedAgainIsNotCompressed()

        /// <summary>
        /// md5 is the handler's, and not computed here (RFC 9530, Section 5). The
        /// filter can neither keep it true over gzip nor drop what the handler
        /// said, so it does not compress.
        /// </summary>
        [Test]
        public async Task ADigestThatCannotBeComputedAgainIsNotCompressed()
        {

            var response = await Get(compressingServer!, "/md5", AcceptEncoding: "gzip");

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,   Is.EqualTo(HTTPStatusCode.OK), response.EntirePDU);
                Assert.That(response.ContentEncoding,  Is.Empty);
                Assert.That(response.HTTPBody,         Is.EqualTo(compressibleText.ToUTF8Bytes()));
                Assert.That(response.ContentDigest,    Does.StartWith("md5=:"));
            });

        }

        #endregion

        #endregion

        #region The server's own digests

        #region TheServerDigestsTheOctetsItSends()

        /// <summary>
        /// Of the octets as sent: a digest of the identity octets on a gzipped
        /// response is one no client can reproduce. Which of the two filters runs
        /// first does not decide that, since the compression computes a digest it
        /// finds again; reversing them leaves this green.
        /// </summary>
        [Test]
        public async Task TheServerDigestsTheOctetsItSends()
        {

            var plain    = await Get(digestingServer!, "/text");
            var gzipped  = await Get(digestingServer!, "/text", AcceptEncoding: "gzip");

            Assert.Multiple(() => {
                Assert.That(plain.  ContentDigest,                                    Is.EqualTo(HTTPDigest.FieldValue(compressibleText.ToUTF8Bytes(), "sha-256")));
                Assert.That(gzipped.ContentEncoding,                                  Is.EqualTo(new[] { "gzip" }), gzipped.EntirePDU);
                Assert.That(HTTPDigest.Verify(gzipped.ContentDigest, gzipped.HTTPBody!), Is.EqualTo(HTTPDigestVerification.Match));
            });

        }

        #endregion

        #region TheServerAnswersWhatIsWanted()

        [Test]
        public async Task TheServerAnswersWhatIsWanted()
        {

            var sha512  = await Get(digestingServer!, "/text", Want: "sha-256=1, sha-512=9");
            var none    = await Get(digestingServer!, "/text", Want: "sha-256=0, sha-512=0");

            Assert.Multiple(() => {
                Assert.That(sha512.ContentDigest,  Is.EqualTo(HTTPDigest.FieldValue(compressibleText.ToUTF8Bytes(), "sha-512")));
                Assert.That(none.  ContentDigest,  Is.Null, "every algorithm ruled out: no digest rather than a declined one");
            });

        }

        #endregion

        #region WithoutContentThereIsNoDigest()

        /// <summary>
        /// A HEAD and a 204 carry no content, so there are no octets for a
        /// Content-Digest to be of.
        /// </summary>
        [Test]
        public async Task WithoutContentThereIsNoDigest()
        {

            var head       = await Get(digestingServer!, "/text", Head: true);
            var noContent  = await Get(digestingServer!, "/nothing");

            Assert.Multiple(() => {
                Assert.That(head.     HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(head.     ContentDigest,   Is.Null);
                Assert.That(noContent.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.NoContent));
                Assert.That(noContent.ContentDigest,   Is.Null);
            });

        }

        #endregion

        #region AHandlersOwnDigestIsKept()

        [Test]
        public async Task AHandlersOwnDigestIsKept()
        {

            var response = await Get(digestingServer!, "/own");

            Assert.That(response.ContentDigest, Is.EqualTo(HTTPDigest.FieldValue(helloWorld, "sha-512")));

        }

        #endregion

        #endregion

        #region The client's VerifyDigests

        #region TheClientAsksForADigestOnlyWhenTold()

        [Test]
        public async Task TheClientAsksForADigestOnlyWhenTold()
        {

            var ok = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK";

            using var server = new RawServer(Raw(ok), Raw(ok));

            using (var verifying = new HTTPClient(server.URL, MaxNumberOfRetries: 1) { VerifyDigests = true })
                await verifying.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            using (var plain     = new HTTPClient(server.URL, MaxNumberOfRetries: 1))
                await plain.    GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            await server.Served.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(server.Requests[0], Does.Contain($"Want-Content-Digest: {HTTPDigest.Want}\r\n"), server.Requests[0]);
                Assert.That(server.Requests[1], Does.Not.Contain("Want-Content-Digest"),                    server.Requests[1]);
            });

        }

        #endregion

        #region AMatchIsReportedAndNothingElseIsAMatch()

        /// <summary>
        /// A digest that matched, none, and one in an algorithm not computed here
        /// are three different answers, and only the first is a verification.
        /// </summary>
        [TestCase("sha-256", nameof(HTTPDigestVerification.Match))]
        [TestCase(null,      nameof(HTTPDigestVerification.NotPresent))]
        [TestCase("md5",     nameof(HTTPDigestVerification.Unsupported))]
        public async Task AMatchIsReportedAndNothingElseIsAMatch(String? Algorithm, String Expected)
        {

            var digest = Algorithm switch {
                             "sha-256"  => $"Content-Digest: {HTTPDigest.FieldValue(helloWorld, "sha-256")}\r\n",
                             "md5"      => $"Content-Digest: md5=:{Convert.ToBase64String(System.Security.Cryptography.MD5.HashData(helloWorld))}:\r\n",
                             _          => ""
                         };

            using var server = new RawServer(
                                   Raw($"HTTP/1.1 200 OK\r\nContent-Length: 12\r\n{digest}Connection: close\r\n\r\n", helloWorld)
                               );

            using var client = new HTTPClient(server.URL, MaxNumberOfRetries: 1) { VerifyDigests = true };

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,                Is.EqualTo(HTTPStatusCode.OK), response.EntirePDU);
                Assert.That(response.DigestVerification.ToString(), Is.EqualTo(Expected));
                Assert.That(response.HTTPBody,                      Is.EqualTo(helloWorld));
            });

        }

        #endregion

        #region AMismatchIsAClientErrorAndNotARetry()

        /// <summary>
        /// Content that disagrees with its digest is not handed to a caller who
        /// asked for it to be checked. The framing was sound, so the request is
        /// not sent again — one request, and the answer says why.
        /// </summary>
        [Test]
        public async Task AMismatchIsAClientErrorAndNotARetry()
        {

            using var server = new RawServer(
                                   Raw("HTTP/1.1 200 OK\r\nContent-Length: 12\r\n" +
                                      $"Content-Digest: {HTTPDigest.FieldValue("Hello World?"u8.ToArray(), "sha-256")}\r\n" +
                                       "Connection: close\r\n\r\n",
                                       helloWorld),
                                   Raw("HTTP/1.1 200 OK\r\nContent-Length: 12\r\nConnection: close\r\n\r\n", helloWorld)
                               );

            using var client = new HTTPClient(server.URL, MaxNumberOfRetries: 3) { VerifyDigests = true };

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,      Is.EqualTo(HTTPStatusCode.ClientError), response.EntirePDU);
                Assert.That(response.DigestVerification,  Is.EqualTo(HTTPDigestVerification.Mismatch));
                Assert.That(response.HTTPBodyAsUTF8String, Does.Contain("Content-Digest"));
                Assert.That(server.Requests,              Has.Count.EqualTo(1));
            });

        }

        #endregion

        #region TheDigestIsCheckedBeforeDecoding()

        /// <summary>
        /// The digest is of the gzip octets; AutomaticDecompression hands the
        /// caller the identity ones. Checked in the order the RFC requires, the
        /// response is a Match and arrives decoded. Without VerifyDigests the
        /// decoder sits in the pipeline, and the octets the digest is of are never
        /// seen at all.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task TheDigestIsCheckedBeforeDecoding(Boolean Chunked)
        {

            var gzipped  = HTTPContentCoding.Encode(compressibleText.ToUTF8Bytes(), "gzip");
            var digest   = HTTPDigest.FieldValue(gzipped, "sha-256");

            using var server = new RawServer(
                                   Chunked
                                       ? Raw("HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nTransfer-Encoding: chunked\r\n" +
                                            $"Content-Digest: {digest}\r\nConnection: close\r\n\r\n" +
                                            $"{gzipped.Length:x}\r\n",
                                             [.. gzipped, .. "\r\n0\r\n\r\n"u8.ToArray()])
                                       : Raw("HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\n" +
                                            $"Content-Length: {gzipped.Length}\r\nContent-Digest: {digest}\r\nConnection: close\r\n\r\n",
                                             gzipped)
                               );

            using var client = new HTTPClient(server.URL, MaxNumberOfRetries: 1) {
                                   AutomaticDecompression  = true,
                                   VerifyDigests           = true
                               };

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,          Is.EqualTo(HTTPStatusCode.OK), response.EntirePDU);
                Assert.That(response.DigestVerification,      Is.EqualTo(HTTPDigestVerification.Match));
                Assert.That(response.DecodedContentEncoding,  Is.EqualTo("gzip"));
                Assert.That(response.HTTPBodyAsUTF8String,    Is.EqualTo(compressibleText));
            });

        }

        #endregion

        #region ADigestInTheTrailerIsChecked()

        /// <summary>
        /// RFC 9530, Section 2: Content-Digest can be sent in the trailer section
        /// — which is where a sender streaming its content knows it.
        /// </summary>
        [TestCase("Hello World!", nameof(HTTPDigestVerification.Match))]
        [TestCase("Hello World?", nameof(HTTPDigestVerification.Mismatch))]
        public async Task ADigestInTheTrailerIsChecked(String Digested, String Expected)
        {

            using var server = new RawServer(
                                   Raw("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nTrailer: Content-Digest\r\nConnection: close\r\n\r\n" +
                                       "c\r\nHello World!\r\n0\r\n" +
                                      $"Content-Digest: {HTTPDigest.FieldValue(Digested.ToUTF8Bytes(), "sha-256")}\r\n\r\n")
                               );

            using var client = new HTTPClient(server.URL, MaxNumberOfRetries: 1) { VerifyDigests = true };

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.That(response.DigestVerification.ToString(), Is.EqualTo(Expected), response.EntirePDU);

        }

        #endregion

        #region ATrailerOnTheStreamedPathIsNotSeen()

        /// <summary>
        /// The limit of the above, pinned so that it is not mistaken for a check:
        /// a chunked body the client does not consume on arrival is read as a
        /// stream, and that path keeps no trailer section. So the digest in it is
        /// not there, and a false one is not found either.
        /// </summary>
        [Test]
        public async Task ATrailerOnTheStreamedPathIsNotSeen()
        {

            using var server = new RawServer(
                                   Raw("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nTrailer: Content-Digest\r\nConnection: close\r\n\r\n" +
                                       "c\r\nHello World!\r\n0\r\n" +
                                      $"Content-Digest: {HTTPDigest.FieldValue("Hello World?"u8.ToArray(), "sha-256")}\r\n\r\n")
                               );

            using var client = new HTTPClient(server.URL, MaxNumberOfRetries: 1) { VerifyDigests = true };

            var response = await client.GET(HTTPPath.Root,
                                            ConsumeResponseChunkedTEImmediately:  false,
                                            RequestTimeout:                       TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,      Is.EqualTo(HTTPStatusCode.OK), response.EntirePDU);
                Assert.That(response.HTTPBody,            Is.EqualTo(helloWorld));
                Assert.That(response.DigestVerification,  Is.EqualTo(HTTPDigestVerification.NotPresent));
            });

        }

        #endregion

        #region WithoutTheOptInNothingIsChecked()

        /// <summary>
        /// The opt-in from the other end: a false digest is the caller's to look
        /// at, and the response is delivered as it came.
        /// </summary>
        [Test]
        public async Task WithoutTheOptInNothingIsChecked()
        {

            using var server = new RawServer(
                                   Raw("HTTP/1.1 200 OK\r\nContent-Length: 12\r\n" +
                                      $"Content-Digest: {HTTPDigest.FieldValue("Hello World?"u8.ToArray(), "sha-256")}\r\n" +
                                       "Connection: close\r\n\r\n",
                                       helloWorld)
                               );

            using var client = new HTTPClient(server.URL, MaxNumberOfRetries: 1);

            var response = await client.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            Assert.Multiple(() => {
                Assert.That(response.HTTPStatusCode,         Is.EqualTo(HTTPStatusCode.OK));
                Assert.That(response.DigestVerification,     Is.EqualTo(HTTPDigestVerification.NotPresent));
                Assert.That(response.VerifyContentDigest(),  Is.EqualTo(HTTPDigestVerification.Mismatch));
            });

        }

        #endregion

        #endregion

    }

}
