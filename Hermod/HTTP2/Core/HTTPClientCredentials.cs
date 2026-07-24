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
    /// What a client knows that lets it answer a challenge (RFC 9110, Section 11):
    /// a user name and password for the password-based schemes, and/or a bearer
    /// token. Which of them is usable depends on the scheme the server asks for.
    ///
    /// Deliberately a plain carrier with no store behind it — the same choice the
    /// server side makes with its app-supplied validators. Where the values come
    /// from (a config file, a secret manager, an interactive prompt) is the
    /// application's business, not this stack's.
    /// </summary>
    public sealed record HTTPClientCredentials
    {

        /// <summary>User name for Basic (RFC 7617) and Digest (RFC 7616).</summary>
        public String? UserName { get; init; }

        /// <summary>Password for Basic and Digest. Never sent in the clear by Digest.</summary>
        public String? Password { get; init; }

        /// <summary>Token for Bearer (RFC 6750) and the non-standard Token scheme.</summary>
        public String? Token    { get; init; }

        /// <summary>Credentials for the password-based schemes (Basic, Digest).</summary>
        public static HTTPClientCredentials UserNameAndPassword(String UserName, String Password)
            => new() { UserName = UserName, Password = Password };

        /// <summary>Credentials for the token-based schemes (Bearer, Token).</summary>
        public static HTTPClientCredentials BearerToken(String Token)
            => new() { Token = Token };

    }

}
