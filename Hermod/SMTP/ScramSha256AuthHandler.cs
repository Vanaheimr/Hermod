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
    /// SCRAM-SHA-256 authentication (RFC 7677)
    /// Provides mutual authentication without transmitting the password.
    /// </summary>
    public sealed class ScramSha256AuthHandler(IUserStore userStore, ILogger logger) : AuthHandler
    {
        public override string MechanismName => "SCRAM-SHA-256";

        private enum State { WaitingClientFirst, WaitingClientFinal, Complete }
        private State _state = State.WaitingClientFirst;

        private string? _username;
        private string? _clientNonce;
        private string? _serverNonce;
        private string? _clientFirstBare;
        private string? _serverFirstMessage;
        private UserCredentials? _userCredentials;

        public override async Task<AuthResponse> ProcessAsync(string? clientResponse, CancellationToken ct = default)
        {
            try
            {
                return _state switch
                {
                    State.WaitingClientFirst => await ProcessClientFirstAsync(clientResponse, ct),
                    State.WaitingClientFinal => await ProcessClientFinalAsync(clientResponse, ct),
                    _ => new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Authentication failed")
                };
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, $"SCRAM-SHA-256 auth error: {ex.Message}");
                return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Authentication failed");
            }
        }

        private async Task<AuthResponse> ProcessClientFirstAsync(string? clientResponse, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(clientResponse))
            {
                // Initial request without data
                return new AuthResponse(AuthResult.Continue, Challenge: "");
            }

            // Decode client-first-message
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(clientResponse));
            logger.Log(LogLevel.Debug, $"SCRAM client-first: {decoded}");

            // Parse: gs2-header, client-first-message-bare
            // Format: n,,n=username,r=clientNonce
            // Or with authzid: n,a=authzid,n=username,r=clientNonce
        
            var parts = decoded.Split(',');
            if (parts.Length < 4 || parts[0] != "n")
            {
                return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Invalid SCRAM message");
            }

            // Find client-first-message-bare (everything after gs2-header)
            var gs2HeaderEnd = decoded.IndexOf(',', decoded.IndexOf(',') + 1) + 1;
            _clientFirstBare = decoded[gs2HeaderEnd..];

            // Parse attributes
            var attrs = ParseScramAttributes(_clientFirstBare);
        
            if (!attrs.TryGetValue("n", out _username) || !attrs.TryGetValue("r", out _clientNonce))
            {
                return new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Invalid SCRAM message");
            }

            // Get user credentials
            _userCredentials = await userStore.GetUserAsync(_username, ct);
            if (_userCredentials?.ScramStoredKey is null)
            {
                // User not found or no SCRAM credentials - still continue to prevent user enumeration
                logger.Log(LogLevel.Warning, $"SCRAM: User '{_username}' not found or no SCRAM credentials");
                // Generate fake credentials to prevent timing attacks
                _userCredentials = new UserCredentials(
                    _username,
                    null,
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)),
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                    4096,
                    []
                );
            }

            // Generate server nonce
            _serverNonce = _clientNonce + Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));

            // Build server-first-message
            _serverFirstMessage = $"r={_serverNonce},s={_userCredentials.ScramSalt},i={_userCredentials.ScramIterations}";
        
            logger.Log(LogLevel.Debug, $"SCRAM server-first: {_serverFirstMessage}");

            _state = State.WaitingClientFinal;
            return new AuthResponse(
                AuthResult.Continue,
                Challenge: Convert.ToBase64String(Encoding.UTF8.GetBytes(_serverFirstMessage))
            );
        }

        private Task<AuthResponse> ProcessClientFinalAsync(string? clientResponse, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(clientResponse))
            {
                return Task.FromResult(new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Authentication failed"));
            }

            // Decode client-final-message
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(clientResponse));
            logger.Log(LogLevel.Debug, $"SCRAM client-final: {decoded}");

            // Parse: c=channel-binding,r=nonce,p=ClientProof
            var attrs = ParseScramAttributes(decoded);

            if (!attrs.TryGetValue("r", out var nonce) || 
                !attrs.TryGetValue("p", out var clientProofBase64) ||
                !attrs.TryGetValue("c", out var channelBinding))
            {
                return Task.FromResult(new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Invalid SCRAM message"));
            }

            // Verify nonce
            if (nonce != _serverNonce)
            {
                logger.Log(LogLevel.Warning, "SCRAM: Nonce mismatch");
                return Task.FromResult(new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Authentication failed"));
            }

            // Build AuthMessage
            var clientFinalWithoutProof = $"c={channelBinding},r={nonce}";
            var authMessage = $"{_clientFirstBare},{_serverFirstMessage},{clientFinalWithoutProof}";
        
            logger.Log(LogLevel.Debug, $"SCRAM AuthMessage: {authMessage}");

            // Compute ClientSignature = HMAC(StoredKey, AuthMessage)
            var storedKey = Convert.FromBase64String(_userCredentials!.ScramStoredKey!);
            var clientSignature = HMACSHA256.HashData(storedKey, Encoding.UTF8.GetBytes(authMessage));

            // Compute ClientKey = ClientProof XOR ClientSignature
            var clientProof = Convert.FromBase64String(clientProofBase64);
            var clientKey = new byte[clientProof.Length];
            for (int i = 0; i < clientProof.Length; i++)
            {
                clientKey[i] = (byte)(clientProof[i] ^ clientSignature[i]);
            }

            // Verify: StoredKey == SHA256(ClientKey)
            var computedStoredKey = SHA256.HashData(clientKey);
            if (!CryptographicOperations.FixedTimeEquals(computedStoredKey, storedKey))
            {
                logger.Log(LogLevel.Warning, $"SCRAM: Authentication failed for '{_username}'");
                return Task.FromResult(new AuthResponse(AuthResult.Fail, ErrorCode: "535 5.7.8 Authentication credentials invalid"));
            }

            // Compute ServerSignature = HMAC(ServerKey, AuthMessage)
            var serverKey = Convert.FromBase64String(_userCredentials.ScramServerKey!);
            var serverSignature = HMACSHA256.HashData(serverKey, Encoding.UTF8.GetBytes(authMessage));
            var serverFinalMessage = $"v={Convert.ToBase64String(serverSignature)}";

            logger.Log(LogLevel.Info, $"SCRAM-SHA-256 auth: User '{_username}' authenticated");
            _state = State.Complete;

            return Task.FromResult(new AuthResponse(
                AuthResult.Success,
                Username: _username,
                Message: Convert.ToBase64String(Encoding.UTF8.GetBytes(serverFinalMessage))
            ));
        }

        private static Dictionary<string, string> ParseScramAttributes(string message)
        {
            var result = new Dictionary<string, string>();
            foreach (var part in message.Split(','))
            {
                var eqIndex = part.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = part[..eqIndex];
                    var value = part[(eqIndex + 1)..];
                    result[key] = value;
                }
            }
            return result;
        }

        public override void Reset()
        {
            _state = State.WaitingClientFirst;
            _username = null;
            _clientNonce = null;
            _serverNonce = null;
            _clientFirstBare = null;
            _serverFirstMessage = null;
            _userCredentials = null;
        }
    }

}
