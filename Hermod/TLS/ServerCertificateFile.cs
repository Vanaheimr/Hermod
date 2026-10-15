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

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A server certificate kept in files - certbot's fullchain.pem and
    /// privkey.pem, say, or a PKCS#12 file - and read again whenever they
    /// change, so that a renewed certificate is presented from the next
    /// handshake on, without a restart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>How a change is noticed.</b> By the content of the files, looked at
    /// no more often than <see cref="CheckInterval"/>: their SHA-256 against
    /// that of what was loaded. Not by their time stamps - certbot replaces
    /// symbolic links, deploy hooks copy with the times of the original, and
    /// a second-resolution clock misses two changes in one second. The files
    /// are a few kilobytes; reading them every few seconds costs nothing.
    /// </para>
    /// <para>
    /// <b>What a failed load does: nothing.</b> The certificate loaded last is
    /// kept and presented, and <see cref="LastError"/> says why the new one
    /// was not taken. That is what half of a renewal looks like - the
    /// certificate replaced, the key not yet - and the next look, a few
    /// seconds later, finds both. A server should never fall back to no TLS
    /// because somebody was in the middle of copying a file.
    /// </para>
    /// <para>
    /// Thread-safe: <see cref="Current"/> is asked from every handshake, and
    /// at most one of them reads the files.
    /// </para>
    /// </remarks>
    public sealed class ServerCertificateFile
    {

        #region Data

        /// <summary>
        /// How often the files are looked at when nothing else is said.
        /// </summary>
        public static readonly TimeSpan  DefaultCheckInterval  = TimeSpan.FromSeconds(10);

        private readonly Lock            sync                  = new ();
        private          String?         loadedDigest;
        private          DateTimeOffset  lastCheck             = DateTimeOffset.MinValue;

        private volatile ServerCertificateChain?  current;
        private volatile String?                  lastError;

        #endregion

        #region Properties

        /// <summary>
        /// The file with the certificate and its intermediates (PEM), or the PKCS#12 file.
        /// </summary>
        public String                   CertificatePath    { get; }

        /// <summary>
        /// For PEM: the file with the private key; null when it is in the certificate file.
        /// </summary>
        public String?                  KeyPath            { get; }

        /// <summary>
        /// For PKCS#12: its password.
        /// </summary>
        public String?                  Password           { get; }

        /// <summary>
        /// How often the files are looked at, at most.
        /// </summary>
        public TimeSpan                 CheckInterval      { get; }

        /// <summary>
        /// When the certificate presented now was read.
        /// </summary>
        public DateTimeOffset?          LoadedAt           { get; private set; }

        /// <summary>
        /// Why the files as they are now were not taken, or null when they were.
        /// </summary>
        public String?                  LastError
            => lastError;

        /// <summary>
        /// The certificate to present now, with its intermediates - read again
        /// first if the files have changed - or null while there has never been
        /// one that could be read.
        /// </summary>
        public ServerCertificateChain?  Current
        {
            get
            {
                CheckForChanges();
                return current;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// A new certificate was read, and is presented from now on.
        /// </summary>
        public event Action<ServerCertificateChain>?  OnLoaded;

        /// <summary>
        /// The files changed, but could not be read; the certificate before stays.
        /// </summary>
        public event Action<String>?                  OnLoadFailed;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A certificate in files. They are read for the first time here; that
        /// they are not there yet is no error - a server that waits for its
        /// first certificate from certbot has to start without one.
        /// </summary>
        /// <param name="CertificatePath">A PEM file with the certificate and its intermediates (certbot's fullchain.pem), or a PKCS#12 file.</param>
        /// <param name="KeyPath">For PEM: the file with the private key (certbot's privkey.pem); null when it is in the certificate file.</param>
        /// <param name="Password">For PKCS#12: its password.</param>
        /// <param name="CheckInterval">How often the files are looked at, at most; <see cref="DefaultCheckInterval"/> by default.</param>
        public ServerCertificateFile(String     CertificatePath,
                                     String?    KeyPath         = null,
                                     String?    Password        = null,
                                     TimeSpan?  CheckInterval   = null)
        {

            this.CertificatePath  = CertificatePath;
            this.KeyPath          = KeyPath;
            this.Password         = Password;
            this.CheckInterval    = CheckInterval ?? DefaultCheckInterval;

            CheckForChanges(Force: true);

        }

        #endregion


        #region Selector(ServerName)

        /// <summary>
        /// What a TLS server asks at every handshake: the same certificate for
        /// every name. As SMTPServerConfig.ServerCertificateSelector, say.
        /// </summary>
        public ServerCertificateChain? Selector(String? ServerName)
            => Current;

        #endregion

        #region Reload()

        /// <summary>
        /// Look at the files now, whenever they were looked at last.
        /// </summary>
        /// <returns>Whether a certificate is there to present.</returns>
        public Boolean Reload()
        {
            CheckForChanges(Force: true);
            return current is not null;
        }

        #endregion

        #region (private) CheckForChanges(Force = false)

        private void CheckForChanges(Boolean Force = false)
        {

            var now = DateTimeOffset.UtcNow;

            if (!Force && now - lastCheck < CheckInterval)
                return;

            // One handshake reads the files; the others present what is there.
            if (!sync.TryEnter())
            {
                if (!Force)
                    return;
                sync.Enter();
            }

            try
            {

                if (!Force && now - lastCheck < CheckInterval)
                    return;

                lastCheck = now;

                Byte[]  certificateOctets;
                Byte[]? keyOctets;

                try
                {
                    certificateOctets  = File.ReadAllBytes(CertificatePath);
                    keyOctets          = KeyPath is not null ? File.ReadAllBytes(KeyPath) : null;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Fail($"The certificate cannot be read: {e.Message}");
                    return;
                }

                var digest = Convert.ToHexString(SHA256.HashData([ .. certificateOctets, .. keyOctets ?? [] ]));

                if (digest == loadedDigest)
                    return;

                ServerCertificateChain chain;

                try
                {
                    chain = ServerCertificateChain.FromOctets(certificateOctets, keyOctets, Password, CertificatePath);
                }
                catch (Exception e)
                {
                    Fail($"The certificate in '{CertificatePath}' cannot be used: {e.Message}");
                    return;
                }

                loadedDigest  = digest;
                current       = chain;
                lastError     = null;
                LoadedAt      = now;

                OnLoaded?.Invoke(chain);

            }
            finally
            {
                sync.Exit();
            }

        }

        private void Fail(String Error)
        {

            // Said once per change, not at every look at the same broken files.
            if (Error == lastError)
                return;

            lastError = Error;
            OnLoadFailed?.Invoke(Error);

        }

        #endregion


        #region (override) ToString()

        public override String ToString()

            => current is not null
                   ? $"{current} from '{CertificatePath}'"
                   : $"no certificate yet from '{CertificatePath}'";

        #endregion

    }

}
