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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// M4 password / keyboard-interactive authentication and TOTP second-factor (RFC 4252 §8, RFC 4256,
    /// RFC 6238), exercised over a loopback pipe.
    /// </summary>
    [TestFixture]
    public class PasswordKeyboardTotpTests
    {

        private sealed class FixedTimeProvider(DateTimeOffset Now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
        }


        #region Totp_Rfc6238_Sha1_Vectors

        [Test]
        public void Totp_Rfc6238_Sha1_Vectors()
        {

            // RFC 6238 Appendix B: SHA-1, 8 digits, 30 s step, secret ASCII "12345678901234567890".
            var secret = Encoding.ASCII.GetBytes("12345678901234567890");

            (Int64 Time, String Code)[] vectors =
            [
                (59,          "94287082"),
                (1111111109,  "07081804"),
                (1111111111,  "14050471"),
                (1234567890,  "89005924"),
                (2000000000,  "69279037"),
            ];

            Assert.Multiple(() => {
                foreach (var (time, code) in vectors)
                {
                    var totp = new Totp(secret, Digits: 8, Algorithm: HashAlgorithmName.SHA1, StepSeconds: 30);
                    Assert.That(totp.Compute(DateTimeOffset.FromUnixTimeSeconds(time)), Is.EqualTo(code), $"t={time}");
                }
            });

        }

        #endregion

        #region Totp_Verify_SkewAndReplay

        [Test]
        public void Totp_Verify_SkewAndReplay()
        {

            var secret  = RandomNumberGenerator.GetBytes(20);
            var now     = new FixedTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
            var totp    = new Totp(secret, Digits: 6, TimeProvider: now);

            var current = totp.Compute(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
            var previous = totp.Compute(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000 - 30));

            Assert.Multiple(() => {
                Assert.That(totp.Verify("000000"),   Is.False, "a wrong code is rejected");
                Assert.That(totp.Verify(previous),   Is.True,  "±1 step skew is tolerated");
                Assert.That(totp.Verify(current),    Is.True,  "the current code is accepted");
                Assert.That(totp.Verify(current),    Is.False, "the same code cannot be replayed");
            });

        }

        #endregion

        #region Password_Auth_SucceedsAndFails

        [Test]
        [CancelAfter(15000)]
        public async Task Password_Auth_SucceedsAndFails(CancellationToken CancellationToken)
        {

            var authenticator = new SshAuthenticationPolicy()
                                    .WithPassword((user, pw, _) => ValueTask.FromResult(user == "achim" && pw == "s3cr3t"));

            Assert.That(await RunPasswordAsync(authenticator, "achim", "s3cr3t", CancellationToken), Is.True);
            Assert.That(await RunPasswordAsync(authenticator, "achim", "wrong",  CancellationToken), Is.False);

        }

        private static async Task<Boolean> RunPasswordAsync(ISshUserAuthenticator Authenticator, String User, String Password, CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                try   { await UserAuthentication.ServerAuthenticateAsync(t, Authenticator, MaxAuthTries: 1, CancellationToken: CancellationToken); }
                catch { /* the client stops after one attempt on failure */ }
            }, CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var ok = await UserAuthentication.ClientPasswordAuthenticateAsync(client, User, Password, CancellationToken: CancellationToken);

            clientPipe.Output.Complete();
            try { await serverRun; } catch { }
            return ok;

        }

        #endregion

        #region TwoFactor_PublicKeyThenTotp

        [Test]
        [CancelAfter(15000)]
        public async Task TwoFactor_PublicKeyThenTotp(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();

            var secret  = RandomNumberGenerator.GetBytes(20);
            var clock   = new FixedTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1_700_000_123));
            var serverTotp = new Totp(secret, TimeProvider: clock);
            var clientTotp = new Totp(secret, TimeProvider: clock);

            // Server: publickey + a required TOTP second factor for this user.
            var authenticator = new SshAuthenticationPolicy()
                                    .WithPublicKey((req, _) => ValueTask.FromResult(req.PublicKeyBlob.AsSpan().SequenceEqual(userKey.PublicKeyBlob)))
                                    .WithSecondFactor(_ => new TotpKeyboardInteractive(serverTotp));

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                return await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
            }, CancellationToken);

            var clientRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                return await UserAuthentication.ClientAuthenticateAsync(
                           t, "achim",
                           Keys: [ userKey ],
                           KeyboardInteractive: (challenge, _) =>
                           {
                               Assert.That(challenge.Prompts, Has.Count.EqualTo(1));
                               return ValueTask.FromResult(new[] { clientTotp.ComputeNow() });
                           },
                           CancellationToken: CancellationToken);
            }, CancellationToken);

            var clientOk = await clientRun;
            var result   = await serverRun;

            Assert.Multiple(() => {
                Assert.That(clientOk,          Is.True, "publickey + TOTP must complete the 2FA chain");
                Assert.That(result.Username,   Is.EqualTo("achim"));
                Assert.That(result.Method,     Is.EqualTo("keyboard-interactive"), "the final method that completed the chain");
            });

        }

        #endregion

        #region TwoFactor_WrongTotp_IsRejected

        [Test]
        [CancelAfter(15000)]
        public async Task TwoFactor_WrongTotp_IsRejected(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();
            var serverTotp = new Totp(RandomNumberGenerator.GetBytes(20));

            var authenticator = new SshAuthenticationPolicy()
                                    .WithPublicKey((req, _) => ValueTask.FromResult(req.PublicKeyBlob.AsSpan().SequenceEqual(userKey.PublicKeyBlob)))
                                    .WithSecondFactor(_ => new TotpKeyboardInteractive(serverTotp));

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                try   { await UserAuthentication.ServerAuthenticateAsync(t, authenticator, MaxAuthTries: 1, CancellationToken: CancellationToken); }
                catch { }
            }, CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            var ok = await UserAuthentication.ClientAuthenticateAsync(
                         client, "achim",
                         Keys: [ userKey ],
                         KeyboardInteractive: (_, _) => ValueTask.FromResult(new[] { "000000" }),   // wrong code
                         CancellationToken: CancellationToken);

            Assert.That(ok, Is.False, "a valid key but wrong TOTP must fail overall");

            clientPipe.Output.Complete();
            try { await serverRun; } catch { }

        }

        #endregion

    }

}
