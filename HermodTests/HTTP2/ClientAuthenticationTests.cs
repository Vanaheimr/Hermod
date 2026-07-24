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

using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP2;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// The client half of RFC 9110, Section 11: answering a 401.
    ///
    /// The schemes in HTTP2/Auth only ever *validated* credentials — the side that
    /// computes them did not exist, so every test until now had to hand-roll a
    /// Digest response. With <c>Credentials</c> the client parses the challenge,
    /// picks the strongest scheme it can answer, and re-issues the request once.
    ///
    /// Both ends of each algorithm are ours, so these tests are also the closest
    /// thing to a self-check on the scheme implementations: our client's Digest
    /// response is verified by our server's Digest validator.
    /// </summary>
    [TestFixture]
    public class ClientAuthenticationTests
    {

        #region (servers)

        // Basic + Bearer + Token, so the preference order has something to choose from.
        private static HTTP2RequestHandler MultiSchemeServer()
        {

            var authenticator = new HTTPAuthenticator("demo",
                new BasicAuthenticationScheme ((u, p, _) => Task.FromResult<HTTPAuthenticatedIdentity?>(
                    u == "alice" && p == "secret"    ? new HTTPAuthenticatedIdentity { Name = "alice" }      : null)),
                new BearerAuthenticationScheme((t, _)    => Task.FromResult<HTTPAuthenticatedIdentity?>(
                    t == "valid-token-123"           ? new HTTPAuthenticatedIdentity { Name = "token-user" } : null)));

            return Respond(authenticator);

        }

        private static HTTP2RequestHandler DigestServer()

            => Respond(new HTTPAuthenticator("demo",
                   new DigestAuthenticationScheme("demo",
                       (u, _) => Task.FromResult<String?>(u == "alice" ? "secret" : null))));

        private static HTTP2RequestHandler Respond(HTTPAuthenticator Authenticator)

            => HTTPAuthentication.RequireAuthentication(Authenticator,
                   (identity, sid, h, b, ct) => {
                       var body = Encoding.UTF8.GetBytes($"Authenticated as: {identity.Name}");
                       return Task.FromResult<(List<(String, String)>, Byte[]?)>(
                           ([(":status", "200"), ("content-type", "text/plain"), ("content-length", body.Length.ToString())], body));
                   });

        #endregion


        #region Challenges_AreParsed()

        [Test]
        public void Challenges_AreParsed()
        {

            // One challenge per field line — what this stack's server emits.
            var perLine = HTTPClientAuthenticator.ParseChallenges([
                              "Basic realm=\"demo\", charset=\"UTF-8\"",
                              "Digest realm=\"demo\", qop=\"auth\", algorithm=SHA-256, nonce=\"abc\""
                          ]);

            // Two challenges in one line: the comma before "Digest" separates
            // challenges, the commas inside separate auth-params.
            var oneLine = HTTPClientAuthenticator.ParseChallenges([
                              "Basic realm=\"demo\", Digest realm=\"demo\", nonce=\"abc\""
                          ]);

            // A comma inside a quoted value must not be mistaken for a separator.
            var quoted  = HTTPClientAuthenticator.ParseChallenges([
                              "Digest realm=\"a,b\", nonce=\"x\""
                          ]);

            Assert.Multiple(() =>
            {
                Assert.That(perLine.Select(c => c.Scheme), Is.EqualTo(new[] { "Basic", "Digest" }));
                Assert.That(perLine[1].Parameters,          Does.Contain("nonce=\"abc\""));

                Assert.That(oneLine.Select(c => c.Scheme), Is.EqualTo(new[] { "Basic", "Digest" }), "split on the challenge boundary");

                Assert.That(quoted,                        Has.Count.EqualTo(1),                    "a quoted comma is not a boundary");
                Assert.That(quoted[0].Parameters,          Does.Contain("realm=\"a,b\""));
            });

        }

        #endregion

        #region StrongestOfferedScheme_Wins()

        // Basic hands the password to the server; Digest does not. Offered both, a
        // client must not pick the one that gives the secret away.
        [Test]
        public void StrongestOfferedScheme_Wins()
        {

            var auth = new HTTPClientAuthenticator(HTTPClientCredentials.UserNameAndPassword("alice", "secret"));

            // A request target that does not itself contain the password, so the
            // "password never on the wire" assertion below means what it says.
            var both  = auth.Answer(["Basic realm=\"demo\"",
                                     "Digest realm=\"demo\", qop=\"auth\", algorithm=SHA-256, nonce=\"abc\""],
                                    "GET", "/private");

            var basic = auth.Answer(["Basic realm=\"demo\""], "GET", "/private");

            Assert.Multiple(() =>
            {
                Assert.That(both,  Does.StartWith("Digest "), "Digest preferred over Basic");
                Assert.That(both,  Does.Not.Contain("secret"), "the password never appears in the credential");
                Assert.That(basic, Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:secret"))));
            });

        }

        #endregion

        #region UnanswerableChallenges_ReturnNull()

        // Null means "do not retry": re-sending unchanged would only earn a second
        // 401. Covers a scheme we don't implement, a scheme we lack credentials
        // for, a Digest algorithm we cannot compute, and an auth-int-only qop.
        [Test]
        public void UnanswerableChallenges_ReturnNull()
        {

            var password = new HTTPClientAuthenticator(HTTPClientCredentials.UserNameAndPassword("alice", "secret"));
            var token    = new HTTPClientAuthenticator(HTTPClientCredentials.BearerToken("t"));

            Assert.Multiple(() =>
            {
                Assert.That(password.Answer(["Negotiate"],                                                    "GET", "/"), Is.Null, "unimplemented scheme");
                Assert.That(password.Answer(["Bearer realm=\"demo\""],                                        "GET", "/"), Is.Null, "no token held");
                Assert.That(token.   Answer(["Basic realm=\"demo\""],                                         "GET", "/"), Is.Null, "no password held");
                Assert.That(password.Answer(["Digest realm=\"d\", nonce=\"n\", algorithm=SHA-512"],           "GET", "/"), Is.Null, "algorithm we cannot compute");
                Assert.That(password.Answer(["Digest realm=\"d\", nonce=\"n\", qop=\"auth-int\""],            "GET", "/"), Is.Null, "auth-int not implemented");
                Assert.That(password.Answer(["Digest realm=\"d\""],                                           "GET", "/"), Is.Null, "challenge without a nonce");
                Assert.That(password.Answer([],                                                               "GET", "/"), Is.Null, "no challenge at all");
            });

        }

        #endregion

        #region DigestNonceCount_Increases()

        // RFC 7616: nc must strictly increase while a nonce is reused, or a
        // captured credential could simply be replayed.
        [Test]
        public void DigestNonceCount_Increases()
        {

            var auth      = new HTTPClientAuthenticator(HTTPClientCredentials.UserNameAndPassword("alice", "secret"));
            var challenge = new[] { "Digest realm=\"demo\", qop=\"auth\", algorithm=SHA-256, nonce=\"same-nonce\"" };

            var first  = auth.Answer(challenge, "GET", "/secret");
            var second = auth.Answer(challenge, "GET", "/secret");
            var other  = auth.Answer(["Digest realm=\"demo\", qop=\"auth\", algorithm=SHA-256, nonce=\"other\""], "GET", "/secret");

            Assert.Multiple(() =>
            {
                Assert.That(first,  Does.Contain("nc=00000001"));
                Assert.That(second, Does.Contain("nc=00000002"), "same nonce -> next count");
                Assert.That(other,  Does.Contain("nc=00000001"), "a fresh nonce starts over");
                Assert.That(first,  Is.Not.EqualTo(second),      "and the response hash differs accordingly");
            });

        }

        #endregion


        #region Client_AnswersBasicChallenge()

        [Test]
        public async Task Client_AnswersBasicChallenge()
        {

            await using var srv = await TestH2Server.StartAsync(MultiSchemeServer());

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                           Options: new HTTP2ClientOptions {
                               Credentials = HTTPClientCredentials.UserNameAndPassword("alice", "secret")
                           });

            var ok = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/secret");
            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(ok.Status,                        Is.EqualTo(200),                   "401 answered transparently");
                Assert.That(Encoding.UTF8.GetString(ok.Body), Is.EqualTo("Authenticated as: alice"));
            });

        }

        #endregion

        #region Client_AnswersBearerChallenge()

        [Test]
        public async Task Client_AnswersBearerChallenge()
        {

            await using var srv = await TestH2Server.StartAsync(MultiSchemeServer());

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                           Options: new HTTP2ClientOptions {
                               Credentials = HTTPClientCredentials.BearerToken("valid-token-123")
                           });

            var ok = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/secret");
            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(ok.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(ok.Body), Is.EqualTo("Authenticated as: token-user"));
            });

        }

        #endregion

        #region Client_AnswersDigestChallenge()

        // The end-to-end proof: our client computes the RFC 7616 response and our
        // server's validator accepts it — two independent implementations of the
        // same algorithm agreeing, with the password never on the wire.
        [Test]
        public async Task Client_AnswersDigestChallenge()
        {

            await using var srv = await TestH2Server.StartAsync(DigestServer());

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                           Options: new HTTP2ClientOptions {
                               Credentials = HTTPClientCredentials.UserNameAndPassword("alice", "secret")
                           });

            var ok     = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/digest");
            var second = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/digest");

            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(ok.Status,                        Is.EqualTo(200));
                Assert.That(Encoding.UTF8.GetString(ok.Body), Is.EqualTo("Authenticated as: alice"));
                Assert.That(second.Status,                    Is.EqualTo(200), "and again on the same connection");
            });

        }

        #endregion

        #region WrongCredentials_SurfaceThe401_WithoutLooping()

        // One retry, not a loop: a second 401 is the answer, not a trigger.
        [Test]
        public async Task WrongCredentials_SurfaceThe401_WithoutLooping()
        {

            await using var srv = await TestH2Server.StartAsync(MultiSchemeServer());

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                           Options: new HTTP2ClientOptions {
                               Credentials = HTTPClientCredentials.UserNameAndPassword("alice", "wrong")
                           });

            var denied = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/secret");
            await conn.CloseAsync();

            Assert.That(denied.Status, Is.EqualTo(401), "the second 401 is returned, not retried again");

        }

        #endregion

        #region CallersOwnAuthorization_IsNotOverridden()

        // A caller managing authorization by hand owns it — we must not answer the
        // challenge over their head, even when we hold credentials that would work.
        [Test]
        public async Task CallersOwnAuthorization_IsNotOverridden()
        {

            await using var srv = await TestH2Server.StartAsync(MultiSchemeServer());

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert,
                           Options: new HTTP2ClientOptions {
                               Credentials = HTTPClientCredentials.UserNameAndPassword("alice", "secret")
                           });

            var denied = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/secret",
                             ExtraHeaders: [("authorization", "Bearer definitely-not-valid")]);

            await conn.CloseAsync();

            Assert.That(denied.Status, Is.EqualTo(401), "the caller's own credential stands");

        }

        #endregion

        #region WithoutCredentials_NothingChanges()

        [Test]
        public async Task WithoutCredentials_NothingChanges()
        {

            await using var srv = await TestH2Server.StartAsync(MultiSchemeServer());

            var conn = await HTTP2Client.ConnectAsync("localhost", srv.Port, H2.AcceptAnyServerCert);
            var anon = await conn.SendRequestAsync("GET", "https", $"localhost:{srv.Port}", "/secret");
            await conn.CloseAsync();

            Assert.Multiple(() =>
            {
                Assert.That(anon.Status,                                     Is.EqualTo(401), "the 401 is the caller's to handle");
                Assert.That(anon.Headers.Any(h => h.Name == "www-authenticate"), Is.True,     "challenge left intact");
            });

        }

        #endregion

    }

}
