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

using System.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Drives the SSH authentication protocol (RFC 4252) over an established <see cref="SshTransport"/>:
    /// the <c>ssh-userauth</c> service request/accept, the <c>publickey</c> method with the query-then-sign
    /// flow, the authentication banner and success/failure handling.
    /// </summary>
    public static class UserAuthentication
    {

        #region Constants

        /// <summary>The authentication service name requested after the transport is up.</summary>
        public const String AuthService        = "ssh-userauth";

        /// <summary>The service a successful authentication grants access to (the connection protocol).</summary>
        public const String ConnectionService  = "ssh-connection";

        /// <summary>The <c>publickey</c> authentication method (RFC 4252 §7).</summary>
        public const String PublicKeyMethod     = "publickey";

        /// <summary>The <c>password</c> authentication method (RFC 4252 §8).</summary>
        public const String PasswordMethod       = "password";

        /// <summary>The <c>keyboard-interactive</c> authentication method (RFC 4256) — carries TOTP prompts.</summary>
        public const String KeyboardInteractiveMethod = "keyboard-interactive";

        /// <summary>The <c>none</c> method — used only to probe which methods the server will accept.</summary>
        public const String NoneMethod          = "none";

        #endregion


        #region ServerAuthenticateAsync(Transport, Authenticator, Banner = null, MaxAuthTries = 6, CancellationToken = default)

        /// <summary>
        /// Run the server side of authentication: accept the <c>ssh-userauth</c> service, optionally send a
        /// banner, then evaluate <c>publickey</c> requests until one succeeds or the attempt budget is spent.
        /// </summary>
        /// <param name="Transport">The established transport.</param>
        /// <param name="Authenticator">The authentication policy.</param>
        /// <param name="Banner">An optional pre-auth banner (SSH_MSG_USERAUTH_BANNER).</param>
        /// <param name="MaxAuthTries">The maximum number of failed attempts before disconnecting.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static async ValueTask<SshAuthResult> ServerAuthenticateAsync(SshTransport           Transport,
                                                                             ISshUserAuthenticator  Authenticator,
                                                                             String?                Banner             = null,
                                                                             Int32                  MaxAuthTries       = 6,
                                                                             ISshAuditSink?         AuditSink          = null,
                                                                             TimeProvider?          TimeProvider       = null,
                                                                             CancellationToken      CancellationToken  = default)
        {

            var clock = TimeProvider ?? System.TimeProvider.System;

            // 1. Wait for SERVICE_REQUEST("ssh-userauth") — skipping a client-sent EXT_INFO.
            await ExpectServiceRequestAsync(Transport, CancellationToken).ConfigureAwait(false);
            await Transport.SendPacketAsync(BuildServiceAccept(AuthService), CancellationToken).ConfigureAwait(false);

            if (Banner is not null)
            {
                await Transport.SendPacketAsync(BuildBanner(Banner), CancellationToken).ConfigureAwait(false);
                await EmitAsync(AuditSink, new BannerSentEvent(clock.GetUtcNow()), CancellationToken).ConfigureAwait(false);
            }

            // 2. Process authentication requests, tracking the methods completed for a multi-factor chain.
            var failedAttempts   = 0;
            var completedMethods = new HashSet<String>(StringComparer.Ordinal);

            // Restrictions the succeeding credential imposes on the session. Read off the blob that was
            // actually authenticated — the certificate has already had its CA signature, validity,
            // principals and critical options checked by the authenticator at this point.
            var restrictions     = SshSessionRestrictions.None;

            while (true)
            {

                var payload  = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var request  = ParseAuthRequest(payload);   // fully parse before any await (ref struct)

                var (succeeded, isQueryOnly) = request.Method switch {
                    PublicKeyMethod            => await EvaluatePublicKeyAsync(Transport, Authenticator, request, CancellationToken).ConfigureAwait(false),
                    PasswordMethod             => (await Authenticator.AuthorizePasswordAsync(request.Username, request.Password, CancellationToken).ConfigureAwait(false), false),
                    KeyboardInteractiveMethod  => (await EvaluateKeyboardInteractiveAsync(Transport, Authenticator, request.Username, CancellationToken).ConfigureAwait(false), false),
                    _                          => (false, false)
                };

                if (isQueryOnly)
                    continue;   // a publickey probe answered with PK_OK — no attempt consumed

                if (succeeded)
                {

                    completedMethods.Add(request.Method);
                    await EmitAsync(AuditSink, new AuthMethodSucceededEvent(clock.GetUtcNow(), request.Username, request.Method), CancellationToken).ConfigureAwait(false);

                    if (request.Method == PublicKeyMethod)
                    {

                        // A certificate carries the CA's constraints; an authorized_keys entry carries
                        // the administrator's. Both can apply, and the stricter side wins — a credential
                        // may only ever narrow what another source already permits.
                        if (SshCertificate.IsCertificateAlgorithm(request.Algorithm) &&
                            SshCertificate.TryParse(request.PublicKeyBlob, out var authenticatedCert))
                            restrictions = restrictions.And(SshCertificateRestrictions.FromCertificate(authenticatedCert!));

                        var fromEntry = await Authenticator.GetRestrictionsAsync(
                                                  new SshPublicKeyAuthRequest(request.Username, request.Algorithm, request.PublicKeyBlob),
                                                  CancellationToken).ConfigureAwait(false);

                        restrictions = restrictions.And(fromEntry);

                    }

                    var remaining = Authenticator.RemainingMethods(request.Username, completedMethods);

                    if (remaining.Count == 0)
                    {
                        await Transport.SendPacketAsync(new Byte[] { (Byte) SshMessageNumber.UserAuthSuccess }, CancellationToken).ConfigureAwait(false);
                        await EmitAsync(AuditSink, new AuthenticationSucceededEvent(clock.GetUtcNow(), request.Username, [.. completedMethods]), CancellationToken).ConfigureAwait(false);
                        return new SshAuthResult(request.Username, request.Method, restrictions);
                    }

                    // A factor succeeded but more are required — partial success (RFC 4252).
                    await Transport.SendPacketAsync(BuildFailure(remaining, PartialSuccess: true), CancellationToken).ConfigureAwait(false);
                    continue;

                }

                if (request.Method != NoneMethod)
                    await EmitAsync(AuditSink, new AuthMethodFailedEvent(clock.GetUtcNow(), request.Username, request.Method, "rejected"), CancellationToken).ConfigureAwait(false);

                await Transport.SendPacketAsync(BuildFailure(Authenticator.OfferedMethods, PartialSuccess: false), CancellationToken).ConfigureAwait(false);

                // "none" probes don't burn the budget; a real failed attempt does.
                if (request.Method != NoneMethod)
                {
                    failedAttempts++;
                    if (failedAttempts >= MaxAuthTries)
                    {
                        await EmitAsync(AuditSink, new AuthenticationFailedEvent(clock.GetUtcNow(), request.Username, failedAttempts), CancellationToken).ConfigureAwait(false);
                        throw new SshAuthenticationException($"Authentication failed after {failedAttempts} attempts.");
                    }
                }

            }

        }

        private static ValueTask EmitAsync(ISshAuditSink? Sink, SshAuditEvent Event, CancellationToken CancellationToken)
            => Sink?.WriteAsync(Event, CancellationToken) ?? ValueTask.CompletedTask;

        #endregion

        #region (private) server method evaluation

        // Returns (succeeded, isQueryOnly). A publickey query (no signature) is answered with PK_OK here.
        private static async ValueTask<(Boolean Succeeded, Boolean QueryOnly)> EvaluatePublicKeyAsync(SshTransport Transport, ISshUserAuthenticator Authenticator, AuthRequest Request, CancellationToken CancellationToken)
        {

            var authorized = await Authenticator.AuthorizePublicKeyAsync(
                                     new SshPublicKeyAuthRequest(Request.Username, Request.Algorithm, Request.PublicKeyBlob),
                                     CancellationToken).ConfigureAwait(false);

            if (!Request.HasSignature)
            {
                if (authorized)
                {
                    await Transport.SendPacketAsync(BuildPublicKeyOk(Request.Algorithm, Request.PublicKeyBlob), CancellationToken).ConfigureAwait(false);
                    return (false, true);
                }
                return (false, false);
            }

            var signedData = BuildPublicKeySignedData(Transport.SessionId, Request.Username, Request.Algorithm, Request.PublicKeyBlob);
            return (authorized && SshSignature.Verify(Request.PublicKeyBlob, signedData, Request.Signature), false);

        }

        private static async ValueTask<Boolean> EvaluateKeyboardInteractiveAsync(SshTransport Transport, ISshUserAuthenticator Authenticator, String Username, CancellationToken CancellationToken)
        {

            var factor = Authenticator.GetKeyboardInteractive(Username);
            if (factor is null)
                return false;

            await Transport.SendPacketAsync(BuildInfoRequest(factor), CancellationToken).ConfigureAwait(false);

            var responsePayload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
            var responses       = ParseInfoResponse(responsePayload);

            return await factor.ValidateAsync(responses, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region ClientPublicKeyAuthenticateAsync(Transport, Username, Key, Algorithm = null, BannerCallback = null, CancellationToken = default)

        /// <summary>
        /// Run the client side of <c>publickey</c> authentication for a single key: request the
        /// <c>ssh-userauth</c> service, query whether the key is acceptable, and only then sign.
        /// </summary>
        /// <param name="Transport">The established transport.</param>
        /// <param name="Username">The login name.</param>
        /// <param name="Key">The signing key (the user's private key).</param>
        /// <param name="Algorithm">
        /// The signature algorithm to use; when null, chosen from the key's algorithms, preferring one the
        /// server advertised via <c>server-sig-algs</c> (so RSA keys use rsa-sha2-256/512, never SHA-1).
        /// </param>
        /// <param name="BannerCallback">An optional callback receiving a server banner (text, language).</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <returns>True on success; false if the server rejected the key.</returns>
        public static async ValueTask<Boolean> ClientPublicKeyAuthenticateAsync(SshTransport             Transport,
                                                                                String                   Username,
                                                                                ISshHostKey              Key,
                                                                                String?                  Algorithm       = null,
                                                                                Action<String, String>?  BannerCallback  = null,
                                                                                CancellationToken        CancellationToken = default)
        {
            await EnsureAuthServiceAsync(Transport, BannerCallback, CancellationToken).ConfigureAwait(false);
            return await TryPublicKeyAsync(Transport, Username, Key, Algorithm, BannerCallback, CancellationToken).ConfigureAwait(false) == AttemptOutcome.Success;
        }

        #endregion

        #region ClientPasswordAuthenticateAsync(Transport, Username, Password, …)

        /// <summary>Run the client side of <c>password</c> authentication (RFC 4252 §8).</summary>
        public static async ValueTask<Boolean> ClientPasswordAuthenticateAsync(SshTransport             Transport,
                                                                               String                   Username,
                                                                               String                   Password,
                                                                               Action<String, String>?  BannerCallback  = null,
                                                                               CancellationToken        CancellationToken = default)
        {
            await EnsureAuthServiceAsync(Transport, BannerCallback, CancellationToken).ConfigureAwait(false);
            return await TryPasswordAsync(Transport, Username, Password, BannerCallback, CancellationToken).ConfigureAwait(false) == AttemptOutcome.Success;
        }

        #endregion

        #region ClientKeyboardInteractiveAuthenticateAsync(Transport, Username, Responder, …)

        /// <summary>
        /// Run the client side of <c>keyboard-interactive</c> authentication (RFC 4256): the
        /// <paramref name="Responder"/> answers each challenge (e.g. supplies a TOTP code).
        /// </summary>
        public static async ValueTask<Boolean> ClientKeyboardInteractiveAuthenticateAsync(SshTransport                                                                Transport,
                                                                                          String                                                                      Username,
                                                                                          Func<SshKeyboardInteractiveChallenge, CancellationToken, ValueTask<String[]>>  Responder,
                                                                                          Action<String, String>?                                                     BannerCallback  = null,
                                                                                          CancellationToken                                                           CancellationToken = default)
        {
            await EnsureAuthServiceAsync(Transport, BannerCallback, CancellationToken).ConfigureAwait(false);
            return await TryKeyboardInteractiveAsync(Transport, Username, Responder, BannerCallback, CancellationToken).ConfigureAwait(false) == AttemptOutcome.Success;
        }

        #endregion

        #region ClientAuthenticateAsync(Transport, Username, Keys?, Password?, KeyboardInteractive?, …)

        /// <summary>
        /// Drive client authentication across methods, handling multi-factor chains via partial success:
        /// try each public key (query-then-sign), and when the server signals partial success (2FA) or when
        /// no key succeeds, continue with keyboard-interactive and/or password as available.
        /// </summary>
        public static async ValueTask<Boolean> ClientAuthenticateAsync(SshTransport                                                                  Transport,
                                                                       String                                                                        Username,
                                                                       IEnumerable<ISshHostKey>?                                                     Keys                 = null,
                                                                       String?                                                                       Password             = null,
                                                                       Func<SshKeyboardInteractiveChallenge, CancellationToken, ValueTask<String[]>>?  KeyboardInteractive  = null,
                                                                       Action<String, String>?                                                       BannerCallback       = null,
                                                                       CancellationToken                                                             CancellationToken     = default)
        {

            await EnsureAuthServiceAsync(Transport, BannerCallback, CancellationToken).ConfigureAwait(false);

            var partial = false;

            // Phase 1: public keys (query-then-sign).
            foreach (var key in Keys ?? [])
            {
                var outcome = await TryPublicKeyAsync(Transport, Username, key, null, BannerCallback, CancellationToken).ConfigureAwait(false);
                if (outcome == AttemptOutcome.Success)         return true;
                if (outcome == AttemptOutcome.PartialSuccess) { partial = true; break; }
            }

            // Phase 2: a second factor after partial success (2FA), or a fallback method otherwise.
            if (KeyboardInteractive is not null)
            {
                var outcome = await TryKeyboardInteractiveAsync(Transport, Username, KeyboardInteractive, BannerCallback, CancellationToken).ConfigureAwait(false);
                if (outcome == AttemptOutcome.Success) return true;
            }

            if (!partial && Password is not null)
            {
                var outcome = await TryPasswordAsync(Transport, Username, Password, BannerCallback, CancellationToken).ConfigureAwait(false);
                if (outcome == AttemptOutcome.Success) return true;
            }

            return false;

        }

        #endregion


        #region (private) Flow helpers

        private static async ValueTask ExpectServiceRequestAsync(SshTransport Transport, CancellationToken CancellationToken)
        {
            while (true)
            {

                var payload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);

                if (Transport.TryHandleExtInfo(payload))
                    continue;

                var reader = new SshPacketReader(payload);
                if (reader.ReadByte() != (Byte) SshMessageNumber.ServiceRequest)
                    throw new SshWireException("Expected SSH_MSG_SERVICE_REQUEST (5) after the key exchange.");

                var service = reader.ReadString();
                if (service != AuthService)
                    throw new SshWireException($"The client requested the unsupported service '{service}'.");

                return;

            }
        }

        // The client sends SERVICE_REQUEST and waits for SERVICE_ACCEPT (surfacing banners / ext-info).
        private static async ValueTask EnsureAuthServiceAsync(SshTransport Transport, Action<String, String>? BannerCallback, CancellationToken CancellationToken)
        {

            await Transport.SendPacketAsync(BuildServiceRequest(AuthService), CancellationToken).ConfigureAwait(false);

            while (true)
            {

                var payload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);

                if (Transport.TryHandleExtInfo(payload))
                    continue;

                var reader  = new SshPacketReader(payload);
                var message = (SshMessageNumber) reader.ReadByte();

                if (message == SshMessageNumber.UserAuthBanner)
                {
                    HandleBanner(ref reader, BannerCallback);
                    continue;
                }

                if (message == SshMessageNumber.ServiceAccept)
                    return;

                throw new SshWireException($"Expected SSH_MSG_SERVICE_ACCEPT (6), but found message number {(Byte) message}.");

            }

        }

        #endregion

        #region (private) client method attempts

        private enum AttemptOutcome { Success, PartialSuccess, Failure }

        private static async ValueTask<AttemptOutcome> TryPublicKeyAsync(SshTransport Transport, String Username, ISshHostKey Key, String? Algorithm, Action<String, String>? BannerCallback, CancellationToken CancellationToken)
        {

            var algorithm      = Algorithm ?? ChooseAlgorithm(Key, Transport.PeerServerSignatureAlgorithms);
            var publicKeyBlob  = Key.PublicKeyBlob;

            // Query: offer the key unsigned; the server answers PK_OK (would accept) or FAILURE.
            await Transport.SendPacketAsync(BuildPublicKeyRequest(Username, algorithm, publicKeyBlob, Signature: null), CancellationToken).ConfigureAwait(false);

            var (queryReply, _) = await ReadReplyAsync(Transport, BannerCallback, CancellationToken).ConfigureAwait(false);
            if (queryReply == (Byte) SshMessageNumber.UserAuthFailure)
                return AttemptOutcome.Failure;
            if (queryReply != (Byte) SshMessageNumber.UserAuth60)
                throw new SshWireException($"Expected SSH_MSG_USERAUTH_PK_OK (60), but found message number {queryReply}.");

            // Sign the session-bound request and send it for real.
            var signedData = BuildPublicKeySignedData(Transport.SessionId, Username, algorithm, publicKeyBlob);
            var signature  = Key.Sign(algorithm, signedData);
            await Transport.SendPacketAsync(BuildPublicKeyRequest(Username, algorithm, publicKeyBlob, signature), CancellationToken).ConfigureAwait(false);

            return await ReadOutcomeAsync(Transport, BannerCallback, CancellationToken).ConfigureAwait(false);

        }

        private static async ValueTask<AttemptOutcome> TryPasswordAsync(SshTransport Transport, String Username, String Password, Action<String, String>? BannerCallback, CancellationToken CancellationToken)
        {
            await Transport.SendPacketAsync(BuildPasswordRequest(Username, Password), CancellationToken).ConfigureAwait(false);
            return await ReadOutcomeAsync(Transport, BannerCallback, CancellationToken).ConfigureAwait(false);
        }

        private static async ValueTask<AttemptOutcome> TryKeyboardInteractiveAsync(SshTransport Transport, String Username, Func<SshKeyboardInteractiveChallenge, CancellationToken, ValueTask<String[]>> Responder, Action<String, String>? BannerCallback, CancellationToken CancellationToken)
        {

            await Transport.SendPacketAsync(BuildKeyboardInteractiveRequest(Username), CancellationToken).ConfigureAwait(false);

            while (true)
            {

                var payload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var message = payload.Length > 0 ? payload[0] : (Byte) 0;

                if (message == (Byte) SshMessageNumber.UserAuthBanner)
                    continue;

                if (message == (Byte) SshMessageNumber.UserAuth60)   // INFO_REQUEST
                {
                    var challenge  = ParseInfoRequest(payload);
                    var responses  = await Responder(challenge, CancellationToken).ConfigureAwait(false);
                    await Transport.SendPacketAsync(BuildInfoResponse(responses), CancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (message == (Byte) SshMessageNumber.UserAuthSuccess) return AttemptOutcome.Success;
                if (message == (Byte) SshMessageNumber.UserAuthFailure) return ParsePartialSuccess(payload) ? AttemptOutcome.PartialSuccess : AttemptOutcome.Failure;

                throw new SshWireException($"Unexpected message {message} during keyboard-interactive authentication.");

            }

        }

        // Read the next SUCCESS / FAILURE outcome (surfacing banners), distinguishing partial success.
        private static async ValueTask<AttemptOutcome> ReadOutcomeAsync(SshTransport Transport, Action<String, String>? BannerCallback, CancellationToken CancellationToken)
        {

            var (message, payload) = await ReadReplyAsync(Transport, BannerCallback, CancellationToken).ConfigureAwait(false);

            return message == (Byte) SshMessageNumber.UserAuthSuccess ? AttemptOutcome.Success
                 : message == (Byte) SshMessageNumber.UserAuthFailure ? (ParsePartialSuccess(payload) ? AttemptOutcome.PartialSuccess : AttemptOutcome.Failure)
                 : throw new SshWireException($"Expected SSH_MSG_USERAUTH_SUCCESS/FAILURE, but found message number {message}.");

        }

        // Read the next reply, surfacing banners, returning both the message number and the raw payload.
        private static async ValueTask<(Byte Message, Byte[] Payload)> ReadReplyAsync(SshTransport Transport, Action<String, String>? BannerCallback, CancellationToken CancellationToken)
        {
            while (true)
            {

                var payload = await Transport.ReceivePacketAsync(CancellationToken).ConfigureAwait(false);
                var reader  = new SshPacketReader(payload);
                var message = reader.ReadByte();

                if (message == (Byte) SshMessageNumber.UserAuthBanner)
                {
                    HandleBanner(ref reader, BannerCallback);
                    continue;
                }

                return (message, payload);

            }
        }

        private static Boolean ParsePartialSuccess(ReadOnlySpan<Byte> FailurePayload)
        {
            var reader = new SshPacketReader(FailurePayload);
            reader.ReadByte();          // SSH_MSG_USERAUTH_FAILURE
            reader.ReadNameList();      // remaining methods
            return reader.ReadBoolean();
        }

        private static void HandleBanner(ref SshPacketReader Reader, Action<String, String>? BannerCallback)
        {
            var text      = Reader.ReadString();
            var language  = Reader.ReadString();
            BannerCallback?.Invoke(text, language);
        }

        // Prefer a signature algorithm the server advertised (server-sig-algs); otherwise the key's default.
        private static String ChooseAlgorithm(ISshHostKey Key, String[]? ServerSignatureAlgorithms)
        {

            if (ServerSignatureAlgorithms is not null)
                foreach (var candidate in Key.AlgorithmNames)
                    if (Array.IndexOf(ServerSignatureAlgorithms, candidate) >= 0)
                        return candidate;

            return Key.AlgorithmNames[0];

        }

        #endregion

        #region (private) ParseAuthRequest(Payload)

        private readonly record struct AuthRequest(String   Username,
                                                   String   Method,
                                                   Boolean  HasSignature,
                                                   String   Algorithm,
                                                   Byte[]   PublicKeyBlob,
                                                   Byte[]   Signature,
                                                   String   Password);

        // Fully parse a USERAUTH_REQUEST synchronously — the ref-struct reader must not cross an await.
        private static AuthRequest ParseAuthRequest(ReadOnlySpan<Byte> Payload)
        {

            var reader        = new SshPacketReader(Payload);
            var messageNumber = reader.ReadByte();

            if (messageNumber != (Byte) SshMessageNumber.UserAuthRequest)
                throw new SshWireException($"Expected SSH_MSG_USERAUTH_REQUEST (50) during authentication, but found {(SshMessageNumber) messageNumber} ({messageNumber}).");

            var username  = reader.ReadString();
            _             = reader.ReadString();   // service name ("ssh-connection")
            var method    = reader.ReadString();

            if (method == PublicKeyMethod)
            {
                var hasSignature   = reader.ReadBoolean();
                var algorithm      = reader.ReadString();
                var publicKeyBlob  = reader.ReadBinaryString();
                var signature      = hasSignature ? reader.ReadBinaryString() : [];
                return new AuthRequest(username, method, hasSignature, algorithm, publicKeyBlob, signature, "");
            }

            if (method == PasswordMethod)
            {
                _             = reader.ReadBoolean();   // change-password flag (unsupported)
                var pw        = reader.ReadString();
                return new AuthRequest(username, method, false, "", [], [], pw);
            }

            return new AuthRequest(username, method, false, "", [], [], "");

        }

        #endregion

        #region (private) Message builders

        private static Byte[] BuildServiceRequest(String Service)
        {
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteByte((Byte) SshMessageNumber.ServiceRequest);
            writer.WriteString(Service);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildServiceAccept(String Service)
        {
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteByte((Byte) SshMessageNumber.ServiceAccept);
            writer.WriteString(Service);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildBanner(String Message)
        {
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteByte((Byte) SshMessageNumber.UserAuthBanner);
            writer.WriteString(Message);
            writer.WriteString("");   // language tag
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildFailure(IReadOnlyList<String> Methods, Boolean PartialSuccess)
        {
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteByte((Byte) SshMessageNumber.UserAuthFailure);
            writer.WriteNameList(Methods);
            writer.WriteBoolean(PartialSuccess);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildInfoRequest(ISshKeyboardInteractiveFactor Factor)
        {

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);

            writer.WriteByte((Byte) SshMessageNumber.UserAuth60);   // SSH_MSG_USERAUTH_INFO_REQUEST
            writer.WriteString(Factor.Name);
            writer.WriteString(Factor.Instruction);
            writer.WriteString("");                                 // language tag
            writer.WriteUInt32((UInt32) Factor.Prompts.Count);

            foreach (var prompt in Factor.Prompts)
            {
                writer.WriteString(prompt.Text);
                writer.WriteBoolean(prompt.Echo);
            }

            return abw.WrittenSpan.ToArray();

        }

        private static String[] ParseInfoResponse(ReadOnlySpan<Byte> Payload)
        {

            var reader = new SshPacketReader(Payload);

            if (reader.ReadByte() != (Byte) SshMessageNumber.UserAuthInfoResponse)
                throw new SshWireException("Expected SSH_MSG_USERAUTH_INFO_RESPONSE (61).");

            var count      = reader.ReadUInt32();
            if (count > 64)
                throw new SshWireException("Implausible keyboard-interactive response count.");

            var responses  = new String[count];
            for (var i = 0U; i < count; i++)
                responses[i] = reader.ReadString();

            return responses;

        }

        private static Byte[] BuildPublicKeyOk(String Algorithm, ReadOnlySpan<Byte> PublicKeyBlob)
        {
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteByte((Byte) SshMessageNumber.UserAuth60);   // SSH_MSG_USERAUTH_PK_OK
            writer.WriteString(Algorithm);
            writer.WriteBinaryString(PublicKeyBlob);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildPublicKeyRequest(String Username, String Algorithm, ReadOnlySpan<Byte> PublicKeyBlob, ReadOnlySpan<Byte> Signature)
        {

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);

            writer.WriteByte((Byte) SshMessageNumber.UserAuthRequest);
            writer.WriteString(Username);
            writer.WriteString(ConnectionService);
            writer.WriteString(PublicKeyMethod);
            writer.WriteBoolean(!Signature.IsEmpty);
            writer.WriteString(Algorithm);
            writer.WriteBinaryString(PublicKeyBlob);

            if (!Signature.IsEmpty)
                writer.WriteBinaryString(Signature);

            return abw.WrittenSpan.ToArray();

        }

        private static Byte[] BuildPasswordRequest(String Username, String Password)
        {
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteByte((Byte) SshMessageNumber.UserAuthRequest);
            writer.WriteString(Username);
            writer.WriteString(ConnectionService);
            writer.WriteString(PasswordMethod);
            writer.WriteBoolean(false);        // not a password change
            writer.WriteString(Password);
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildKeyboardInteractiveRequest(String Username)
        {
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteByte((Byte) SshMessageNumber.UserAuthRequest);
            writer.WriteString(Username);
            writer.WriteString(ConnectionService);
            writer.WriteString(KeyboardInteractiveMethod);
            writer.WriteString("");            // language tag
            writer.WriteString("");            // submethods
            return abw.WrittenSpan.ToArray();
        }

        private static Byte[] BuildInfoResponse(IReadOnlyList<String> Responses)
        {
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteByte((Byte) SshMessageNumber.UserAuthInfoResponse);
            writer.WriteUInt32((UInt32) Responses.Count);
            foreach (var response in Responses)
                writer.WriteString(response);
            return abw.WrittenSpan.ToArray();
        }

        private static SshKeyboardInteractiveChallenge ParseInfoRequest(ReadOnlySpan<Byte> Payload)
        {

            var reader = new SshPacketReader(Payload);
            reader.ReadByte();                 // SSH_MSG_USERAUTH_INFO_REQUEST

            var name         = reader.ReadString();
            var instruction  = reader.ReadString();
            _                = reader.ReadString();   // language tag
            var count        = reader.ReadUInt32();

            if (count > 64)
                throw new SshWireException("Implausible keyboard-interactive prompt count.");

            var prompts = new List<SshPrompt>((Int32) count);
            for (var i = 0U; i < count; i++)
            {
                var text = reader.ReadString();
                var echo = reader.ReadBoolean();
                prompts.Add(new SshPrompt(text, echo));
            }

            return new SshKeyboardInteractiveChallenge(name, instruction, prompts);

        }

        // The data a publickey signature covers (RFC 4252 §7): session id + the request up to the key blob.
        private static Byte[] BuildPublicKeySignedData(ReadOnlySpan<Byte> SessionId, String Username, String Algorithm, ReadOnlySpan<Byte> PublicKeyBlob)
        {

            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);

            writer.WriteBinaryString(SessionId);
            writer.WriteByte((Byte) SshMessageNumber.UserAuthRequest);
            writer.WriteString(Username);
            writer.WriteString(ConnectionService);
            writer.WriteString(PublicKeyMethod);
            writer.WriteBoolean(true);
            writer.WriteString(Algorithm);
            writer.WriteBinaryString(PublicKeyBlob);

            return abw.WrittenSpan.ToArray();

        }

        #endregion

    }


    /// <summary>Thrown when SSH user authentication fails or is exhausted.</summary>
    public sealed class SshAuthenticationException : Exception
    {
        /// <summary>Create a new authentication exception.</summary>
        public SshAuthenticationException(String Message) : base(Message) { }
    }

}
