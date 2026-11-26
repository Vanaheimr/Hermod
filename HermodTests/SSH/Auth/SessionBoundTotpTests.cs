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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// The authentication method <c>totp@open.charging.cloud</c>: a time-based one-time password bound
    /// to the connection by exporter material of its keying material exporter, exercised over a loopback pipe.
    /// </summary>
    [TestFixture]
    public class SessionBoundTotpTests
    {

        private static readonly TOTPConfig Config = new ("a-shared-secret-of-enough-length", TimeSpan.FromSeconds(30), 12);


        #region TheBindingIsTheExportersAndATokenIsBoundAsOverTLS

        [Test]
        public void TheBindingIsTheExportersAndATokenIsBoundAsOverTLS()
        {

            // The connection of Appendix A.2 of the white paper "SSH Keying Material Exporter":
            // curve25519-sha256, K the shared secret of RFC 7748 section 6.1 as an mpint, H = 20 21 ... 3f.
            var k          = Convert.FromHexString("000000204a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742");
            var sessionId  = Convert.FromHexString("202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f");
            var now        = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

            using var exporter = SshExporter.Create(System.Security.Cryptography.HashAlgorithmName.SHA256, k, sessionId, sessionId);

            var binding    = SshSessionBoundTotp.BindingOf(exporter, sessionId);

            Assert.Multiple(() => {

                // Appendix A.4 of that white paper: the session identifier as nonce, the TOTP label, no context.
                Assert.That(Convert.ToHexStringLower(binding),
                            Is.EqualTo("40f294c52877ab676e1c139752f7be2d2d3ed650e5f1eb7069cdc036e9492c04"),
                            "the exporter material of the test vector");

                Assert.That(binding, Is.EqualTo(exporter.Export(sessionId, TOTPGenerator.TLSExporterLabel, [], TOTPGenerator.TLSExporterLength)),
                            "the binding is what the exporter gives under the label of the TLS binding");

                Assert.That(SshSessionBoundTotp.Generate(Config, binding, now), Is.EqualTo("pYSPAwzAqiHq"),
                            "the bound token of the test vector");

                // One token function for both transports: the same exporter material binds the same token
                // over TLS v1.3.
                Assert.That(SshSessionBoundTotp.Generate(Config, binding, now),
                            Is.EqualTo(TOTPGenerator.GenerateTOTP(Config.SharedSecret, Config.ValidityTime, Config.Length ?? 12, Config.Alphabet, now, binding, Config.HashAlgorithm).Current),
                            "the token of the TLS binding with the same material");

                // Not the token of version 0.1 of "SSH Authentication Extensions for OCPP", which was bound
                // to the session identifier itself.
                Assert.That(SshSessionBoundTotp.Generate(Config, binding, now),
                            Is.Not.EqualTo(SshSessionBoundTotp.Generate(Config, sessionId, now)),
                            "a token bound to the session identifier");

                Assert.That(() => SshSessionBoundTotp.BindingOf(exporter, []), Throws.ArgumentException,
                            "no session identifier");

            });

        }

        #endregion

        #region ATokenIsOfItsBindingAndItsTime

        [Test]
        public void ATokenIsOfItsBindingAndItsTime()
        {

            var here   = new Byte[32];
            var there  = new Byte[32];
            there[0]   = 1;

            var now    = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
            var token  = SshSessionBoundTotp.Generate(Config, here, now);

            Assert.Multiple(() => {

                Assert.That(token, Has.Length.EqualTo(12));

                // The test vector of the white paper "SSH Authentication Extensions for OCPP": the
                // exporter material 00 01 02 ... 1f, at 1700000000, with the alphabet 0-9a-zA-Z.
                Assert.That(TOTPGenerator.GenerateTOTP(now, Config.SharedSecret, Config.ValidityTime, Config.Length).Current,
                            Is.EqualTo("0aFolGu5wL1m"), "the unbound token of the test vector");

                Assert.That(SshSessionBoundTotp.Generate(Config, [.. Enumerable.Range(0, 32).Select(index => (Byte) index)], now),
                            Is.EqualTo("D4nFD0C2iovo"), "the bound token of the test vector");

                Assert.That(token, Is.Not.EqualTo(TOTPGenerator.GenerateTOTP(now, Config.SharedSecret, Config.ValidityTime, Config.Length).Current),
                            "a bound token is the unbound one");

                Assert.That(SshSessionBoundTotp.Verify(Config, token, here,  now),                 Is.True,  "the token of this binding and this time");
                Assert.That(SshSessionBoundTotp.Verify(Config, token, here,  now.AddSeconds(30)),  Is.True,  "a clock one slot ahead");
                Assert.That(SshSessionBoundTotp.Verify(Config, token, here,  now.AddSeconds(-30)), Is.True,  "a clock one slot behind");
                Assert.That(SshSessionBoundTotp.Verify(Config, token, here,  now.AddSeconds(90)),  Is.False, "a token that ran out");
                Assert.That(SshSessionBoundTotp.Verify(Config, token, there, now),                 Is.False, "the token of another connection");
                Assert.That(SshSessionBoundTotp.Verify(Config, token, [],    now),                 Is.False, "no binding");
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
                             .WithSessionBoundTotp((user, totp, binding, _) =>
                                                       ValueTask.FromResult(user == "cs001" && SshSessionBoundTotp.Verify(Config, totp, binding.Span)));

            Assert.That(policy.OfferedMethods, Is.EqualTo(new[] { "totp@open.charging.cloud" }));

            Assert.That(await RunAsync(policy, "cs001", binding => SshSessionBoundTotp.Generate(Config, binding), null, CancellationToken),
                        Is.True,  "the token of this connection");

            Assert.That(await RunAsync(policy, "cs002", binding => SshSessionBoundTotp.Generate(Config, binding), null, CancellationToken),
                        Is.False, "another login");

            // What somebody between the two would have: a token good in a connection of its own.
            Assert.That(await RunAsync(policy, "cs001", binding => SshSessionBoundTotp.Generate(Config, new Byte[binding.Length]), null, CancellationToken),
                        Is.False, "the token of another connection");

            // And the unbound token - the one a password field would carry - is not taken for it.
            Assert.That(await RunAsync(policy, "cs001", binding => TOTPGenerator.GenerateTOTP(Config.SharedSecret, Config.ValidityTime, Config.Length).Current, null, CancellationToken),
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
                           .WithSessionBoundTotp((user, totp, binding, _) =>
                                                     ValueTask.FromResult(SshSessionBoundTotp.Verify(Config, totp, binding.Span)));

            Assert.That(await RunAsync(both, "cs001", binding => SshSessionBoundTotp.Generate(Config, binding), "a password", CancellationToken), Is.True);
            Assert.That(passwords, Is.Empty, "the password went to a server that takes a bound token");

            // A server that does not know the method is never sent it, and is given the password.
            var passwordOnly = new SshAuthenticationPolicy()
                                   .WithPassword((user, password, _) => { passwords.Add(password); return ValueTask.FromResult(password == "a password"); });

            var asked = 0;

            Assert.That(await RunAsync(passwordOnly, "cs001", binding => { asked++; return "never sent"; }, "a password", CancellationToken), Is.True);
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
            var ok = await UserAuthentication.ClientAuthenticateAsync(client, User_Id.Parse(User), Password: Password, CancellationToken: CancellationToken, SessionBoundTotp: Totp);

            clientPipe.Output.Complete();
            try { await serverRun; } catch { }
            return ok;

        }

    }

}
