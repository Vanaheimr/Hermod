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

using System.Buffers.Text;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

using V = org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.RFC9421Vectors;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// HTTP Message Signatures (RFC 9421): the signature base, the derived
    /// components, the algorithms, verification and what an application
    /// requires of it, and both on the wire.
    /// </summary>
    /// <remarks>
    /// Measured before: Hermod had no RFC 9421 at all. Signature-Input,
    /// Signature and Accept-Signature were three names in the set of fields
    /// that are lists, and nothing else. The examples are the RFC's own, from
    /// <see cref="RFC9421Vectors"/>; where an algorithm is deterministic (HMAC,
    /// Ed25519) the signature is computed and compared octet for octet, and
    /// where it is not (RSA-PSS, ECDSA) the RFC's signature is verified.
    /// </remarks>
    [TestFixture]
    public class MessageSignatureTests
    {

        #region Keys

        private static Byte[] FromBase64Url(String Text)
            => Base64Url.DecodeFromChars(Text);

        private static Byte[] Pad(Byte[] Value, Int32 Length)
            => Value.Length >= Length ? Value : [.. new Byte[Length - Value.Length], .. Value];

        private static RSA RSAFromJWK(String JWK)
        {

            using var json = JsonDocument.Parse(JWK);
            var key        = json.RootElement;
            var modulus    = FromBase64Url(key.GetProperty("n").GetString()!);
            var half       = modulus.Length / 2;

            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters {
                Modulus   = modulus,
                Exponent  = FromBase64Url(key.GetProperty("e"). GetString()!),
                D         = Pad(FromBase64Url(key.GetProperty("d"). GetString()!), modulus.Length),
                P         = Pad(FromBase64Url(key.GetProperty("p"). GetString()!), half),
                Q         = Pad(FromBase64Url(key.GetProperty("q"). GetString()!), half),
                DP        = Pad(FromBase64Url(key.GetProperty("dp").GetString()!), half),
                DQ        = Pad(FromBase64Url(key.GetProperty("dq").GetString()!), half),
                InverseQ  = Pad(FromBase64Url(key.GetProperty("qi").GetString()!), half)
            });

            return rsa;

        }

        private static String JWKField(String JWK, String Name)
        {
            using var json = JsonDocument.Parse(JWK);
            return json.RootElement.GetProperty(Name).GetString()!;
        }

        private static RSA PEM(String Text)
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(Text);
            return rsa;
        }

        private static ECDsa ECPEM(String Text)
        {
            var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(Text);
            return ecdsa;
        }

        private static readonly Byte[] sharedSecret  = Convert.FromBase64String(V.SharedSecretBase64.Trim());

        private static HTTPSignatureKey RSAPSSPrivate      => HTTPSignatureKey.RSAPSS     (RSAFromJWK(V.RSAPSSJWK),           "test-key-rsa-pss");
        private static HTTPSignatureKey RSAPSSPublic       => HTTPSignatureKey.RSAPSS     (PEM(V.RSAPSSPublicKeyPEM),         "test-key-rsa-pss");
        private static HTTPSignatureKey RSAv15Public       => HTTPSignatureKey.RSAPKCS1v15(PEM(V.RSAPublicKeyPEM),            "test-key-rsa");
        private static HTTPSignatureKey ECCP256Private     => HTTPSignatureKey.ECDSA      (ECPEM(V.ECCP256PrivateKeyPEM),     "test-key-ecc-p256");
        private static HTTPSignatureKey ECCP256Public      => HTTPSignatureKey.ECDSA      (ECPEM(V.ECCP256PublicKeyPEM),      "test-key-ecc-p256");
        private static HTTPSignatureKey Ed25519Private     => HTTPSignatureKey.Ed25519    (FromBase64Url(JWKField(V.Ed25519JWK, "d")), "test-key-ed25519");
        private static HTTPSignatureKey Ed25519Public      => HTTPSignatureKey.Ed25519PublicKey(FromBase64Url(JWKField(V.Ed25519JWK, "x")), "test-key-ed25519");
        private static HTTPSignatureKey SharedSecret       => HTTPSignatureKey.HMACSHA256 (sharedSecret,                      "test-shared-secret");

        /// <summary>
        /// The verifier's keys: public keys wherever the RFC gives one.
        /// </summary>
        private static HTTPSignatureKey? KnownKey(HTTPSignatureParameters Parameters)

            => Parameters.KeyId switch {
                   "test-key-rsa-pss"    => RSAPSSPublic,
                   "test-key-rsa"        => RSAv15Public,
                   "test-key-ecc-p256"   => ECCP256Public,
                   "test-key-ed25519"    => Ed25519Public,
                   "test-shared-secret"  => SharedSecret,
                   _                     => null
               };

        /// <summary>
        /// The RFC's time, a few seconds after its signatures were made.
        /// </summary>
        private static readonly DateTimeOffset rfcNow = DateTimeOffset.FromUnixTimeSeconds(1618884480);

        private static HTTPSignatureVerificationOptions Options(String? Label = null)

            => new () {
                   KeyResolver  = KnownKey,
                   Label        = Label,
                   Now          = rfcNow
               };

        #endregion

        #region Messages

        private static String LF(String Text)
            => Text.ReplaceLineEndings("\n");

        private static String With(String Header, String Fields)
            => LF(Header).TrimEnd('\n') + "\n" + LF(Fields);

        private static HTTPSignatureMessage Message(String                                       Header,
                                                    String?                                      Scheme    = "https",
                                                    HTTPSignatureMessage?                        Request   = null,
                                                    IEnumerable<KeyValuePair<String, String>>?   Trailers  = null)
        {

            Assert.That(HTTPSignatureMessage.TryParse(LF(Header), Scheme, out var message, Trailers, Request), Is.True, Header);

            return message!;

        }

        /// <summary>
        /// The parameters of the signature with the given label, from the
        /// Signature-Input line of a message.
        /// </summary>
        private static HTTPSignatureParameters ParametersOf(String Header, String Label)
        {

            var line = LF(Header).Split('\n').Single(line => line.StartsWith("Signature-Input: ", StringComparison.Ordinal));

            Assert.That(StructuredFields.TryParseDictionary(line["Signature-Input: ".Length..], out var dictionary), Is.True);
            Assert.That(dictionary!.TryGet(Label, out var member),                                                    Is.True);
            Assert.That(HTTPSignatureParameters.TryParse(member!, out var parameters, out var error),                 Is.True, error);

            return parameters!;

        }

        private static String BaseOf(HTTPSignatureMessage     Message,
                                     HTTPSignatureParameters  Parameters,
                                     IReadOnlyDictionary<String, HTTPStructuredFieldType>? Types = null)
        {

            Assert.That(HTTPSignatureBase.TryCreate(Message, Parameters, out var signatureBase, out var error, Types), Is.True, error);

            return signatureBase!;

        }

        private static String? ErrorOf(HTTPSignatureMessage     Message,
                                       params HTTPSignatureComponent[] Components)
        {

            Assert.That(HTTPSignatureBase.TryCreate(Message, new HTTPSignatureParameters(Components), out _, out var error), Is.False);

            return error;

        }

        private static String ValueOf(HTTPSignatureMessage     Message,
                                      HTTPSignatureComponent   Component,
                                      IReadOnlyDictionary<String, HTTPStructuredFieldType>? Types = null)
        {

            Assert.That(HTTPSignatureBase.TryGetComponentValue(Message, Component, out var value, out var error, Types), Is.True, error);

            return value!;

        }

        private static HTTPSignatureComponent C(String Name)
            => new (Name);

        #endregion


        #region The signature base, as the RFC gives it

        [Test]
        public void The_Base_Is_Figure_1()
        {

            var parameters = new HTTPSignatureParameters(
                                 [ C("@method"), C("@authority"), C("@path"), C("content-digest"), C("content-length"), C("content-type") ],
                                 new SFParameters([ new ("created", SFBareItem.Integer(1618884473)),
                                                    new ("keyid",   SFBareItem.String ("test-key-rsa-pss")) ])
                             );

            Assert.That(BaseOf(Message(V.TestRequest), parameters), Is.EqualTo(LF(V.Figure1Base)));

        }

        [TestCase("21", false)]
        [TestCase("22", false)]
        [TestCase("23", false)]
        [TestCase("24", true )]
        [TestCase("25", false)]
        [TestCase("26", false)]
        public void The_Base_Of_Appendix_B2(String Example, Boolean OnTheResponse)
        {

            var fields  = typeof(V).GetField("FieldsB" + Example)!.GetValue(null) as String;
            var header  = With(OnTheResponse ? V.TestResponse : V.TestRequest, fields!);
            var expect  = typeof(V).GetField("BaseB"   + Example)!.GetValue(null) as String;

            Assert.That(BaseOf(Message(header), ParametersOf(header, "sig-b" + Example)), Is.EqualTo(LF(expect!)));

        }

        [Test]
        public void The_Base_Of_The_TLS_Terminating_Proxy_B3()

            => Assert.That(BaseOf(Message(V.ProxiedTLSRequest), ParametersOf(V.ProxiedTLSRequest, "ttrp")),
                           Is.EqualTo(LF(V.BaseB3)));

        [Test]
        public void The_Base_Of_The_Transformed_Request_B4()

            => Assert.That(BaseOf(Message(V.B4Original), ParametersOf(V.B4Original, "transform")),
                           Is.EqualTo(LF(V.BaseB4)));

        [Test]
        public void The_Base_Of_A_Response_With_Request_Components_Section_2_4()
        {

            Assert.Multiple(() => {

                Assert.That(BaseOf(Message(V.Section24ResponseA, Request: Message(V.Section24Request)),
                                   ParametersOf(V.Section24ResponseA, "reqres")),
                            Is.EqualTo(LF(V.Section24BaseA)));

                Assert.That(BaseOf(Message(V.Section24ResponseB, Request: Message(V.Section24SignedRequest)),
                                   ParametersOf(V.Section24ResponseB, "reqres")),
                            Is.EqualTo(LF(V.Section24BaseB)));

            });

        }

        [Test]
        public void The_Base_Of_The_Proxy_Signature_Section_4_3()

            => Assert.That(BaseOf(Message(V.Section43ProxiedRequest), ParametersOf(V.Section43ProxiedRequest, "proxy_sig")),
                           Is.EqualTo(LF(V.Section43ProxyBase)));

        #endregion

        #region The RFC's signatures verify

        [Test]
        public void The_RSA_PSS_Signature_Of_Section_3_2_Verifies()
        {

            var result = HTTPMessageSignatures.Verify(Message(V.Section32Request), Options("sig1"));

            Assert.That(result.Status, Is.EqualTo(HTTPSignatureVerificationStatus.Valid), result.ToString());

        }

        [TestCase("21", false)]
        [TestCase("22", false)]
        [TestCase("23", false)]
        [TestCase("24", true )]
        [TestCase("25", false)]
        [TestCase("26", false)]
        public void The_Signatures_Of_Appendix_B2_Verify(String Example, Boolean OnTheResponse)
        {

            var fields  = typeof(V).GetField("FieldsB" + Example)!.GetValue(null) as String;
            var result  = HTTPMessageSignatures.Verify(Message(With(OnTheResponse ? V.TestResponse : V.TestRequest, fields!)),
                                                       Options("sig-b" + Example));

            Assert.That(result.Status, Is.EqualTo(HTTPSignatureVerificationStatus.Valid), result.ToString());

        }

        [Test]
        public void The_Signature_Of_The_TLS_Terminating_Proxy_B3_Verifies()
        {

            var result = HTTPMessageSignatures.Verify(Message(V.ProxiedTLSRequest), Options("ttrp"));

            Assert.That(result.Status, Is.EqualTo(HTTPSignatureVerificationStatus.Valid), result.ToString());

        }

        /// <summary>
        /// Appendix B.4: what intermediaries may do, and what they may not.
        /// </summary>
        [TestCase(nameof(V.B4Original),            HTTPSignatureVerificationStatus.Valid)]
        [TestCase(nameof(V.B4AddedQueryAndField),  HTTPSignatureVerificationStatus.Valid)]
        [TestCase(nameof(V.B4FieldsChanged),       HTTPSignatureVerificationStatus.Valid)]
        [TestCase(nameof(V.B4Reordered),           HTTPSignatureVerificationStatus.Valid)]
        [TestCase(nameof(V.B4MethodAndAuthority),  HTTPSignatureVerificationStatus.Invalid)]
        [TestCase(nameof(V.B4AcceptLinesSwapped),  HTTPSignatureVerificationStatus.Invalid)]
        public void The_Transformations_Of_Appendix_B4(String Variant, HTTPSignatureVerificationStatus Expected)
        {

            var header = typeof(V).GetField(Variant)!.GetValue(null) as String;
            var result = HTTPMessageSignatures.Verify(Message(header!), Options("transform"));

            Assert.That(result.Status, Is.EqualTo(Expected), result.ToString());

        }

        [Test]
        public void The_Response_Signatures_Of_Section_2_4_Verify()
        {

            var first   = HTTPMessageSignatures.Verify(Message(V.Section24ResponseA, Request: Message(V.Section24Request)),       Options("reqres"));
            var second  = HTTPMessageSignatures.Verify(Message(V.Section24ResponseB, Request: Message(V.Section24SignedRequest)), Options("reqres"));

            Assert.Multiple(() => {
                Assert.That(first. Status, Is.EqualTo(HTTPSignatureVerificationStatus.Valid), first. ToString());
                Assert.That(second.Status, Is.EqualTo(HTTPSignatureVerificationStatus.Valid), second.ToString());
            });

        }

        /// <summary>
        /// Without the request, a response signature that covers request
        /// components cannot be checked, and is not taken as valid.
        /// </summary>
        [Test]
        public void A_Response_Signature_Over_Request_Components_Needs_The_Request()
        {

            var result = HTTPMessageSignatures.Verify(Message(V.Section24ResponseA), Options("reqres"));

            Assert.That(result.Status, Is.EqualTo(HTTPSignatureVerificationStatus.SignatureBaseError), result.ToString());

        }

        /// <summary>
        /// Section 4.3: the client's signature verifies on the client's request
        /// and not on the one the proxy changed the authority of; the proxy's
        /// verifies on that one, with test-key-rsa and rsa-v1_5-sha256.
        /// </summary>
        [Test]
        public void The_Signatures_Of_Section_4_3()
        {

            var clientOnOriginal  = HTTPMessageSignatures.Verify(Message(V.Section43ClientRequest),   Options("sig1"));
            var clientOnProxied   = HTTPMessageSignatures.Verify(Message(V.Section43ProxiedRequest),  Options("sig1"));
            var proxyOnProxied    = HTTPMessageSignatures.Verify(Message(V.Section43ProxiedRequest),  Options("proxy_sig"));

            Assert.Multiple(() => {
                Assert.That(clientOnOriginal.Status,  Is.EqualTo(HTTPSignatureVerificationStatus.Valid),   clientOnOriginal.ToString());
                Assert.That(clientOnProxied. Status,  Is.EqualTo(HTTPSignatureVerificationStatus.Invalid), clientOnProxied. ToString());
                Assert.That(proxyOnProxied.  Status,  Is.EqualTo(HTTPSignatureVerificationStatus.Valid),   proxyOnProxied.  ToString());
            });

        }

        /// <summary>
        /// Without a label, the first signature that meets the options is the
        /// one: in the proxied request of Section 4.3, sig1 fails and proxy_sig
        /// verifies.
        /// </summary>
        [Test]
        public void Without_A_Label_The_First_Signature_That_Verifies_Is_The_One()
        {

            var result = HTTPMessageSignatures.Verify(Message(V.Section43ProxiedRequest), Options());

            Assert.Multiple(() => {
                Assert.That(result.Status, Is.EqualTo(HTTPSignatureVerificationStatus.Valid), result.ToString());
                Assert.That(result.Label,  Is.EqualTo("proxy_sig"));
            });

        }

        #endregion

        #region The deterministic algorithms sign as the RFC does

        [Test]
        public void HMAC_SHA256_Signs_As_Appendix_B25()
        {

            var header = With(V.TestRequest, V.FieldsB25);

            Assert.That(HTTPMessageSignatures.TrySign(Message(V.TestRequest), "sig-b25", ParametersOf(header, "sig-b25"), SharedSecret, out var signature, out var error), Is.True, error);
            Assert.That(signature!.SignatureMember, Is.EqualTo("sig-b25=:pxcQw6G3AjtMBQjwo8XzkZf/bws5LelbaMk5rGIGtE8=:"));

        }

        [Test]
        public void Ed25519_Signs_As_Appendix_B26()
        {

            var header = With(V.TestRequest, V.FieldsB26);

            Assert.That(HTTPMessageSignatures.TrySign(Message(V.TestRequest), "sig-b26", ParametersOf(header, "sig-b26"), Ed25519Private, out var signature, out var error), Is.True, error);
            Assert.Multiple(() => {
                Assert.That(signature!.SignatureMember,       Is.EqualTo("sig-b26=:wqcAqbmYJ2ji2glfAMaRy4gruYYnx2nEFN2HN6jrnDnQCK1u02Gb04v9EDgwUPiu4A0w6vuQv5lIp5WPpBKRCw==:"));
                Assert.That(signature!.SignatureInputMember,  Is.EqualTo(LF(V.FieldsB26).Split('\n')[0]["Signature-Input: ".Length..]));
            });

        }

        #endregion


        #region HTTP fields (Section 2.1)

        private const String FieldExamples = """
            GET / HTTP/1.1
            Host: www.example.com
            Date: Tue, 20 Apr 2021 02:07:56 GMT
            X-OWS-Header:   Leading and trailing whitespace.
            X-Obs-Fold-Header: Obsolete
                line folding.
            Cache-Control: max-age=60
            Cache-Control:    must-revalidate
            Example-Dict:  a=1,    b=2;x=1;y=2,   c=(a   b   c)
            X-Empty-Header:
            """;

        [TestCase("host",               "www.example.com")]
        [TestCase("date",               "Tue, 20 Apr 2021 02:07:56 GMT")]
        [TestCase("x-ows-header",       "Leading and trailing whitespace.")]
        [TestCase("x-obs-fold-header",  "Obsolete line folding.")]
        [TestCase("cache-control",      "max-age=60, must-revalidate")]
        [TestCase("example-dict",       "a=1,    b=2;x=1;y=2,   c=(a   b   c)")]
        [TestCase("x-empty-header",     "")]
        public void A_Field_Value_Is_Its_Lines_Combined(String Field, String Expected)

            => Assert.That(ValueOf(Message(FieldExamples), C(Field)), Is.EqualTo(Expected));

        [Test]
        public void A_Field_Name_Is_Lower_Case_And_A_Field_Matches_In_Any_Case()
        {

            var component = HTTPSignatureComponent.Field("Cache-Control");

            Assert.Multiple(() => {
                Assert.That(component.Name,                              Is.EqualTo("cache-control"));
                Assert.That(ValueOf(Message(FieldExamples), component),  Is.EqualTo("max-age=60, must-revalidate"));
            });

        }

        private static readonly IReadOnlyDictionary<String, HTTPStructuredFieldType> exampleDict

            = new Dictionary<String, HTTPStructuredFieldType>(StringComparer.OrdinalIgnoreCase) {
                  { "example-dict", HTTPStructuredFieldType.Dictionary }
              };

        [Test]
        public void sf_Is_The_Strict_Serialization_Section_2_1_1()

            => Assert.That(ValueOf(Message(FieldExamples), C("example-dict").AsStructuredField(), exampleDict),
                           Is.EqualTo("a=1, b=2;x=1;y=2, c=(a b c)"));

        [TestCase("a", "1")]
        [TestCase("d", "?1")]
        [TestCase("b", "2;x=1;y=2")]
        [TestCase("c", "(a b c)")]
        public void key_Is_One_Member_Section_2_1_2(String Key, String Expected)
        {

            var message = Message("GET / HTTP/1.1\nExample-Dict:  a=1, b=2;x=1;y=2, c=(a   b    c), d");

            Assert.That(ValueOf(message, C("example-dict").Member(Key), exampleDict), Is.EqualTo(Expected));

        }

        [Test]
        public void bs_Tells_Two_Lines_From_One_Section_2_1_3()
        {

            var twoLines  = Message("GET / HTTP/1.1\nExample-Header: value, with, lots\nExample-Header: of, commas");
            var oneLine   = Message("GET / HTTP/1.1\nExample-Header: value, with, lots, of, commas");

            Assert.Multiple(() => {
                Assert.That(ValueOf(twoLines, C("example-header")),                    Is.EqualTo("value, with, lots, of, commas"));
                Assert.That(ValueOf(oneLine,  C("example-header")),                    Is.EqualTo("value, with, lots, of, commas"));
                Assert.That(ValueOf(twoLines, C("example-header").AsBinaryWrapped()),  Is.EqualTo(":dmFsdWUsIHdpdGgsIGxvdHM=:, :b2YsIGNvbW1hcw==:"));
                Assert.That(ValueOf(oneLine,  C("example-header").AsBinaryWrapped()),  Is.EqualTo(":dmFsdWUsIHdpdGgsIGxvdHMsIG9mLCBjb21tYXM=:"));
            });

        }

        [Test]
        public void tr_Is_From_The_Trailers_Section_2_1_4()
        {

            var message = Message("HTTP/1.1 200 OK\nContent-Type: text/plain\nTransfer-Encoding: chunked\nTrailer: Expires",
                                  Trailers: [ new ("Expires", "Wed, 9 Nov 2022 07:28:00 GMT") ]);

            var parameters = new HTTPSignatureParameters([ C("@status"), C("trailer"), C("expires").FromTrailers() ]);

            Assert.Multiple(() => {
                Assert.That(BaseOf(message, parameters),  Is.EqualTo("\"@status\": 200\n\"trailer\": Expires\n\"expires\";tr: Wed, 9 Nov 2022 07:28:00 GMT\n\"@signature-params\": (\"@status\" \"trailer\" \"expires\";tr)"));
                Assert.That(ErrorOf(message, C("expires")), Does.Contain("not in the response"));
            });

        }

        #endregion

        #region Derived components (Section 2.2)

        [Test]
        public void The_Derived_Components_Of_A_Request()
        {

            var https  = Message("POST /path?param=value HTTP/1.1\nHost: www.example.com", "https");
            var http   = Message("POST /path?param=value HTTP/1.1\nHost: www.example.com", "http");

            Assert.Multiple(() => {
                Assert.That(ValueOf(https, HTTPSignatureComponent.Method),         Is.EqualTo("POST"));
                Assert.That(ValueOf(https, HTTPSignatureComponent.TargetURI),      Is.EqualTo("https://www.example.com/path?param=value"));
                Assert.That(ValueOf(https, HTTPSignatureComponent.Authority),      Is.EqualTo("www.example.com"));
                Assert.That(ValueOf(https, HTTPSignatureComponent.Scheme),         Is.EqualTo("https"));
                Assert.That(ValueOf(http,  HTTPSignatureComponent.Scheme),         Is.EqualTo("http"));
                Assert.That(ValueOf(https, HTTPSignatureComponent.RequestTarget),  Is.EqualTo("/path?param=value"));
                Assert.That(ValueOf(https, HTTPSignatureComponent.Path),           Is.EqualTo("/path"));
                Assert.That(ValueOf(https, HTTPSignatureComponent.Query),          Is.EqualTo("?param=value"));
            });

        }

        /// <summary>
        /// Section 2.2.5: the four forms of the request-target.
        /// </summary>
        [TestCase("GET https://www.example.com/path?param=value HTTP/1.1",  "https://www.example.com/path?param=value")]
        [TestCase("CONNECT www.example.com:80 HTTP/1.1\nHost: www.example.com", "www.example.com:80")]
        [TestCase("OPTIONS * HTTP/1.1\nHost: www.example.com",               "*")]
        [TestCase("GET /path?param=value HTTP/1.1\nHost: www.example.com",   "/path?param=value")]
        public void The_Request_Target_In_Each_Form(String Header, String Expected)

            => Assert.That(ValueOf(Message(Header), HTTPSignatureComponent.RequestTarget), Is.EqualTo(Expected));

        [Test]
        public void The_Absolute_Form_Brings_Its_Own_Scheme_And_Authority()
        {

            var message = Message("GET https://www.example.com/path?param=value HTTP/1.1\nHost: other.example", "http");

            Assert.Multiple(() => {
                Assert.That(ValueOf(message, HTTPSignatureComponent.Scheme),     Is.EqualTo("https"));
                Assert.That(ValueOf(message, HTTPSignatureComponent.Authority),  Is.EqualTo("www.example.com"));
                Assert.That(ValueOf(message, HTTPSignatureComponent.TargetURI),  Is.EqualTo("https://www.example.com/path?param=value"));
                Assert.That(ValueOf(message, HTTPSignatureComponent.Path),       Is.EqualTo("/path"));
            });

        }

        /// <summary>
        /// Section 2.2.3 and RFC 9110, Section 4.2.3: the host in lower case,
        /// the default port left out.
        /// </summary>
        [TestCase("Example.COM",       "https", "example.com")]
        [TestCase("example.com:443",   "https", "example.com")]
        [TestCase("example.com:80",    "http",  "example.com")]
        [TestCase("example.com:80",    "https", "example.com:80")]
        [TestCase("example.com:8443",  "https", "example.com:8443")]
        [TestCase("[2001:DB8::1]:443", "https", "[2001:db8::1]")]
        [TestCase("[2001:db8::1]:8080","https", "[2001:db8::1]:8080")]
        public void The_Authority_Is_Normalized(String Host, String Scheme, String Expected)

            => Assert.That(ValueOf(Message($"GET / HTTP/1.1\nHost: {Host}", Scheme), HTTPSignatureComponent.Authority), Is.EqualTo(Expected));

        [TestCase("GET /path?param=value&foo=bar&baz=bat%2Dman HTTP/1.1", "?param=value&foo=bar&baz=bat%2Dman")]
        [TestCase("POST /path?queryString HTTP/1.1",                      "?queryString")]
        [TestCase("GET /path HTTP/1.1",                                   "?")]
        public void The_Query_Is_Undecoded_With_Its_Question_Mark(String RequestLine, String Expected)

            => Assert.That(ValueOf(Message(RequestLine + "\nHost: www.example.com"), HTTPSignatureComponent.Query), Is.EqualTo(Expected));

        [TestCase("GET / HTTP/1.1", "/")]
        [TestCase("GET /a%2Fb/c HTTP/1.1", "/a%2Fb/c")]
        [TestCase("GET https://www.example.com?x=1 HTTP/1.1", "/")]
        public void The_Path_Is_Undecoded(String RequestLine, String Expected)

            => Assert.That(ValueOf(Message(RequestLine + "\nHost: www.example.com"), HTTPSignatureComponent.Path), Is.EqualTo(Expected));

        [TestCase("baz",   "batman")]
        [TestCase("qux",   "")]
        [TestCase("param", "value")]
        public void A_Query_Parameter_Section_2_2_8(String Name, String Expected)
        {

            var message = Message("GET /path?param=value&foo=bar&baz=batman&qux= HTTP/1.1\nHost: www.example.com");

            Assert.That(ValueOf(message, HTTPSignatureComponent.QueryParam(Name)), Is.EqualTo(Expected));

        }

        [TestCase("var",                   "this%20is%20a%20big%0Amultiline%20value")]
        [TestCase("bar",                   "with%20plus%20whitespace")]
        [TestCase("fa%C3%A7ade%22%3A%20",  "something")]
        public void A_Query_Parameter_Is_Encoded_Again_Section_2_2_8(String Name, String Expected)
        {

            var message = Message("GET /parameters?var=this%20is%20a%20big%0Amultiline%20value&bar=with+plus+whitespace&fa%C3%A7ade%22%3A%20=something HTTP/1.1\nHost: www.example.com");

            Assert.That(ValueOf(message, HTTPSignatureComponent.QueryParam(Name)), Is.EqualTo(Expected));

        }

        [Test]
        public void The_Status_Of_A_Response()

            => Assert.That(ValueOf(Message("HTTP/1.1 200 OK\nDate: Fri, 26 Mar 2010 00:05:00 GMT"), HTTPSignatureComponent.Status), Is.EqualTo("200"));

        #endregion

        #region What fails the base (Section 2.5)

        private static HTTPSignatureMessage Request
            => Message(V.TestRequest);

        private static HTTPSignatureMessage Response
            => Message(V.TestResponse);

        [Test]
        public void A_Component_Covered_Twice()
        {

            // key and sf together are redundant, not wrong (Section 2.1).
            var foo1 = new HTTPSignatureComponent("content-digest", new SFParameters([ new ("key", SFBareItem.String("sha-512")), new ("sf",  SFBareItem.Boolean(true)) ]));
            var foo2 = new HTTPSignatureComponent("content-digest", new SFParameters([ new ("sf",  SFBareItem.Boolean(true)),     new ("key", SFBareItem.String("sha-512")) ]));

            Assert.Multiple(() => {
                Assert.That(ErrorOf(Request, C("@method"), C("@method")),  Does.Contain("covered twice"));
                Assert.That(foo1,                                          Is.EqualTo(foo2), "the order of the parameters does not make two identifiers");
                Assert.That(ErrorOf(Request, foo1, foo2),                  Does.Contain("covered twice"));
            });

        }

        [Test]
        public void The_Signature_Parameters_Covered()
            => Assert.That(ErrorOf(Request, C("@signature-params")), Does.Contain("must not be a covered component"));

        [Test]
        public void An_Unknown_Derived_Component()
            => Assert.That(ErrorOf(Request, C("@foo")), Does.Contain("not understood"));

        [Test]
        public void An_Unknown_Component_Parameter()
            => Assert.That(ErrorOf(Request, new HTTPSignatureComponent("content-type", new SFParameters([ new ("xyz", SFBareItem.Boolean(true)) ]))),
                           Does.Contain("not understood"));

        [Test]
        public void bs_With_sf_Or_key()
        {
            Assert.Multiple(() => {
                Assert.That(ErrorOf(Request, C("content-digest").AsStructuredField().AsBinaryWrapped()),  Does.Contain("incompatible"));
                Assert.That(ErrorOf(Request, C("content-digest").Member("sha-512").AsBinaryWrapped()),    Does.Contain("incompatible"));
            });
        }

        [Test]
        public void A_Parameter_That_Does_Not_Apply()
        {
            Assert.Multiple(() => {
                Assert.That(ErrorOf(Request, C("@method").AsStructuredField()),                                                            Does.Contain("does not apply"));
                Assert.That(ErrorOf(Request, new HTTPSignatureComponent("content-type", new SFParameters([ new ("name", SFBareItem.String("x")) ]))), Does.Contain("@query-param only"));
                Assert.That(ErrorOf(Request, new HTTPSignatureComponent("@query-param")),                                                   Does.Contain("requires the parameter 'name'"));
            });
        }

        [Test]
        public void req_On_A_Request()
            => Assert.That(ErrorOf(Request, C("@method").FromTheRequest()), Does.Contain("must not be used in a signature on a request"));

        [Test]
        public void status_On_A_Request_And_A_Request_Component_On_A_Response()
        {
            Assert.Multiple(() => {
                Assert.That(ErrorOf(Request,  C("@status")),  Does.Contain("must not be used in a request"));
                Assert.That(ErrorOf(Response, C("@method")),  Does.Contain("is derived from a request"));
            });
        }

        [Test]
        public void A_Field_That_Is_Not_There()
            => Assert.That(ErrorOf(Request, C("x-not-there")), Does.Contain("is not in the request"));

        [Test]
        public void A_Dictionary_Member_That_Is_Not_There()
            => Assert.That(ErrorOf(Request, C("content-digest").Member("sha-256")), Does.Contain("has no member 'sha-256'"));

        [Test]
        public void sf_Or_key_On_A_Field_Not_Known_To_Be_Structured()
        {
            Assert.Multiple(() => {
                Assert.That(ErrorOf(Request, C("content-type").AsStructuredField()),  Does.Contain("not known to be a Structured Field"));
                Assert.That(ErrorOf(Request, C("date").Member("a")),                  Does.Contain("not known to be a Structured Field"));
                Assert.That(ErrorOf(Message("GET / HTTP/1.1\nClient-Cert: :AAAA:"), C("client-cert").Member("a")), Does.Contain("not a Dictionary"));
            });
        }

        [Test]
        public void A_Query_Parameter_That_Is_There_Twice_Or_Not_At_All()
        {

            var message = Message("GET /path?a=1&a=2&b=3 HTTP/1.1\nHost: www.example.com");

            Assert.Multiple(() => {
                Assert.That(ErrorOf(message, HTTPSignatureComponent.QueryParam("a")),  Does.Contain("more than once"));
                Assert.That(ErrorOf(message, HTTPSignatureComponent.QueryParam("c")),  Does.Contain("no parameter named 'c'"));
                Assert.That(ValueOf(message, HTTPSignatureComponent.QueryParam("b")),  Is.EqualTo("3"));
            });

        }

        [Test]
        public void A_Path_Of_The_Asterisk_Form()
            => Assert.That(ErrorOf(Message("OPTIONS * HTTP/1.1\nHost: www.example.com"), C("@path")), Does.Contain("has no path"));

        [Test]
        public void A_Value_That_Is_Not_ASCII()
            => Assert.That(ErrorOf(Message("GET / HTTP/1.1\nX-Name: Müller"), C("x-name")), Does.Contain("not printable ASCII"));

        [Test]
        public void Without_A_Scheme_No_Scheme_And_No_Target_URI()
        {

            var message = Message("GET /path HTTP/1.1\nHost: www.example.com", Scheme: null);

            Assert.Multiple(() => {
                Assert.That(ErrorOf(message, C("@scheme")),      Does.Contain("not known"));
                Assert.That(ErrorOf(message, C("@target-uri")),  Does.Contain("not known"));
                Assert.That(ValueOf(message, C("@authority")),   Is.EqualTo("www.example.com"));
            });

        }

        [Test]
        public void Two_Host_Lines_Name_No_Authority()
            => Assert.That(ErrorOf(Message("GET / HTTP/1.1\nHost: a.example\nHost: b.example"), C("@authority")), Does.Contain("no single authority"));

        #endregion

        #region Signature-Input and Signature (Section 4)

        private static HTTPSignatureVerification VerifyB21With(String Fields, HTTPSignatureVerificationOptions? Options = null)

            => HTTPMessageSignatures.Verify(Message(With(V.TestRequest, Fields)), Options ?? MessageSignatureTests.Options());

        private static String B21Input
            => LF(V.FieldsB21).Split('\n')[0];

        private static String B21Signature
            => LF(V.FieldsB21).Split('\n')[1];

        [Test]
        public void No_Signature_Is_Not_Present()
            => Assert.That(HTTPMessageSignatures.Verify(Request, Options()).Status, Is.EqualTo(HTTPSignatureVerificationStatus.NotPresent));

        [Test]
        public void A_Label_In_One_Field_And_Not_The_Other()
        {
            var inputOnly      = VerifyB21With(B21Input);
            var signatureOnly  = VerifyB21With(B21Signature);

            Assert.Multiple(() => {
                Assert.That(inputOnly.    Status, Is.EqualTo(HTTPSignatureVerificationStatus.Malformed));
                Assert.That(inputOnly.    Error,  Does.Contain("is in Signature-Input and not in Signature "));
                Assert.That(signatureOnly.Status, Is.EqualTo(HTTPSignatureVerificationStatus.Malformed));
                Assert.That(signatureOnly.Error,  Does.Contain("is in Signature and not in Signature-Input "));
            });
        }

        /// <summary>
        /// One Dictionary would keep the last of two members with the same key
        /// (RFC 9651, Section 4.2.2), and the first signature would be gone
        /// without a word. On two lines the label is there twice.
        /// </summary>
        [Test]
        public void A_Label_Twice_Across_Two_Lines()
        {

            var result = VerifyB21With(B21Input + "\n" + B21Input + "\n" + B21Signature);

            Assert.Multiple(() => {
                Assert.That(result.Status,  Is.EqualTo(HTTPSignatureVerificationStatus.Malformed));
                Assert.That(result.Error,   Does.Contain("occurs twice"));
            });

        }

        [Test]
        public void Signature_Members_Must_Be_Byte_Sequences()
            => Assert.That(VerifyB21With(B21Input + "\nSignature: sig-b21=\"not bytes\"").Status, Is.EqualTo(HTTPSignatureVerificationStatus.Malformed));

        [Test]
        public void A_Field_Name_In_Upper_Case_In_Signature_Input()
        {

            var result = VerifyB21With("Signature-Input: sig-b21=(\"Content-Type\");created=1618884473;keyid=\"test-key-rsa-pss\"\n" + B21Signature);

            Assert.Multiple(() => {
                Assert.That(result.Status,  Is.EqualTo(HTTPSignatureVerificationStatus.Malformed));
                Assert.That(result.Error,   Does.Contain("lower case"));
            });

        }

        [Test]
        public void created_Must_Be_An_Integer()
            => Assert.That(VerifyB21With("Signature-Input: sig-b21=();created=\"1618884473\";keyid=\"test-key-rsa-pss\"\n" + B21Signature).Status,
                           Is.EqualTo(HTTPSignatureVerificationStatus.Malformed));

        [Test]
        public void A_Changed_Signature_Does_Not_Verify()
        {

            var changed = B21Signature.Replace("sig-b21=:d2pm", "sig-b21=:d2pn");

            Assert.That(VerifyB21With(B21Input + "\n" + changed).Status, Is.EqualTo(HTTPSignatureVerificationStatus.Invalid));

        }

        [Test]
        public void A_Changed_Parameter_Does_Not_Verify()
        {

            var changed = B21Input.Replace("created=1618884473", "created=1618884474");

            Assert.That(VerifyB21With(changed + "\n" + B21Signature).Status, Is.EqualTo(HTTPSignatureVerificationStatus.Invalid));

        }

        #endregion

        #region What an application requires (Section 3.2.1)

        [Test]
        public void A_Component_The_Application_Requires_Is_Not_Covered()
        {

            var result = VerifyB21With(V.FieldsB21, new () { KeyResolver = KnownKey, Now = rfcNow, RequiredComponents = [ HTTPSignatureComponent.Method ] });

            Assert.That(result.Status, Is.EqualTo(HTTPSignatureVerificationStatus.InsufficientCoverage), result.ToString());

        }

        [Test]
        public void The_Required_Components_Covered_In_Another_Order()
        {

            var result = HTTPMessageSignatures.Verify(Message(V.Section32Request),
                                                      new () {
                                                          KeyResolver         = KnownKey,
                                                          Now                 = rfcNow,
                                                          RequiredComponents  = [ C("content-type"), C("content-digest"), HTTPSignatureComponent.Method, HTTPSignatureComponent.Authority ]
                                                      });

            Assert.That(result.Status, Is.EqualTo(HTTPSignatureVerificationStatus.Valid), result.ToString());

        }

        [Test]
        public void created_Required_And_Missing()
        {

            var unsigned = With(V.TestRequest, "Signature-Input: sig=();keyid=\"test-shared-secret\"");

            Assert.That(HTTPMessageSignatures.TrySign(Message(unsigned), "sig", ParametersOf(unsigned, "sig"), SharedSecret, out var signature, out var error), Is.True, error);

            var signed  = With(V.TestRequest, $"Signature-Input: {signature!.SignatureInputMember}\nSignature: {signature.SignatureMember}");

            Assert.Multiple(() => {
                Assert.That(HTTPMessageSignatures.Verify(Message(signed), Options()).Status,                                                                Is.EqualTo(HTTPSignatureVerificationStatus.Valid));
                Assert.That(HTTPMessageSignatures.Verify(Message(signed), new () { KeyResolver = KnownKey, RequireCreated = true }).Status,                  Is.EqualTo(HTTPSignatureVerificationStatus.CreatedMissing));
                Assert.That(HTTPMessageSignatures.Verify(Message(signed), new () { KeyResolver = KnownKey, MaximumAge = TimeSpan.FromMinutes(5) }).Status, Is.EqualTo(HTTPSignatureVerificationStatus.CreatedMissing));
            });

        }

        [Test]
        public void Too_Old_And_In_The_Future()
        {

            Assert.Multiple(() => {

                Assert.That(VerifyB21With(V.FieldsB21, new () { KeyResolver = KnownKey, MaximumAge = TimeSpan.FromMinutes(5) }).Status,
                            Is.EqualTo(HTTPSignatureVerificationStatus.TooOld), "created in 2021, verified now");

                Assert.That(VerifyB21With(V.FieldsB21, new () { KeyResolver = KnownKey, MaximumAge = TimeSpan.FromMinutes(5), Now = rfcNow }).Status,
                            Is.EqualTo(HTTPSignatureVerificationStatus.Valid));

                Assert.That(VerifyB21With(V.FieldsB21, new () { KeyResolver = KnownKey, Now = DateTimeOffset.FromUnixTimeSeconds(1618884473 - 3600) }).Status,
                            Is.EqualTo(HTTPSignatureVerificationStatus.CreatedInTheFuture));

            });

        }

        /// <summary>
        /// Section 4.3's proxy signature expires a minute after it was made.
        /// </summary>
        [Test]
        public void Expired()
        {

            var message = Message(V.Section43ProxiedRequest);

            Assert.Multiple(() => {
                Assert.That(HTTPMessageSignatures.Verify(message, new () { KeyResolver = KnownKey, Label = "proxy_sig", Now = rfcNow }).Status,                                        Is.EqualTo(HTTPSignatureVerificationStatus.Valid));
                Assert.That(HTTPMessageSignatures.Verify(message, new () { KeyResolver = KnownKey, Label = "proxy_sig", Now = DateTimeOffset.FromUnixTimeSeconds(1618884540 + 3600) }).Status, Is.EqualTo(HTTPSignatureVerificationStatus.Expired));
                Assert.That(HTTPMessageSignatures.Verify(message, new () { KeyResolver = KnownKey, Label = "proxy_sig", RejectExpired = false }).Status,                               Is.EqualTo(HTTPSignatureVerificationStatus.Valid));
            });

        }

        [Test]
        public void An_Unknown_Key()
            => Assert.That(VerifyB21With(V.FieldsB21, new () { KeyResolver = _ => null }).Status, Is.EqualTo(HTTPSignatureVerificationStatus.UnknownKey));

        [Test]
        public void An_Algorithm_The_Application_Does_Not_Accept()
            => Assert.That(VerifyB21With(V.FieldsB21, new () { KeyResolver = KnownKey, AllowedAlgorithms = new HashSet<String> { HTTPSignatureAlgorithms.Ed25519 } }).Status,
                           Is.EqualTo(HTTPSignatureVerificationStatus.AlgorithmNotAllowed));

        /// <summary>
        /// Section 3.2, step 6.5: the key is for rsa-pss-sha512, and alg says
        /// otherwise. The verification fails at that step, before the signature
        /// is even looked at.
        /// </summary>
        [Test]
        public void An_alg_That_Is_Not_The_Keys()
        {

            var changed = B21Input.Replace("keyid=\"test-key-rsa-pss\"", "keyid=\"test-key-rsa-pss\";alg=\"rsa-v1_5-sha256\"");

            Assert.That(VerifyB21With(changed + "\n" + B21Signature).Status, Is.EqualTo(HTTPSignatureVerificationStatus.AlgorithmMismatch));

        }

        [Test]
        public void Label_And_Tag_Choose_The_Signature()
        {

            var b22 = Message(With(V.TestRequest, V.FieldsB22));

            Assert.Multiple(() => {
                Assert.That(HTTPMessageSignatures.Verify(b22, new () { KeyResolver = KnownKey, Tag = "header-example" }).Status,  Is.EqualTo(HTTPSignatureVerificationStatus.Valid));
                Assert.That(HTTPMessageSignatures.Verify(b22, new () { KeyResolver = KnownKey, Tag = "other" }).Status,           Is.EqualTo(HTTPSignatureVerificationStatus.NoMatchingSignature));
                Assert.That(HTTPMessageSignatures.Verify(b22, new () { KeyResolver = KnownKey, Label = "nope" }).Status,          Is.EqualTo(HTTPSignatureVerificationStatus.NoMatchingSignature));
            });

        }

        #endregion

        #region Every algorithm, signed and verified

        private static IEnumerable<TestCaseData> EveryAlgorithm()
        {

            yield return new TestCaseData(HTTPSignatureAlgorithms.RSAPSSSHA512)   .SetName("Sign_And_Verify(rsa-pss-sha512)");
            yield return new TestCaseData(HTTPSignatureAlgorithms.RSAV15SHA256)   .SetName("Sign_And_Verify(rsa-v1_5-sha256)");
            yield return new TestCaseData(HTTPSignatureAlgorithms.HMACSHA256)     .SetName("Sign_And_Verify(hmac-sha256)");
            yield return new TestCaseData(HTTPSignatureAlgorithms.ECDSAP256SHA256).SetName("Sign_And_Verify(ecdsa-p256-sha256)");
            yield return new TestCaseData(HTTPSignatureAlgorithms.ECDSAP384SHA384).SetName("Sign_And_Verify(ecdsa-p384-sha384)");
            yield return new TestCaseData(HTTPSignatureAlgorithms.Ed25519)        .SetName("Sign_And_Verify(ed25519)");

        }

        private static (HTTPSignatureKey Signer, HTTPSignatureKey Verifier) KeysFor(String Algorithm)

            => Algorithm switch {
                   HTTPSignatureAlgorithms.RSAPSSSHA512     => (RSAPSSPrivate,  RSAPSSPublic),
                   HTTPSignatureAlgorithms.RSAV15SHA256     => RSAv15(),
                   HTTPSignatureAlgorithms.HMACSHA256       => (SharedSecret,   SharedSecret),
                   HTTPSignatureAlgorithms.ECDSAP256SHA256  => (ECCP256Private, ECCP256Public),
                   HTTPSignatureAlgorithms.ECDSAP384SHA384  => P384(),
                   HTTPSignatureAlgorithms.Ed25519          => (Ed25519Private, Ed25519Public),
                   _                                        => throw new ArgumentException(Algorithm)
               };

        /// <summary>
        /// A key of its own: the private key of test-key-rsa has primes of 1088
        /// and 960 bits, which is a valid RSA key and one the BCL cannot import.
        /// Its public key verifies Section 4.3 above.
        /// </summary>
        private static (HTTPSignatureKey, HTTPSignatureKey) RSAv15()
        {

            var key     = RSA.Create(2048);
            var @public = RSA.Create();
            @public.ImportSubjectPublicKeyInfo(key.ExportSubjectPublicKeyInfo(), out _);

            return (HTTPSignatureKey.RSAPKCS1v15(key, "rsa"), HTTPSignatureKey.RSAPKCS1v15(@public, "rsa"));

        }

        private static (HTTPSignatureKey, HTTPSignatureKey) P384()
        {

            var key     = ECDsa.Create(ECCurve.NamedCurves.nistP384);
            var @public = ECDsa.Create();
            @public.ImportSubjectPublicKeyInfo(key.ExportSubjectPublicKeyInfo(), out _);

            return (HTTPSignatureKey.ECDSA(key, "p384"), HTTPSignatureKey.ECDSA(@public, "p384"));

        }

        [TestCaseSource(nameof(EveryAlgorithm))]
        public void Sign_And_Verify(String Algorithm)
        {

            var (signer, verifier) = KeysFor(Algorithm);

            var parameters = new HTTPSignatureParameters([ HTTPSignatureComponent.Method, HTTPSignatureComponent.Authority, HTTPSignatureComponent.Path, C("content-digest") ],
                                                         Created:    rfcNow,
                                                         KeyId:      signer.KeyId,
                                                         Algorithm:  Algorithm);

            Assert.That(HTTPMessageSignatures.TrySign(Request, "sig", parameters, signer, out var signature, out var error), Is.True, error);

            var signed    = Message(With(V.TestRequest, $"Signature-Input: {signature!.SignatureInputMember}\nSignature: {signature.SignatureMember}"));
            var tampered  = Message(With(V.TestRequest.Replace("/foo?", "/bar?"), $"Signature-Input: {signature.SignatureInputMember}\nSignature: {signature.SignatureMember}"));

            var options   = new HTTPSignatureVerificationOptions { KeyResolver = _ => verifier, Now = rfcNow, RequireCreated = true };

            Assert.Multiple(() => {
                Assert.That(signer.Algorithm,                                         Is.EqualTo(Algorithm));
                Assert.That(HTTPMessageSignatures.Verify(signed,   options).Status,   Is.EqualTo(HTTPSignatureVerificationStatus.Valid));
                Assert.That(HTTPMessageSignatures.Verify(tampered, options).Status,   Is.EqualTo(HTTPSignatureVerificationStatus.Invalid));
            });

        }

        [Test]
        public void A_Public_Key_Does_Not_Sign()
        {

            Assert.Multiple(() => {
                Assert.That(Ed25519Public.CanSign,  Is.False);
                Assert.That(ECCP256Public.CanSign,  Is.False);
                Assert.That(RSAPSSPublic. CanSign,  Is.False);
                Assert.That(HTTPMessageSignatures.TrySign(Request, "sig", new HTTPSignatureParameters([ HTTPSignatureComponent.Method ]), Ed25519Public, out _, out var error), Is.False);
                Assert.That(error, Does.Contain("cannot sign"));
            });

        }

        [Test]
        public void A_Label_Must_Be_An_sf_Key_And_alg_The_Keys()
        {

            Assert.Multiple(() => {
                Assert.That(HTTPMessageSignatures.TrySign(Request, "Sig1", new HTTPSignatureParameters([ HTTPSignatureComponent.Method ]), SharedSecret, out _, out var labelError), Is.False);
                Assert.That(labelError, Does.Contain("not a valid signature label"));
                Assert.That(HTTPMessageSignatures.TrySign(Request, "sig", new HTTPSignatureParameters([ HTTPSignatureComponent.Method ], Created: rfcNow, Algorithm: HTTPSignatureAlgorithms.Ed25519), SharedSecret, out _, out var algError), Is.False);
                Assert.That(algError, Does.Contain("another algorithm"));
            });

        }

        [Test]
        public void An_ECDSA_Key_Off_The_Two_Curves_Is_Refused()
            => Assert.That(() => HTTPSignatureKey.ECDSA(ECDsa.Create(ECCurve.NamedCurves.nistP521)), Throws.ArgumentException);

        [Test]
        public void An_ECDSA_Signature_In_DER_Is_Refused()
        {

            var ecdsa      = ECPEM(V.ECCP256PrivateKeyPEM);
            var data       = "the signature base"u8.ToArray();
            var der        = ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            var p1363      = ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

            Assert.Multiple(() => {
                Assert.That(ECCP256Public.Verify(data, p1363),  Is.True);
                Assert.That(ECCP256Public.Verify(data, der),    Is.False);
            });

        }

        #endregion

        #region A builder signed more than once (Section 4.3)

        [Test]
        public void Two_Signatures_On_One_Response_And_A_Label_Taken()
        {

            var request  = HTTPRequest.TryParse(LF(V.TestRequest).Replace("\n", "\r\n"), out var parsed) ? parsed! : throw new InvalidOperationException();

            var builder  = new HTTPResponse.Builder(request) {
                               HTTPStatusCode  = HTTPStatusCode.OK,
                               ContentType     = HTTPContentType.Text.PLAIN,
                               Content         = "signed twice".ToUTF8Bytes()
                           };

            var first    = new HTTPSignatureParameters([ HTTPSignatureComponent.Status, C("content-type") ],           Created: rfcNow, KeyId: "test-key-ed25519");
            var second   = new HTTPSignatureParameters([ HTTPSignatureComponent.Status, HTTPSignatureComponent.Method.FromTheRequest() ], Created: rfcNow, KeyId: "test-shared-secret");

            Assert.Multiple(() => {

                Assert.That(builder.TrySign("one", first,  Ed25519Private, out var firstError),  Is.True, firstError);
                Assert.That(builder.TrySign("two", second, SharedSecret,   out var secondError), Is.True, secondError);
                Assert.That(builder.TrySign("one", second, SharedSecret,   out var takenError),  Is.False);
                Assert.That(takenError, Does.Contain("already has a signature labeled 'one'"));

                var response = builder.AsImmutable;

                Assert.That(response.VerifySignature(Options("one")).Status, Is.EqualTo(HTTPSignatureVerificationStatus.Valid));
                Assert.That(response.VerifySignature(Options("two")).Status, Is.EqualTo(HTTPSignatureVerificationStatus.Valid));

            });

        }

        #endregion

        #region Accept-Signature (Section 5)

        [Test]
        public void Accept_Signature_As_Section_5_1_Has_It()
        {

            Assert.That(HTTPMessageSignatures.TryParseAcceptSignature(
                            "sig1=(\"@method\" \"@target-uri\" \"@authority\" \"content-digest\" \"cache-control\");keyid=\"test-key-rsa-pss\";created;tag=\"app-123\"",
                            out var requests,
                            out var error),
                        Is.True, error);

            var request = requests!.Single();

            Assert.Multiple(() => {
                Assert.That(request.Label,                                   Is.EqualTo("sig1"));
                Assert.That(request.Components.Select(c => c.Name),          Is.EqualTo(new[] { "@method", "@target-uri", "@authority", "content-digest", "cache-control" }));
                Assert.That(request.Parameters.ContainsKey("created"),       Is.True);
                Assert.That(request.AcceptSignatureMember,                   Is.EqualTo("sig1=(\"@method\" \"@target-uri\" \"@authority\" \"content-digest\" \"cache-control\");keyid=\"test-key-rsa-pss\";created;tag=\"app-123\""));
            });

        }

        [Test]
        public void A_Signature_As_Requested_Section_5_2()
        {

            Assert.That(HTTPMessageSignatures.TryParseAcceptSignature(
                            "sig1=(\"@method\" \"@authority\" \"content-digest\");keyid=\"test-key-rsa-pss\";created;tag=\"app-123\"",
                            out var requests, out _),
                        Is.True);

            var request = requests!.Single();

            Assert.Multiple(() => {

                Assert.That(HTTPMessageSignatures.TrySignAsRequested(Request, request, RSAPSSPrivate, out var signature, out var error, Now: rfcNow), Is.True, error);
                Assert.That(signature!.SignatureInputMember, Is.EqualTo("sig1=(\"@method\" \"@authority\" \"content-digest\");keyid=\"test-key-rsa-pss\";created=1618884480;tag=\"app-123\""));

                var signed = Message(With(V.TestRequest, $"Signature-Input: {signature.SignatureInputMember}\nSignature: {signature.SignatureMember}"));
                Assert.That(HTTPMessageSignatures.Verify(signed, Options("sig1")).Status, Is.EqualTo(HTTPSignatureVerificationStatus.Valid));

                Assert.That(HTTPMessageSignatures.TrySignAsRequested(Request, request, Ed25519Private, out _, out var keyError), Is.False, "the key asked for is another");
                Assert.That(keyError, Does.Contain("test-key-rsa-pss"));

            });

        }

        [Test]
        public void A_Requested_Parameter_That_Cannot_Be_Fulfilled()
        {

            Assert.That(HTTPMessageSignatures.TryParseAcceptSignature("sig1=(\"@method\");expires;unknown=1", out var requests, out _), Is.True);

            Assert.Multiple(() => {
                Assert.That(HTTPMessageSignatures.TrySignAsRequested(Request, requests!.Single(), SharedSecret, out _, out var noLifetime), Is.False);
                Assert.That(noLifetime, Does.Contain("no lifetime"));
                Assert.That(HTTPMessageSignatures.TrySignAsRequested(Request, requests!.Single(), SharedSecret, out _, out var unknown, Lifetime: TimeSpan.FromMinutes(1)), Is.False);
                Assert.That(unknown, Does.Contain("not understood"));
            });

        }

        #endregion


        #region On the wire, with HTTP/1.1

        private HTTPServer? server;

        [TearDown]
        public async Task TearDown()
        {

            if (server is not null)
                await server.DisposeAsync();

            server = null;

        }

        private static readonly String compressibleText = String.Concat(Enumerable.Repeat("Signed content compresses well, and must not be compressed. ", 100));

        /// <summary>
        /// A server whose GET /demo answers with the outcome of verifying the
        /// request's signature, and whose GET /signed answers with a signed
        /// response, compressible and over the compression threshold.
        /// </summary>
        private HTTPServer StartServer()
        {

            server = new HTTPServer(
                         TCPPort:    IPPort.Zero,
                         AutoStart:  true
                     ) {
                         AutomaticContentCompression = true
                     };

            var api = new HTTPAPI(server);

            api.AddHandler(HTTPPath.Root + "demo",
                           HTTPMethod:    HTTPMethod.GET,
                           HTTPDelegate:  request => {

                                              var result = request.VerifySignature(new HTTPSignatureVerificationOptions {
                                                               KeyResolver         = KnownKey,
                                                               RequiredComponents  = [ HTTPSignatureComponent.Method, HTTPSignatureComponent.Authority ]
                                                           });

                                              return Task.FromResult(
                                                         new HTTPResponse.Builder(request) {
                                                             HTTPStatusCode  = HTTPStatusCode.OK,
                                                             ContentType     = HTTPContentType.Text.PLAIN,
                                                             Content         = result.ToString().ToUTF8Bytes(),
                                                             Connection      = ConnectionType.Close
                                                         }.AsImmutable);

                                          });

            foreach (var signed in new[] { true, false })
            {
                api.AddHandler(HTTPPath.Root + (signed ? "signed" : "unsigned"),
                               HTTPMethod:    HTTPMethod.GET,
                               HTTPDelegate:  request => {

                                                  var builder = new HTTPResponse.Builder(request) {
                                                                    HTTPStatusCode  = HTTPStatusCode.OK,
                                                                    ContentType     = HTTPContentType.Text.PLAIN,
                                                                    Content         = compressibleText.ToUTF8Bytes(),
                                                                    Connection      = ConnectionType.Close
                                                                };

                                                  if (signed &&
                                                     !builder.TrySign("res",
                                                                      new HTTPSignatureParameters([ HTTPSignatureComponent.Status, C("content-type"), C("content-length"),
                                                                                                    HTTPSignatureComponent.Method.FromTheRequest(), HTTPSignatureComponent.Path.FromTheRequest(),
                                                                                                    HTTPSignatureComponent.Authority.FromTheRequest() ],
                                                                                                  Created: DateTimeOffset.UtcNow,
                                                                                                  KeyId:   "test-key-ed25519"),
                                                                      Ed25519Private,
                                                                      out var error))
                                                      throw new InvalidOperationException(error);

                                                  return Task.FromResult(builder.AsImmutable);

                                              });
            }

            return server;

        }

        /// <summary>
        /// The server reads the components from the request as it arrived: the
        /// two Accept lines of Appendix B.4 are two lines, and in the order they
        /// came. Sent over a socket, as the RFC writes them.
        /// </summary>
        [TestCase(nameof(V.B4Original),           "Valid (transform)")]
        [TestCase(nameof(V.B4AcceptLinesSwapped), "Invalid (transform)")]
        public async Task The_Server_Verifies_A_Request_As_It_Arrived(String Variant, String Expected)
        {

            var server  = StartServer();
            var header  = (typeof(V).GetField(Variant)!.GetValue(null) as String)!.ReplaceLineEndings("\r\n");

            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

            await using var stream = client.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes(header + "\r\nConnection: close\r\n\r\n"));

            using var reader  = new StreamReader(stream, Encoding.ASCII);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response      = await reader.ReadToEndAsync(timeout.Token);

            Assert.Multiple(() => {
                Assert.That(response, Does.StartWith("HTTP/1.1 200"));
                Assert.That(response, Does.Contain(Expected));
            });

        }

        /// <summary>
        /// The client signs as its builder makes the request, and the server
        /// verifies what arrived; with an authority the client did not sign,
        /// it does not verify.
        /// </summary>
        [Test]
        public async Task A_Client_Signs_And_The_Server_Verifies()
        {

            var server = StartServer();

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{server.TCPPort}"));

            String? signError = null;

            var response = await client.RunRequest(
                                     HTTPMethod.GET,
                                     HTTPPath.Root + "demo",
                                     QueryString:     QueryString.Parse("a=1&b=two%20words"),
                                     RequestBuilder:  builder => {
                                                          if (!builder.TrySign("client",
                                                                               new HTTPSignatureParameters([ HTTPSignatureComponent.Method, HTTPSignatureComponent.TargetURI,
                                                                                                             HTTPSignatureComponent.Authority, HTTPSignatureComponent.Path,
                                                                                                             HTTPSignatureComponent.QueryParam("b"), C("user-agent") ],
                                                                                                           Created: DateTimeOffset.UtcNow,
                                                                                                           KeyId:   "test-key-ed25519"),
                                                                               Ed25519Private,
                                                                               out signError,
                                                                               Scheme: "http"))
                                                              throw new InvalidOperationException(signError);
                                                      },
                                     RequestTimeout:  TimeSpan.FromSeconds(10)
                                 );

            Assert.Multiple(() => {
                Assert.That(signError,                      Is.Null);
                Assert.That(response.HTTPStatusCode,        Is.EqualTo(HTTPStatusCode.OK), response.EntirePDU);
                Assert.That(response.HTTPBodyAsUTF8String,  Is.EqualTo("Valid (client)"));
            });

        }

        /// <summary>
        /// The server signs a response, with components of the request it
        /// answers, and the client verifies it against the request it sent. The
        /// server compresses an unsigned response of the same content and not
        /// the signed one: compressing would change Content-Length, which the
        /// signature covers.
        /// </summary>
        [Test]
        public async Task The_Server_Signs_And_Does_Not_Compress_What_It_Signed()
        {

            var server = StartServer();

            using var client = new HTTPClient(URL.Parse($"http://127.0.0.1:{server.TCPPort}"));

            async Task<HTTPResponse> Get(String Path)

                => await client.RunRequest(HTTPMethod.GET,
                                           HTTPPath.Parse(Path),
                                           RequestBuilder:  builder => builder.AcceptEncoding = "gzip",
                                           RequestTimeout:  TimeSpan.FromSeconds(10));

            var unsigned  = await Get("/unsigned");
            var signed    = await Get("/signed");

            var result    = signed.VerifySignature(new HTTPSignatureVerificationOptions {
                                KeyResolver         = KnownKey,
                                MaximumAge          = TimeSpan.FromMinutes(5),
                                RequiredComponents  = [ HTTPSignatureComponent.Status, C("content-length"), HTTPSignatureComponent.Path.FromTheRequest() ]
                            });

            Assert.Multiple(() => {
                Assert.That(unsigned.ContentEncoding,  Is.EqualTo(new[] { "gzip" }), "the calibration: the same content unsigned is compressed");
                Assert.That(signed.  ContentEncoding,  Is.Empty,                     signed.EntirePDU);
                Assert.That(signed.  HTTPBodyAsUTF8String, Is.EqualTo(compressibleText));
                Assert.That(result.Status,             Is.EqualTo(HTTPSignatureVerificationStatus.Valid), result.ToString());
            });

        }

        #endregion

    }

}
