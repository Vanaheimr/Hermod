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
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.ACME
{

    /// <summary>
    /// A small ACME certificate authority for the tests, on a Hermod HTTP
    /// server on 127.0.0.1: enough of RFC 8555 to issue certificates, and
    /// strict where a client could get it wrong.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Strict:</b> every request is a JWS whose ES256 signature is checked
    /// against the account's key, whose nonce must be one handed out and not
    /// used before, and whose "url" must be the URL it was sent to. A "jwk"
    /// is taken for a new account only, a "kid" for everything else. A
    /// certificate signing request must be signed by its key and name exactly
    /// the identifiers of the order.
    /// </para>
    /// <para>
    /// <b>Challenges are really looked at:</b> http-01 by a GET to
    /// <see cref="HTTP01Port"/> on 127.0.0.1 with the identifier as Host, as a
    /// certificate authority would on port 80; dns-01 in <see cref="TXTRecords"/>,
    /// the zone the tests' dns-01 delegates write to. Both asynchronously, as
    /// a real one does, so that the client has to wait for the verdict.
    /// </para>
    /// <para>
    /// The certificates come from an intermediate under a root of its own,
    /// with an authority key identifier, and with renewal information
    /// (RFC 9773) where <see cref="OfferRenewalInfo"/> says.
    /// </para>
    /// </remarks>
    public sealed class TestACMEServer : IAsyncDisposable
    {

        #region Data

        private readonly HTTPServer                                    httpServer;
        private readonly HttpClient                                    validationClient  = new (new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
        private readonly ConcurrentDictionary<String, Byte>            nonces            = new ();
        private readonly ConcurrentDictionary<String, Account>         accounts          = new ();
        private readonly ConcurrentDictionary<String, Order>           orders            = new ();
        private readonly ConcurrentDictionary<String, Authorization>   authorizations    = new ();
        private readonly ConcurrentDictionary<String, Challenge>       challenges        = new ();
        private readonly ConcurrentDictionary<String, Issued>          certificates      = new ();
        private          Int32                                         counter;

        private readonly ECDsa             rootKey;
        private readonly ECDsa             intermediateKey;

        #endregion

        #region Properties

        /// <summary>Where the directory is.</summary>
        public Uri                                        DirectoryURL           { get; }

        /// <summary>The root of the certificates issued.</summary>
        public X509Certificate2                           Root                   { get; }

        /// <summary>The intermediate that signs them.</summary>
        public X509Certificate2                           Intermediate           { get; }

        /// <summary>Where http-01 answers are asked for: the port of the tests' "port 80".</summary>
        public UInt16                                     HTTP01Port             { get; set; }

        /// <summary>The zone dns-01 answers are looked for in: name to values.</summary>
        public ConcurrentDictionary<String, ConcurrentDictionary<String, Byte>>  TXTRecords  { get; } = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>How long issued certificates hold.</summary>
        public TimeSpan                                   CertificateLifetime    { get; set; } = TimeSpan.FromDays(90);

        /// <summary>Whether the directory names renewal information (RFC 9773).</summary>
        public Boolean                                    OfferRenewalInfo       { get; set; } = true;

        /// <summary>The thumbprints of the certificates whose window has been moved into the past - as before a revocation.</summary>
        public ConcurrentDictionary<String, Byte>          DueAtOnce              { get; } = new ();

        /// <summary>The MAC key of the external account binding asked for, by key identifier; none asked for when null.</summary>
        public (String KeyId, Byte[] Key)?                ExternalAccountBinding { get; set; }

        /// <summary>How many of the next nonces, good as they are, are refused as bad.</summary>
        public Int32                                      RefuseNextNonces;

        /// <summary>How many nonces were refused as bad.</summary>
        public Int32                                      NoncesRefused;

        /// <summary>How many orders were placed.</summary>
        public Int32                                      Orders;

        /// <summary>The "replaces" of every order that had one.</summary>
        public ConcurrentQueue<String>                    Replaces               { get; } = new ();

        /// <summary>What was validated how: "http-01 example.org", "dns-01 *.example.org".</summary>
        public ConcurrentQueue<String>                    Validations            { get; } = new ();

        /// <summary>The ARI identifiers of the certificates issued, as the authority sees them.</summary>
        public ConcurrentDictionary<String, String>       CertificateIdentifiers { get; } = new ();

        #endregion

        #region Constructor(s)

        private TestACMEServer(HTTPServer HTTPServer)
        {

            this.httpServer    = HTTPServer;
            this.DirectoryURL  = new Uri($"{BaseURL}/directory");

            rootKey            = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            intermediateKey    = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            var rootRequest    = new CertificateRequest("CN=Hermod Test ACME Root", rootKey, HashAlgorithmName.SHA256);
            rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            rootRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));
            Root               = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));

            var intermediateRequest = new CertificateRequest("CN=Hermod Test ACME Intermediate", intermediateKey, HashAlgorithmName.SHA256);
            intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
            intermediateRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(intermediateRequest.PublicKey, false));
            intermediateRequest.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(Root, true, false));
            Intermediate       = intermediateRequest.Create(Root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5), [ 0x01, 0x02 ]).CopyWithPrivateKey(intermediateKey);

            Routes();

        }

        #endregion

        #region (static) StartAsync()

        /// <summary>
        /// A new certificate authority, listening.
        /// </summary>
        public static Task<TestACMEServer> StartAsync()

            => Task.FromResult(
                   new TestACMEServer(
                       new HTTPServer(
                           IPAddress:  IPv4Address.Localhost,
                           TCPPort:    IPPort.Zero,
                           AutoStart:  true
                       )
                   )
               );

        #endregion


        private String BaseURL
            => $"http://127.0.0.1:{httpServer.TCPPort}";

        private String Next()
            => Interlocked.Increment(ref counter).ToString();


        #region (private) Routes()

        private void Routes()
        {

            httpServer.AddMethodCallback(HTTPHostname.Any, HTTPMethod.GET,  HTTPPath.Parse("/directory"),
                                         HTTPDelegate: request => Task.FromResult(Json(request, HTTPStatusCode.OK, Directory())));

            httpServer.AddMethodCallback(HTTPHostname.Any, HTTPMethod.HEAD, HTTPPath.Parse("/new-nonce"),
                                         HTTPDelegate: request => Task.FromResult(Plain(request, HTTPStatusCode.OK)));

            httpServer.AddMethodCallback(HTTPHostname.Any, HTTPMethod.GET,  HTTPPath.Parse("/new-nonce"),
                                         HTTPDelegate: request => Task.FromResult(Plain(request, HTTPStatusCode.NoContent)));

            httpServer.AddMethodCallback(HTTPHostname.Any, HTTPMethod.GET,  HTTPPath.Parse("/renewal-info/{id}"),
                                         HTTPDelegate: request => Task.FromResult(RenewalInfo(request, request.ParsedURLParametersX["id"])));

            Post("/new-account",     (request, jws)     => NewAccount(request, jws), AllowJWK: true);
            Post("/new-order",       (request, jws)     => NewOrder  (request, jws));
            Post("/order/{id}",      (request, jws, id) => Json(request, HTTPStatusCode.OK, OrderJSON(Owned(orders, id, jws))));
            Post("/authz/{id}",      (request, jws, id) => Json(request, HTTPStatusCode.OK, AuthorizationJSON(Owned(authorizations, id, jws))));
            Post("/chall/{id}",      (request, jws, id) => RespondToChallenge(request, jws, id));
            Post("/finalize/{id}",   (request, jws, id) => Finalize(request, jws, id));
            Post("/cert/{id}",       (request, jws, id) => Certificate(request, jws, id));

        }

        private void Post(String Path, Func<HTTPRequest, VerifiedJWS, HTTPResponse> Handler, Boolean AllowJWK = false)

            => httpServer.AddMethodCallback(HTTPHostname.Any, HTTPMethod.POST, HTTPPath.Parse(Path),
                                            HTTPDelegate: request => Task.FromResult(Handle(request, AllowJWK, Handler)));

        private void Post(String Path, Func<HTTPRequest, VerifiedJWS, String, HTTPResponse> Handler)

            => Post(Path, (request, jws) => Handler(request, jws, request.ParsedURLParametersX["id"]));

        #endregion

        #region (private) Handle(Request, AllowJWK, Handler)

        private HTTPResponse Handle(HTTPRequest                                   Request,
                                    Boolean                                       AllowJWK,
                                    Func<HTTPRequest, VerifiedJWS, HTTPResponse>  Handler)
        {
            try
            {
                return Handler(Request, Verify(Request, AllowJWK));
            }
            catch (ProblemException problem)
            {
                return Problem(Request, problem.Status, problem.Type, problem.Message);
            }
        }

        #endregion

        #region (private) Verify(Request, AllowJWK)

        /// <summary>
        /// Check a request as RFC 8555, section 6.2 asks: a flattened JWS, ES256,
        /// a fresh nonce, the URL it was sent to, a "jwk" only where it may be,
        /// and a signature by the key of the account.
        /// </summary>
        private VerifiedJWS Verify(HTTPRequest  Request,
                                   Boolean      AllowJWK)
        {

            if (Request.ContentType?.ToString().StartsWith("application/jose+json", StringComparison.Ordinal) != true)
                throw new ProblemException(415, "malformed", $"Content-Type is '{Request.ContentType}', not application/jose+json.");

            var jws        = JsonConvert.DeserializeObject<JObject>(Request.HTTPBodyAsUTF8String ?? "")
                                 ?? throw new ProblemException(400, "malformed", "No JWS.");

            var protected_ = jws["protected"]?.Value<String>() ?? throw new ProblemException(400, "malformed", "No protected header.");
            var payload    = jws["payload"]  ?.Value<String>() ?? throw new ProblemException(400, "malformed", "No payload.");
            var signature  = Base64Url.DecodeFromChars(jws["signature"]?.Value<String>() ?? "");
            var header     = JObject.Parse(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(protected_)));

            if (header["alg"]?.Value<String>() != "ES256")
                throw new ProblemException(400, "badSignatureAlgorithm", "ES256 only.");

            var nonce      = header["nonce"]?.Value<String>() ?? "";

            if (!nonces.TryRemove(nonce, out _))
                throw new ProblemException(400, "badNonce", $"Nonce '{nonce}' was never handed out, or used already.");

            if (Interlocked.Decrement(ref RefuseNextNonces) >= 0)
            {
                Interlocked.Increment(ref NoncesRefused);
                throw new ProblemException(400, "badNonce", "Nonce refused for the test.");
            }

            var url        = $"{BaseURL}{Request.Path}";

            if (header["url"]?.Value<String>() != url)
                throw new ProblemException(401, "unauthorized", $"The JWS is for '{header["url"]}', not for '{url}'.");

            Account? account = null;
            JObject  jwk;

            if (header["jwk"] is JObject givenJWK)
            {
                if (!AllowJWK)
                    throw new ProblemException(400, "malformed", "A jwk where a kid belongs.");
                if (header["kid"] is not null)
                    throw new ProblemException(400, "malformed", "Both jwk and kid.");
                jwk = givenJWK;
            }
            else
            {
                if (AllowJWK)
                    throw new ProblemException(400, "malformed", "A kid where a jwk belongs.");
                account = accounts.Values.FirstOrDefault(candidate => candidate.URL == header["kid"]?.Value<String>())
                              ?? throw new ProblemException(400, "accountDoesNotExist", $"No account '{header["kid"]}'.");
                jwk     = account.JWK;
            }

            using var key = KeyOf(jwk);

            if (!key.VerifyData(Encoding.ASCII.GetBytes($"{protected_}.{payload}"), signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                throw new ProblemException(400, "malformed", "The signature does not check.");

            return new VerifiedJWS(
                       jwk,
                       account,
                       payload.Length == 0 ? null : JObject.Parse(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(payload)))
                   );

        }

        private static ECDsa KeyOf(JObject JWK)
        {

            if (JWK["kty"]?.Value<String>() != "EC" || JWK["crv"]?.Value<String>() != "P-256")
                throw new ProblemException(400, "badPublicKey", "P-256 only.");

            return ECDsa.Create(new ECParameters {
                       Curve  = ECCurve.NamedCurves.nistP256,
                       Q      = new ECPoint {
                                    X = Base64Url.DecodeFromChars(JWK["x"]!.Value<String>()!),
                                    Y = Base64Url.DecodeFromChars(JWK["y"]!.Value<String>()!)
                                }
                   });

        }

        private static String ThumbprintOf(JObject JWK)

            => Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(
                   new JObject(
                       new JProperty("crv", JWK["crv"]),
                       new JProperty("kty", JWK["kty"]),
                       new JProperty("x",   JWK["x"]),
                       new JProperty("y",   JWK["y"])
                   ).ToString(Formatting.None))));

        #endregion


        #region (private) NewAccount(Request, JWS)

        private HTTPResponse NewAccount(HTTPRequest  Request,
                                        VerifiedJWS  JWS)
        {

            var thumbprint = ThumbprintOf(JWS.JWK);

            if (accounts.TryGetValue(thumbprint, out var existing))
                return Json(Request, HTTPStatusCode.OK, existing.ToJSON(), existing.URL);

            if (JWS.Payload?["onlyReturnExisting"]?.Value<Boolean>() == true)
                throw new ProblemException(400, "accountDoesNotExist", "No account for this key.");

            if (JWS.Payload?["termsOfServiceAgreed"]?.Value<Boolean>() != true)
                throw new ProblemException(400, "malformed", "The terms of service are to be agreed to.");

            if (ExternalAccountBinding is (String keyId, Byte[] macKey))
            {

                var binding    = JWS.Payload?["externalAccountBinding"] as JObject
                                     ?? throw new ProblemException(400, "externalAccountRequired", "An external account binding is required.");

                var protected_ = binding["protected"]!.Value<String>()!;
                var payload    = binding["payload"]!.Value<String>()!;
                var header     = JObject.Parse(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(protected_)));

                if (header["alg"]?.Value<String>() != "HS256" ||
                    header["kid"]?.Value<String>() != keyId ||
                    header["url"]?.Value<String>() != $"{BaseURL}/new-account")
                    throw new ProblemException(400, "malformed", "The external account binding's header is not right.");

                if (!CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(macKey, Encoding.ASCII.GetBytes($"{protected_}.{payload}")),
                                                             Base64Url.DecodeFromChars(binding["signature"]!.Value<String>()!)))
                    throw new ProblemException(400, "unauthorized", "The external account binding's MAC does not check.");

                if (ThumbprintOf(JObject.Parse(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(payload)))) != thumbprint)
                    throw new ProblemException(400, "malformed", "The external account binding binds another key.");

            }

            var account = new Account(
                              $"{BaseURL}/acct/{Next()}",
                              JWS.JWK,
                              (JWS.Payload?["contact"] as JArray)?.Select(contact => contact.Value<String>()!).ToArray() ?? []
                          );

            accounts[thumbprint] = account;

            return Json(Request, HTTPStatusCode.Created, account.ToJSON(), account.URL);

        }

        #endregion

        #region (private) NewOrder(Request, JWS)

        private HTTPResponse NewOrder(HTTPRequest  Request,
                                      VerifiedJWS  JWS)
        {

            var identifiers = (JWS.Payload?["identifiers"] as JArray)?.OfType<JObject>().
                                  Select(identifier => (Type: identifier["type"]!.Value<String>()!, Value: identifier["value"]!.Value<String>()!)).
                                  ToArray() ?? [];

            if (identifiers.Length == 0)
                throw new ProblemException(400, "malformed", "No identifiers.");

            if (JWS.Payload?["replaces"]?.Value<String>() is String replaces)
            {
                if (!CertificateIdentifiers.Values.Contains(replaces))
                    throw new ProblemException(409, "conflict", $"'{replaces}' is no certificate of this authority.");
                Replaces.Enqueue(replaces);
            }

            Interlocked.Increment(ref Orders);

            var order = new Order(Next(), JWS.Account!, identifiers, JWS.Payload?["profile"]?.Value<String>());

            foreach (var (type, value) in identifiers)
            {

                var wildcard       = value.StartsWith("*.", StringComparison.Ordinal);
                var authorization  = new Authorization(Next(), JWS.Account!, (type, wildcard ? value[2..] : value), wildcard);
                var token          = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

                // As Let's Encrypt: no http-01 for a wildcard, no dns-01 for an address.
                if (!wildcard)
                    authorization.Challenges.Add(challenges[$"{authorization.Id}h"] = new Challenge($"{authorization.Id}h", authorization, "http-01", token));

                if (type == "dns")
                    authorization.Challenges.Add(challenges[$"{authorization.Id}d"] = new Challenge($"{authorization.Id}d", authorization, "dns-01",  token));

                authorizations[authorization.Id] = authorization;
                order.Authorizations.Add(authorization);

            }

            orders[order.Id] = order;

            return Json(Request, HTTPStatusCode.Created, OrderJSON(order), $"{BaseURL}/order/{order.Id}");

        }

        #endregion

        #region (private) RespondToChallenge(Request, JWS, Id)

        private HTTPResponse RespondToChallenge(HTTPRequest  Request,
                                                VerifiedJWS  JWS,
                                                String       Id)
        {

            var challenge = Owned(challenges, Id, JWS);

            if (JWS.Payload is null)
                return Json(Request, HTTPStatusCode.OK, ChallengeJSON(challenge));

            lock (challenge.Authorization)
            {

                if (challenge.Status != "pending" || challenge.Authorization.Status != "pending")
                    return Json(Request, HTTPStatusCode.OK, ChallengeJSON(challenge));

                challenge.Status = "processing";

            }

            var keyAuthorization = $"{challenge.Token}.{ThumbprintOf(JWS.Account!.JWK)}";

            _ = Task.Run(async () => {

                // A moment later, as a real authority would: the client has to wait.
                await Task.Delay(150);

                String? error = null;

                try
                {

                    if (challenge.Type == "http-01")
                    {

                        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{HTTP01Port}/.well-known/acme-challenge/{challenge.Token}");
                        request.Headers.Host = challenge.Authorization.Identifier.Value;

                        using var response = await validationClient.SendAsync(request);
                        var       body     = (await response.Content.ReadAsStringAsync()).Trim();

                        if (!response.IsSuccessStatusCode)
                            error = $"http-01: HTTP {(Int32) response.StatusCode} from {challenge.Authorization.Identifier.Value}";
                        else if (body != keyAuthorization)
                            error = $"http-01: '{body}' is not the key authorization";

                    }
                    else
                    {

                        var expected = Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(keyAuthorization)));

                        if (!TXTRecords.TryGetValue($"_acme-challenge.{challenge.Authorization.Identifier.Value}", out var values) ||
                            !values.ContainsKey(expected))
                            error = $"dns-01: no TXT record '{expected}' at _acme-challenge.{challenge.Authorization.Identifier.Value}";

                    }

                }
                catch (Exception e)
                {
                    error = $"{challenge.Type}: {e.Message}";
                }

                lock (challenge.Authorization)
                {

                    challenge.Status               = error is null ? "valid" : "invalid";
                    challenge.Error                = error;
                    challenge.Authorization.Status = challenge.Status;

                    if (error is null)
                        Validations.Enqueue($"{challenge.Type} {(challenge.Authorization.Wildcard ? "*." : "")}{challenge.Authorization.Identifier.Value}");

                }

            });

            return Json(Request, HTTPStatusCode.OK, ChallengeJSON(challenge));

        }

        #endregion

        #region (private) Finalize(Request, JWS, Id)

        private HTTPResponse Finalize(HTTPRequest  Request,
                                      VerifiedJWS  JWS,
                                      String       Id)
        {

            var order = Owned(orders, Id, JWS);

            if (StatusOf(order) != "ready")
                throw new ProblemException(403, "orderNotReady", $"The order is {StatusOf(order)}.");

            var csr = CertificateRequest.LoadSigningRequest(
                          Base64Url.DecodeFromChars(JWS.Payload?["csr"]?.Value<String>() ?? ""),
                          HashAlgorithmName.SHA256,
                          CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions
                      );

            var named = csr.CertificateExtensions.OfType<X509SubjectAlternativeNameExtension>().
                            SelectMany(names => names.EnumerateDnsNames().  Select(name    => (Type: "dns", Value: name)).
                                       Concat(names.EnumerateIPAddresses().Select(address => (Type: "ip",  Value: address.ToString())))).
                            ToHashSet();

            if (!named.SetEquals(order.Identifiers))
                throw new ProblemException(400, "badCSR", $"The CSR names {String.Join(", ", named)}, the order {String.Join(", ", order.Identifiers)}.");

            order.Finalized = true;

            _ = Task.Run(async () => {

                await Task.Delay(150);

                var request = new CertificateRequest(csr.SubjectName, csr.PublicKey, HashAlgorithmName.SHA256);

                foreach (var extension in csr.CertificateExtensions)
                    request.CertificateExtensions.Add(extension);

                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ new Oid("1.3.6.1.5.5.7.3.1") ], false));
                request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(Intermediate, true, false));

                // A serial number whose first octet has its high bit set: its DER
                // encoding gets a leading zero, which belongs in the ARI identifier.
                var serial    = RandomNumberGenerator.GetBytes(16);
                serial[0]    |= 0x80;

                var notBefore = DateTimeOffset.UtcNow.AddMinutes(-1);
                var leaf      = request.Create(Intermediate, notBefore, notBefore + CertificateLifetime, serial);
                var issued    = new Issued(Next(), JWS.Account!, leaf,
                                           leaf.ExportCertificatePem() + "\n" + Intermediate.ExportCertificatePem() + "\n");

                // The authority's own reckoning of the ARI identifier: the key
                // identifier of the intermediate, and the serial number as encoded.
                var keyIdentifier = new X509SubjectKeyIdentifierExtension(
                                        new AsnEncodedData(Intermediate.Extensions.First(extension => extension.Oid?.Value == "2.5.29.14")), false
                                    ).SubjectKeyIdentifierBytes;

                CertificateIdentifiers[issued.Id] = $"{Base64Url.EncodeToString(keyIdentifier.Span)}.{Base64Url.EncodeToString([ 0x00, .. serial ])}";
                certificates[issued.Id]           = issued;
                order.CertificateId               = issued.Id;

            });

            return Json(Request, HTTPStatusCode.OK, OrderJSON(order));

        }

        #endregion

        #region (private) Certificate(Request, JWS, Id)

        private HTTPResponse Certificate(HTTPRequest  Request,
                                         VerifiedJWS  JWS,
                                         String       Id)
        {

            if (Request.Accept.ToString().Contains("application/pem-certificate-chain") != true)
                throw new ProblemException(406, "malformed", "Accept application/pem-certificate-chain.");

            var issued = Owned(certificates, Id, JWS);

            return new HTTPResponse.Builder(Request) {
                       HTTPStatusCode  = HTTPStatusCode.OK,
                       ContentType     = ContentType("application/pem-certificate-chain"),
                       Content         = Encoding.ASCII.GetBytes(issued.Pem)
                   }.Set("Replay-Nonce", NewNonce()).AsImmutable;

        }

        #endregion

        #region (private) RenewalInfo(Request, Id)

        private HTTPResponse RenewalInfo(HTTPRequest  Request,
                                         String       Id)
        {

            var issued = certificates.Values.FirstOrDefault(certificate => CertificateIdentifiers.TryGetValue(certificate.Id, out var id) && id == Id);

            if (issued is null)
                return Problem(Request, 404, "malformed", $"No certificate '{Id}'.");

            var (start, end) = DueAtOnce.ContainsKey(issued.Certificate.Thumbprint)
                                   ? (DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1))
                                   : (new DateTimeOffset(issued.Certificate.NotAfter.ToUniversalTime()) - CertificateLifetime / 3,
                                      new DateTimeOffset(issued.Certificate.NotAfter.ToUniversalTime()) - CertificateLifetime / 4);

            return Json(Request, HTTPStatusCode.OK,
                        new JObject(
                            new JProperty("suggestedWindow", new JObject(
                                new JProperty("start", start.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                                new JProperty("end",   end.  UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                            ))
                        ));

        }

        #endregion


        #region (private) JSON of the resources

        private JObject Directory()
        {

            var directory = new JObject(
                                new JProperty("newNonce",    $"{BaseURL}/new-nonce"),
                                new JProperty("newAccount",  $"{BaseURL}/new-account"),
                                new JProperty("newOrder",    $"{BaseURL}/new-order"),
                                new JProperty("meta",        new JObject(
                                                                 new JProperty("termsOfService",           "https://example.org/terms"),
                                                                 new JProperty("externalAccountRequired",  ExternalAccountBinding is not null),
                                                                 new JProperty("profiles",                 new JObject(new JProperty("classic", "The usual one.")))
                                                             ))
                            );

            if (OfferRenewalInfo)
                directory["renewalInfo"] = $"{BaseURL}/renewal-info";

            return directory;

        }

        private String StatusOf(Order Order)

            => Order.CertificateId is not null                                       ? "valid"
             : Order.Authorizations.Any(authorization => authorization.Status == "invalid") ? "invalid"
             : Order.Finalized                                                        ? "processing"
             : Order.Authorizations.All(authorization => authorization.Status == "valid")   ? "ready"
             :                                                                          "pending";

        private JObject OrderJSON(Order Order)
        {

            var json = new JObject(
                           new JProperty("status",          StatusOf(Order)),
                           new JProperty("expires",         DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-ddTHH:mm:ssZ")),
                           new JProperty("identifiers",     new JArray(Order.Identifiers.Select(identifier => new JObject(new JProperty("type", identifier.Type), new JProperty("value", identifier.Value))))),
                           new JProperty("authorizations",  new JArray(Order.Authorizations.Select(authorization => $"{BaseURL}/authz/{authorization.Id}"))),
                           new JProperty("finalize",        $"{BaseURL}/finalize/{Order.Id}")
                       );

            if (Order.CertificateId is not null)
                json["certificate"] = $"{BaseURL}/cert/{Order.CertificateId}";

            if (Order.Profile is not null)
                json["profile"] = Order.Profile;

            return json;

        }

        private JObject AuthorizationJSON(Authorization Authorization)
        {

            lock (Authorization)
            {

                var json = new JObject(
                               new JProperty("status",      Authorization.Status),
                               new JProperty("identifier",  new JObject(new JProperty("type", Authorization.Identifier.Type), new JProperty("value", Authorization.Identifier.Value))),
                               new JProperty("challenges",  new JArray(Authorization.Challenges.Select(ChallengeJSON)))
                           );

                if (Authorization.Wildcard)
                    json["wildcard"] = true;

                return json;

            }

        }

        private JObject ChallengeJSON(Challenge Challenge)
        {

            var json = new JObject(
                           new JProperty("type",    Challenge.Type),
                           new JProperty("url",     $"{BaseURL}/chall/{Challenge.Id}"),
                           new JProperty("status",  Challenge.Status),
                           new JProperty("token",   Challenge.Token)
                       );

            if (Challenge.Error is not null)
                json["error"] = new JObject(
                                    new JProperty("type",    "urn:ietf:params:acme:error:unauthorized"),
                                    new JProperty("detail",  Challenge.Error),
                                    new JProperty("status",  403)
                                );

            return json;

        }

        #endregion

        #region (private) Responses

        private String NewNonce()
        {
            var nonce = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
            nonces[nonce] = 0;
            return nonce;
        }

        private static HTTPContentType ContentType(String Text)

            => HTTPContentType.TryParse(Text, out var contentType)
                   ? contentType
                   : throw new InvalidOperationException(Text);

        private HTTPResponse Json(HTTPRequest     Request,
                                  HTTPStatusCode  StatusCode,
                                  JObject         JSON,
                                  String?         Location = null)
        {

            var builder = new HTTPResponse.Builder(Request) {
                              HTTPStatusCode  = StatusCode,
                              ContentType     = HTTPContentType.Application.JSON_UTF8,
                              Content         = Encoding.UTF8.GetBytes(JSON.ToString(Formatting.None))
                          };

            builder.Set("Replay-Nonce", NewNonce());

            if (Location is not null)
                builder.Set("Location", Location);

            return builder.AsImmutable;

        }

        private HTTPResponse Plain(HTTPRequest     Request,
                                   HTTPStatusCode  StatusCode)

            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode  = StatusCode,
                   CacheControl    = "no-store"
               }.Set("Replay-Nonce", NewNonce()).AsImmutable;

        private HTTPResponse Problem(HTTPRequest  Request,
                                     Int32        Status,
                                     String       Type,
                                     String       Detail)

            => new HTTPResponse.Builder(Request) {
                   HTTPStatusCode  = HTTPStatusCode.TryParseUInt16((UInt16) Status, out var statusCode) ? statusCode : HTTPStatusCode.BadRequest,
                   ContentType     = ContentType("application/problem+json"),
                   Content         = Encoding.UTF8.GetBytes(new JObject(
                                                                new JProperty("type",    $"urn:ietf:params:acme:error:{Type}"),
                                                                new JProperty("detail",  Detail),
                                                                new JProperty("status",  Status)
                                                            ).ToString(Formatting.None))
               }.Set("Replay-Nonce", NewNonce()).AsImmutable;

        private static T Owned<T>(ConcurrentDictionary<String, T>  Resources,
                                  String                           Id,
                                  VerifiedJWS                      JWS) where T : IOwned

            => Resources.TryGetValue(Id, out var resource)
                   ? resource.Owner == JWS.Account
                         ? resource
                         : throw new ProblemException(403, "unauthorized", "Not this account's.")
                   : throw new ProblemException(404, "malformed", $"No '{Id}'.");

        #endregion


        #region (private) Types

        private sealed record VerifiedJWS(JObject JWK, Account? Account, JObject? Payload);

        private sealed class ProblemException(Int32 Status, String Type, String Detail) : Exception(Detail)
        {
            public Int32  Status { get; } = Status;
            public String Type   { get; } = Type;
        }

        private interface IOwned
        {
            Account Owner { get; }
        }

        private sealed record Account(String URL, JObject JWK, String[] Contacts)
        {
            public JObject ToJSON()
                => new (new JProperty("status", "valid"), new JProperty("contact", new JArray(Contacts)));
        }

        private sealed class Order(String Id, Account Owner, (String Type, String Value)[] Identifiers, String? Profile) : IOwned
        {
            public String                         Id              { get; } = Id;
            public Account                        Owner           { get; } = Owner;
            public (String Type, String Value)[]  Identifiers     { get; } = Identifiers;
            public String?                        Profile         { get; } = Profile;
            public List<Authorization>            Authorizations  { get; } = [];
            public volatile Boolean               Finalized;
            public volatile String?               CertificateId;
        }

        private sealed class Authorization(String Id, Account Owner, (String Type, String Value) Identifier, Boolean Wildcard) : IOwned
        {
            public String                      Id          { get; } = Id;
            public Account                     Owner       { get; } = Owner;
            public (String Type, String Value) Identifier  { get; } = Identifier;
            public Boolean                     Wildcard    { get; } = Wildcard;
            public String                      Status      { get; set; } = "pending";
            public List<Challenge>             Challenges  { get; } = [];
        }

        private sealed class Challenge(String Id, Authorization Authorization, String Type, String Token) : IOwned
        {
            public String         Id             { get; } = Id;
            public Authorization  Authorization  { get; } = Authorization;
            public Account        Owner          => Authorization.Owner;
            public String         Type           { get; } = Type;
            public String         Token          { get; } = Token;
            public String         Status         { get; set; } = "pending";
            public String?        Error          { get; set; }
        }

        private sealed record Issued(String Id, Account Owner, X509Certificate2 Certificate, String Pem) : IOwned;

        #endregion


        #region DisposeAsync()

        /// <summary>
        /// Stop the server.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            validationClient.Dispose();
            await httpServer.DisposeAsync();
            rootKey.Dispose();
            intermediateKey.Dispose();
        }

        #endregion

    }

}
