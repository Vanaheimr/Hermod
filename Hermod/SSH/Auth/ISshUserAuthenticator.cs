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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A public-key authentication request the server evaluates before verifying the signature: the login
    /// name, the offered signature algorithm and the SSH public-key blob (RFC 4252 §7).
    /// </summary>
    public sealed record SshPublicKeyAuthRequest(String  Username,
                                                 String  Algorithm,
                                                 Byte[]  PublicKeyBlob);


    /// <summary>
    /// The outcome of a successful user authentication.
    /// </summary>
    /// <summary>
    /// The outcome of a successful authentication.
    /// </summary>
    /// <param name="Username">The authenticated account.</param>
    /// <param name="Method">The RFC 4252 method name that succeeded.</param>
    /// <param name="Restrictions">
    /// What the credential confines this session to — an OpenSSH certificate's enforced critical
    /// options (<c>force-command</c>, <c>source-address</c>). <see cref="SshSessionRestrictions.None"/>
    /// for an ordinary credential. The session layer must apply these; a certificate carrying a
    /// restriction nobody applies is refused at validation time rather than silently widened.
    /// </param>
    public sealed record SshAuthResult(String                  Username,
                                       String                  Method,
                                       SshSessionRestrictions  Restrictions);


    /// <summary>
    /// The server-side authentication policy (RFC 4252): which methods are offered, and whether a given
    /// public key is acceptable for a user. Signature verification itself is done by the auth driver — an
    /// authenticator only decides <i>authorization</i> (is this key allowed for this account?).
    /// </summary>
    public interface ISshUserAuthenticator
    {

        /// <summary>
        /// The authentication methods offered to clients (RFC 4252 names), e.g. <c>publickey</c>.
        /// </summary>
        IReadOnlyList<String> OfferedMethods { get; }

        /// <summary>
        /// Whether the given public key is authorized for the user. Called first for the client's
        /// "query" (no signature) and again once the signature has been verified.
        /// </summary>
        ValueTask<Boolean> AuthorizePublicKeyAsync(SshPublicKeyAuthRequest  Request,
                                                   CancellationToken        CancellationToken = default);

        /// <summary>
        /// The restrictions the authorizing entry places on the session — an <c>authorized_keys</c>
        /// line's <c>command=</c>/<c>from=</c>/<c>restrict</c>/<c>no-*</c> options. Called only after the
        /// key has authenticated. Defaults to unrestricted, so an authenticator that models no options
        /// need not implement it. Certificate critical options travel separately, read off the
        /// authenticated blob itself.
        /// </summary>
        /// <param name="Request">The public key that authenticated.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        ValueTask<SshSessionRestrictions> GetRestrictionsAsync(SshPublicKeyAuthRequest  Request,
                                                               CancellationToken        CancellationToken = default)
            => ValueTask.FromResult(SshSessionRestrictions.None);

        /// <summary>
        /// Whether the given password authenticates the user (RFC 4252 §8). Default: no.
        /// </summary>
        ValueTask<Boolean> AuthorizePasswordAsync(String             Username,
                                                  String             Password,
                                                  CancellationToken  CancellationToken = default)
            => ValueTask.FromResult(false);

        /// <summary>
        /// The keyboard-interactive factor for the user (e.g. a TOTP prompt), or null. Default: none.
        /// </summary>
        ISshKeyboardInteractiveFactor? GetKeyboardInteractive(String Username)
            => null;

        /// <summary>
        /// The methods still required for <paramref name="Username"/> given the methods already completed —
        /// this is what drives multi-factor chains via partial success. An empty result means the user is
        /// fully authenticated. Default: any single successful method completes authentication.
        /// </summary>
        IReadOnlyList<String> RemainingMethods(String                Username,
                                               IReadOnlySet<String>  CompletedMethods)
            => [];

    }


    /// <summary>
    /// A minimal delegate-backed <see cref="ISshUserAuthenticator"/> for public-key authentication.
    /// </summary>
    public sealed class SshUserAuthenticator : ISshUserAuthenticator
    {

        #region Data

        private readonly Func<SshPublicKeyAuthRequest, CancellationToken, ValueTask<Boolean>> authorizePublicKey;

        #endregion

        #region Properties

        public IReadOnlyList<String> OfferedMethods { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an authenticator from a public-key authorization callback.
        /// </summary>
        public SshUserAuthenticator(Func<SshPublicKeyAuthRequest, CancellationToken, ValueTask<Boolean>>  AuthorizePublicKey,
                                    IReadOnlyList<String>?                                                 OfferedMethods = null)
        {
            this.authorizePublicKey  = AuthorizePublicKey;
            this.OfferedMethods      = OfferedMethods ?? [ "publickey" ];
        }

        #endregion


        #region (static) ForAuthorizedKeys(AuthorizedKeyBlobs)

        /// <summary>
        /// An authenticator that accepts any user presenting one of the given public-key blobs (an
        /// in-memory <c>authorized_keys</c> equivalent, ignoring the login name).
        /// </summary>
        public static SshUserAuthenticator ForAuthorizedKeys(params Byte[][] AuthorizedKeyBlobs)
        {

            var authorized = AuthorizedKeyBlobs.ToArray();

            return new SshUserAuthenticator((request, _) =>
                ValueTask.FromResult(authorized.Any(blob => blob.AsSpan().SequenceEqual(request.PublicKeyBlob))));

        }

        /// <summary>
        /// An authenticator backed by parsed <c>authorized_keys</c> entries: a key is accepted only when it
        /// matches an entry that is a plain key (not a CA) and is within its validity window at the moment
        /// of authentication (evaluated via <paramref name="TimeProvider"/>).
        /// </summary>
        public static SshUserAuthenticator ForAuthorizedKeys(IEnumerable<AuthorizedKey>  AuthorizedKeys,
                                                             TimeProvider?               TimeProvider = null)
        {

            var keys   = AuthorizedKeys.ToArray();
            var clock  = TimeProvider ?? System.TimeProvider.System;

            AuthorizedKey? Match(SshPublicKeyAuthRequest request)
            {
                var now = clock.GetUtcNow();
                return keys.FirstOrDefault(key => !key.IsCertAuthority &&
                                                   key.Matches(request.PublicKeyBlob) &&
                                                   key.IsValidAt(now));
            }

            return new SshUserAuthenticator((request, _) => ValueTask.FromResult(Match(request) is not null)) {
                       // The entry that matched carries the line's command=/from=/restrict/no-* options
                       // into the session; without this they would be parsed and then ignored.
                       Restrictions = request => ValueTask.FromResult(Match(request)?.Restrictions ?? SshSessionRestrictions.None)
                   };

        }

        #endregion

        #region AuthorizePublicKeyAsync(Request, CancellationToken)

        public ValueTask<Boolean> AuthorizePublicKeyAsync(SshPublicKeyAuthRequest  Request,
                                                          CancellationToken        CancellationToken = default)

            => authorizePublicKey(Request, CancellationToken);

        /// <summary>
        /// Supplies the restrictions of the entry that authorized the key, if any.
        /// </summary>
        public Func<SshPublicKeyAuthRequest, ValueTask<SshSessionRestrictions>>? Restrictions { get; init; }

        /// <inheritdoc />
        public ValueTask<SshSessionRestrictions> GetRestrictionsAsync(SshPublicKeyAuthRequest  Request,
                                                                      CancellationToken        CancellationToken = default)

            => Restrictions?.Invoke(Request) ?? ValueTask.FromResult(SshSessionRestrictions.None);

        #endregion

    }

}
