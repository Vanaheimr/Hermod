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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// SSH public-key fingerprints over the public-key wire blob, in the formats printed by
    /// <c>ssh-keygen -l</c> and shown by OpenSSH on first connect: the modern
    /// <c>SHA256:&lt;unpadded-base64&gt;</c> and the legacy <c>MD5:aa:bb:…</c> hex.
    /// </summary>
    public static class SshFingerprint
    {

        #region Sha256(PublicKeyBlob)

        /// <summary>
        /// The <c>SHA256:&lt;base64&gt;</c> fingerprint (SHA-256 over the blob, standard base64 without
        /// padding) — exactly as OpenSSH prints and as used for host-key pins.
        /// </summary>
        public static String Sha256(ReadOnlySpan<Byte> PublicKeyBlob)
        {
            var hash    = SHA256.HashData(PublicKeyBlob);
            var base64  = Convert.ToBase64String(hash).TrimEnd('=');
            return "SHA256:" + base64;
        }

        #endregion

        #region Md5(PublicKeyBlob)

        /// <summary>
        /// The legacy <c>MD5:aa:bb:…</c> fingerprint (colon-separated lower-case hex of the MD5 digest).
        /// Provided for compatibility; SHA-256 is preferred.
        /// </summary>
        public static String Md5(ReadOnlySpan<Byte> PublicKeyBlob)
        {

            var hash     = MD5.HashData(PublicKeyBlob);
            var builder  = new StringBuilder("MD5:", 4 + (3 * hash.Length));

            for (var i = 0; i < hash.Length; i++)
            {
                if (i > 0)
                    builder.Append(':');
                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();

        }

        #endregion

        #region Matches(PublicKeyBlob, Fingerprint)

        /// <summary>
        /// Whether the given fingerprint string (either <c>SHA256:…</c> or <c>MD5:…</c>, with or without
        /// the <c>MD5:</c> prefix) matches the public-key blob. Comparison is ordinal and case-sensitive
        /// for SHA-256, case-insensitive for the MD5 hex.
        /// </summary>
        public static Boolean Matches(ReadOnlySpan<Byte> PublicKeyBlob, String Fingerprint)
        {

            var trimmed = Fingerprint.Trim();

            if (trimmed.StartsWith("SHA256:", StringComparison.Ordinal))
                return String.Equals(Sha256(PublicKeyBlob), trimmed, StringComparison.Ordinal);

            var md5 = trimmed.StartsWith("MD5:", StringComparison.OrdinalIgnoreCase) ? trimmed[4..] : trimmed;
            return String.Equals(Md5(PublicKeyBlob)[4..], md5, StringComparison.OrdinalIgnoreCase);

        }

        #endregion

    }

}
