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
    /// The token of Hermod's own time-based one-time password, unbound, asked for over keyboard-interactive
    /// (<see cref="HermodTotpKeyboardInteractive"/>) - the variant an OpenSSH peer can take part in.
    /// </summary>
    [TestFixture]
    public class HermodTotpKeyboardInteractiveTests
    {

        private sealed class FixedTimeProvider(DateTimeOffset Now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
        }

        /// <summary>
        /// The configuration of the canonical vector "defaults-slot-boundary" (totp-test-vectors.json).
        /// </summary>
        private static readonly TOTPConfig      VectorConfig  = new ("secure!Charging!", TimeSpan.FromSeconds(30), 12);
        private static readonly DateTimeOffset  VectorTime    = DateTimeOffset.FromUnixTimeSeconds(1_718_611_200);


        #region TheTokenIsTheUnboundTokenOfTheVectors

        [Test]
        public void TheTokenIsTheUnboundTokenOfTheVectors()
        {

            Assert.Multiple(() => {

                Assert.That(HermodTotpKeyboardInteractive.Generate(VectorConfig, VectorTime),                 Is.EqualTo("akF3c7qY2uiu"), "current of the vector");
                Assert.That(HermodTotpKeyboardInteractive.Generate(VectorConfig, VectorTime.AddSeconds(-30)), Is.EqualTo("QT1cCdKsIb9e"), "previous of the vector");
                Assert.That(HermodTotpKeyboardInteractive.Generate(VectorConfig, VectorTime.AddSeconds( 30)), Is.EqualTo("1U70OgaBA48M"), "next of the vector");

                // Every parameter of the configuration is the generator's.
                var custom = new TOTPConfig("another-secret-of-enough-length", TimeSpan.FromSeconds(10), 8, "0123456789", TOTPHashAlgorithm.SHA512);
                Assert.That(HermodTotpKeyboardInteractive.Generate(custom, VectorTime),
                            Is.EqualTo(TOTPGenerator.GenerateTOTP(custom.SharedSecret, custom.ValidityTime, custom.Length, custom.Alphabet, VectorTime, null, custom.HashAlgorithm).Current));
                Assert.That(HermodTotpKeyboardInteractive.Generate(custom, VectorTime), Does.Match("^[0-9]{8}$"));

            });

        }

        #endregion

        #region ATokenIsGoodInItsSlotAndTheTwoBesideIt

        [Test]
        public void ATokenIsGoodInItsSlotAndTheTwoBesideIt()
        {

            const String token = "akF3c7qY2uiu";

            Assert.Multiple(() => {

                Assert.That(HermodTotpKeyboardInteractive.Verify(VectorConfig, token,          VectorTime),                 Is.True,  "its own slot");
                Assert.That(HermodTotpKeyboardInteractive.Verify(VectorConfig, token,          VectorTime.AddSeconds( 30)), Is.True,  "a clock one slot ahead");
                Assert.That(HermodTotpKeyboardInteractive.Verify(VectorConfig, token,          VectorTime.AddSeconds(-30)), Is.True,  "a clock one slot behind");
                Assert.That(HermodTotpKeyboardInteractive.Verify(VectorConfig, token,          VectorTime.AddSeconds( 60)), Is.False, "a token that ran out");
                Assert.That(HermodTotpKeyboardInteractive.Verify(VectorConfig, token,          VectorTime.AddSeconds(-60)), Is.False, "a token not yet valid");
                Assert.That(HermodTotpKeyboardInteractive.Verify(VectorConfig, token.ToLowerInvariant(), VectorTime),       Is.False, "the case of the alphabet counts");
                Assert.That(HermodTotpKeyboardInteractive.Verify(VectorConfig, token + " ",    VectorTime),                 Is.False, "nothing after the token");
                Assert.That(HermodTotpKeyboardInteractive.Verify(VectorConfig, "",             VectorTime),                 Is.False, "no token");
                Assert.That(HermodTotpKeyboardInteractive.Verify(VectorConfig, null,           VectorTime),                 Is.False, "no token at all");
                Assert.That(HermodTotpKeyboardInteractive.Verify(new TOTPConfig("not-the-secret-of-enough-length", TimeSpan.FromSeconds(30), 12), token, VectorTime),
                            Is.False, "the token of another secret");

                // The token of the session-bound variant is another one.
                Assert.That(HermodTotpKeyboardInteractive.Verify(VectorConfig, SshSessionBoundTotp.Generate(VectorConfig, new Byte[32], VectorTime), VectorTime),
                            Is.False, "a bound token");

            });

        }

        #endregion

        #region AConfigurationThatAsksForBinding_IsRefused

        [Test]
        public void AConfigurationThatAsksForBinding_IsRefused()
        {

            Assert.Multiple(() => {
                Assert.That(() => new HermodTotpKeyboardInteractive(new TOTPConfig("secure!Charging!", TLSChannelBinding: true)), Throws.ArgumentException);
                Assert.That(() => new HermodTotpKeyboardInteractive(new TOTPConfig(" ")),                                       Throws.ArgumentException);
                Assert.That(() => new HermodTotpKeyboardInteractive(new TOTPConfig("too-short")),                               Throws.ArgumentException, "a secret the generator refuses");
                Assert.That(() => new HermodTotpKeyboardInteractive(new TOTPConfig("secure!Charging!", Length: 2)),             Throws.ArgumentException, "a length the generator refuses");
                Assert.That(new HermodTotpKeyboardInteractive(new TOTPConfig("secure!Charging!", TLSChannelBinding: false)).Prompts.Single(),
                            Is.EqualTo(new SshPrompt(HermodTotpKeyboardInteractive.DefaultPrompt, Echo: false)));
            });

        }

        #endregion


        #region SecondFactor_CompletesPublicKeyThenToken

        [Test]
        [CancelAfter(15000)]
        public async Task SecondFactor_CompletesPublicKeyThenToken(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey   = Ed25519KeyPair.Generate();
            var userKey   = SshHostKey.GenerateEd25519();
            var clock     = new FixedTimeProvider(VectorTime.AddSeconds(7));
            var config    = new TOTPConfig("a-shared-secret-of-enough-length", TimeSpan.FromSeconds(10), 16, "abcdefghijkmnpqrstuvwxyz23456789", TOTPHashAlgorithm.SHA384);
            var prompts   = new List<SshPrompt>();

            var authenticator = new SshAuthenticationPolicy()
                                    .WithPublicKey((req, _) => ValueTask.FromResult(req.PublicKeyBlob.AsSpan().SequenceEqual(userKey.PublicKeyBlob)))
                                    .WithSecondFactor(_ => new HermodTotpKeyboardInteractive(config, "Hermod TOTP: ", TimeProvider: clock));

            var serverRun = Task.Run(async () => {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                return await UserAuthentication.ServerAuthenticateAsync(t, authenticator, CancellationToken: CancellationToken);
            }, CancellationToken);

            var answer    = HermodTotpKeyboardInteractive.Answer(config, clock);

            var clientRun = Task.Run(async () => {
                using var t = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                return await UserAuthentication.ClientAuthenticateAsync(
                           t, User_Id.Parse("alice"),
                           Keys:                 [ userKey ],
                           KeyboardInteractive:  (challenge, cancellationToken) => {
                                                     prompts.AddRange(challenge.Prompts);
                                                     return answer(challenge, cancellationToken);
                                                 },
                           CancellationToken:    CancellationToken);
            }, CancellationToken);

            var clientOk = await clientRun;
            var result   = await serverRun;

            Assert.Multiple(() => {
                Assert.That(clientOk,       Is.True,  "publickey and the token complete the chain");
                Assert.That(result.Method,  Is.EqualTo("keyboard-interactive"), "the method that completed it");
                Assert.That(prompts,        Is.EqualTo(new[] { new SshPrompt("Hermod TOTP: ", Echo: false) }), "the prompt the server asked with");
            });

        }

        #endregion

        #region SecondFactor_RefusesATokenOfAnotherSecret

        [Test]
        [CancelAfter(15000)]
        public async Task SecondFactor_RefusesATokenOfAnotherSecret(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey   = Ed25519KeyPair.Generate();
            var userKey   = SshHostKey.GenerateEd25519();

            var authenticator = new SshAuthenticationPolicy()
                                    .WithPublicKey((req, _) => ValueTask.FromResult(req.PublicKeyBlob.AsSpan().SequenceEqual(userKey.PublicKeyBlob)))
                                    .WithSecondFactor(_ => new HermodTotpKeyboardInteractive(VectorConfig));

            var serverRun = Task.Run(async () => {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                try   { await UserAuthentication.ServerAuthenticateAsync(t, authenticator, MaxAuthTries: 1, CancellationToken: CancellationToken); }
                catch { }
            }, CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);

            var ok = await UserAuthentication.ClientAuthenticateAsync(
                         client, User_Id.Parse("alice"),
                         Keys:                 [ userKey ],
                         KeyboardInteractive:  HermodTotpKeyboardInteractive.Answer(new TOTPConfig("not-the-secret-of-enough-length", TimeSpan.FromSeconds(30), 12)),
                         CancellationToken:    CancellationToken);

            Assert.That(ok, Is.False, "a valid key and the token of another secret");

            clientPipe.Output.Complete();
            try { await serverRun; } catch { }

        }

        #endregion

    }

}
