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
using System.Security.Cryptography;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.ACME
{

    /// <summary>
    /// One challenge to be answered: what is to be proven, and the answer.
    /// </summary>
    /// <param name="Identifier">The identifier - without "*." for a wildcard.</param>
    /// <param name="Wildcard">Whether it is for a wildcard certificate.</param>
    /// <param name="Type">The challenge type, as "http-01".</param>
    /// <param name="Token">The token the certificate authority chose.</param>
    /// <param name="KeyAuthorization">The token and the account key's thumbprint: the answer.</param>
    public sealed record ACMEChallengeTask(ACMEIdentifier  Identifier,
                                           Boolean         Wildcard,
                                           String          Type,
                                           String          Token,
                                           String          KeyAuthorization)
    {

        /// <summary>
        /// For http-01: the path the certificate authority asks on port 80.
        /// </summary>
        public String  HTTPPath
            => $"/.well-known/acme-challenge/{Token}";

        /// <summary>
        /// For dns-01: the name of the TXT record (RFC 8555, section 8.4).
        /// </summary>
        public String  DNSRecordName
            => $"_acme-challenge.{Identifier.Value}";

        /// <summary>
        /// For dns-01: the text of the TXT record - the SHA-256 of the key
        /// authorization, base64url-encoded, not the key authorization itself.
        /// </summary>
        public String  DNSRecordValue
            => Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(KeyAuthorization)));

    }


    /// <summary>
    /// Something that can put the answer to a challenge where the certificate
    /// authority looks for it: a web server for http-01, a DNS zone for dns-01.
    /// </summary>
    /// <remarks>
    /// An order is answered all at once: every challenge is prepared, then
    /// every responder is asked whether its answers are out there yet
    /// (<see cref="AwaitPublicationAsync"/>), and only then is the certificate
    /// authority told to look. A DNS zone that takes a minute to reach its
    /// secondaries is waited for once, not once per name.
    /// </remarks>
    public interface IACMEChallengeResponder
    {

        /// <summary>
        /// The challenge type it answers, as "http-01" or "dns-01".
        /// </summary>
        String   ChallengeType    { get; }

        /// <summary>
        /// Whether it can answer for this identifier. http-01 cannot for a
        /// wildcard, for instance.
        /// </summary>
        Boolean  CanAnswer(ACMEIdentifier  Identifier,
                           Boolean         Wildcard);

        /// <summary>
        /// Put the answer in place.
        /// </summary>
        Task     PrepareAsync(ACMEChallengeTask  Challenge,
                              CancellationToken  CancellationToken);

        /// <summary>
        /// Wait until whatever was prepared can be seen from outside.
        /// </summary>
        Task     AwaitPublicationAsync(CancellationToken CancellationToken)
            => Task.CompletedTask;

        /// <summary>
        /// Take the answer away again - whether or not it was accepted.
        /// </summary>
        Task     CleanUpAsync(ACMEChallengeTask  Challenge,
                              CancellationToken  CancellationToken);

    }

}
