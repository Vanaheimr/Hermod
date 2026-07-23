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

using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// RFC 9110 Section 11 authentication framework (401 / WWW-Authenticate /
    /// Authorization) with Basic (RFC 7617), Bearer (RFC 6750), Token
    /// (non-standard) and Digest (RFC 7616), plus transport-layer mutual TLS —
    /// driven by our own client and .NET HttpClient. In-process.
    /// </summary>
    [TestFixture]
    public class AuthenticationTests
    {

        #region (shared) authenticated handler for Basic + Bearer + Token

        private static HTTP2RequestHandler SecretHandler()
        {
            var authenticator = new HTTPAuthenticator("demo",
                new BasicAuthenticationScheme((u, p, _) => Task.FromResult<HTTPAuthenticatedIdentity?>(
                    u == "alice" && p == "secret" ? new HTTPAuthenticatedIdentity { Name = "alice" } : null)),
                new BearerAuthenticationScheme((t, _) => Task.FromResult<HTTPAuthenticatedIdentity?>(
                    t == "valid-token-123" ? new HTTPAuthenticatedIdentity { Name = "token-user" } : null)),
                new TokenAuthenticationScheme((t, parameters, _) => Task.FromResult<HTTPAuthenticatedIdentity?>(
                    t == "secret-token-abc" ? new HTTPAuthenticatedIdentity { Name = "api-user" } : null)));

            return HTTPAuthentication.RequireAuthentication(authenticator,
                (identity, sid, h, b, ct) =>
                {
                    var body = Encoding.UTF8.GetBytes($"Authenticated as: {identity.Name}");
                    return Task.FromResult<(List<(String, String)>, Byte[]?)>(
                        ([(":status", "200"), ("content-type", "text/plain"), ("content-length", body.Length.ToString())], body));
                });
        }

        #endregion


        #region Framework_BasicBearerToken()

        [Test]
        public async Task Framework_BasicBearerToken()
        {

            await using var srv = await TestH2Server.StartAsync(SecretHandler());

            using var http = H2.MakeHttpClient();
            var baseUri = $"https://127.0.0.1:{srv.Port}/secret";

            async Task<HttpResponseMessage> Send(AuthenticationHeaderValue? auth)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, baseUri)
                {
                    Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionExact
                };
                if (auth is not null) req.Headers.Authorization = auth;
                return await http.SendAsync(req);
            }

            // No credentials -> 401 + WWW-Authenticate for all three schemes.
            var anon       = await Send(null);
            var challenges = anon.Headers.WwwAuthenticate.Select(h => h.Scheme).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(anon.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "no creds -> 401");
                Assert.That(challenges, Is.SupersetOf(new[] { "Basic", "Bearer", "Token" }),
                            "challenge advertises Basic + Bearer + Token");
            });

            Byte[] B64(String s) => Encoding.UTF8.GetBytes(s);

            var basicOk = await Send(new AuthenticationHeaderValue("Basic", Convert.ToBase64String(B64("alice:secret"))));
            Assert.Multiple(() =>
            {
                Assert.That(basicOk.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Basic alice:secret -> 200");
                Assert.That(basicOk.Content.ReadAsStringAsync().Result, Is.EqualTo("Authenticated as: alice"), "identity surfaced");
            });

            Assert.That((await Send(new AuthenticationHeaderValue("Basic", Convert.ToBase64String(B64("alice:wrong"))))).StatusCode,
                        Is.EqualTo(HttpStatusCode.Unauthorized), "Basic wrong password -> 401");
            Assert.That((await Send(new AuthenticationHeaderValue("Basic", "not-base64!!"))).StatusCode,
                        Is.EqualTo(HttpStatusCode.Unauthorized), "Basic malformed -> 401");

            var bearerOk = await Send(new AuthenticationHeaderValue("Bearer", "valid-token-123"));
            Assert.Multiple(() =>
            {
                Assert.That(bearerOk.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Bearer valid -> 200");
                Assert.That(bearerOk.Content.ReadAsStringAsync().Result, Is.EqualTo("Authenticated as: token-user"), "bearer identity surfaced");
            });

            Assert.That((await Send(new AuthenticationHeaderValue("Bearer", "nope"))).StatusCode,   Is.EqualTo(HttpStatusCode.Unauthorized), "Bearer invalid -> 401");
            Assert.That((await Send(new AuthenticationHeaderValue("Digest", "whatever"))).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "unsupported scheme -> 401");

            var tokenBare = await Send(new AuthenticationHeaderValue("Token", "secret-token-abc"));
            Assert.Multiple(() =>
            {
                Assert.That(tokenBare.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Token bare form -> 200");
                Assert.That(tokenBare.Content.ReadAsStringAsync().Result, Is.EqualTo("Authenticated as: api-user"), "token identity surfaced");
            });

            Assert.That((await Send(new AuthenticationHeaderValue("Token", "token=\"secret-token-abc\", nonce=\"xyz\""))).StatusCode,
                        Is.EqualTo(HttpStatusCode.OK), "Token parameterized form -> 200");
            Assert.That((await Send(new AuthenticationHeaderValue("Token", "wrong-token"))).StatusCode,
                        Is.EqualTo(HttpStatusCode.Unauthorized), "Token invalid -> 401");

            // Same, via OUR client — proves the framework isn't HttpClient-specific.
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var basicViaOurClient = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/secret",
                ExtraHeaders: [("authorization", "Basic " + Convert.ToBase64String(B64("alice:secret")))]);
            var tokenViaOurClient = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/secret",
                ExtraHeaders: [("authorization", "Token secret-token-abc")]);
            var anonViaOurClient  = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/secret");
            Assert.Multiple(() =>
            {
                Assert.That(basicViaOurClient.Status, Is.EqualTo(200), "our client + Basic -> 200");
                Assert.That(tokenViaOurClient.Status, Is.EqualTo(200), "our client + Token -> 200");
                Assert.That(anonViaOurClient.Status,  Is.EqualTo(401), "our client, no creds -> 401");
            });
            await conn.CloseAsync();

        }

        #endregion

        #region MutualTLS()

        [Test]
        public async Task MutualTLS()
        {

            var serverCert = H2.MakeCert("localhost");
            var clientCert = H2.MakeCert("test-client", ClientAuth: true);

            // mTLS handler echoes the surfaced client-cert subject.
            Task<(List<(String, String)>, Byte[]?)> Handle(UInt32 sid, List<(String Name, String Value)> h, Byte[]? b, CancellationToken ct)
            {
                var subject = h.FirstOrDefault(x => x.Name == "x-client-cert-subject").Value ?? "(none)";
                var body    = Encoding.UTF8.GetBytes(subject);
                return Task.FromResult<(List<(String, String)>, Byte[]?)>(
                    ([(":status", "200"), ("content-length", body.Length.ToString())], body));
            }

            RemoteCertificateValidationCallback requireCert = (_, cert, _, _) => cert is not null;

            await using var srv = await TestH2Server.StartAsync(Handle,
                Certificate: serverCert, RequireClientCertificate: true, ValidateClientCertificate: requireCert);

            // HttpClient WITH a client cert -> ok, subject surfaced.
            using (var http = H2.MakeHttpClient(clientCert))
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"https://127.0.0.1:{srv.Port}/")
                {
                    Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionExact
                };
                var resp    = await http.SendAsync(req);
                var subject = await resp.Content.ReadAsStringAsync();
                Assert.Multiple(() =>
                {
                    Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "HttpClient + client cert -> 200");
                    Assert.That(subject, Does.Contain("test-client"), "x-client-cert-subject surfaced");
                });
            }

            // HttpClient WITHOUT a client cert -> handshake fails.
            using (var http = H2.MakeHttpClient())
            {
                var threw = false;
                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, $"https://127.0.0.1:{srv.Port}/")
                    {
                        Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionExact
                    };
                    await http.SendAsync(req);
                }
                catch { threw = true; }
                Assert.That(threw, Is.True, "HttpClient without client cert -> rejected");
            }

            // OUR client WITH a client cert -> ok.
            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert, ClientCertificate: clientCert);
            var ok = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/");
            Assert.Multiple(() =>
            {
                Assert.That(ok.Status, Is.EqualTo(200), "our client + client cert -> 200");
                Assert.That(Encoding.UTF8.GetString(ok.Body), Does.Contain("test-client"), "our client sees cert subject");
            });
            await conn.CloseAsync();

            // OUR client WITHOUT a client cert -> fails.
            var ourThrew = false;
            try { await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert); }
            catch { ourThrew = true; }
            Assert.That(ourThrew, Is.True, "our client without cert -> rejected");

        }

        #endregion

        #region Digest()

        [Test]
        public async Task Digest()
        {

            var digestAuth = new HTTPAuthenticator("demo",
                new DigestAuthenticationScheme("demo",
                    (u, _) => Task.FromResult<String?>(u == "alice" ? "secret" : null)));
            var digestHandler = HTTPAuthentication.RequireAuthentication(digestAuth,
                (identity, sid, h, b, ct) =>
                {
                    var body = Encoding.UTF8.GetBytes($"Digest as: {identity.Name}");
                    return Task.FromResult<(List<(String, String)>, Byte[]?)>(
                        ([(":status", "200"), ("content-type", "text/plain"), ("content-length", body.Length.ToString())], body));
                });

            await using var srv = await TestH2Server.StartAsync(digestHandler);

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);

            // 1. No credentials -> 401 with a Digest challenge.
            var anon      = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/digest");
            var challenge = anon.Headers.FirstOrDefault(h => h.Name == "www-authenticate").Value ?? "";
            Assert.Multiple(() =>
            {
                Assert.That(anon.Status, Is.EqualTo(401), "Digest: no creds -> 401");
                Assert.That(challenge.StartsWith("Digest") && challenge.Contains("realm=\"demo\"") &&
                            challenge.Contains("nonce=") && challenge.Contains("qop=\"auth\"") && challenge.Contains("algorithm=SHA-256"),
                            Is.True, "Digest: challenge has realm + nonce + qop=auth + algorithm");
            });

            // 2. Correct SHA-256 response -> 200 (the password never crossed the wire).
            var okResp = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/digest",
                             ExtraHeaders: [("authorization", BuildAuth(challenge, "alice", "secret", "GET", "/digest"))]);
            Assert.Multiple(() =>
            {
                Assert.That(okResp.Status, Is.EqualTo(200), "Digest: valid response -> 200");
                Assert.That(Encoding.UTF8.GetString(okResp.Body), Is.EqualTo("Digest as: alice"), "Digest: identity surfaced");
            });

            // 3-6. Failure modes -> 401.
            var wrongPw = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/digest",
                              ExtraHeaders: [("authorization", BuildAuth(challenge, "alice", "wrong", "GET", "/digest"))]);
            var unknownUser = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/digest",
                                  ExtraHeaders: [("authorization", BuildAuth(challenge, "bob", "secret", "GET", "/digest"))]);
            var forgedNonce = Convert.ToBase64String(Encoding.ASCII.GetBytes("638000000000000000:bogusmac"));
            var badNonce = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/digest",
                               ExtraHeaders: [("authorization", BuildAuth(challenge, "alice", "secret", "GET", "/digest", nonceOverride: forgedNonce))]);
            var wrongUri = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/digest",
                               ExtraHeaders: [("authorization", BuildAuth(challenge, "alice", "secret", "GET", "/elsewhere"))]);
            Assert.Multiple(() =>
            {
                Assert.That(wrongPw.Status,     Is.EqualTo(401), "Digest: wrong password -> 401");
                Assert.That(unknownUser.Status, Is.EqualTo(401), "Digest: unknown user -> 401");
                Assert.That(badNonce.Status,    Is.EqualTo(401), "Digest: forged nonce -> 401");
                Assert.That(wrongUri.Status,    Is.EqualTo(401), "Digest: uri mismatch -> 401");
            });

            await conn.CloseAsync();

            // The production peer: .NET HttpClient does the whole Digest dance itself.
            var credCache = new CredentialCache { { new Uri($"https://127.0.0.1:{srv.Port}/"), "Digest", new NetworkCredential("alice", "secret") } };
            var handler = new SocketsHttpHandler
            {
                Credentials = credCache,
                SslOptions  = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true }
            };
            using var httpDigest = new HttpClient(handler)
            {
                DefaultRequestVersion = HttpVersion.Version20, DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
                Timeout = TimeSpan.FromSeconds(8)
            };
            var dReq = new HttpRequestMessage(HttpMethod.Get, $"https://127.0.0.1:{srv.Port}/digest")
            {
                Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            var dResp = await httpDigest.SendAsync(dReq);
            Assert.Multiple(() =>
            {
                Assert.That(dResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "HttpClient Digest (CredentialCache) -> 200");
                Assert.That(dResp.Content.ReadAsStringAsync().Result, Is.EqualTo("Digest as: alice"), "HttpClient Digest identity surfaced");
            });

        }

        #endregion

        #region DigestNonceExpiry_FakeClock()

        // The Digest scheme issues nonces stamped with its injected TimeProvider
        // and rejects them once they are older than NonceMaxAge. With a fake
        // clock this is deterministic: no Task.Delay, no real waiting.
        [Test]
        public async Task DigestNonceExpiry_FakeClock()
        {

            var clock   = new FakeClock(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
            var scheme  = new DigestAuthenticationScheme(
                              "demo",
                              (user, ct) => Task.FromResult<String?>(user == "alice" ? "secret" : null),
                              NonceMaxAge:  TimeSpan.FromMinutes(5),
                              TimeProvider: clock);

            var challenge = scheme.BuildChallenge("demo");
            var auth      = BuildAuth(challenge, "alice", "secret", "GET", "/digest")["Digest ".Length..];

            // Fresh nonce -> authenticated.
            var fresh = await scheme.AuthenticateAsync(auth, "GET", "/digest", CancellationToken.None);
            Assert.That(fresh?.Name, Is.EqualTo("alice"), "fresh nonce -> authenticated");

            // Just inside NonceMaxAge -> still valid (the nonce itself is stateless).
            clock.UtcNow += TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59);
            Assert.That(await scheme.AuthenticateAsync(auth, "GET", "/digest", CancellationToken.None),
                        Is.Not.Null, "4:59 old nonce -> still valid");

            // Beyond NonceMaxAge -> stale, rejected.
            clock.UtcNow += TimeSpan.FromSeconds(2);
            Assert.That(await scheme.AuthenticateAsync(auth, "GET", "/digest", CancellationToken.None),
                        Is.Null, "expired nonce -> rejected");

        }

        /// <summary>
        /// A minimal manually-advanced clock: only GetUtcNow() is overridden,
        /// which is all the Digest scheme consumes.
        /// </summary>
        private sealed class FakeClock(DateTimeOffset InitialTime) : TimeProvider
        {

            public DateTimeOffset UtcNow { get; set; } = InitialTime;

            public override DateTimeOffset GetUtcNow()
                => UtcNow;

        }

        #endregion

        #region (helpers) hand-rolled Digest response computation

        private static String HH(String alg, String s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            var d = alg.StartsWith("SHA-256", StringComparison.OrdinalIgnoreCase) ? SHA256.HashData(bytes) : MD5.HashData(bytes);
            return Convert.ToHexStringLower(d);
        }

        private static Dictionary<String, String> ParseChallenge(String v)
        {
            var rest = v.StartsWith("Digest", StringComparison.OrdinalIgnoreCase) ? v[6..] : v;
            var d = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(rest, "(\\w+)=(?:\"([^\"]*)\"|([^,]+))"))
                d[m.Groups[1].Value] = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value.Trim();
            return d;
        }

        private static String BuildAuth(String challenge, String user, String pass, String method, String uri, String? nonceOverride = null, String alg = "SHA-256")
        {
            var p      = ParseChallenge(challenge);
            var realm  = p["realm"];
            var nonce  = nonceOverride ?? p["nonce"];
            var qop    = p.GetValueOrDefault("qop", "");
            var cnonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
            var nc     = "00000001";
            var ha1    = HH(alg, $"{user}:{realm}:{pass}");
            var ha2    = HH(alg, $"{method}:{uri}");
            var resp   = qop.Length > 0 ? HH(alg, $"{ha1}:{nonce}:{nc}:{cnonce}:auth:{ha2}")
                                        : HH(alg, $"{ha1}:{nonce}:{ha2}");
            var h = $"Digest username=\"{user}\", realm=\"{realm}\", nonce=\"{nonce}\", uri=\"{uri}\", algorithm={alg}, response=\"{resp}\"";
            if (qop.Length > 0) h += $", qop=auth, nc={nc}, cnonce=\"{cnonce}\"";
            return h;
        }

        #endregion

    }

}
