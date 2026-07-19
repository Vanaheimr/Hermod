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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// ARC (RFC 8617) chain validation, wiring the DNS-backed public-key lookup into the pure
    /// <see cref="ArcValidator"/>.
    /// </summary>
    public sealed partial class DNSVerifier
    {

        /// <summary>
        /// Validate the Authenticated Received Chain of a message (RFC 8617). Returns None when
        /// the message carries no ARC headers, Pass when the chain is intact, Fail otherwise.
        /// </summary>
        public Task<ArcResult> VerifyArcAsync(EMailMessage message, CancellationToken ct = default)
            => new ArcValidator(ResolveDkimPublicKeyAsync, Logger).ValidateAsync(message.RawMessage, ct);

        // Resolve the base64 p= public key for a (domain, selector) from the DKIM key record.
        private async Task<string?> ResolveDkimPublicKeyAsync(string domain, string selector, CancellationToken ct)
        {
            var record = await GetTxtRecordAsync($"{selector}._domainkey.{domain}", "v=DKIM1", ct);
            if (record is null)
                return null;
            return ParseDkimRecord(record).GetValueOrDefault("p");
        }

    }

}
