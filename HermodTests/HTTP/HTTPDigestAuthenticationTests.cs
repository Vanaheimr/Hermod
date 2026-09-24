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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// RFC 7616 Digest credentials, and the challenge that asks for them.
    ///
    /// Until 2026-09-24 the type under test parsed
    /// <c>Digest base64(user):base64(secret)</c> — a password in the clear under
    /// the name of the scheme that exists not to send one — and nothing
    /// referenced it, not even the <c>Authorization</c> dispatcher. Finding H-3
    /// of the HTTP/1.1 conformance suite. Whether the credentials are *valid* is
    /// DigestAuthenticationScheme's question and is tested with it; these tests
    /// are about reading them off the wire and about what the server asks for.
    /// </summary>
    [TestFixture]
    public class HTTPDigestAuthenticationTests
    {

        #region Data

        // A complete qop=auth credential, of the shape curl and every browser send.
        private const String FullCredential =
            "Digest username=\"alice\", realm=\"Hermod HTTP/1.1 demo\", " +
            "nonce=\"bm9uY2U=\", uri=\"/secret\", algorithm=SHA-256, " +
            "response=\"6629fae49393a05397450978507c4ef1\", " +
            "qop=auth, nc=00000001, cnonce=\"0a4f113b\", opaque=\"5ccc069c\"";

        /// <summary>
        /// A clock that moves one tick every time it is read.
        ///
        /// It exists because of a test that passed against a broken
        /// implementation. The nonce is <c>base64(ticks ":" HMAC(secret, ticks))</c>
        /// — deterministic in the ticks — so two calls to CreateNonce() inside the
        /// same tick return the SAME nonce. An implementation that minted one per
        /// algorithm was therefore indistinguishable from one that shares a nonce,
        /// and the assertion below could not fail. With this clock the two are
        /// distinguishable: a shared nonce reads the clock once.
        /// </summary>
        private sealed class TickingClock : TimeProvider
        {

            private Int64 ticks;

            public override DateTimeOffset GetUtcNow()
                => DateTimeOffset.UnixEpoch.AddTicks(++ticks);

        }

        #endregion


        #region AFullCredentialParsesFieldByField()

        [Test]
        public void AFullCredentialParsesFieldByField()
        {

            Assert.That(HTTPDigestAuthentication.TryParseHTTPHeader(FullCredential, out var digest), Is.True);

            Assert.Multiple(() => {
                Assert.That(digest!.Username,    Is.EqualTo("alice"));
                Assert.That(digest. Realm,       Is.EqualTo("Hermod HTTP/1.1 demo"));
                Assert.That(digest. Nonce,       Is.EqualTo("bm9uY2U="));
                Assert.That(digest. DigestURI,   Is.EqualTo("/secret"));
                Assert.That(digest. Response,    Is.EqualTo("6629fae49393a05397450978507c4ef1"));
                Assert.That(digest. Algorithm,   Is.EqualTo("SHA-256"));
                Assert.That(digest. QoP,         Is.EqualTo("auth"));
                Assert.That(digest. NonceCount,  Is.EqualTo("00000001"));
                Assert.That(digest. ClientNonce, Is.EqualTo("0a4f113b"));
                Assert.That(digest. Opaque,      Is.EqualTo("5ccc069c"));
                Assert.That(digest. Userhash,    Is.False);
            });

        }

        #endregion

        #region TheLegacyRFC2069FormParsesToo()

        /// <summary>
        /// Without a qop there is no nc and no cnonce, and the response is computed
        /// without them (RFC 2069). Still a credential, and still verifiable — so
        /// requiring the newer fields would reject a client that is doing nothing
        /// wrong.
        /// </summary>
        [Test]
        public void TheLegacyRFC2069FormParsesToo()
        {

            Assert.That(
                HTTPDigestAuthentication.TryParseHTTPHeader(
                    "Digest username=\"alice\", realm=\"r\", nonce=\"n\", uri=\"/\", response=\"abc\"",
                    out var digest
                ),
                Is.True
            );

            Assert.Multiple(() => {
                Assert.That(digest!.QoP,         Is.Null);
                Assert.That(digest. NonceCount,  Is.Null);
                Assert.That(digest. ClientNonce, Is.Null);
                Assert.That(digest. Algorithm,   Is.Null, "absent means MD5, and absent is not \"MD5\"");
            });

        }

        #endregion

        #region AQoPWithoutNonceCountOrClientNonceIsRefused()

        /// <summary>
        /// With a qop the response covers nc and cnonce. A credential that names
        /// the qop and omits them is not merely incomplete — there is no password
        /// for which it could be recomputed, so accepting it would hand a
        /// validator something it can only ever reject.
        /// </summary>
        [Test]
        public void AQoPWithoutNonceCountOrClientNonceIsRefused()
        {

            Assert.Multiple(() => {

                Assert.That(
                    HTTPDigestAuthentication.TryParseHTTPHeader(
                        "Digest username=\"a\", realm=\"r\", nonce=\"n\", uri=\"/\", response=\"x\", qop=auth, nc=00000001",
                        out _
                    ),
                    Is.False,
                    "cnonce missing"
                );

                Assert.That(
                    HTTPDigestAuthentication.TryParseHTTPHeader(
                        "Digest username=\"a\", realm=\"r\", nonce=\"n\", uri=\"/\", response=\"x\", qop=auth, cnonce=\"c\"",
                        out _
                    ),
                    Is.False,
                    "nc missing"
                );

            });

        }

        #endregion

        #region EachRequiredFieldIsRequired()

        /// <summary>
        /// username, realm, nonce, uri and response (RFC 7616, Section 3.4). One
        /// case per field, because a check that only ever drops the same one
        /// proves nothing about the other four.
        /// </summary>
        [Test]
        public void EachRequiredFieldIsRequired()
        {

            var fields = new Dictionary<String, String> {
                             { "username", "\"alice\"" },
                             { "realm",    "\"r\""     },
                             { "nonce",    "\"n\""     },
                             { "uri",      "\"/\""     },
                             { "response", "\"abc\""   }
                         };

            // The whole set parses — otherwise every case below would pass for
            // the wrong reason.
            Assert.That(
                HTTPDigestAuthentication.TryParseHTTPHeader(
                    "Digest " + String.Join(", ", fields.Select(f => $"{f.Key}={f.Value}")),
                    out _
                ),
                Is.True
            );

            Assert.Multiple(() => {
                foreach (var dropped in fields.Keys)
                {

                    var text = "Digest " + String.Join(
                                               ", ",
                                               fields.Where (f => f.Key != dropped).
                                                      Select(f => $"{f.Key}={f.Value}")
                                           );

                    Assert.That(HTTPDigestAuthentication.TryParseHTTPHeader(text, out _),
                                Is.False,
                                $"parsed without {dropped}");

                }
            });

        }

        #endregion

        #region ANonDigestHeaderIsNotADigestCredential()

        [Test]
        public void ANonDigestHeaderIsNotADigestCredential()
        {

            Assert.Multiple(() => {

                Assert.That(HTTPDigestAuthentication.TryParseHTTPHeader("Basic YWxpY2U6c2VjcmV0", out _), Is.False);
                Assert.That(HTTPDigestAuthentication.TryParseHTTPHeader("Bearer abc",               out _), Is.False);
                Assert.That(HTTPDigestAuthentication.TryParseHTTPHeader("Digest",                   out _), Is.False);
                Assert.That(HTTPDigestAuthentication.TryParseHTTPHeader("",                         out _), Is.False);

                // "Digestive" starts with "Digest" and is not it. The scheme token
                // ends at whitespace (RFC 9110, Section 11.4).
                Assert.That(HTTPDigestAuthentication.TryParseHTTPHeader(
                                "Digestive username=\"a\", realm=\"r\", nonce=\"n\", uri=\"/\", response=\"x\"",
                                out _
                            ),
                            Is.False);

            });

        }

        #endregion

        #region QuotedValuesKeepTheirCommasAndEscapes()

        /// <summary>
        /// The auth-param list is comma-separated and its values may be quoted
        /// strings containing commas — which is exactly why splitting on commas
        /// is the wrong way to read one.
        /// </summary>
        [Test]
        public void QuotedValuesKeepTheirCommasAndEscapes()
        {

            Assert.That(
                HTTPDigestAuthentication.TryParseHTTPHeader(
                    "Digest username=\"al, ice\", realm=\"a \\\"quoted\\\" realm\", " +
                    "nonce=\"n\", uri=\"/a,b\", response=\"x\"",
                    out var digest
                ),
                Is.True
            );

            Assert.Multiple(() => {
                Assert.That(digest!.Username,  Is.EqualTo("al, ice"));
                Assert.That(digest. Realm,     Is.EqualTo("a \"quoted\" realm"));
                Assert.That(digest. DigestURI, Is.EqualTo("/a,b"));
            });

        }

        #endregion

        #region UserhashIsReadAsAFlag()

        [Test]
        public void UserhashIsReadAsAFlag()
        {

            Assert.Multiple(() => {

                Assert.That(
                    HTTPDigestAuthentication.TryParseHTTPHeader(
                        "Digest username=\"488869477bf257147b804c45308cd62d\", realm=\"r\", nonce=\"n\", uri=\"/\", response=\"x\", userhash=true",
                        out var hashed
                    ) && hashed.Userhash,
                    Is.True
                );

                Assert.That(
                    HTTPDigestAuthentication.TryParseHTTPHeader(
                        "Digest username=\"alice\", realm=\"r\", nonce=\"n\", uri=\"/\", response=\"x\", userhash=false",
                        out var plain
                    ) && plain.Userhash,
                    Is.False
                );

            });

        }

        #endregion

        #region TheAuthorizationDispatcherYieldsIt()

        /// <summary>
        /// The wiring, and the half that was missing for longer than the parser
        /// was wrong: <see cref="HTTPAuthenticationExtensions"/> tried Basic,
        /// Bearer, Token and TOTP and never Digest, so even a correct parser would
        /// not have been reached from <c>request.Authorization</c>.
        /// </summary>
        [Test]
        public void TheAuthorizationDispatcherYieldsIt()
        {

            Assert.That(HTTPAuthenticationExtensions.TryParse(FullCredential, out var authentication), Is.True);

            Assert.Multiple(() => {
                Assert.That(authentication,                                 Is.InstanceOf<HTTPDigestAuthentication>());
                Assert.That(((HTTPDigestAuthentication) authentication!).Username, Is.EqualTo("alice"));
                Assert.That(authentication.HTTPText,                        Does.StartWith("Digest username=\"alice\""));
            });

        }

        #endregion

        #region TheCredentialsAreKeptVerbatim()

        /// <summary>
        /// A validator hashes what the client sent. Reserializing from the parsed
        /// fields would be a different string — different spacing, different
        /// quoting, different order — and any of those differences is the
        /// difference between a response that verifies and one that does not.
        /// </summary>
        [Test]
        public void TheCredentialsAreKeptVerbatim()
        {

            Assert.That(HTTPDigestAuthentication.TryParseHTTPHeader(FullCredential, out var digest), Is.True);

            Assert.Multiple(() => {
                Assert.That(digest!.Credentials, Is.EqualTo(FullCredential["Digest ".Length..]));
                Assert.That(digest. HTTPText,    Is.EqualTo(FullCredential));
            });

        }

        #endregion

        #region TheChallengeOffersEveryAlgorithmOnOneNonce()

        /// <summary>
        /// RFC 7616, Section 3.3: several challenges, most preferred first. They
        /// share one nonce on purpose — that is what makes them one offer the
        /// client may answer either way, rather than several the server would then
        /// have to tell apart.
        ///
        /// Also asserts that `algorithm` goes out as a token. Section 3.3 defines
        /// it as one, and the WWWAuthenticate type's own serializer quotes every
        /// parameter it holds, which is why this challenge is built as text.
        /// </summary>
        [Test]
        public void TheChallengeOffersEveryAlgorithmOnOneNonce()
        {

            var scheme     = new DigestAuthenticationScheme(
                                 Realm:           "test-realm",
                                 LookupPassword:  (username, cancellationToken) => Task.FromResult<String?>("secret"),
                                 TimeProvider:    new TickingClock()
                             );

            var challenge  = scheme.BuildChallenges("test-realm", "SHA-256", "MD5");

            Assert.Multiple(() => {

                Assert.That(challenge, Does.Contain("algorithm=SHA-256").And.Not.Contain("algorithm=\"SHA-256\""));
                Assert.That(challenge, Does.Contain("algorithm=MD5"));

                // Most preferred first.
                Assert.That(challenge.IndexOf("algorithm=SHA-256", StringComparison.Ordinal),
                            Is.LessThan(challenge.IndexOf("algorithm=MD5", StringComparison.Ordinal)));

                // One nonce, twice.
                var nonces = challenge.Split("nonce=\"").Skip(1).
                                       Select(part => part[..part.IndexOf('"')]).
                                       ToArray();

                Assert.That(nonces,              Has.Length.EqualTo(2));
                Assert.That(nonces[0],           Is.Not.Empty);
                Assert.That(nonces[0],           Is.EqualTo(nonces[1]),
                            "the two challenges are one offer, so they carry one nonce — " +
                            "and the clock ticks between reads, so equal here means shared rather than simultaneous");

                // And a single algorithm still yields a single challenge.
                Assert.That(scheme.BuildChallenges("test-realm", "MD5").Split("Digest ").Length - 1, Is.EqualTo(1));

            });

        }

        #endregion

    }

}
