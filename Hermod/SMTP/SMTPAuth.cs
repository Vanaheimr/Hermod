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

    #region Auth Result

    public enum AuthResult { Success, Fail, Continue, InvalidMechanism }

    public sealed record AuthResponse(
        AuthResult  Result,
        string?     Message      = null,
        string?     Challenge    = null,
        string?     Username     = null,
        string?     ErrorCode    = null
    );

    #endregion

    #region User Store

    public sealed record UserCredentials(
        string   Username,
        string?  PasswordHash,        // For PLAIN/LOGIN: SHA256 hash
        string?  ScramSalt,           // For SCRAM: Base64 salt
        string?  ScramStoredKey,      // For SCRAM: Base64 StoredKey
        string?  ScramServerKey,      // For SCRAM: Base64 ServerKey
        int      ScramIterations,     // For SCRAM: PBKDF2 iterations
        string[] AllowedCertThumbprints  // For EXTERNAL: allowed client certs
    );

    public interface IUserStore
    {
        Task<UserCredentials?> GetUserAsync(string username, CancellationToken ct = default);
        Task<UserCredentials?> GetUserByCertificateAsync(X509Certificate2 cert, CancellationToken ct = default);
        Task<bool> ValidatePasswordAsync(string username, string password, CancellationToken ct = default);
    }

    /// <summary>
    /// Simple file-based user store for demonstration.
    /// In production, use a database or LDAP.
    /// </summary>
    public sealed class FileUserStore : IUserStore
    {
        private readonly string _usersFilePath;
        private readonly Dictionary<string, UserCredentials> _users = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UserCredentials> _certThumbprints = new(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastLoad;

        public FileUserStore(string usersFilePath)
        {
            _usersFilePath = usersFilePath;
            EnsureDefaultUsers();
            LoadUsers();
        }

        private void EnsureDefaultUsers()
        {
            if (File.Exists(_usersFilePath))
                return;

            // Create default users file with example users
            var defaultContent = """
                # SMTP User Database
                # Format: username:password_sha256:scram_salt:scram_stored_key:scram_server_key:iterations:cert_thumbprints
                # 
                # Generate password hash: echo -n "password" | sha256sum
                # Generate SCRAM credentials: use ScramCredentialGenerator
                #
                # Example users (password for all: "test123"):
            
                admin:a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3:AAAAAAAAAAAAAAAAAAAAAA==:StoredKeyBase64:ServerKeyBase64:4096:
                user:a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3:AAAAAAAAAAAAAAAAAAAAAA==:StoredKeyBase64:ServerKeyBase64:4096:
                certuser:::::::AABBCCDD11223344
                """;

            Directory.CreateDirectory(Path.GetDirectoryName(_usersFilePath) ?? ".");
            File.WriteAllText(_usersFilePath, defaultContent);

            // Also create properly generated SCRAM credentials
            GenerateScramUsersFile();
        }

        private void GenerateScramUsersFile()
        {
            var users = new[]
            {
                ("admin", "test123"),
                ("user", "test123"),
                ("demo", "demo")
            };

            var lines = new List<string>
            {
                "# SMTP User Database - Auto-generated with SCRAM-SHA-256 credentials",
                "# Format: username:password_sha256:scram_salt:scram_stored_key:scram_server_key:iterations:cert_thumbprints",
                ""
            };

            foreach (var (username, password) in users)
            {
                var creds = ScramCredentialGenerator.Generate(password);
                var passwordHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
            
                lines.Add($"{username}:{passwordHash}:{creds.SaltBase64}:{creds.StoredKeyBase64}:{creds.ServerKeyBase64}:{creds.Iterations}:");
            }

            // Add a certificate-only user
            lines.Add("");
            lines.Add("# Certificate-authenticated user (no password)");
            lines.Add("certonly:::::::*");  // * means any valid client cert

            File.WriteAllLines(_usersFilePath, lines);
        }

        private void LoadUsers()
        {
            if (!File.Exists(_usersFilePath))
                return;

            var fileTime = File.GetLastWriteTimeUtc(_usersFilePath);
            if (fileTime <= _lastLoad)
                return;

            _users.Clear();
            _certThumbprints.Clear();

            foreach (var line in File.ReadAllLines(_usersFilePath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    continue;

                var parts = line.Split(':');
                if (parts.Length < 7)
                    continue;

                var username = parts[0];
                var creds = new UserCredentials(
                    Username: username,
                    PasswordHash: string.IsNullOrEmpty(parts[1]) ? null : parts[1],
                    ScramSalt: string.IsNullOrEmpty(parts[2]) ? null : parts[2],
                    ScramStoredKey: string.IsNullOrEmpty(parts[3]) ? null : parts[3],
                    ScramServerKey: string.IsNullOrEmpty(parts[4]) ? null : parts[4],
                    ScramIterations: int.TryParse(parts[5], out var iter) ? iter : 4096,
                    AllowedCertThumbprints: string.IsNullOrEmpty(parts[6]) 
                        ? [] 
                        : parts[6].Split(',', StringSplitOptions.RemoveEmptyEntries)
                );

                _users[username] = creds;

                // Index by certificate thumbprints
                foreach (var thumbprint in creds.AllowedCertThumbprints)
                {
                    _certThumbprints[thumbprint] = creds;
                }
            }

            _lastLoad = fileTime;
        }

        public Task<UserCredentials?> GetUserAsync(string username, CancellationToken ct = default)
        {
            LoadUsers(); // Reload if changed
            return Task.FromResult(_users.GetValueOrDefault(username));
        }

        public Task<UserCredentials?> GetUserByCertificateAsync(X509Certificate2 cert, CancellationToken ct = default)
        {
            LoadUsers();
        
            var thumbprint = cert.Thumbprint;
        
            // Check for exact thumbprint match
            if (_certThumbprints.TryGetValue(thumbprint, out var user))
                return Task.FromResult<UserCredentials?>(user);

            // Check for wildcard (any cert accepted)
            if (_certThumbprints.TryGetValue("*", out var wildcardUser))
                return Task.FromResult<UserCredentials?>(wildcardUser);

            // Check by subject CN
            var cn = cert.GetNameInfo(X509NameType.SimpleName, false);
            if (cn is not null && _users.TryGetValue(cn, out var cnUser))
                return Task.FromResult<UserCredentials?>(cnUser);

            return Task.FromResult<UserCredentials?>(null);
        }

        public async Task<bool> ValidatePasswordAsync(string username, string password, CancellationToken ct = default)
        {
            var user = await GetUserAsync(username, ct);
            if (user?.PasswordHash is null)
                return false;

            var inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
            return inputHash == user.PasswordHash.ToLowerInvariant();
        }
    }

    #endregion

    #region SCRAM Credential Generator

    public sealed record ScramCredentials(
        string SaltBase64,
        string StoredKeyBase64,
        string ServerKeyBase64,
        int    Iterations
    );

    public static class ScramCredentialGenerator
    {
        private const int DefaultIterations = 4096;
        private const int SaltLength = 16;

        public static ScramCredentials Generate(string password, int iterations = DefaultIterations)
        {
            // Generate random salt
            var salt = RandomNumberGenerator.GetBytes(SaltLength);

            // SaltedPassword = PBKDF2(password, salt, iterations)
            var saltedPassword = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                32
            );

            // ClientKey = HMAC(SaltedPassword, "Client Key")
            var clientKey = HMACSHA256.HashData(saltedPassword, "Client Key"u8);

            // StoredKey = SHA256(ClientKey)
            var storedKey = SHA256.HashData(clientKey);

            // ServerKey = HMAC(SaltedPassword, "Server Key")
            var serverKey = HMACSHA256.HashData(saltedPassword, "Server Key"u8);

            return new ScramCredentials(
                SaltBase64: Convert.ToBase64String(salt),
                StoredKeyBase64: Convert.ToBase64String(storedKey),
                ServerKeyBase64: Convert.ToBase64String(serverKey),
                Iterations: iterations
            );
        }
    }

    #endregion

    #region Base Auth Handler

    public abstract class AuthHandler
    {
        public abstract string MechanismName { get; }
        public abstract Task<AuthResponse> ProcessAsync(string? clientResponse, CancellationToken ct = default);
        public virtual void Reset() { }
    }

    #endregion

    #region PLAIN Auth Handler

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

    #endregion

    #region LOGIN Auth Handler

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

    #endregion

    #region SCRAM-SHA-256 Auth Handler

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

    #endregion

    #region EXTERNAL Auth Handler

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

    #endregion

    #region Auth Manager

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

        #endregion

}
