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

    /// <summary>M4 typed audit stream: the server emits structured events during authentication.</summary>
    [TestFixture]
    public class AuditStreamTests
    {

        #region AuditStream_RecordsBannerAndSuccess

        [Test]
        [CancelAfter(15000)]
        public async Task AuditStream_RecordsBannerAndSuccess(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();

            var audit         = new CollectingAuditSink();
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                return await UserAuthentication.ServerAuthenticateAsync(t, authenticator, Banner: "Authorized use only.", AuditSink: audit, CancellationToken: CancellationToken);
            }, CancellationToken);

            var clientRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                return await UserAuthentication.ClientPublicKeyAuthenticateAsync(t, "achim", userKey, CancellationToken: CancellationToken);
            }, CancellationToken);

            Assert.That(await clientRun, Is.True);
            await serverRun;

            var events = audit.Events;

            Assert.Multiple(() => {
                Assert.That(events.OfType<BannerSentEvent>().Any(),                                             Is.True, "a BannerSent event");
                Assert.That(events.OfType<AuthMethodSucceededEvent>().Any(e => e.Method == "publickey"),        Is.True, "a publickey success event");
                Assert.That(events.OfType<AuthenticationSucceededEvent>().Any(e => e.Username == "achim"),      Is.True, "an overall success event");
                Assert.That(events[^1],                                                                         Is.InstanceOf<AuthenticationSucceededEvent>());
            });

        }

        #endregion

        #region AuditStream_RecordsFailure

        [Test]
        [CancelAfter(15000)]
        public async Task AuditStream_RecordsFailure(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();

            var audit         = new CollectingAuditSink();
            var authenticator = new SshAuthenticationPolicy()
                                    .WithPassword((_, _, _) => ValueTask.FromResult(false));

            var serverRun = Task.Run(async () =>
            {
                using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                try   { await UserAuthentication.ServerAuthenticateAsync(t, authenticator, MaxAuthTries: 1, AuditSink: audit, CancellationToken: CancellationToken); }
                catch (SshAuthenticationException) { }
            }, CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            await UserAuthentication.ClientPasswordAuthenticateAsync(client, "achim", "wrong", CancellationToken: CancellationToken);

            clientPipe.Output.Complete();
            await serverRun;

            Assert.Multiple(() => {
                Assert.That(audit.Events.OfType<AuthMethodFailedEvent>().Any(),        Is.True, "a method-failed event");
                Assert.That(audit.Events.OfType<AuthenticationFailedEvent>().Any(),    Is.True, "an authentication-failed event");
            });

        }

        #endregion

    }

}
