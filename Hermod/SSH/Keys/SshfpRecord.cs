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

    /// <summary>The SSHFP host-key algorithm numbers (RFC 4255 §3.1 + updates).</summary>
    public enum SshfpAlgorithm : Byte
    {
        /// <summary>Reserved.</summary>
        Reserved  = 0,
        /// <summary>RSA.</summary>
        Rsa       = 1,
        /// <summary>DSA (legacy).</summary>
        Dsa       = 2,
        /// <summary>ECDSA.</summary>
        Ecdsa     = 3,
        /// <summary>Ed25519.</summary>
        Ed25519   = 4,
        /// <summary>Ed448.</summary>
        Ed448     = 6
    }

    /// <summary>The SSHFP fingerprint type numbers (RFC 4255 §3.2).</summary>
    public enum SshfpFingerprintType : Byte
    {
        /// <summary>Reserved.</summary>
        Reserved  = 0,
        /// <summary>SHA-1 (legacy, at best advisory).</summary>
        Sha1      = 1,
        /// <summary>SHA-256 (preferred).</summary>
        Sha256    = 2
    }


    /// <summary>
    /// An SSHFP DNS resource record (type 44): the host-key algorithm, the fingerprint hash type and the
    /// fingerprint over the host-key wire blob. <see cref="FromHostKey"/> emits the records for a key (the
    /// <c>ssh-keygen -r</c> equivalent) and <see cref="ToZoneLine"/> renders a zone-file line.
    /// </summary>
    /// <param name="Algorithm">The host-key algorithm number.</param>
    /// <param name="FingerprintType">The fingerprint hash type.</param>
    /// <param name="Fingerprint">The fingerprint bytes over the host-key blob.</param>
    public sealed record SshfpRecord(SshfpAlgorithm        Algorithm,
                                     SshfpFingerprintType  FingerprintType,
                                     Byte[]                Fingerprint)
    {

        /// <summary>The fingerprint as a lowercase hex string (as it appears in a zone file).</summary>
        public String FingerprintHex => Convert.ToHexStringLower(Fingerprint);

        /// <summary>Whether this record's fingerprint matches the given host-key blob.</summary>
        public Boolean Matches(Byte[] HostKeyBlob)
        {
            var expected = Hash(FingerprintType, HostKeyBlob);
            return expected is not null && CryptographicOperations.FixedTimeEquals(expected, Fingerprint);
        }

        /// <summary>Render an <c>IN SSHFP</c> zone-file line for the given owner name.</summary>
        public String ToZoneLine(String Hostname)
            => $"{Hostname} IN SSHFP {(Byte) Algorithm} {(Byte) FingerprintType} {FingerprintHex}";


        #region (static) FromHostKey / FromBlob

        /// <summary>Emit the SHA-256 and SHA-1 SSHFP records for a host key (matching <c>ssh-keygen -r</c>).</summary>
        public static IReadOnlyList<SshfpRecord> FromHostKey(ISshHostKey HostKey)
            => FromBlob(HostKey.PublicKeyBlob);

        /// <summary>Emit the SHA-256 and SHA-1 SSHFP records for a host-key wire blob.</summary>
        public static IReadOnlyList<SshfpRecord> FromBlob(Byte[] HostKeyBlob)
        {
            var algorithm = AlgorithmOf(HostKeyBlob);
            return [
                new SshfpRecord(algorithm, SshfpFingerprintType.Sha1,   SHA1.HashData(HostKeyBlob)),
                new SshfpRecord(algorithm, SshfpFingerprintType.Sha256, SHA256.HashData(HostKeyBlob))
            ];
        }

        #endregion

        #region (static) helpers

        private static Byte[]? Hash(SshfpFingerprintType Type, Byte[] Blob)
            => Type switch {
                   SshfpFingerprintType.Sha1    => SHA1.HashData(Blob),
                   SshfpFingerprintType.Sha256  => SHA256.HashData(Blob),
                   _                            => null
               };

        private static SshfpAlgorithm AlgorithmOf(Byte[] HostKeyBlob)
        {
            var reader  = new SshPacketReader(HostKeyBlob);
            var keyType = reader.ReadString();
            return keyType switch {
                       "ssh-ed25519"                                                   => SshfpAlgorithm.Ed25519,
                       "ssh-ed448"                                                     => SshfpAlgorithm.Ed448,
                       "ssh-rsa" or "rsa-sha2-256" or "rsa-sha2-512"                    => SshfpAlgorithm.Rsa,
                       "ssh-dss"                                                        => SshfpAlgorithm.Dsa,
                       _ when keyType.StartsWith("ecdsa-sha2-", StringComparison.Ordinal) => SshfpAlgorithm.Ecdsa,
                       _                                                                => SshfpAlgorithm.Reserved
                   };
        }

        #endregion

    }

}
