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

    public sealed class SmtpAuthManager
    {
        private readonly IUserStore _userStore;
        private readonly ILogger _logger;
        private readonly Dictionary<string, Func<X509Certificate2?, AuthHandler>> _handlerFactories;
    
        private AuthHandler? _currentHandler;
        private X509Certificate2? _clientCertificate;

        public bool IsAuthenticated { get; private set; }
        public string? AuthenticatedUser { get; private set; }
        public string? AuthenticationMethod { get; private set; }

        public SmtpAuthManager(IUserStore userStore, ILogger logger)
        {
            _userStore = userStore;
            _logger = logger;
        
            _handlerFactories = new Dictionary<string, Func<X509Certificate2?, AuthHandler>>(StringComparer.OrdinalIgnoreCase)
            {
                ["PLAIN"] = _ => new PlainAuthHandler(_userStore, _logger),
                ["LOGIN"] = _ => new LoginAuthHandler(_userStore, _logger),
                ["SCRAM-SHA-256"] = _ => new ScramSha256AuthHandler(_userStore, _logger),
                ["EXTERNAL"] = cert => new ExternalAuthHandler(_userStore, cert, _logger)
            };
        }

        public void SetClientCertificate(X509Certificate2? certificate)
        {
            _clientCertificate = certificate;
        }

        public IEnumerable<string> GetAvailableMechanisms(bool tlsActive)
        {
            // PLAIN and LOGIN require TLS
            if (tlsActive)
            {
                yield return "PLAIN";
                yield return "LOGIN";
                yield return "SCRAM-SHA-256";
            
                if (_clientCertificate is not null)
                    yield return "EXTERNAL";
            }
            else
            {
                // Only SCRAM is safe without TLS (no password transmitted)
                yield return "SCRAM-SHA-256";
            }
        }

        public AuthResponse StartAuth(string mechanism)
        {
            if (!_handlerFactories.TryGetValue(mechanism, out var factory))
            {
                return new AuthResponse(AuthResult.InvalidMechanism, ErrorCode: "504 5.5.4 Unrecognized authentication type");
            }

            _currentHandler = factory(_clientCertificate);
            return new AuthResponse(AuthResult.Continue, Challenge: "");
        }

        public async Task<AuthResponse> ProcessResponseAsync(string? clientResponse, CancellationToken ct = default)
        {
            if (_currentHandler is null)
            {
                return new AuthResponse(AuthResult.Fail, ErrorCode: "503 5.5.1 AUTH not started");
            }

            var result = await _currentHandler.ProcessAsync(clientResponse, ct);

            if (result.Result == AuthResult.Success)
            {
                IsAuthenticated = true;
                AuthenticatedUser = result.Username;
                AuthenticationMethod = _currentHandler.MechanismName;
                _currentHandler = null;
            }
            else if (result.Result == AuthResult.Fail)
            {
                _currentHandler = null;
            }

            return result;
        }

        public void Reset()
        {
            _currentHandler?.Reset();
            _currentHandler = null;
            IsAuthenticated = false;
            AuthenticatedUser = null;
            AuthenticationMethod = null;
        }
    }

}
