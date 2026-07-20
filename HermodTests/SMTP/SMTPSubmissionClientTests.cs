/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System;
using System.Security.Cryptography;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.SMTP
{

    /// <summary>
    /// Tests for the modernized SMTP submission client. The SCRAM-SHA-256 client cryptography is
    /// cross-validated against Hermod's own server-side credential generator — a client proof computed
    /// here must be accepted by a server holding the corresponding StoredKey, and vice-versa for the
    /// server signature.
    /// </summary>
    [TestFixture]
    public class SMTPSubmissionClientTests
    {

        // A fixed SCRAM AuthMessage (client-first-bare , server-first , client-final-without-proof).
        private const String AuthMessage = "n=alice,r=cli-nonce," +
                                           "r=cli-nonceSRV-nonce,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096," +
                                           "c=biws,r=cli-nonceSRV-nonce";

        // Reproduce the server's proof verification (RFC 5802 §3): recover ClientKey and compare its
        // SHA-256 to the stored key.
        private static Boolean ServerAcceptsProof(Byte[] clientProof, String storedKeyBase64, String authMessage)
        {
            var storedKey        = Convert.FromBase64String(storedKeyBase64);
            var clientSignature  = HMACSHA256.HashData(storedKey, Encoding.UTF8.GetBytes(authMessage));

            var recoveredClientKey = new Byte[clientProof.Length];
            for (var i = 0; i < recoveredClientKey.Length; i++)
                recoveredClientKey[i] = (Byte) (clientProof[i] ^ clientSignature[i]);

            return Convert.ToBase64String(SHA256.HashData(recoveredClientKey)) == storedKeyBase64;
        }


        [Test]
        public void Scram_client_proof_is_accepted_by_the_server_credentials()
        {

            const String password = "correct horse battery staple";

            // Server side: derive and store SCRAM credentials for the password.
            var creds          = ScramCredentialGenerator.Generate(password);
            var salt           = Convert.FromBase64String(creds.SaltBase64);

            // Client side: derive the salted password from the SAME salt/iterations and compute the proof.
            var saltedPassword = ScramSha256Client.SaltedPassword(password, salt, creds.Iterations);
            var proof          = ScramSha256Client.ClientProof(saltedPassword, AuthMessage);

            Assert.That(ServerAcceptsProof(proof, creds.StoredKeyBase64, AuthMessage), Is.True,
                        "a proof from the correct password must be accepted by the stored key");

            // And the client can verify the server: our ServerSignature must equal HMAC(ServerKey, authMessage).
            var clientComputed = ScramSha256Client.ServerSignature(saltedPassword, AuthMessage);
            var serverComputed = HMACSHA256.HashData(Convert.FromBase64String(creds.ServerKeyBase64),
                                                     Encoding.UTF8.GetBytes(AuthMessage));

            Assert.That(clientComputed, Is.EqualTo(serverComputed), "the client must be able to verify the server signature");

        }

        [Test]
        public void Scram_proof_from_a_wrong_password_is_rejected()
        {

            var creds          = ScramCredentialGenerator.Generate("the-real-password");
            var salt           = Convert.FromBase64String(creds.SaltBase64);

            var saltedPassword = ScramSha256Client.SaltedPassword("a-different-password", salt, creds.Iterations);
            var proof          = ScramSha256Client.ClientProof(saltedPassword, AuthMessage);

            Assert.That(ServerAcceptsProof(proof, creds.StoredKeyBase64, AuthMessage), Is.False,
                        "a proof from the wrong password must be rejected");

        }

        [Test]
        public void Scram_is_deterministic_for_the_same_inputs()
        {

            var salt = RandomNumberGenerator.GetBytes(16);
            var a    = ScramSha256Client.ClientProof(ScramSha256Client.SaltedPassword("pw", salt, 4096), AuthMessage);
            var b    = ScramSha256Client.ClientProof(ScramSha256Client.SaltedPassword("pw", salt, 4096), AuthMessage);

            Assert.That(a, Is.EqualTo(b));

        }

    }

}
