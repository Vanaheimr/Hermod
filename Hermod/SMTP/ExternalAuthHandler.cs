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

using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server
{

    /// <summary>
    /// EXTERNAL authentication (RFC 4422)
    /// Uses the client certificate from TLS handshake.
    /// </summary>
    public sealed class ExternalAuthHandler(
        IUserStore          userStore,
        X509Certificate2?   clientCertificate,
        ILogger             logger) : AuthHandler
    {
        public override string MechanismName => "EXTERNAL";

        public override async Task<AuthResponse> ProcessAsync(string? clientResponse, CancellationToken ct = default)
        {
            if (clientCertificate is null)
            {
                logger.Log(LogLevel.Warning, "EXTERNAL auth: No client certificate provided");
                return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Client certificate required");
            }

            // Validate certificate
            if (clientCertificate.NotAfter < Timestamp.Now)
            {
                logger.Log(LogLevel.Warning, $"EXTERNAL auth: Certificate expired ({clientCertificate.NotAfter})");
                return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Certificate expired");
            }

            if (clientCertificate.NotBefore > Timestamp.Now)
            {
                logger.Log(LogLevel.Warning, $"EXTERNAL auth: Certificate not yet valid ({clientCertificate.NotBefore})");
                return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Certificate not yet valid");
            }

            // Optional: Check if an authorization identity was provided
            string? requestedAuthzid = null;
            if (!string.IsNullOrEmpty(clientResponse))
            {
                try
                {
                    requestedAuthzid = Encoding.UTF8.GetString(Convert.FromBase64String(clientResponse));
                }
                catch
                {
                    // Ignore decode errors
                }
            }

            // Look up user by certificate
            var user = await userStore.GetUserByCertificateAsync(clientCertificate, ct);
            if (user is null)
            {
                logger.Log(LogLevel.Warning, $"EXTERNAL auth: No user found for certificate {clientCertificate.Thumbprint}");
                return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Certificate not authorized");
            }

            // If authzid was requested, verify it matches
            if (!string.IsNullOrEmpty(requestedAuthzid) && 
                !requestedAuthzid.Equals(user.Username, StringComparison.OrdinalIgnoreCase))
            {
                logger.Log(LogLevel.Warning, $"EXTERNAL auth: Requested authzid '{requestedAuthzid}' doesn't match certificate user '{user.Username}'");
                return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Authorization identity mismatch");
            }

            logger.Log(LogLevel.Info, $"EXTERNAL auth: User '{user.Username}' authenticated via certificate {clientCertificate.Thumbprint[..8]}...");
            return new AuthResponse(AuthResult.Success, Username: user.Username);
        }
    }

}
