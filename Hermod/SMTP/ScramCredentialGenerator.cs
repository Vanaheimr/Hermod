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

}
