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
    /// PLAIN authentication (RFC 4616)
    /// Format: Base64(authzid NUL authcid NUL password)
    /// </summary>
    public sealed class PlainAuthHandler(IUserStore userStore, ILogger logger) : AuthHandler
    {
        public override string MechanismName => "PLAIN";

        public override async Task<AuthResponse> ProcessAsync(string? clientResponse, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(clientResponse))
            {
                // Request credentials
                return new AuthResponse(AuthResult.Continue, Challenge: "");
            }

            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(clientResponse));
                var parts = decoded.Split('\0');

                if (parts.Length < 3)
                {
                    logger.Log(LogLevel.Warning, "PLAIN auth: Invalid format");
                    return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Authentication failed");
                }

                var authzid = parts[0];  // Authorization identity (often empty)
                var authcid = parts[1];  // Authentication identity (username)
                var password = parts[2];

                // Use authzid if provided, otherwise authcid
                var username = string.IsNullOrEmpty(authzid) ? authcid : authzid;

                if (await userStore.ValidatePasswordAsync(username, password, ct))
                {
                    logger.Log(LogLevel.Info, $"PLAIN auth: User '{username}' authenticated");
                    return new AuthResponse(AuthResult.Success, Username: username);
                }

                logger.Log(LogLevel.Warning, $"PLAIN auth: Invalid credentials for '{username}'");
                return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Authentication credentials invalid");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, $"PLAIN auth error: {ex.Message}");
                return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Authentication failed");
            }
        }
    }

}
