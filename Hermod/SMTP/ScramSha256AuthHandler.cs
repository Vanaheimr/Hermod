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
    /// SCRAM-SHA-256 authentication (RFC 7677, RFC 5802).
    /// Provides mutual authentication without transmitting the password: the client proves it
    /// knows the password, and the server proves it knows the user's keys with its
    /// server-final-message - a challenge in SMTP, answered with an empty response (RFC 4422
    /// §3, RFC 4954 §4).
    /// </summary>
    public sealed class ScramSha256AuthHandler(IUserStore userStore, ILogger logger) : AuthHandler
    {
        public override string MechanismName => "SCRAM-SHA-256";

        private enum State { WaitingClientFirst, WaitingClientFinal, WaitingEmptyResponse, Complete }
        private State _state = State.WaitingClientFirst;

        private string? _username;
        private string? _gs2Header;
        private string? _clientNonce;
        private string? _serverNonce;
        private string? _clientFirstBare;
        private string? _serverFirstMessage;
        private UserCredentials? _userCredentials;

        private static AuthResponse Failed(string Reply = "535 5.7.8 Authentication failed")
            => new (AuthResult.Fail, ErrorCode: Reply);

        private static AuthResponse Invalid
            => Failed("535 5.7.8 Invalid SCRAM message");

        public override async Task<AuthResponse> ProcessAsync(string? clientResponse, CancellationToken ct = default)
        {
            try
            {
                return _state switch
                {
                    State.WaitingClientFirst   => await ProcessClientFirstAsync(clientResponse, ct),
                    State.WaitingClientFinal   => ProcessClientFinal(clientResponse),
                    State.WaitingEmptyResponse => ProcessEmptyResponse(clientResponse),
                    _                          => Failed()
                };
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, $"SCRAM-SHA-256 auth error: {ex.Message}");
                return Failed();
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
            if (!TryDecode(clientResponse, out var decoded))
                return Undecodable;

            logger.Log(LogLevel.Debug, $"SCRAM client-first: {decoded}");

            // client-first-message = gs2-header client-first-message-bare
            // gs2-header           = gs2-cbind-flag "," [ authzid ] ","
            // gs2-cbind-flag       = ("p=" cb-name) / "n" / "y"
            var firstComma  = decoded.IndexOf(',');
            var secondComma = firstComma < 0 ? -1 : decoded.IndexOf(',', firstComma + 1);
            if (secondComma < 0)
                return Invalid;

            var cbindFlag = decoded[..firstComma];
            var authzid   = decoded[(firstComma + 1)..secondComma];

            // RFC 5802 §6: "n" - the client does not support channel binding; "y" - it does, and
            // thinks the server does not: "If the flag is set to "y" and the server supports
            // channel binding, the server MUST fail authentication" - this one offers no
            // SCRAM-SHA-256-PLUS, so "y" is no downgrade. "p=" asks for channel binding, which
            // it does not support.
            if (cbindFlag is not "n" and not "y")
            {
                logger.Log(LogLevel.Warning, $"SCRAM: channel binding '{cbindFlag}' requested - not supported");
                return Failed();
            }

            _gs2Header       = decoded[..(secondComma + 1)];
            _clientFirstBare = decoded[(secondComma + 1)..];

            // client-first-message-bare = [reserved-mext ","] username "," nonce ["," extensions]
            var attributes = _clientFirstBare.Split(',');

            // RFC 5802 §5.1, m: "its presence in a client or a server message MUST cause
            // authentication failure when the attribute is parsed by the other end."
            if (attributes[0].StartsWith("m=", StringComparison.Ordinal))
            {
                logger.Log(LogLevel.Warning, "SCRAM: mandatory extension requested - not supported");
                return Failed();
            }

            if (attributes.Length < 2                                    ||
                !attributes[0].StartsWith("n=", StringComparison.Ordinal) ||
                !attributes[1].StartsWith("r=", StringComparison.Ordinal) ||
                attributes[1].Length == 2)
                return Invalid;

            // RFC 5802 §5.1, n: "The characters ',' or '=' in usernames are sent as '=2C' and
            // '=3D' respectively. If the server receives a username that contains '=' not
            // followed by either '2C' or '3D', then the server MUST fail the authentication."
            _username = DecodeSaslName(attributes[0][2..]);
            if (string.IsNullOrEmpty(_username))
                return Failed();

            _clientNonce = attributes[1][2..];

            // RFC 5802 §5.1, a: "Upon the receipt of this value the server verifies its
            // correctness according to the used SASL protocol profile. Failed verification
            // results in failed authentication exchange." As with PLAIN: one may name oneself,
            // and acting as somebody else is nothing this server grants.
            if (authzid.Length > 0)
            {
                var requested = authzid.StartsWith("a=", StringComparison.Ordinal) ? DecodeSaslName(authzid[2..]) : null;

                if (string.IsNullOrEmpty(requested))
                    return Invalid;

                if (!requested.Equals(_username, StringComparison.OrdinalIgnoreCase))
                {
                    logger.Log(LogLevel.Warning, $"SCRAM: '{_username}' asked to act as '{requested}' - refused");
                    return Failed();
                }
            }

            // Get user credentials
            try
            {
                _userCredentials = await userStore.GetUserAsync(_username, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.Log(LogLevel.Error, $"SCRAM-SHA-256 auth: the user store failed: {ex.Message}");
                return TemporaryFailure;
            }
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

        private AuthResponse ProcessClientFinal(string? clientResponse)
        {
            if (string.IsNullOrEmpty(clientResponse))
                return Failed();

            // Decode client-final-message
            if (!TryDecode(clientResponse, out var decoded))
                return Undecodable;

            logger.Log(LogLevel.Debug, $"SCRAM client-final: {decoded}");

            // client-final-message = client-final-message-without-proof "," proof
            // client-final-message-without-proof = channel-binding "," nonce ["," extensions]
            var proofAt = decoded.LastIndexOf(",p=", StringComparison.Ordinal);
            if (proofAt < 0)
                return Invalid;

            var clientFinalWithoutProof = decoded[..proofAt];
            var clientProofBase64       = decoded[(proofAt + 3)..];
            var attributes              = clientFinalWithoutProof.Split(',');

            if (attributes.Any(attribute => attribute.StartsWith("m=", StringComparison.Ordinal)))
            {
                logger.Log(LogLevel.Warning, "SCRAM: mandatory extension in the client-final-message - not supported");
                return Failed();
            }

            if (attributes.Length < 2                                    ||
                !attributes[0].StartsWith("c=", StringComparison.Ordinal) ||
                !attributes[1].StartsWith("r=", StringComparison.Ordinal))
                return Invalid;

            // RFC 5802 §6: "The server MUST always validate the client's "c=" field. The server
            // does this by constructing the value of the "c=" attribute and then checking that it
            // matches the client's c= attribute value." Without channel binding data, c= is the
            // GS2 header of the client-first-message.
            byte[] channelBinding;
            try
            {
                channelBinding = Convert.FromBase64String(attributes[0][2..]);
            }
            catch (FormatException)
            {
                return Invalid;
            }

            if (!channelBinding.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(_gs2Header!)))
            {
                logger.Log(LogLevel.Warning, "SCRAM: the channel binding does not match the GS2 header");
                return Failed();
            }

            // Verify nonce
            if (attributes[1][2..] != _serverNonce)
            {
                logger.Log(LogLevel.Warning, "SCRAM: Nonce mismatch");
                return Failed();
            }

            // Build AuthMessage
            var authMessage = $"{_clientFirstBare},{_serverFirstMessage},{clientFinalWithoutProof}";

            logger.Log(LogLevel.Debug, $"SCRAM AuthMessage: {authMessage}");

            // Compute ClientSignature = HMAC(StoredKey, AuthMessage)
            var storedKey = Convert.FromBase64String(_userCredentials!.ScramStoredKey!);
            var clientSignature = HMACSHA256.HashData(storedKey, Encoding.UTF8.GetBytes(authMessage));

            // Compute ClientKey = ClientProof XOR ClientSignature
            var clientProof = Convert.FromBase64String(clientProofBase64);
            if (clientProof.Length != clientSignature.Length)
                return Failed("535 5.7.8 Authentication credentials invalid");

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
                return Failed("535 5.7.8 Authentication credentials invalid");
            }

            // Compute ServerSignature = HMAC(ServerKey, AuthMessage)
            var serverKey = Convert.FromBase64String(_userCredentials.ScramServerKey!);
            var serverSignature = HMACSHA256.HashData(serverKey, Encoding.UTF8.GetBytes(authMessage));
            var serverFinalMessage = $"v={Convert.ToBase64String(serverSignature)}";

            // RFC 4954 §4: "the SMTP protocol doesn't support the SASL feature of returning
            // additional data with a successful outcome"; RFC 4422 §3: "the additional data is
            // sent as a challenge whose response is empty. After receiving this response, the
            // server then indicates the successful outcome."
            _state = State.WaitingEmptyResponse;

            return new AuthResponse(
                AuthResult.Continue,
                Challenge: Convert.ToBase64String(Encoding.UTF8.GetBytes(serverFinalMessage))
            );
        }

        private AuthResponse ProcessEmptyResponse(string? clientResponse)
        {
            // The response to the server-final-message is empty: an empty line, or "=".
            if (!string.IsNullOrEmpty(clientResponse) && clientResponse != "=")
            {
                logger.Log(LogLevel.Warning, $"SCRAM: '{_username}' answered the server-final-message with data");
                return Failed();
            }

            logger.Log(LogLevel.Info, $"SCRAM-SHA-256 auth: User '{_username}' authenticated");
            _state = State.Complete;

            return new AuthResponse(AuthResult.Success, Username: _username);
        }

        /// <summary>
        /// A saslname (RFC 5802 §7): "=2C" is ",", "=3D" is "=", and any other "=" makes it no
        /// saslname - null.
        /// </summary>
        internal static string? DecodeSaslName(string value)
        {
            var name = new StringBuilder(value.Length);

            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != '=')
                {
                    name.Append(value[i]);
                    continue;
                }

                var escape = value.Length >= i + 3 ? value.Substring(i + 1, 2) : "";

                // Quoted strings of ABNF, which do not care about case.
                if (escape.Equals("2C", StringComparison.OrdinalIgnoreCase))
                    name.Append(',');
                else if (escape.Equals("3D", StringComparison.OrdinalIgnoreCase))
                    name.Append('=');
                else
                    return null;

                i += 2;
            }

            return name.ToString();
        }

        public override void Reset()
        {
            _state = State.WaitingClientFirst;
            _username = null;
            _gs2Header = null;
            _clientNonce = null;
            _serverNonce = null;
            _clientFirstBare = null;
            _serverFirstMessage = null;
            _userCredentials = null;
        }
    }

}
