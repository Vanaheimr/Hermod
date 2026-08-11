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

using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Generates SSH key pairs (the <c>ssh-keygen -t</c> equivalent) and loads private keys from the
    /// common on-disk formats. Also provides first-run host-key generation for a server.
    /// </summary>
    public static class SshKeyGenerator
    {

        #region Generate(KeyType, RsaKeySizeInBits = 3072)

        /// <summary>
        /// Generate a fresh key pair of the given type: <c>ssh-ed25519</c>,
        /// <c>ecdsa-sha2-nistp256/384/521</c> or <c>ssh-rsa</c>/<c>rsa</c>.
        /// </summary>
        public static ISshHostKey Generate(String KeyType, Int32 RsaKeySizeInBits = 3072)
            => KeyType switch {
                   "ssh-ed25519" or "ed25519"       => SshHostKey.GenerateEd25519(),
                   SshAlgorithmNames.HostKey.EcdsaNistP256 or "ecdsa"       => SshHostKey.GenerateEcdsa(SshAlgorithmNames.HostKey.EcdsaNistP256),
                   SshAlgorithmNames.HostKey.EcdsaNistP384                  => SshHostKey.GenerateEcdsa(SshAlgorithmNames.HostKey.EcdsaNistP384),
                   SshAlgorithmNames.HostKey.EcdsaNistP521                  => SshHostKey.GenerateEcdsa(SshAlgorithmNames.HostKey.EcdsaNistP521),
                   SshAlgorithmNames.HostKey.SshRsa or "rsa"               => SshHostKey.GenerateRsa(RsaKeySizeInBits),
                   _                                => throw new ArgumentException($"Unknown key type '{KeyType}'.", nameof(KeyType))
               };

        #endregion

        #region WriteKeyPairAsync(Key, PrivateKeyPath, Comment = "", CancellationToken = default)

        /// <summary>
        /// Write a key pair to disk as OpenSSH does: the unencrypted <c>openssh-key-v1</c> private key at
        /// <paramref name="PrivateKeyPath"/> — owner-readable only, as OpenSSH insists — and the public
        /// key at <c>&lt;path&gt;.pub</c>.
        /// </summary>
        public static async Task WriteKeyPairAsync(ISshHostKey        Key,
                                                   String             PrivateKeyPath,
                                                   String             Comment            = "",
                                                   CancellationToken  CancellationToken  = default)
        {
            await SshPrivateKeyFile.WriteAsync(PrivateKeyPath, OpenSshPrivateKey.Format(Key, Comment), CancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(PrivateKeyPath + ".pub", SshPublicKey.FromHostKey(Key, Comment).ToAuthorizedKeyLine() + "\n", CancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region LoadOrCreateHostKeyAsync(Path, KeyType = "ssh-ed25519", CancellationToken = default)

        /// <summary>
        /// Load a server host key from <paramref name="Path"/>, generating and persisting a fresh one on
        /// first run if the file does not yet exist (matching the sibling projects' first-run pattern).
        /// </summary>
        public static async Task<ISshHostKey> LoadOrCreateHostKeyAsync(String             Path,
                                                                       String             KeyType            = "ssh-ed25519",
                                                                       CancellationToken  CancellationToken  = default)
        {

            if (File.Exists(Path))
                return LoadPrivateKey(await File.ReadAllTextAsync(Path, CancellationToken).ConfigureAwait(false)).Key;

            var key = Generate(KeyType);
            await WriteKeyPairAsync(key, Path, Comment: $"hermod-host-key@{Environment.MachineName}", CancellationToken).ConfigureAwait(false);
            return key;

        }

        #endregion


        #region LoadPrivateKey(Text, Passphrase = null)

        /// <summary>
        /// Load a private key from text, auto-detecting the format: <c>openssh-key-v1</c> (with or without
        /// a passphrase), or PKCS#8/PKCS#1/SEC1 PEM for RSA and ECDSA keys.
        /// </summary>
        public static SshLoadedKey LoadPrivateKey(String Text, String? Passphrase = null)
        {

            if (Text.Contains("BEGIN OPENSSH PRIVATE KEY", StringComparison.Ordinal))
                return OpenSshPrivateKey.Parse(Text, Passphrase);

            return LoadPemKey(Text, Passphrase);

        }

        // RSA / ECDSA PEM (PKCS#8, PKCS#1, SEC1), optionally passphrase-encrypted, via the BCL.
        private static SshLoadedKey LoadPemKey(String Text, String? Passphrase)
        {

            // A SEC1 "EC PRIVATE KEY" block is unambiguously ECDSA.
            if (Text.Contains("EC PRIVATE KEY", StringComparison.Ordinal) && TryEcdsa(Text, Passphrase, out var ecFirst))
                return new SshLoadedKey(ecFirst!, "");

            // PKCS#1 "RSA PRIVATE KEY" or a PKCS#8 "PRIVATE KEY" that holds an RSA key.
            try
            {
                var rsa = RSA.Create();
                if (Passphrase is not null)
                    rsa.ImportFromEncryptedPem(Text, Passphrase);
                else
                    rsa.ImportFromPem(Text);
                return new SshLoadedKey(new RsaHostKey(rsa.ExportParameters(true)), "");
            }
            catch (CryptographicException)
            { /* not RSA — fall through to ECDSA */ }

            // A PKCS#8 "PRIVATE KEY" that holds an ECDSA key.
            if (TryEcdsa(Text, Passphrase, out var ecFallback))
                return new SshLoadedKey(ecFallback!, "");

            throw new SshWireException("Unsupported or unreadable PEM private key (supported: openssh-key-v1, RSA/ECDSA PKCS#8/PEM).");

        }

        private static Boolean TryEcdsa(String Text, String? Passphrase, out ISshHostKey? Key)
        {
            Key = null;
            try
            {
                using var ecdsa = ECDsa.Create();
                if (Passphrase is not null)
                    ecdsa.ImportFromEncryptedPem(Text, Passphrase);
                else
                    ecdsa.ImportFromPem(Text);

                var algorithm = ecdsa.KeySize switch {
                                    256 => SshAlgorithmNames.HostKey.EcdsaNistP256,
                                    384 => SshAlgorithmNames.HostKey.EcdsaNistP384,
                                    521 => SshAlgorithmNames.HostKey.EcdsaNistP521,
                                    _   => throw new SshWireException($"Unsupported ECDSA key size {ecdsa.KeySize}.")
                                };

                Key = new EcdsaHostKey(algorithm, ecdsa.ExportParameters(true));
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        #endregion

    }

}
