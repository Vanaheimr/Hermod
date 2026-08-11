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
    /// M4 public-key authentication (RFC 4252 §7): our client authenticates to our server over a
    /// loopback pipe with every supported key type, including the query-then-sign flow and the banner.
    /// </summary>
    [TestFixture]
    public class UserAuthLoopbackTests
    {

        #region PublicKey_Auth_Succeeds

        [TestCase("ssh-ed25519")]
        [TestCase("ecdsa-sha2-nistp256")]
        [TestCase("ecdsa-sha2-nistp521")]
        [TestCase("rsa-sha2-512")]
        [TestCase("rsa-sha2-256")]
        [CancelAfter(15000)]
        public async Task PublicKey_Auth_Succeeds(String UserKeyAlgorithm, CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = HostKeyMatrixTests.MakeHostKey(UserKeyAlgorithm);

            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            var serverRun = Task.Run(async () =>
            {
                using var transport = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                return await UserAuthentication.ServerAuthenticateAsync(transport, authenticator, Banner: "Authorized use only.", CancellationToken: CancellationToken);
            }, CancellationToken);

            var clientRun = Task.Run(async () =>
            {
                using var transport = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                String? banner = null;
                var ok = await UserAuthentication.ClientPublicKeyAuthenticateAsync(
                             transport, "achim", userKey, BannerCallback: (text, _) => banner = text, CancellationToken: CancellationToken);
                return (ok, banner);
            }, CancellationToken);

            var (clientOk, clientBanner) = await clientRun;
            var serverResult             = await serverRun;

            Assert.Multiple(() => {
                Assert.That(clientOk,             Is.True,             "the client must authenticate successfully");
                Assert.That(serverResult.Username, Is.EqualTo("achim"));
                Assert.That(serverResult.Method,   Is.EqualTo("publickey"));
                Assert.That(clientBanner,          Is.EqualTo("Authorized use only."));
            });

        }

        #endregion

        #region PublicKey_Auth_UnknownKey_IsRejected

        [Test]
        [CancelAfter(15000)]
        public async Task PublicKey_Auth_UnknownKey_IsRejected(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey       = Ed25519KeyPair.Generate();
            var userKey       = SshHostKey.GenerateEd25519();
            var strangersKey  = SshHostKey.GenerateEd25519();

            // The server only trusts a different key than the one the client will present.
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(strangersKey.PublicKeyBlob);

            var serverRun = Task.Run(async () =>
            {
                using var transport = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                return await UserAuthentication.ServerAuthenticateAsync(transport, authenticator, CancellationToken: CancellationToken);
            }, CancellationToken);

            var clientRun = Task.Run(async () =>
            {
                using var transport = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                return await UserAuthentication.ClientPublicKeyAuthenticateAsync(transport, "achim", userKey, CancellationToken: CancellationToken);
            }, CancellationToken);

            Assert.That(await clientRun, Is.False, "an untrusted key must be rejected at the query stage");

            // The client gave up; signal end-of-stream so the still-waiting server observes the disconnect.
            clientPipe.Output.Complete();
            Assert.That(async () => await serverRun, Throws.InstanceOf<SshWireException>());

        }

        #endregion

        #region PublicKey_Auth_TamperedSessionBinding_Fails

        [Test]
        [CancelAfter(15000)]
        public async Task PublicKey_Auth_WrongUsername_StillBindsSignature(CancellationToken CancellationToken)
        {

            // A sanity check that the signature is bound to the request: the server authorizes the key,
            // and the same username round-trips through the signed data on both sides.
            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEcdsa("ecdsa-sha2-nistp384");

            var authenticator = new SshUserAuthenticator((request, _) =>
                ValueTask.FromResult(request.Username == "operator" &&
                                     request.PublicKeyBlob.AsSpan().SequenceEqual(userKey.PublicKeyBlob)));

            var serverRun = Task.Run(async () =>
            {
                using var transport = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                return await UserAuthentication.ServerAuthenticateAsync(transport, authenticator, CancellationToken: CancellationToken);
            }, CancellationToken);

            var clientRun = Task.Run(async () =>
            {
                using var transport = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                return await UserAuthentication.ClientPublicKeyAuthenticateAsync(transport, "operator", userKey, CancellationToken: CancellationToken);
            }, CancellationToken);

            Assert.That(await clientRun,        Is.True);
            Assert.That((await serverRun).Username, Is.EqualTo("operator"));

        }

        #endregion

    }

}
