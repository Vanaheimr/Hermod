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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// The authentication method <c>totp@open.charging.cloud</c>: a time-based one-time password bound
    /// to the session identifier, exercised over a loopback pipe.
    /// </summary>
    [TestFixture]
    public class SessionBoundTotpTests
    {

        private static readonly TOTPConfig Config = new ("a-shared-secret-of-enough-length", TimeSpan.FromSeconds(30), 12);


        #region ATokenIsOfItsSessionAndItsTime

        [Test]
        public void ATokenIsOfItsSessionAndItsTime()
        {

            var here   = new Byte[32];
            var there  = new Byte[32];
            there[0]   = 1;

            var now    = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
            var token  = SshSessionBoundTotp.Generate(Config, here, now);

            Assert.Multiple(() => {

                Assert.That(token, Has.Length.EqualTo(12));

                // The test vector of the white paper "SSH Authentication Extensions for OCPP": the
                // session identifier 00 01 02 ... 1f, at 1700000000, with the alphabet 0-9a-zA-Z.
                Assert.That(TOTPGenerator.GenerateTOTP(now, Config.SharedSecret, Config.ValidityTime, Config.Length).Current,
                            Is.EqualTo("0aFolGu5wL1m"), "the unbound token of the test vector");

                Assert.That(SshSessionBoundTotp.Generate(Config, [.. Enumerable.Range(0, 32).Select(index => (Byte) index)], now),
                            Is.EqualTo("D4nFD0C2iovo"), "the bound token of the test vector");

                Assert.That(token, Is.Not.EqualTo(TOTPGenerator.GenerateTOTP(now, Config.SharedSecret, Config.ValidityTime, Config.Length).Current),
                            "a bound token is the unbound one");

                Assert.That(SshSessionBoundTotp.Verify(Config, token, here,  now),                 Is.True,  "the token of this session and this time");
                Assert.That(SshSessionBoundTotp.Verify(Config, token, here,  now.AddSeconds(30)),  Is.True,  "a clock one slot ahead");
                Assert.That(SshSessionBoundTotp.Verify(Config, token, here,  now.AddSeconds(-30)), Is.True,  "a clock one slot behind");
                Assert.That(SshSessionBoundTotp.Verify(Config, token, here,  now.AddSeconds(90)),  Is.False, "a token that ran out");
                Assert.That(SshSessionBoundTotp.Verify(Config, token, there, now),                 Is.False, "the token of another session");
                Assert.That(SshSessionBoundTotp.Verify(Config, "",    here,  now),                 Is.False, "no token");

                Assert.That(SshSessionBoundTotp.Verify(new TOTPConfig("another-shared-secret-of-length", TimeSpan.FromSeconds(30), 12), token, here, now),
                            Is.False, "the token of another secret");

            });

        }

        #endregion

        #region TheMethod_LetsInWhoeverKnowsTheSecret_AndNobodyElse

        [Test]
        [CancelAfter(15000)]
        public async Task TheMethod_LetsInWhoeverKnowsTheSecret_AndNobodyElse(CancellationToken CancellationToken)
        {

            var policy = new SshAuthenticationPolicy()
                             .WithSessionBoundTotp((user, totp, sessionId, _) =>
                                                       ValueTask.FromResult(user == "cs001" && SshSessionBoundTotp.Verify(Config, totp, sessionId.Span)));

            Assert.That(policy.OfferedMethods, Is.EqualTo(new[] { "totp@open.charging.cloud" }));

            Assert.That(await RunAsync(policy, "cs001", sessionId => SshSessionBoundTotp.Generate(Config, sessionId), null, CancellationToken),
                        Is.True,  "the token of this session");

            Assert.That(await RunAsync(policy, "cs002", sessionId => SshSessionBoundTotp.Generate(Config, sessionId), null, CancellationToken),
                        Is.False, "another login");

            // What somebody between the two would have: a token good in a session of its own.
            Assert.That(await RunAsync(policy, "cs001", sessionId => SshSessionBoundTotp.Generate(Config, new Byte[sessionId.Length]), null, CancellationToken),
                        Is.False, "the token of another session");

            // And the unbound token - the one a password field would carry - is not taken for it.
            Assert.That(await RunAsync(policy, "cs001", sessionId => TOTPGenerator.GenerateTOTP(Config.SharedSecret, Config.ValidityTime, Config.Length).Current, null, CancellationToken),
                        Is.False, "an unbound token");

        }

        #endregion

        #region WhatCannotBeReplayedGoesBeforeWhatCan

        [Test]
        [CancelAfter(15000)]
        public async Task WhatCannotBeReplayedGoesBeforeWhatCan(CancellationToken CancellationToken)
        {

            var passwords = new List<String>();

            var both = new SshAuthenticationPolicy()
                           .WithPassword((user, password, _) => { passwords.Add(password); return ValueTask.FromResult(true); })
                           .WithSessionBoundTotp((user, totp, sessionId, _) =>
                                                     ValueTask.FromResult(SshSessionBoundTotp.Verify(Config, totp, sessionId.Span)));

            Assert.That(await RunAsync(both, "cs001", sessionId => SshSessionBoundTotp.Generate(Config, sessionId), "a password", CancellationToken), Is.True);
            Assert.That(passwords, Is.Empty, "the password went to a server that takes a bound token");

            // A server that does not know the method is never sent it, and is given the password.
            var passwordOnly = new SshAuthenticationPolicy()
                                   .WithPassword((user, password, _) => { passwords.Add(password); return ValueTask.FromResult(password == "a password"); });

            var asked = 0;

            Assert.That(await RunAsync(passwordOnly, "cs001", sessionId => { asked++; return "never sent"; }, "a password", CancellationToken), Is.True);
            Assert.That(asked,     Is.Zero, "a token was worked out for a server that does not take one");
            Assert.That(passwords, Is.EqualTo(new[] { "a password" }));

        }

        #endregion


        private static async Task<Boolean> RunAsync(ISshUserAuthenticator  Authenticator,
                                                    String                 User,
                                                    Func<Byte[], String>   Totp,
                                                    String?                Password,
                                                    CancellationToken      CancellationToken)
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
            var ok = await UserAuthentication.ClientAuthenticateAsync(client, User, Password: Password, CancellationToken: CancellationToken, SessionBoundTotp: Totp);

            clientPipe.Output.Complete();
            try { await serverRun; } catch { }
            return ok;

        }

    }

}
