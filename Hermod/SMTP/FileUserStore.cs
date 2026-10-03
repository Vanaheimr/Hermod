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
    /// Simple file-based user store for demonstration.
    /// In production, use a database or LDAP.
    /// </summary>
    /// <remarks>
    /// One account per line: <c>username:password_sha256:scram_salt:scram_stored_key:scram_server_key:iterations:cert_thumbprints</c>.
    /// A password is checked against the salted SCRAM-SHA-256 keys (PBKDF2) when the account
    /// has them; the unsalted SHA-256 column is only a fallback for accounts without them.
    /// A client certificate (SASL EXTERNAL) authenticates an account only when its SHA-1 or
    /// SHA-256 thumbprint is listed for that account.
    /// </remarks>
    public sealed class FileUserStore : IUserStore
    {
        private readonly string _usersFilePath;
        private readonly Dictionary<string, UserCredentials> _users = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UserCredentials> _certThumbprints = new(StringComparer.OrdinalIgnoreCase);
        private DateTimeOffset _lastLoad;

        public FileUserStore(string usersFilePath)
        {
            _usersFilePath = usersFilePath;
            EnsureUsersFile();
            LoadUsers();
        }

        /// <summary>
        /// Create a users file without accounts when there is none. It used to be filled with
        /// demo accounts whose passwords are published ("test123", "demo"), which made every
        /// server started without a users file accept them.
        /// </summary>
        private void EnsureUsersFile()
        {
            if (File.Exists(_usersFilePath))
                return;

            var template = """
                # SMTP User Database
                # Format: username:password_sha256:scram_salt:scram_stored_key:scram_server_key:iterations:cert_thumbprints
                #
                # Generate the SCRAM columns with ScramCredentialGenerator.Generate(password); a
                # password is then checked against them. password_sha256 (hex SHA-256 of the
                # password) is only used for accounts without SCRAM columns, and may stay empty.
                #
                # cert_thumbprints: comma-separated SHA-1 or SHA-256 thumbprints (hex) of the
                # client certificates that authenticate this account via SASL EXTERNAL.
                #
                # No accounts are configured.

                """;

            Directory.CreateDirectory(Path.GetDirectoryName(_usersFilePath) ?? ".");
            File.WriteAllText(_usersFilePath, template);
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

                // Index by certificate thumbprints. A thumbprint names one certificate; anything
                // else ("*" for any certificate) would let a self-made certificate in.
                foreach (var thumbprint in creds.AllowedCertThumbprints)
                {
                    if (IsThumbprint(thumbprint))
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

        /// <summary>
        /// The account whose listed thumbprints include this certificate's SHA-1 or SHA-256
        /// thumbprint. Nothing else matches: the TLS handshake accepts any client certificate
        /// without a CA, so its subject (CN=admin) is whatever its maker wrote there, while
        /// its thumbprint names the one key pair the handshake proved the client holds.
        /// </summary>
        public Task<UserCredentials?> GetUserByCertificateAsync(X509Certificate2 cert, CancellationToken ct = default)
        {
            LoadUsers();

            if (_certThumbprints.TryGetValue(cert.Thumbprint, out var user) ||
                _certThumbprints.TryGetValue(cert.GetCertHashString(HashAlgorithmName.SHA256), out user))
                return Task.FromResult<UserCredentials?>(user);

            return Task.FromResult<UserCredentials?>(null);
        }

        public async Task<bool> ValidatePasswordAsync(string username, string password, CancellationToken ct = default)
        {
            var user = await GetUserAsync(username, ct);
            if (user is null)
                return false;

            // Salted and iterated: StoredKey = SHA-256(HMAC(PBKDF2(password, salt, i), "Client Key"))
            // (RFC 5802 §3), the same derivation ScramCredentialGenerator stores.
            if (user.ScramSalt is not null && user.ScramStoredKey is not null)
            {
                try
                {
                    var saltedPassword = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password),
                                                                   Convert.FromBase64String(user.ScramSalt),
                                                                   user.ScramIterations,
                                                                   HashAlgorithmName.SHA256,
                                                                   32);
                    var storedKey      = SHA256.HashData(HMACSHA256.HashData(saltedPassword, "Client Key"u8));

                    return CryptographicOperations.FixedTimeEquals(storedKey, Convert.FromBase64String(user.ScramStoredKey));
                }
                catch (Exception e) when (e is FormatException or ArgumentException)
                {
                    // Columns that are no SCRAM keys (hand-edited placeholders): the SHA-256 column decides.
                }
            }

            if (user.PasswordHash is null)
                return false;

            var inputHash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            try
            {
                return CryptographicOperations.FixedTimeEquals(inputHash, Convert.FromHexString(user.PasswordHash));
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// A SHA-1 (40) or SHA-256 (64) thumbprint in hex.
        /// </summary>
        private static bool IsThumbprint(string text)
            => text.Length is 40 or 64 && text.All(Char.IsAsciiHexDigit);
    }

}
