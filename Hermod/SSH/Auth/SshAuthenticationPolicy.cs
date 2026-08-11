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
    /// A composable server authentication policy: combine <c>publickey</c>, <c>password</c> and
    /// keyboard-interactive factors, and require a second factor per user to build a multi-factor chain
    /// (e.g. <c>publickey,keyboard-interactive</c> for TOTP) via partial-success.
    /// </summary>
    public sealed class SshAuthenticationPolicy : ISshUserAuthenticator
    {

        #region Data

        private Func<SshPublicKeyAuthRequest, CancellationToken, ValueTask<Boolean>>?  publicKey;
        private Func<String, String, CancellationToken, ValueTask<Boolean>>?           password;
        private Func<String, ISshKeyboardInteractiveFactor?>?                          keyboardInteractive;
        private Func<String, IReadOnlyList<String>>?                                   requiredMethods;

        #endregion

        #region Properties

        /// <summary>The methods advertised to a client before any success (RFC 4252).</summary>
        public IReadOnlyList<String> OfferedMethods
        {
            get
            {
                var methods = new List<String>();
                if (publicKey           is not null) methods.Add(UserAuthentication.PublicKeyMethod);
                if (password            is not null) methods.Add(UserAuthentication.PasswordMethod);
                if (keyboardInteractive is not null) methods.Add(UserAuthentication.KeyboardInteractiveMethod);
                return methods;
            }
        }

        #endregion


        #region WithPublicKey / WithAuthorizedKeys

        /// <summary>Enable <c>publickey</c> with a custom authorization callback.</summary>
        public SshAuthenticationPolicy WithPublicKey(Func<SshPublicKeyAuthRequest, CancellationToken, ValueTask<Boolean>> AuthorizePublicKey)
        {
            publicKey = AuthorizePublicKey;
            return this;
        }

        /// <summary>Enable <c>publickey</c> against parsed authorized-keys entries (validity windows enforced).</summary>
        public SshAuthenticationPolicy WithAuthorizedKeys(IEnumerable<AuthorizedKey> AuthorizedKeys, TimeProvider? TimeProvider = null)
        {
            var keys   = AuthorizedKeys.ToArray();
            var clock  = TimeProvider ?? System.TimeProvider.System;
            publicKey  = (request, _) =>
            {
                var now = clock.GetUtcNow();
                return ValueTask.FromResult(keys.Any(key => !key.IsCertAuthority && key.Matches(request.PublicKeyBlob) && key.IsValidAt(now)));
            };
            return this;
        }

        /// <summary>
        /// Accept user certificates signed by a trusted CA (OpenSSH <c>TrustedUserCAKeys</c> equivalent):
        /// a presented certificate is validated (CA trust, signature, validity, principals = the login name,
        /// critical options, revocation) before the client's own signature is checked. Combine with
        /// <see cref="WithAuthorizedKeys"/> / <see cref="WithPublicKey"/> for plain-key fallback.
        /// </summary>
        public SshAuthenticationPolicy WithCertificateAuthority(SshCertificateAuthorityTrust Trust, TimeProvider? TimeProvider = null)
        {

            var previous  = publicKey;
            var clock     = TimeProvider ?? System.TimeProvider.System;

            publicKey = (request, cancellationToken) =>
            {

                if (SshCertificate.IsCertificateAlgorithm(request.Algorithm) &&
                    SshCertificate.TryParse(request.PublicKeyBlob, out var certificate))
                {
                    // The restrictions a certificate carries are applied by the session layer, which
                    // reads them straight off the authenticated blob — so declare them enforced here.
                    var validation = SshCertificateValidator.Validate(certificate!, SshCertType.User, request.Username, Trust, clock.GetUtcNow(),
                                                                      SshSessionRestrictions.EnforcedCriticalOptions);
                    return ValueTask.FromResult(validation.IsValid);
                }

                // Not a certificate — defer to any configured plain public-key policy.
                return previous?.Invoke(request, cancellationToken) ?? ValueTask.FromResult(false);

            };

            return this;

        }

        #endregion

        #region WithPassword

        /// <summary>Enable <c>password</c> authentication with a validation callback.</summary>
        public SshAuthenticationPolicy WithPassword(Func<String, String, CancellationToken, ValueTask<Boolean>> CheckPassword)
        {
            password = CheckPassword;
            return this;
        }

        #endregion

        #region WithKeyboardInteractive / WithSecondFactor

        /// <summary>Enable a keyboard-interactive factor per user (as the sole factor).</summary>
        public SshAuthenticationPolicy WithKeyboardInteractive(Func<String, ISshKeyboardInteractiveFactor?> Factor)
        {
            keyboardInteractive = Factor;
            return this;
        }

        /// <summary>
        /// Require a keyboard-interactive second factor (e.g. TOTP) <i>after</i> publickey for users the
        /// selector returns a factor for — i.e. a <c>publickey,keyboard-interactive</c> 2FA chain. Users
        /// with no second factor stay single-factor publickey.
        /// </summary>
        public SshAuthenticationPolicy WithSecondFactor(Func<String, ISshKeyboardInteractiveFactor?> SecondFactor)
        {

            keyboardInteractive = SecondFactor;
            requiredMethods     = user => SecondFactor(user) is not null
                                              ? [ UserAuthentication.PublicKeyMethod, UserAuthentication.KeyboardInteractiveMethod ]
                                              : [];
            return this;

        }

        #endregion


        #region ISshUserAuthenticator

        public ValueTask<Boolean> AuthorizePublicKeyAsync(SshPublicKeyAuthRequest Request, CancellationToken CancellationToken = default)
            => publicKey?.Invoke(Request, CancellationToken) ?? ValueTask.FromResult(false);

        public ValueTask<Boolean> AuthorizePasswordAsync(String Username, String Password, CancellationToken CancellationToken = default)
            => password?.Invoke(Username, Password, CancellationToken) ?? ValueTask.FromResult(false);

        public ISshKeyboardInteractiveFactor? GetKeyboardInteractive(String Username)
            => keyboardInteractive?.Invoke(Username);

        public IReadOnlyList<String> RemainingMethods(String Username, IReadOnlySet<String> CompletedMethods)
        {

            var required = requiredMethods?.Invoke(Username);

            // No explicit chain: any single successful method fully authenticates.
            if (required is null || required.Count == 0)
                return [];

            return required.Where(method => !CompletedMethods.Contains(method)).ToList();

        }

        #endregion

    }

}
