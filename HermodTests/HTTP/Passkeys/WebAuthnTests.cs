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

using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Passkeys;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.Passkeys
{

    /// <summary>
    /// The WebAuthn ceremonies verified against a software authenticator:
    /// registration and assertion with ES256 and RS256, and the refusals a
    /// relying party must produce.
    /// </summary>
    [TestFixture]
    public class WebAuthnTests
    {

        #region Data

        private const           String            origin    = "http://localhost:8080";
        private static readonly WebAuthnSettings  settings  = new ("localhost", "Hermod Tests", [ origin ]);
        private static readonly User_Id           alice     = User_Id.Parse("usr-0001");
        private static readonly User_Id           bob       = User_Id.Parse("usr-0002");

        #endregion

        #region (private) SoftwareAuthenticator

        /// <summary>
        /// What a platform authenticator does, without the platform: one key
        /// pair, one credential id and a signature counter.
        /// </summary>
        internal sealed class SoftwareAuthenticator : IDisposable
        {

            private readonly ECDsa?  ecdsa;
            private readonly RSA?    rsa;

            public Byte[]  CredentialId    { get; } = RandomNumberGenerator.GetBytes(16);
            public String  CredentialIdText
                => Base64Url.EncodeToString(CredentialId);
            public UInt32  Counter         { get; set; }

            public SoftwareAuthenticator(Boolean UseRSA = false)
            {
                if (UseRSA)
                    rsa    = RSA.Create(2048);
                else
                    ecdsa  = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            }

            private Byte[] COSEKey()
            {

                if (ecdsa is not null)
                {
                    var parameters = ecdsa.ExportParameters(false);
                    return new CBORMap {
                               {  1, 2 },                   // kty: EC2
                               {  3, -7 },                  // alg: ES256
                               { -1, 1 },                   // crv: P-256
                               { -2, parameters.Q.X! },
                               { -3, parameters.Q.Y! }
                           }.ToValue().ToByteArray();
                }

                var rsaParameters = rsa!.ExportParameters(false);
                return new CBORMap {
                           {  1, 3 },                       // kty: RSA
                           {  3, -257 },                    // alg: RS256
                           { -1, rsaParameters.Modulus! },
                           { -2, rsaParameters.Exponent! }
                       }.ToValue().ToByteArray();

            }

            private static Byte[] ClientDataJSON(String Type, String Challenge, String Origin)
                => Encoding.UTF8.GetBytes(new JObject(
                       new JProperty("type",         Type),
                       new JProperty("challenge",    Challenge),
                       new JProperty("origin",       Origin),
                       new JProperty("crossOrigin",  false)
                   ).ToString(Newtonsoft.Json.Formatting.None));

            private Byte[] AuthenticatorData(Byte Flags, Boolean WithCredential)
            {

                var data = new List<Byte>();

                data.AddRange(SHA256.HashData(Encoding.UTF8.GetBytes(settings.RpId)));
                data.Add(Flags);

                var counter = new Byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(counter, Counter);
                data.AddRange(counter);

                if (WithCredential)
                {
                    data.AddRange(new Byte[16]);                                  // AAGUID
                    data.Add((Byte) (CredentialId.Length >> 8));
                    data.Add((Byte)  CredentialId.Length);
                    data.AddRange(CredentialId);
                    data.AddRange(COSEKey());
                }

                return [.. data];

            }

            /// <summary>
            /// The PublicKeyCredential a browser returns from navigator.credentials.create().
            /// </summary>
            public JObject Register(String   Challenge,
                                    String   Origin        = origin,
                                    Byte     Flags         = 0x45,   // user present, user verified, attested credential data
                                    String?  IdOverride    = null)
            {

                var attestationObject = new CBORMap {
                                            { "fmt",       "none" },
                                            { "attStmt",   new CBORMap().ToValue() },
                                            { "authData",  AuthenticatorData(Flags, WithCredential: true) }
                                        }.ToValue().ToByteArray();

                return new JObject(
                           new JProperty("id",        IdOverride ?? CredentialIdText),
                           new JProperty("rawId",     CredentialIdText),
                           new JProperty("type",      "public-key"),
                           new JProperty("response",  new JObject(
                               new JProperty("clientDataJSON",     Base64Url.EncodeToString(ClientDataJSON("webauthn.create", Challenge, Origin))),
                               new JProperty("attestationObject",  Base64Url.EncodeToString(attestationObject)),
                               new JProperty("transports",         new JArray("internal"))
                           )),
                           new JProperty("clientExtensionResults", new JObject(
                               new JProperty("credProps", new JObject(new JProperty("rk", true)))
                           ))
                       );

            }

            /// <summary>
            /// The PublicKeyCredential a browser returns from navigator.credentials.get().
            /// </summary>
            public JObject Authenticate(String   Challenge,
                                        User_Id  UserId,
                                        Boolean  BumpCounter        = true,
                                        String   Origin             = origin,
                                        Boolean  TamperSignature    = false)
            {

                if (BumpCounter)
                    Counter++;

                var authenticatorData  = AuthenticatorData(0x05, WithCredential: false);   // user present, user verified
                var clientDataJSON     = ClientDataJSON("webauthn.get", Challenge, Origin);
                var signedData         = new Byte[authenticatorData.Length + 32];

                authenticatorData.CopyTo(signedData, 0);
                SHA256.HashData(clientDataJSON).CopyTo(signedData, authenticatorData.Length);

                var signature = ecdsa is not null
                                    ? ecdsa.SignData(signedData, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)
                                    : rsa!. SignData(signedData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                if (TamperSignature)
                    signature[^1] ^= 0x01;

                return new JObject(
                           new JProperty("id",        CredentialIdText),
                           new JProperty("rawId",     CredentialIdText),
                           new JProperty("type",      "public-key"),
                           new JProperty("response",  new JObject(
                               new JProperty("clientDataJSON",     Base64Url.EncodeToString(clientDataJSON)),
                               new JProperty("authenticatorData",  Base64Url.EncodeToString(authenticatorData)),
                               new JProperty("signature",          Base64Url.EncodeToString(signature)),
                               new JProperty("userHandle",         Base64Url.EncodeToString(Encoding.UTF8.GetBytes(UserId.ToString())))
                           ))
                       );

            }

            public void Dispose()
            {
                ecdsa?.Dispose();
                rsa?.  Dispose();
            }

        }

        #endregion

        #region (private) Register(Authenticator)

        private static Passkey Register(SoftwareAuthenticator Authenticator)
        {

            var ceremonies  = new CeremonyStore();
            var ceremony    = ceremonies.Create(CeremonyType.Registration, alice);

            Assert.That(WebAuthn.TryVerifyRegistration(settings, ceremony, Authenticator.Register(ceremony.ChallengeBase64Url), "Test key", out var passkey, out var error),
                        Is.True,
                        error);

            return passkey!;

        }

        #endregion


        #region Registration_Produces_A_Passkey()

        [Test]
        public void Registration_Produces_A_Passkey()
        {

            using var authenticator = new SoftwareAuthenticator();

            var passkey = Register(authenticator);

            Assert.That(passkey.Id,              Is.EqualTo(authenticator.CredentialIdText));
            Assert.That(passkey.Algorithm,       Is.EqualTo(WebAuthn.ES256));
            Assert.That(passkey.AlgorithmName,   Is.EqualTo("ES256"));
            Assert.That(passkey.SignCount,       Is.EqualTo(0));
            Assert.That(passkey.Discoverable,    Is.True);
            Assert.That(passkey.Transports,      Is.EqualTo(new[] { "internal" }));
            Assert.That(passkey.AAGUID,          Is.EqualTo(Guid.Empty));
            Assert.That(passkey.BackupEligible,  Is.False);
            Assert.That(passkey.Name,            Is.EqualTo("Test key"));
            Assert.That(passkey.LastUsedAt,      Is.Null);

        }

        #endregion

        #region Registration_Refuses_A_Wrong_Origin_Or_Challenge()

        [Test]
        public void Registration_Refuses_A_Wrong_Origin_Or_Challenge()
        {

            using var authenticator = new SoftwareAuthenticator();

            var ceremonies  = new CeremonyStore();
            var ceremony    = ceremonies.Create(CeremonyType.Registration, alice);
            var other       = ceremonies.Create(CeremonyType.Registration, alice);

            Assert.That(WebAuthn.TryVerifyRegistration(settings, ceremony, authenticator.Register(ceremony.ChallengeBase64Url, Origin: "https://evil.example"), "x", out _, out var originError),  Is.False);
            Assert.That(originError,  Does.Contain("origin"));

            Assert.That(WebAuthn.TryVerifyRegistration(settings, ceremony, authenticator.Register(other.ChallengeBase64Url), "x", out _, out var challengeError),  Is.False);
            Assert.That(challengeError,  Does.Contain("challenge"));

            Assert.That(WebAuthn.TryVerifyRegistration(settings, ceremony, authenticator.Register(ceremony.ChallengeBase64Url, IdOverride: "AAAA"), "x", out _, out var idError),  Is.False);
            Assert.That(idError,  Does.Contain("rawId"));

            Assert.That(WebAuthn.TryVerifyRegistration(settings, ceremony, authenticator.Register(ceremony.ChallengeBase64Url, Flags: 0x44), "x", out _, out var presenceError),  Is.False,  "user not present");
            Assert.That(presenceError,  Does.Contain("present"));

        }

        #endregion

        #region Assertion_Verifies_And_Reports_The_Counter()

        [Test]
        public void Assertion_Verifies_And_Reports_The_Counter()
        {

            using var authenticator = new SoftwareAuthenticator();

            var passkey     = Register(authenticator);
            var ceremonies  = new CeremonyStore();
            var ceremony    = ceremonies.Create(CeremonyType.Authentication);

            Assert.That(WebAuthn.TryVerifyAuthentication(settings, ceremony, authenticator.Authenticate(ceremony.ChallengeBase64Url, alice), passkey, alice, out var result, out var error),
                        Is.True,
                        error);

            Assert.That(result!.SignCount,     Is.EqualTo(1));
            Assert.That(result.UserVerified,   Is.True);
            Assert.That(result.BackedUp,       Is.False);

        }

        #endregion

        #region RS256_Works_End_To_End()

        [Test]
        public void RS256_Works_End_To_End()
        {

            using var authenticator = new SoftwareAuthenticator(UseRSA: true);

            var passkey = Register(authenticator);

            Assert.That(passkey.Algorithm,      Is.EqualTo(WebAuthn.RS256));
            Assert.That(passkey.AlgorithmName,  Is.EqualTo("RS256"));

            var ceremony = new CeremonyStore().Create(CeremonyType.Authentication);

            Assert.That(WebAuthn.TryVerifyAuthentication(settings, ceremony, authenticator.Authenticate(ceremony.ChallengeBase64Url, alice), passkey, alice, out _, out var error),
                        Is.True,
                        error);

        }

        #endregion

        #region Assertion_Refuses_Clones_Strangers_And_Tampering()

        [Test]
        public void Assertion_Refuses_Clones_Strangers_And_Tampering()
        {

            using var authenticator = new SoftwareAuthenticator();

            var passkey     = Register(authenticator);
            var ceremonies  = new CeremonyStore();

            // A first sign-in moves the counter to 1...
            var first = ceremonies.Create(CeremonyType.Authentication);
            Assert.That(WebAuthn.TryVerifyAuthentication(settings, first, authenticator.Authenticate(first.ChallengeBase64Url, alice), passkey, alice, out var result, out _),  Is.True);
            passkey = passkey with { SignCount = result!.SignCount };

            // ...so a second one that does not move it looks like a clone.
            var stuck = ceremonies.Create(CeremonyType.Authentication);
            Assert.That(WebAuthn.TryVerifyAuthentication(settings, stuck, authenticator.Authenticate(stuck.ChallengeBase64Url, alice, BumpCounter: false), passkey, alice, out _, out var counterError),  Is.False);
            Assert.That(counterError,  Does.Contain("counter"));

            var stranger = ceremonies.Create(CeremonyType.Authentication);
            Assert.That(WebAuthn.TryVerifyAuthentication(settings, stranger, authenticator.Authenticate(stranger.ChallengeBase64Url, bob), passkey, alice, out _, out var handleError),  Is.False);
            Assert.That(handleError,  Does.Contain("user handle"));

            var foreign = ceremonies.Create(CeremonyType.Authentication);
            Assert.That(WebAuthn.TryVerifyAuthentication(settings, foreign, authenticator.Authenticate(foreign.ChallengeBase64Url, alice, Origin: "https://evil.example"), passkey, alice, out _, out var originError),  Is.False);
            Assert.That(originError,  Does.Contain("origin"));

            var tampered = ceremonies.Create(CeremonyType.Authentication);
            Assert.That(WebAuthn.TryVerifyAuthentication(settings, tampered, authenticator.Authenticate(tampered.ChallengeBase64Url, alice, TamperSignature: true), passkey, alice, out _, out var signatureError),  Is.False);
            Assert.That(signatureError,  Does.Contain("signature"));

        }

        #endregion

        #region Ceremonies_Are_Single_Use_Typed_And_Expire()

        [Test]
        public async Task Ceremonies_Are_Single_Use_Typed_And_Expire()
        {

            var ceremonies    = new CeremonyStore(Lifetime: TimeSpan.FromMilliseconds(200));
            var registration  = ceremonies.Create(CeremonyType.Registration, alice);
            var login         = ceremonies.Create(CeremonyType.Authentication);

            Assert.That(registration.UserId,          Is.EqualTo(alice));
            Assert.That(login.UserId,                 Is.Null);
            Assert.That(registration.Challenge,       Has.Length.EqualTo(32));
            Assert.That(registration.ChallengeBase64Url,  Does.Not.Contain("="));

            Assert.That(ceremonies.TryTake(registration.Id, CeremonyType.Authentication, out _),  Is.False,  "the wrong type");
            Assert.That(ceremonies.TryTake(registration.Id, CeremonyType.Registration,   out _),  Is.False,  "and it is gone after the first attempt");

            Assert.That(ceremonies.TryTake(login.Id, CeremonyType.Authentication, out var taken),  Is.True);
            Assert.That(taken,                                                                     Is.EqualTo(login));
            Assert.That(ceremonies.TryTake(login.Id, CeremonyType.Authentication, out _),          Is.False,  "single use");

            var late = ceremonies.Create(CeremonyType.Authentication);
            await Task.Delay(400);
            Assert.That(ceremonies.TryTake(late.Id, CeremonyType.Authentication, out _),  Is.False,  "expired");

        }

        #endregion

        #region Options_Use_Base64Url_And_Ask_For_Discoverable_Credentials()

        [Test]
        public void Options_Use_Base64Url_And_Ask_For_Discoverable_Credentials()
        {

            using var authenticator = new SoftwareAuthenticator();

            var passkey   = Register(authenticator);
            var ceremony  = new CeremonyStore().Create(CeremonyType.Registration, alice);
            var options   = WebAuthn.CreationOptions(settings, ceremony, alice, "alice", "Alice", [ passkey ]);
            var creation  = options.ToJSON();

            Assert.That(options.AuthenticatorSelection!.ResidentKey,                           Is.EqualTo(ResidentKeyRequirement.Required));
            Assert.That(options.AuthenticatorSelection.UserVerification,                       Is.EqualTo(UserVerificationRequirement.Preferred));
            Assert.That(options.Attestation,                                                   Is.EqualTo(AttestationConveyancePreference.None));
            Assert.That(options.Extensions!.Count,                                             Is.EqualTo(1));

            Assert.That(creation["rp"]!["id"]!.Value<String>(),                                Is.EqualTo("localhost"));
            Assert.That(creation["user"]!["id"]!.Value<String>(),                              Is.EqualTo(Base64Url.EncodeToString(Encoding.UTF8.GetBytes("usr-0001"))));
            Assert.That(creation["user"]!["name"]!.Value<String>(),                            Is.EqualTo("alice"));
            Assert.That(creation["user"]!["displayName"]!.Value<String>(),                     Is.EqualTo("Alice"));
            Assert.That(creation["challenge"]!.Value<String>(),                                Is.EqualTo(ceremony.ChallengeBase64Url));
            Assert.That(creation["challenge"]!.Value<String>(),                                Does.Not.Match("[=+/]"),  "Base64URL without padding");
            Assert.That(creation["timeout"]!.Value<Int64>(),                                   Is.EqualTo(60000));
            Assert.That(creation["authenticatorSelection"]!["residentKey"]!.Value<String>(),   Is.EqualTo("required"));
            Assert.That(creation["authenticatorSelection"]!["requireResidentKey"]!.Value<Boolean>(),  Is.True);
            Assert.That(creation["authenticatorSelection"]!["userVerification"]!.Value<String>(),     Is.EqualTo("preferred"));
            Assert.That(creation["pubKeyCredParams"]!.Select(p => p["alg"]!.Value<Int32>()),   Is.EquivalentTo(new[] { -7, -257 }));
            Assert.That(creation["pubKeyCredParams"]!.Select(p => p["type"]!.Value<String>()), Has.All.EqualTo("public-key"));
            Assert.That(creation["excludeCredentials"]!.Single()["id"]!.Value<String>(),       Is.EqualTo(passkey.Id));
            Assert.That(creation["extensions"]!["credProps"]!.Value<Boolean>(),                Is.True);
            Assert.That(creation["attestation"]!.Value<String>(),                              Is.EqualTo("none"));

            var request = WebAuthn.RequestOptions(settings, ceremony).ToJSON();

            Assert.That(request["rpId"]!.Value<String>(),                Is.EqualTo("localhost"));
            Assert.That(request["challenge"]!.Value<String>(),           Is.EqualTo(ceremony.ChallengeBase64Url));
            Assert.That(request["userVerification"]!.Value<String>(),    Is.EqualTo("preferred"));
            Assert.That(request["allowCredentials"],                     Is.Null,  "discoverable credentials need no list");
            Assert.That(WebAuthn.RequestOptions(settings, ceremony, [ passkey ]).ToJSON()["allowCredentials"]!.Single()["transports"]!.Values<String>(),  Is.EqualTo(new[] { "internal" }));

        }

        #endregion

        #region Passkey_JSON_Round_Trip()

        [Test]
        public void Passkey_JSON_Round_Trip()
        {

            using var authenticator = new SoftwareAuthenticator();

            var passkey = Register(authenticator) with { LastUsedAt = DateTimeOffset.UtcNow };

            Assert.That(Passkey.TryParse(passkey.ToStorageJSON(), out var parsed, out var error),  Is.True,  error);
            Assert.That(parsed!.Id,          Is.EqualTo(passkey.Id));
            Assert.That(parsed.Algorithm,    Is.EqualTo(passkey.Algorithm));
            Assert.That(parsed.PublicKey,    Is.EqualTo(passkey.PublicKey));
            Assert.That(parsed.Transports,   Is.EqualTo(passkey.Transports));
            Assert.That(parsed.Name,         Is.EqualTo(passkey.Name));
            Assert.That(parsed.LastUsedAt,   Is.EqualTo(passkey.LastUsedAt));

            Assert.That(passkey.ToJSON()["publicKey"],  Is.Null,  "the public view carries no key material");

        }

        #endregion

    }

}
