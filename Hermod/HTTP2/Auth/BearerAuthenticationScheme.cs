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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// RFC 6750 "Bearer" HTTP authentication: the credentials are an opaque bearer
    /// token (an OAuth 2.0 access token / JWT / session token). The scheme just
    /// hands the token to the app validator — decoding/verifying a JWT signature,
    /// checking a token store, etc. is the validator's job and stays out of Core.
    /// </summary>
    public sealed class BearerAuthenticationScheme : IHTTPAuthenticationScheme
    {

        private readonly Func<string, CancellationToken, Task<HTTPAuthenticatedIdentity?>> validate;

        public BearerAuthenticationScheme(Func<string, CancellationToken, Task<HTTPAuthenticatedIdentity?>> Validate)
        {
            validate = Validate;
        }

        public string SchemeName => "Bearer";

        public string BuildChallenge(string Realm)
            => $"Bearer realm=\"{Realm}\"";

        public Task<HTTPAuthenticatedIdentity?> AuthenticateAsync(
            string Credentials, string Method, string RequestTarget, CancellationToken CancellationToken)
            => validate(Credentials.Trim(), CancellationToken);

    }

}
