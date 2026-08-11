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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The wire names of the SSH algorithms and the pseudo-algorithm markers used during
    /// KEXINIT negotiation (RFC 4253, RFC 8308, OpenSSH extensions).
    /// </summary>
    public static class SshAlgorithmNames
    {

        /// <summary>Key exchange method names and negotiation markers.</summary>
        public static class Kex
        {
            public const String Curve25519Sha256         = "curve25519-sha256";
            public const String Curve25519Sha256LibSsh   = "curve25519-sha256@libssh.org";
            public const String EcdhNistP256             = "ecdh-sha2-nistp256";
            public const String EcdhNistP384             = "ecdh-sha2-nistp384";
            public const String EcdhNistP521             = "ecdh-sha2-nistp521";
            public const String DhGroup14Sha256          = "diffie-hellman-group14-sha256";
            public const String DhGroup16Sha512          = "diffie-hellman-group16-sha512";

            /// <summary>Post-quantum hybrid ML-KEM-768 + X25519 with SHA-256 (OpenSSH default since 10.0).</summary>
            public const String MlKem768X25519Sha256           = "mlkem768x25519-sha256";
            /// <summary>Post-quantum hybrid sntrup761 + X25519 with SHA-512 (IANA name).</summary>
            public const String SntruP761X25519Sha512          = "sntrup761x25519-sha512";
            /// <summary>Post-quantum hybrid sntrup761 + X25519 with SHA-512 (original OpenSSH name).</summary>
            public const String SntruP761X25519Sha512LibSsh    = "sntrup761x25519-sha512@openssh.com";

            /// <summary>Client's "I support ext-info" marker (RFC 8308).</summary>
            public const String ExtInfoClient            = "ext-info-c";
            /// <summary>Server's "I support ext-info" marker (RFC 8308).</summary>
            public const String ExtInfoServer            = "ext-info-s";

            /// <summary>Client's strict-KEX marker (Terrapin mitigation, CVE-2023-48795).</summary>
            public const String StrictKexClient          = "kex-strict-c-v00@openssh.com";
            /// <summary>Server's strict-KEX marker (Terrapin mitigation, CVE-2023-48795).</summary>
            public const String StrictKexServer          = "kex-strict-s-v00@openssh.com";

            /// <summary>
            /// Dropbear's guessed-key-exchange marker, sent by both roles under the same name.
            ///
            /// <para>
            /// RFC 4253 §7.1 only lets a peer's guessed first key-exchange packet count when the key
            /// exchange <i>and</i> the host-key algorithm both match what was negotiated — which makes
            /// guessing near-useless, since a client rarely knows which host key it will be offered.
            /// This extension narrows the rule to the key exchange alone. Both sides must advertise it.
            /// </para>
            /// </summary>
            public const String KexGuess2                = "kexguess2@matt.ucc.asn.au";

            /// <summary>
            /// The pseudo-algorithms that may appear in a key-exchange name-list without ever being a
            /// key exchange. They must never be selected during negotiation — <see cref="KexGuess2"/>
            /// makes that a real hazard rather than a theoretical one, because unlike the -c/-s marker
            /// pairs both peers send the identical name.
            /// </summary>
            public static readonly String[] Markers =
            [
                ExtInfoClient, ExtInfoServer,
                StrictKexClient, StrictKexServer,
                KexGuess2
            ];

        }

        /// <summary>Host-key / public-key signature algorithm names.</summary>
        public static class HostKey
        {
            public const String Ed25519                  = "ssh-ed25519";
            public const String EcdsaNistP256            = "ecdsa-sha2-nistp256";
            public const String EcdsaNistP384            = "ecdsa-sha2-nistp384";
            public const String EcdsaNistP521            = "ecdsa-sha2-nistp521";
            public const String RsaSha2_256              = "rsa-sha2-256";
            public const String RsaSha2_512              = "rsa-sha2-512";
            public const String SshRsa                   = "ssh-rsa";   // RSA key type (SHA-1 sig; off by default)

            // OpenSSH certificate host-key algorithms (a host presents a certificate as its host key).
            public const String Ed25519Cert             = "ssh-ed25519-cert-v01@openssh.com";
            public const String EcdsaNistP256Cert       = "ecdsa-sha2-nistp256-cert-v01@openssh.com";
            public const String EcdsaNistP384Cert       = "ecdsa-sha2-nistp384-cert-v01@openssh.com";
            public const String EcdsaNistP521Cert       = "ecdsa-sha2-nistp521-cert-v01@openssh.com";
            public const String SshRsaCert              = "ssh-rsa-cert-v01@openssh.com";
        }

        /// <summary>Encryption algorithm names.</summary>
        public static class Cipher
        {
            public const String Aes256Gcm                = "aes256-gcm@openssh.com";
            public const String Aes128Gcm                = "aes128-gcm@openssh.com";
            public const String Aes256Ctr                = "aes256-ctr";
            public const String Aes192Ctr                = "aes192-ctr";
            public const String Aes128Ctr                = "aes128-ctr";
            public const String ChaCha20Poly1305         = "chacha20-poly1305@openssh.com";
        }

        /// <summary>MAC algorithm names (ignored when an AEAD cipher is selected).</summary>
        public static class Mac
        {
            public const String HmacSha2_256Etm          = "hmac-sha2-256-etm@openssh.com";
            public const String HmacSha2_512Etm          = "hmac-sha2-512-etm@openssh.com";
            public const String HmacSha2_256             = "hmac-sha2-256";
            public const String HmacSha2_512             = "hmac-sha2-512";
        }

        /// <summary>Compression algorithm names.</summary>
        public static class Compression
        {
            public const String None                     = "none";
        }

    }

}
