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
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.ACME
{

    /// <summary>
    /// A client of the Automatic Certificate Management Environment (ACME,
    /// RFC 8555): orders certificates from Let's Encrypt or any other ACME
    /// certificate authority, proves control of their names through http-01
    /// or dns-01, and fetches them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The usual way.</b> <see cref="CreateAccountAsync"/> once - it finds
    /// the account of the key again where there is one - and then
    /// <see cref="ObtainCertificateAsync"/> with the names and the responders
    /// that can answer for them. Whoever wants a certificate kept and renewed
    /// without thinking about it takes <see cref="ACMECertificateManager"/>.
    /// </para>
    /// <para>
    /// <b>Every request is signed</b> with the account key, as a flattened JWS
    /// (RFC 7515) whose protected header names a nonce the server handed out
    /// and the URL it is sent to - so that nobody can replay it, nor send it
    /// somewhere else. Even reading an order or a certificate is such a signed
    /// POST ("POST-as-GET", RFC 8555 section 6.3). A nonce the server no
    /// longer likes (badNonce) is no failure: it comes with a new one, and the
    /// request is sent again.
    /// </para>
    /// <para>
    /// Thread-safe; one client may order several certificates at once.
    /// </para>
    /// </remarks>
    public sealed class ACMEClient : IDisposable
    {

        #region Data

        /// <summary>
        /// The directory of Let's Encrypt.
        /// </summary>
        public static readonly Uri  LetsEncrypt          = new ("https://acme-v02.api.letsencrypt.org/directory");

        /// <summary>
        /// The directory of Let's Encrypt's staging environment: certificates
        /// nobody trusts, with rate limits nobody reaches. For trying things.
        /// </summary>
        public static readonly Uri  LetsEncryptStaging   = new ("https://acme-staging-v02.api.letsencrypt.org/directory");

        /// <summary>
        /// How often a request is sent again after a badNonce.
        /// </summary>
        private const Int32         MaxBadNonceRetries   = 3;

        private static readonly JsonSerializerSettings jsonSettings = new () {
                                                                          DateParseHandling  = DateParseHandling.None
                                                                      };

        private readonly HttpClient               httpClient;
        private readonly ConcurrentQueue<String>  nonces          = new ();
        private readonly SemaphoreSlim            directoryLock   = new (1, 1);
        private volatile ACMEDirectory?           directory;
        private volatile Uri?                     accountURL;

        #endregion

        #region Properties

        /// <summary>
        /// Where the directory of the ACME server is.
        /// </summary>
        public Uri             DirectoryURL    { get; }

        /// <summary>
        /// The key every request is signed with.
        /// </summary>
        public ACMEAccountKey  AccountKey      { get; }

        /// <summary>
        /// The URL of the account of the key, once it is known.
        /// </summary>
        public Uri?            AccountURL
            => accountURL;

        /// <summary>
        /// How long to wait between two looks at an order or an authorization
        /// that is not done yet, where the server does not say.
        /// </summary>
        public TimeSpan        PollInterval    { get; }

        /// <summary>
        /// How long to wait for an order or an authorization before giving up.
        /// </summary>
        public TimeSpan        PollTimeout     { get; }

        /// <summary>
        /// The clock of the waiting.
        /// </summary>
        public TimeProvider    TimeProvider    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A client of the ACME server whose directory is at the given URL.
        /// </summary>
        /// <param name="DirectoryURL">Where its directory is, as <see cref="LetsEncrypt"/>.</param>
        /// <param name="AccountKey">The key of the account, as <see cref="ACMEAccountKey.LoadOrCreate"/> gives it.</param>
        /// <param name="HTTPHandler">How to send HTTP requests; a new handler by default. Not disposed by the client.</param>
        /// <param name="AccountURL">The URL of the account, if known already - it is found otherwise.</param>
        /// <param name="PollInterval">How long to wait between two looks at something not done yet; two seconds by default.</param>
        /// <param name="PollTimeout">How long to wait for it at all; three minutes by default.</param>
        /// <param name="TimeProvider">The clock of the waiting.</param>
        public ACMEClient(Uri                  DirectoryURL,
                          ACMEAccountKey       AccountKey,
                          HttpMessageHandler?  HTTPHandler    = null,
                          Uri?                 AccountURL     = null,
                          TimeSpan?            PollInterval   = null,
                          TimeSpan?            PollTimeout    = null,
                          TimeProvider?        TimeProvider   = null)
        {

            this.DirectoryURL  = DirectoryURL ?? throw new ArgumentNullException(nameof(DirectoryURL));
            this.AccountKey    = AccountKey   ?? throw new ArgumentNullException(nameof(AccountKey));
            this.accountURL    = AccountURL;
            this.PollInterval  = PollInterval ?? TimeSpan.FromSeconds(2);
            this.PollTimeout   = PollTimeout  ?? TimeSpan.FromMinutes(3);
            this.TimeProvider  = TimeProvider ?? TimeProvider.System;

            this.httpClient    = new HttpClient(HTTPHandler ?? new SocketsHttpHandler(), disposeHandler: HTTPHandler is null) {
                                     Timeout = TimeSpan.FromSeconds(60)
                                 };

            // RFC 8555, section 6.1: a client says who it is.
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Hermod-ACME/1.0");

        }

        #endregion


        #region GetDirectoryAsync(CancellationToken = default)

        /// <summary>
        /// The directory of the server - fetched once, and kept.
        /// </summary>
        public async Task<ACMEDirectory> GetDirectoryAsync(CancellationToken CancellationToken = default)
        {

            if (directory is not null)
                return directory;

            await directoryLock.WaitAsync(CancellationToken);

            try
            {

                if (directory is not null)
                    return directory;

                using var response = await httpClient.GetAsync(DirectoryURL, CancellationToken);
                var       text     = await response.Content.ReadAsStringAsync(CancellationToken);

                if (!response.IsSuccessStatusCode)
                    throw Failure($"The ACME directory at {DirectoryURL} could not be fetched", response, text);

                directory = new ACMEDirectory(ParseObject(text, DirectoryURL), DirectoryURL);
                return directory;

            }
            finally
            {
                directoryLock.Release();
            }

        }

        #endregion


        #region CreateAccountAsync(Contacts = null, TermsOfServiceAgreed = false, EABKeyId = null, EABHMACKey = null, ...)

        /// <summary>
        /// Open an account for the account key - or find the one it has already:
        /// an ACME server answers both alike (RFC 8555, section 7.3.1).
        /// </summary>
        /// <param name="Contacts">Where the certificate authority may write - e-mail addresses, with or without "mailto:".</param>
        /// <param name="TermsOfServiceAgreed">Whether the terms of service of the directory's meta data are agreed to. Let's Encrypt insists.</param>
        /// <param name="EABKeyId">For a server that requires an external account binding: the key identifier it gave.</param>
        /// <param name="EABHMACKey">And the MAC key, base64url-encoded, as it gave it.</param>
        /// <param name="CancellationToken">To give up.</param>
        public async Task<ACMEAccount> CreateAccountAsync(IEnumerable<String>?  Contacts               = null,
                                                          Boolean               TermsOfServiceAgreed   = false,
                                                          String?               EABKeyId               = null,
                                                          String?               EABHMACKey             = null,
                                                          CancellationToken     CancellationToken      = default)
        {

            var directory  = await GetDirectoryAsync(CancellationToken);
            var payload    = new JObject();

            var contacts   = Contacts?.Where (contact => !String.IsNullOrWhiteSpace(contact)).
                                       Select(contact => contact.Contains(':') ? contact.Trim() : $"mailto:{contact.Trim()}").
                                       ToArray() ?? [];

            if (contacts.Length > 0)
                payload["contact"] = new JArray(contacts);

            if (TermsOfServiceAgreed)
                payload["termsOfServiceAgreed"] = true;

            if (EABKeyId is not null || EABHMACKey is not null)
                payload["externalAccountBinding"] = ExternalAccountBinding(
                                                        EABKeyId   ?? throw new ArgumentNullException(nameof(EABKeyId),   "An external account binding needs its key identifier."),
                                                        EABHMACKey ?? throw new ArgumentNullException(nameof(EABHMACKey), "An external account binding needs its MAC key."),
                                                        directory.NewAccount
                                                    );

            else if (directory.ExternalAccountRequired)
                throw new ACMEException($"The ACME server at {DirectoryURL} opens accounts only with an external account binding: its key identifier and MAC key.");

            var response   = await PostAsync(directory.NewAccount, payload, UseJWK: true, CancellationToken: CancellationToken);

            return Account(response, Created: response.StatusCode == HttpStatusCode.Created);

        }

        #endregion

        #region FindAccountAsync(CancellationToken = default)

        /// <summary>
        /// The account of the account key, or null where it has none.
        /// </summary>
        public async Task<ACMEAccount?> FindAccountAsync(CancellationToken CancellationToken = default)
        {

            var directory = await GetDirectoryAsync(CancellationToken);

            try
            {

                var response = await PostAsync(directory.NewAccount,
                                               new JObject(new JProperty("onlyReturnExisting", true)),
                                               UseJWK:             true,
                                               CancellationToken:  CancellationToken);

                return Account(response, Created: false);

            }
            catch (ACMEException e) when (e.Problem?.ShortType == "accountDoesNotExist")
            {
                return null;
            }

        }

        #endregion


        #region NewOrderAsync(Identifiers, Profile = null, Replaces = null, CancellationToken = default)

        /// <summary>
        /// Order a certificate for the given names and addresses.
        /// </summary>
        /// <param name="Identifiers">What it is to be for.</param>
        /// <param name="Profile">The certificate profile, where the server offers several - Let's Encrypt's "shortlived", say.</param>
        /// <param name="Replaces">The ARI identifier of the certificate it replaces (RFC 9773, section 5).</param>
        /// <param name="CancellationToken">To give up.</param>
        public async Task<ACMEOrder> NewOrderAsync(IEnumerable<ACMEIdentifier>  Identifiers,
                                                   String?                      Profile             = null,
                                                   String?                      Replaces            = null,
                                                   CancellationToken            CancellationToken   = default)
        {

            var directory  = await GetDirectoryAsync(CancellationToken);
            var payload    = new JObject(
                                 new JProperty("identifiers", new JArray(Identifiers.Select(identifier => identifier.ToJSON())))
                             );

            if (Profile  is not null)
                payload["profile"]  = Profile;

            if (Replaces is not null)
                payload["replaces"] = Replaces;

            var response   = await PostAsync(directory.NewOrder, payload, CancellationToken: CancellationToken);

            return new ACMEOrder(response.JSON(),
                                 response.Location ?? throw new ACMEException($"The ACME server answered a new order without saying where it is."));

        }

        #endregion

        #region GetOrderAsync(OrderURL, CancellationToken = default)

        /// <summary>
        /// The order as it is now.
        /// </summary>
        public async Task<ACMEOrder> GetOrderAsync(Uri                OrderURL,
                                                   CancellationToken  CancellationToken = default)

            => new ((await PostAsync(OrderURL, null, CancellationToken: CancellationToken)).JSON(), OrderURL);

        #endregion

        #region GetAuthorizationAsync(AuthorizationURL, CancellationToken = default)

        /// <summary>
        /// The authorization as it is now.
        /// </summary>
        public async Task<ACMEAuthorization> GetAuthorizationAsync(Uri                AuthorizationURL,
                                                                   CancellationToken  CancellationToken = default)

            => new ((await PostAsync(AuthorizationURL, null, CancellationToken: CancellationToken)).JSON(), AuthorizationURL);

        #endregion

        #region RespondToChallengeAsync(ChallengeURL, CancellationToken = default)

        /// <summary>
        /// Tell the server that the answer to the challenge is in place, and
        /// that it may look.
        /// </summary>
        public async Task<ACMEChallenge> RespondToChallengeAsync(Uri                ChallengeURL,
                                                                 CancellationToken  CancellationToken = default)

            => new ((await PostAsync(ChallengeURL, new JObject(), CancellationToken: CancellationToken)).JSON());

        #endregion

        #region FinalizeAsync(Order, CertificateSigningRequest, CancellationToken = default)

        /// <summary>
        /// Send the certificate signing request of a ready order.
        /// </summary>
        /// <param name="Order">The order, with every authorization won.</param>
        /// <param name="CertificateSigningRequest">The PKCS#10 request, DER-encoded.</param>
        /// <param name="CancellationToken">To give up.</param>
        public async Task<ACMEOrder> FinalizeAsync(ACMEOrder          Order,
                                                   Byte[]             CertificateSigningRequest,
                                                   CancellationToken  CancellationToken = default)

            => new ((await PostAsync(Order.Finalize,
                                     new JObject(new JProperty("csr", Base64Url.EncodeToString(CertificateSigningRequest))),
                                     CancellationToken: CancellationToken)).JSON(),
                    Order.URL);

        #endregion

        #region DownloadCertificateAsync(CertificateURL, CancellationToken = default)

        /// <summary>
        /// The issued certificate and its intermediates, in PEM.
        /// </summary>
        public async Task<String> DownloadCertificateAsync(Uri                CertificateURL,
                                                           CancellationToken  CancellationToken = default)

            => (await PostAsync(CertificateURL,
                                null,
                                Accept:             "application/pem-certificate-chain",
                                CancellationToken:  CancellationToken)).Text;

        #endregion

        #region RevokeCertificateAsync(Certificate, Reason = null, CancellationToken = default)

        /// <summary>
        /// Revoke a certificate of this account (RFC 8555, section 7.6).
        /// </summary>
        /// <param name="Certificate">The certificate.</param>
        /// <param name="Reason">The CRL reason code (RFC 5280, section 5.3.1): 4 for "superseded", 1 for "keyCompromise"...</param>
        /// <param name="CancellationToken">To give up.</param>
        public async Task RevokeCertificateAsync(X509Certificate2   Certificate,
                                                 Int32?             Reason              = null,
                                                 CancellationToken  CancellationToken   = default)
        {

            var directory = await GetDirectoryAsync(CancellationToken);
            var payload   = new JObject(new JProperty("certificate", Base64Url.EncodeToString(Certificate.RawData)));

            if (Reason.HasValue)
                payload["reason"] = Reason.Value;

            await PostAsync(directory.RevokeCertificate ?? throw new ACMEException($"The ACME server at {DirectoryURL} revokes no certificates."),
                            payload,
                            CancellationToken: CancellationToken);

        }

        #endregion


        #region (static) CertificateIdentifier(Certificate)

        /// <summary>
        /// The ARI identifier of a certificate (RFC 9773, section 4.1): the key
        /// identifier of its issuer and its serial number, both base64url-encoded,
        /// with a dot between them. Null for a certificate without an authority
        /// key identifier, which no ACME server issues.
        /// </summary>
        public static String? CertificateIdentifier(X509Certificate2 Certificate)
        {

            foreach (var extension in Certificate.Extensions)
            {
                if (extension.Oid?.Value == "2.5.29.35")
                {

                    var keyIdentifier = new X509AuthorityKeyIdentifierExtension(extension.RawData, extension.Critical).KeyIdentifier;

                    if (keyIdentifier is null)
                        return null;

                    // The serial number as its DER INTEGER content octets: big-endian,
                    // with the leading zero octet a positive number with its high bit
                    // set has. The hex of SerialNumber is exactly those octets.
                    return $"{Base64Url.EncodeToString(keyIdentifier.Value.Span)}.{Base64Url.EncodeToString(Convert.FromHexString(Certificate.SerialNumber))}";

                }
            }

            return null;

        }

        #endregion

        #region GetRenewalInfoAsync(Certificate, CancellationToken = default)

        /// <summary>
        /// When the certificate authority would have the given certificate
        /// renewed (ARI, RFC 9773) - or null, where it does not say.
        /// </summary>
        /// <remarks>
        /// A plain GET, not signed: the window of a certificate is no secret.
        /// </remarks>
        public async Task<ACMERenewalInfo?> GetRenewalInfoAsync(X509Certificate2   Certificate,
                                                                CancellationToken  CancellationToken = default)
        {

            var directory   = await GetDirectoryAsync(CancellationToken);
            var identifier  = CertificateIdentifier(Certificate);

            if (directory.RenewalInfo is null || identifier is null)
                return null;

            using var response = await httpClient.GetAsync($"{directory.RenewalInfo.ToString().TrimEnd('/')}/{identifier}", CancellationToken);
            var       text     = await response.Content.ReadAsStringAsync(CancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
                throw Failure($"The renewal information of {Certificate.Subject} could not be fetched", response, text);

            var json    = ParseObject(text, directory.RenewalInfo);
            var window  = json["suggestedWindow"] as JObject;
            var start   = ACMEJSON.Timestamp(window?["start"]);
            var end     = ACMEJSON.Timestamp(window?["end"]);

            if (start is null || end is null || end < start)
                throw new ACMEException($"The renewal information of {Certificate.Subject} has no suggested window.");

            return new ACMERenewalInfo(
                       start.Value,
                       end.Value,
                       json["explanationURL"]?.Value<String>() is String explanation && Uri.TryCreate(explanation, UriKind.Absolute, out var explanationURL) ? explanationURL : null,
                       RetryAfter(response)
                   );

        }

        #endregion


        #region ObtainCertificateAsync(Names, Responders, KeyType = ECDSA_P256, Profile = null, Replacing = null, CancellationToken = default)

        /// <summary>
        /// Order a certificate, prove control of everything it is for, have it
        /// issued and fetch it: the whole of RFC 8555, section 7.4, in one call.
        /// </summary>
        /// <param name="Names">The DNS names - "*.example.org" for a wildcard - and IP addresses it is to be for.</param>
        /// <param name="Responders">What answers the challenges; for each name, the first that can and that the server offers.</param>
        /// <param name="KeyType">The kind of the new certificate's key - a new one for every certificate.</param>
        /// <param name="Profile">The certificate profile, where the server offers several.</param>
        /// <param name="Replacing">The certificate this one replaces, so that the server may know (ARI).</param>
        /// <param name="CancellationToken">To give up.</param>
        public async Task<ACMECertificate> ObtainCertificateAsync(IEnumerable<String>                   Names,
                                                                  IEnumerable<IACMEChallengeResponder>  Responders,
                                                                  ACMEKeyType                           KeyType             = ACMEKeyType.ECDSA_P256,
                                                                  String?                               Profile             = null,
                                                                  X509Certificate2?                     Replacing           = null,
                                                                  CancellationToken                     CancellationToken   = default)
        {

            var identifiers  = Names.Select(ACMEIdentifier.From).Distinct().ToArray();
            var responders   = Responders.ToArray();

            if (identifiers.Length == 0)
                throw new ArgumentException("A certificate is for at least one name.", nameof(Names));

            if (AccountURL is null && await FindAccountAsync(CancellationToken) is null)
                throw new ACMEException($"The account key has no account at {DirectoryURL} yet: CreateAccountAsync opens one.");

            var directory    = await GetDirectoryAsync(CancellationToken);
            var replaces     = Replacing is not null && directory.RenewalInfo is not null
                                   ? CertificateIdentifier(Replacing)
                                   : null;

            ACMEOrder order;

            try
            {
                order = await NewOrderAsync(identifiers, Profile, replaces, CancellationToken);
            }
            catch (ACMEException e) when (replaces is not null && e.Problem?.ShortType is "alreadyReplaced" or "conflict" or "malformed")
            {
                // Replaced already - by an order that never finished, say. Then it
                // is a new certificate, not a replacement.
                order = await NewOrderAsync(identifiers, Profile, null, CancellationToken);
            }

            if (order.Status is "pending")
                await AuthorizeAsync(order, responders, CancellationToken);

            order = await PollOrderAsync(order.URL, status => status is "pending", "to be ready", CancellationToken);

            if (order.Status is "ready")
            {

                var (csr, keyPem) = CertificateSigningRequest(identifiers, KeyType);

                order = await FinalizeAsync(order, csr, CancellationToken);
                order = await PollOrderAsync(order.URL, status => status is "processing" or "ready", "to be issued", CancellationToken);

                if (order.Status != "valid" || order.Certificate is null)
                    throw order.Error is not null
                              ? new ACMEException($"The order at {order.URL} was not issued", order.Error)
                              : new ACMEException($"The order at {order.URL} was not issued: it is {order.Status}.");

                var fullChainPem = await DownloadCertificateAsync(order.Certificate, CancellationToken);

                return ACMECertificate.FromPem(fullChainPem, keyPem, order.Certificate.ToString());

            }

            throw order.Error is not null
                      ? new ACMEException($"The order at {order.URL} is {order.Status}", order.Error)
                      : new ACMEException($"The order at {order.URL} is {order.Status}, not ready to be finalized.");

        }

        #endregion

        #region (private) AuthorizeAsync(Order, Responders, CancellationToken)

        /// <summary>
        /// Win every authorization of an order that is not won yet: prepare all
        /// answers, wait until they can be seen, tell the server to look at all
        /// of them, and wait for its verdicts. And take the answers away again,
        /// whatever came of it.
        /// </summary>
        private async Task AuthorizeAsync(ACMEOrder                  Order,
                                          IACMEChallengeResponder[]  Responders,
                                          CancellationToken          CancellationToken)
        {

            var open = new List<(IACMEChallengeResponder Responder, ACMEChallengeTask Task, ACMEChallenge Challenge, ACMEAuthorization Authorization)>();

            try
            {

                foreach (var authorizationURL in Order.Authorizations)
                {

                    var authorization = await GetAuthorizationAsync(authorizationURL, CancellationToken);

                    if (authorization.Status == "valid")
                        continue;

                    if (authorization.Status != "pending")
                        throw new ACMEException($"The authorization of {authorization.Identifier} is {authorization.Status}.");

                    var (responder, challenge) = Responders.
                                                     Where (responder => responder.CanAnswer(authorization.Identifier, authorization.Wildcard)).
                                                     SelectMany(responder => authorization.Challenges.
                                                                                 Where (challenge => challenge.Type == responder.ChallengeType).
                                                                                 Select(challenge => (responder, challenge))).
                                                     FirstOrDefault();

                    if (responder is null || challenge is null)
                        throw new ACMEException(
                                  $"Nothing here can prove control of {(authorization.Wildcard ? "*." : "")}{authorization.Identifier}: " +
                                  $"the server offers {String.Join(", ", authorization.Challenges.Select(challenge => challenge.Type))}, " +
                                  $"and here there is {(Responders.Length == 0 ? "no responder" : String.Join(", ", Responders.Select(responder => responder.ChallengeType)))}" +
                                  (authorization.Wildcard ? " - a wildcard is proven by dns-01 only." : "."));

                    var task = new ACMEChallengeTask(
                                   authorization.Identifier,
                                   authorization.Wildcard,
                                   challenge.Type,
                                   challenge.Token,
                                   AccountKey.KeyAuthorization(challenge.Token)
                               );

                    open.Add((responder, task, challenge, authorization));

                    await responder.PrepareAsync(task, CancellationToken);

                }

                foreach (var responder in open.Select(challenge => challenge.Responder).Distinct())
                    await responder.AwaitPublicationAsync(CancellationToken);

                foreach (var (_, _, challenge, _) in open)
                    await RespondToChallengeAsync(challenge.URL, CancellationToken);

                var failures = new List<(ACMEAuthorization Authorization, ACMEProblem? Problem)>();

                foreach (var (_, task, _, authorization) in open)
                {

                    var result = await PollAuthorizationAsync(authorization.URL, CancellationToken);

                    if (result.Status != "valid")
                        failures.Add((result,
                                      result.Challenges.FirstOrDefault(challenge => challenge.Type == task.Type && challenge.Error is not null)?.Error ??
                                      result.Challenges.FirstOrDefault(challenge => challenge.Error is not null)?.Error));

                }

                if (failures.Count > 0)
                {

                    var message = $"Control of {String.Join(", ", failures.Select(failure => failure.Authorization.Identifier))} was not proven";

                    throw failures[0].Problem is ACMEProblem problem
                              ? new ACMEException(message, failures.Count == 1
                                                               ? problem
                                                               : problem with { Subproblems = failures.Where (failure => failure.Problem is not null).
                                                                                                       Select(failure => failure.Problem! with { Identifier = failure.Authorization.Identifier }).
                                                                                                       ToArray() })
                              : new ACMEException($"{message}.");

                }

            }
            finally
            {
                foreach (var (responder, task, _, _) in open)
                {
                    try
                    {
                        await responder.CleanUpAsync(task, CancellationToken.None);
                    }
                    catch
                    {
                        // An answer left behind is harmless: it answers a token nobody will
                        // ask for again. A failure here must not hide the one before it.
                    }
                }
            }

        }

        #endregion

        #region (private) PollAuthorizationAsync(AuthorizationURL, CancellationToken)

        private async Task<ACMEAuthorization> PollAuthorizationAsync(Uri                AuthorizationURL,
                                                                     CancellationToken  CancellationToken)
        {

            var deadline = TimeProvider.GetUtcNow() + PollTimeout;

            while (true)
            {

                var response       = await PostAsync(AuthorizationURL, null, CancellationToken: CancellationToken);
                var authorization  = new ACMEAuthorization(response.JSON(), AuthorizationURL);

                if (authorization.Status is not "pending" and not "processing")
                    return authorization;

                await WaitAsync(response.RetryAfter, deadline, $"the authorization of {authorization.Identifier}", CancellationToken);

            }

        }

        #endregion

        #region (private) PollOrderAsync(OrderURL, NotDoneYet, What, CancellationToken)

        private async Task<ACMEOrder> PollOrderAsync(Uri                  OrderURL,
                                                     Func<String, Boolean> NotDoneYet,
                                                     String               What,
                                                     CancellationToken    CancellationToken)
        {

            var deadline = TimeProvider.GetUtcNow() + PollTimeout;

            while (true)
            {

                var response  = await PostAsync(OrderURL, null, CancellationToken: CancellationToken);
                var order     = new ACMEOrder(response.JSON(), OrderURL);

                if (!NotDoneYet(order.Status))
                    return order;

                await WaitAsync(response.RetryAfter, deadline, $"the order at {OrderURL} {What}", CancellationToken);

            }

        }

        #endregion

        #region (private) WaitAsync(RetryAfter, Deadline, What, CancellationToken)

        private async Task WaitAsync(TimeSpan?          RetryAfter,
                                     DateTimeOffset     Deadline,
                                     String             What,
                                     CancellationToken  CancellationToken)
        {

            var left = Deadline - TimeProvider.GetUtcNow();

            if (left <= TimeSpan.Zero)
                throw new ACMEException($"Waited {PollTimeout} for {What}, in vain.");

            // As long as the server asks, but not longer than there is time left,
            // and not more often than every tenth of a second.
            var delay = RetryAfter ?? PollInterval;

            if (delay < TimeSpan.FromMilliseconds(100))
                delay = TimeSpan.FromMilliseconds(100);

            if (delay > left)
                delay = left;

            await Task.Delay(delay, TimeProvider, CancellationToken);

        }

        #endregion

        #region (private static) CertificateSigningRequest(Identifiers, KeyType)

        /// <summary>
        /// A new key and the PKCS#10 request for a certificate of it, for the
        /// given names and addresses - all of them as subject alternative names,
        /// and the first DNS name as the common name as well, where it fits.
        /// </summary>
        internal static (Byte[] CSR, String KeyPem) CertificateSigningRequest(IReadOnlyList<ACMEIdentifier>  Identifiers,
                                                                              ACMEKeyType                    KeyType)
        {

            using AsymmetricAlgorithm key = KeyType switch {
                                                ACMEKeyType.ECDSA_P384  => ECDsa.Create(ECCurve.NamedCurves.nistP384),
                                                ACMEKeyType.RSA_2048    => RSA.Create(2048),
                                                ACMEKeyType.RSA_4096    => RSA.Create(4096),
                                                _                       => ECDsa.Create(ECCurve.NamedCurves.nistP256)
                                            };

            // A common name is limited to 64 characters (RFC 5280, ub-common-name),
            // and is no more than a courtesy: the names that count are the
            // subject alternative ones.
            var commonName  = Identifiers.FirstOrDefault(identifier => identifier.Type == "dns" && identifier.Value.Length <= 64)?.Value;
            var subject     = new X500DistinguishedName(commonName is not null ? $"CN={commonName}" : "");

            var request     = key switch {
                                  ECDsa ecdsa  => new CertificateRequest(subject, ecdsa, KeyType == ACMEKeyType.ECDSA_P384 ? HashAlgorithmName.SHA384 : HashAlgorithmName.SHA256),
                                  RSA   rsa    => new CertificateRequest(subject, rsa,   HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
                                  _            => throw new InvalidOperationException()
                              };

            var names       = new SubjectAlternativeNameBuilder();

            foreach (var identifier in Identifiers)
            {
                if (identifier.Type == "ip")
                    names.AddIpAddress(System.Net.IPAddress.Parse(identifier.Value));
                else
                    names.AddDnsName(identifier.Value);
            }

            request.CertificateExtensions.Add(names.Build());

            return (request.CreateSigningRequest(),
                    key.ExportPkcs8PrivateKeyPem());

        }

        #endregion


        #region (private) PostAsync(URL, Payload, UseJWK = false, Accept = null, CancellationToken = default)

        /// <summary>
        /// Send a signed request: with the payload, or with none for a
        /// POST-as-GET.
        /// </summary>
        private async Task<ACMEResponse> PostAsync(Uri                URL,
                                                   JObject?           Payload,
                                                   Boolean            UseJWK              = false,
                                                   String?            Accept              = null,
                                                   CancellationToken  CancellationToken   = default)
        {

            var directory = await GetDirectoryAsync(CancellationToken);

            if (!UseJWK && accountURL is null)
                throw new ACMEException($"The account key has no account at {DirectoryURL} yet: CreateAccountAsync opens one.");

            for (var attempt = 0; ; attempt++)
            {

                var header = new JObject(
                                 new JProperty("alg",    AccountKey.Algorithm),
                                 new JProperty("nonce",  await NonceAsync(directory, CancellationToken)),
                                 new JProperty("url",    URL.ToString())
                             );

                if (UseJWK)
                    header["jwk"] = AccountKey.JWK;
                else
                    header["kid"] = accountURL!.ToString();

                using var request = new HttpRequestMessage(HttpMethod.Post, URL) {
                                        Content = new StringContent(JWS(header, Payload).ToString(Formatting.None), Encoding.UTF8)
                                    };

                // Exactly this, without a charset (RFC 8555, section 6.2).
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/jose+json");

                if (Accept is not null)
                    request.Headers.Accept.ParseAdd(Accept);

                using var response = await httpClient.SendAsync(request, CancellationToken);
                RememberNonce(response);

                var text = await response.Content.ReadAsStringAsync(CancellationToken);

                if (response.IsSuccessStatusCode)
                    return new ACMEResponse(response.StatusCode,
                                            text,
                                            response.Headers.Location is Uri location ? new Uri(URL, location) : null,
                                            RetryAfter(response),
                                            URL);

                var failure = Failure($"{URL} refused the request", response, text);

                // RFC 8555, section 6.5: the answer to a bad nonce carries a good one.
                if (failure.Problem?.ShortType == "badNonce" && attempt < MaxBadNonceRetries)
                    continue;

                throw failure;

            }

        }

        #endregion

        #region (private) JWS(Header, Payload)

        /// <summary>
        /// The flattened JSON serialization of a JWS (RFC 7515, section 7.2.2).
        /// </summary>
        private JObject JWS(JObject   Header,
                            JObject?  Payload)
        {

            var protectedHeader  = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(Header.ToString(Formatting.None)));
            var payload          = Payload is not null
                                       ? Base64Url.EncodeToString(Encoding.UTF8.GetBytes(Payload.ToString(Formatting.None)))
                                       : "";

            return new JObject(
                       new JProperty("protected",  protectedHeader),
                       new JProperty("payload",    payload),
                       new JProperty("signature",  Base64Url.EncodeToString(AccountKey.Sign(Encoding.ASCII.GetBytes($"{protectedHeader}.{payload}"))))
                   );

        }

        #endregion

        #region (private) ExternalAccountBinding(KeyId, HMACKey, NewAccountURL)

        /// <summary>
        /// The external account binding (RFC 8555, section 7.3.4): the account
        /// key, signed with the MAC key the certificate authority gave out of
        /// band - which tells it whose account this is.
        /// </summary>
        private JObject ExternalAccountBinding(String  KeyId,
                                               String  HMACKey,
                                               Uri     NewAccountURL)
        {

            var protectedHeader  = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(new JObject(
                                                                                       new JProperty("alg",  "HS256"),
                                                                                       new JProperty("kid",  KeyId),
                                                                                       new JProperty("url",  NewAccountURL.ToString())
                                                                                   ).ToString(Formatting.None)));

            var payload          = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(AccountKey.JWK.ToString(Formatting.None)));

            var signature        = HMACSHA256.HashData(Base64Url.DecodeFromChars(HMACKey.Trim().TrimEnd('=')),
                                                       Encoding.ASCII.GetBytes($"{protectedHeader}.{payload}"));

            return new JObject(
                       new JProperty("protected",  protectedHeader),
                       new JProperty("payload",    payload),
                       new JProperty("signature",  Base64Url.EncodeToString(signature))
                   );

        }

        #endregion

        #region (private) NonceAsync(Directory, CancellationToken)

        private async Task<String> NonceAsync(ACMEDirectory      Directory,
                                              CancellationToken  CancellationToken)
        {

            if (nonces.TryDequeue(out var nonce))
                return nonce;

            using var request   = new HttpRequestMessage(HttpMethod.Head, Directory.NewNonce);
            using var response  = await httpClient.SendAsync(request, CancellationToken);

            if (response.Headers.TryGetValues("Replay-Nonce", out var values) &&
                values.FirstOrDefault() is String fresh &&
                fresh.Length > 0)
            {
                return fresh;
            }

            throw new ACMEException($"The ACME server at {Directory.NewNonce} handed out no nonce (HTTP {(Int32) response.StatusCode}).");

        }

        #endregion

        #region (private) RememberNonce(Response)

        private void RememberNonce(HttpResponseMessage Response)
        {

            if (Response.Headers.TryGetValues("Replay-Nonce", out var values) &&
                values.FirstOrDefault() is String nonce &&
                nonce.Length > 0)
            {

                nonces.Enqueue(nonce);

                // Nonces are used one by one, and a server forgets old ones; there
                // is no point in keeping more than a handful.
                while (nonces.Count > 16)
                    nonces.TryDequeue(out _);

            }

        }

        #endregion

        #region (private) Account(Response, Created)

        private ACMEAccount Account(ACMEResponse  Response,
                                    Boolean       Created)
        {

            var url  = Response.Location ?? throw new ACMEException("The ACME server answered for an account without saying where it is.");
            var json = Response.JSON();

            accountURL = url;

            return new ACMEAccount(
                       url,
                       json["status"]?.Value<String>() ?? "valid",
                       (json["contact"] as JArray)?.Select(contact => contact.Value<String>() ?? "").ToArray() ?? [],
                       Created
                   );

        }

        #endregion

        #region (private static) Failure(Message, Response, Text)

        private static ACMEException Failure(String               Message,
                                             HttpResponseMessage  Response,
                                             String               Text)
        {

            ACMEProblem? problem = null;

            try
            {
                if (Text.TrimStart().StartsWith('{'))
                    problem = ACMEProblem.Parse(JsonConvert.DeserializeObject<JObject>(Text, jsonSettings));
            }
            catch (JsonException)
            { }

            return problem is not null
                       ? new ACMEException(Message, problem, RetryAfter(Response))
                       : new ACMEException($"{Message}: HTTP {(Int32) Response.StatusCode} {Response.ReasonPhrase}");

        }

        #endregion

        #region (private static) RetryAfter(Response)

        private static TimeSpan? RetryAfter(HttpResponseMessage Response)

            => Response.Headers.RetryAfter switch {
                   { Delta: TimeSpan delta }          => delta,
                   { Date:  DateTimeOffset date }     => date - DateTimeOffset.UtcNow is var wait && wait > TimeSpan.Zero ? wait : TimeSpan.Zero,
                   _                                  => null
               };

        #endregion

        #region (private static) ParseObject(Text, Source)

        private static JObject ParseObject(String  Text,
                                           Uri     Source)
        {
            try
            {
                return JsonConvert.DeserializeObject<JObject>(Text, jsonSettings)
                           ?? throw new ACMEException($"{Source} answered with no JSON object.");
            }
            catch (JsonException e)
            {
                throw new ACMEException($"{Source} answered with no JSON object: {e.Message}", e);
            }
        }

        #endregion


        #region (private) ACMEResponse

        private sealed record ACMEResponse(HttpStatusCode  StatusCode,
                                           String          Text,
                                           Uri?            Location,
                                           TimeSpan?       RetryAfter,
                                           Uri             Source)
        {
            public JObject JSON()
                => ParseObject(Text, Source);
        }

        #endregion

        #region Dispose()

        /// <summary>
        /// Close the HTTP client.
        /// </summary>
        public void Dispose()
        {
            httpClient.Dispose();
            directoryLock.Dispose();
        }

        #endregion

    }

}
