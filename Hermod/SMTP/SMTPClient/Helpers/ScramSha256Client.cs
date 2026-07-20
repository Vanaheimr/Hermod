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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// The client-side cryptography of SCRAM-SHA-256 (RFC 7677 / RFC 5802), factored out of the SMTP
    /// authentication exchange so it can be unit-tested against the server-side credential generator.
    /// Mirrors the server's derivation exactly: PBKDF2-HMAC-SHA256, "Client Key"/"Server Key" HMACs.
    /// </summary>
    public static class ScramSha256Client
    {

        /// <summary>SaltedPassword = PBKDF2-HMAC-SHA256(password, salt, iterations) (32 bytes).</summary>
        public static Byte[] SaltedPassword(String password, Byte[] salt, Int32 iterations)
            => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);

        /// <summary>
        /// ClientProof = ClientKey XOR HMAC(StoredKey, authMessage), where ClientKey = HMAC(SaltedPassword,
        /// "Client Key") and StoredKey = SHA256(ClientKey). The server accepts it iff
        /// SHA256(proof XOR HMAC(StoredKey, authMessage)) == its stored StoredKey.
        /// </summary>
        public static Byte[] ClientProof(Byte[] saltedPassword, String authMessage)
        {

            var clientKey        = HMACSHA256.HashData(saltedPassword, "Client Key"u8.ToArray());
            var storedKey        = SHA256.HashData(clientKey);
            var clientSignature  = HMACSHA256.HashData(storedKey, Encoding.UTF8.GetBytes(authMessage));

            var proof            = new Byte[clientKey.Length];
            for (var i = 0; i < proof.Length; i++)
                proof[i] = (Byte) (clientKey[i] ^ clientSignature[i]);

            return proof;

        }

        /// <summary>
        /// ServerSignature = HMAC(ServerKey, authMessage), where ServerKey = HMAC(SaltedPassword,
        /// "Server Key"). The client verifies the server's final message against this.
        /// </summary>
        public static Byte[] ServerSignature(Byte[] saltedPassword, String authMessage)
            => HMACSHA256.HashData(HMACSHA256.HashData(saltedPassword, "Server Key"u8.ToArray()),
                                   Encoding.UTF8.GetBytes(authMessage));

    }

}
