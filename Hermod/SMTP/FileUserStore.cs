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

}
