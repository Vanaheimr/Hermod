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
    /// LOGIN authentication (draft-murchison-sasl-login)
    /// Step 1: Server sends "Username:" (Base64)
    /// Step 2: Client sends username (Base64)
    /// Step 3: Server sends "Password:" (Base64)
    /// Step 4: Client sends password (Base64)
    /// </summary>
    public sealed class LoginAuthHandler(IUserStore userStore, ILogger logger) : AuthHandler
    {
        public override string MechanismName => "LOGIN";

        private enum State { WaitingUsername, WaitingPassword, Complete }
        private State _state = State.WaitingUsername;
        private string? _username;

        public override async Task<AuthResponse> ProcessAsync(string? clientResponse, CancellationToken ct = default)
        {
            try
            {
                switch (_state)
                {
                    case State.WaitingUsername:
                        if (string.IsNullOrEmpty(clientResponse))
                        {
                            // Initial request - send username prompt
                            _state = State.WaitingUsername;
                            return new AuthResponse(
                                AuthResult.Continue,
                                Challenge: Convert.ToBase64String("Username:"u8)
                            );
                        }
                        else
                        {
                            // Received username
                            _username = Encoding.UTF8.GetString(Convert.FromBase64String(clientResponse));
                            _state = State.WaitingPassword;
                            return new AuthResponse(
                                AuthResult.Continue,
                                Challenge: Convert.ToBase64String("Password:"u8)
                            );
                        }

                    case State.WaitingPassword:
                        if (string.IsNullOrEmpty(clientResponse))
                        {
                            return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Authentication failed");
                        }

                        var password = Encoding.UTF8.GetString(Convert.FromBase64String(clientResponse));
                        _state = State.Complete;

                        if (await userStore.ValidatePasswordAsync(_username!, password, ct))
                        {
                            logger.Log(LogLevel.Info, $"LOGIN auth: User '{_username}' authenticated");
                            return new AuthResponse(AuthResult.Success, Username: _username);
                        }

                        logger.Log(LogLevel.Warning, $"LOGIN auth: Invalid credentials for '{_username}'");
                        return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Authentication credentials invalid");

                    default:
                        return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Authentication failed");
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, $"LOGIN auth error: {ex.Message}");
                return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Authentication failed");
            }
        }

        public override void Reset()
        {
            _state = State.WaitingUsername;
            _username = null;
        }
    }

}
