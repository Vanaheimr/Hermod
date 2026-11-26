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

using System.Buffers;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Illias;

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
            var login   = User_Id.Parse("achim");

            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob);

            var serverRun = Task.Run(async () =>
            {
                using var transport = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                return await UserAuthentication.ServerAuthenticateAsync(transport, authenticator, Banner: "Authorized use only.", CancellationToken: CancellationToken);
            }, CancellationToken);

            var clientRun = Task.Run(async () => {
                using var transport = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                String? banner = null;
                var ok = await UserAuthentication.ClientPublicKeyAuthenticateAsync(
                                   transport,
                                   login,
                                   userKey,
                                   BannerCallback: (text, _) => banner = text,
                                   CancellationToken: CancellationToken
                               );
                return (ok, banner);
            }, CancellationToken);

            var (clientOk, clientBanner) = await clientRun;
            var serverResult             = await serverRun;

            Assert.Multiple(() => {
                Assert.That(clientOk,              Is.True,             "the client must authenticate successfully");
                Assert.That(serverResult.Username, Is.EqualTo(login));
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
            var login         = User_Id.Parse("achim");

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
                return await UserAuthentication.ClientPublicKeyAuthenticateAsync(transport, login, userKey, CancellationToken: CancellationToken);
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
            var (clientPipe, serverPipe)  = DuplexPipe.CreateConnectedPair();
            var hostKey                   = Ed25519KeyPair.Generate();
            var userKey                   = SshHostKey.GenerateEcdsa("ecdsa-sha2-nistp384");
            var operatorUser              = User_Id.Parse("operator");

            var authenticator = new SshUserAuthenticator((request, _) =>
                ValueTask.FromResult(request.Username == operatorUser &&
                                     request.PublicKeyBlob.AsSpan().SequenceEqual(userKey.PublicKeyBlob)));

            var serverRun = Task.Run(async () => {
                using var transport = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                return await UserAuthentication.ServerAuthenticateAsync(transport, authenticator, CancellationToken: CancellationToken);
            }, CancellationToken);

            var clientRun = Task.Run(async () =>
            {
                using var transport = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
                return await UserAuthentication.ClientPublicKeyAuthenticateAsync(transport, operatorUser, userKey, CancellationToken: CancellationToken);
            }, CancellationToken);

            Assert.That(await clientRun,            Is.True);
            Assert.That((await serverRun).Username, Is.EqualTo(operatorUser));

        }

        #endregion

        #region PublicKey_Auth_AlgorithmMismatchInSignature_IsRejected

        /// <summary>
        /// RFC 8332: when <c>SSH_MSG_USERAUTH_REQUEST</c> names <c>rsa-sha2-512</c> as the public-key
        /// algorithm, a signature produced with <c>rsa-sha2-256</c> (mismatching the declared algorithm)
        /// must be rejected even though both hashes are valid for the same RSA key.
        /// </summary>
        [Test]
        [CancelAfter(15000)]
        public async Task PublicKey_Auth_AlgorithmMismatchInSignature_IsRejected(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var rsaKey  = SshHostKey.GenerateRsa();
            var authenticator = SshUserAuthenticator.ForAuthorizedKeys(rsaKey.PublicKeyBlob);

            var serverRun = Task.Run(async () =>
            {
                using var transport = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                return await UserAuthentication.ServerAuthenticateAsync(transport, authenticator, MaxAuthTries: 1, CancellationToken: CancellationToken);
            }, CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);

            // Request ssh-userauth.
            var svcReq = new ArrayBufferWriter<Byte>();
            var w1     = new SshPacketWriter(svcReq);
            w1.WriteByte((Byte) SshMessageNumber.ServiceRequest);
            w1.WriteString("ssh-userauth");
            await client.SendPacketAsync(svcReq.WrittenSpan.ToArray(), CancellationToken);

            Byte[] reply;
            do { reply = await client.ReceivePacketAsync(CancellationToken); }
            while ((SshMessageNumber) reply[0] == SshMessageNumber.ExtInfo);
            Assert.That((SshMessageNumber) reply[0], Is.EqualTo(SshMessageNumber.ServiceAccept));

            // Declare "rsa-sha2-512" in the request, but sign with "rsa-sha2-256".
            var toSign = new ArrayBufferWriter<Byte>();
            var ws     = new SshPacketWriter(toSign);
            ws.WriteBinaryString(client.SessionId);
            ws.WriteByte((Byte) SshMessageNumber.UserAuthRequest);
            ws.WriteString("achim");
            ws.WriteString("ssh-connection");
            ws.WriteString("publickey");
            ws.WriteBoolean(true);
            ws.WriteString("rsa-sha2-512");
            ws.WriteBinaryString(rsaKey.PublicKeyBlob);
            var mismatchedSig = rsaKey.Sign("rsa-sha2-256", toSign.WrittenSpan);

            var req = new ArrayBufferWriter<Byte>();
            var w2  = new SshPacketWriter(req);
            w2.WriteByte((Byte) SshMessageNumber.UserAuthRequest);
            w2.WriteString("achim");
            w2.WriteString("ssh-connection");
            w2.WriteString("publickey");
            w2.WriteBoolean(true);
            w2.WriteString("rsa-sha2-512");
            w2.WriteBinaryString(rsaKey.PublicKeyBlob);
            w2.WriteBinaryString(mismatchedSig);
            await client.SendPacketAsync(req.WrittenSpan.ToArray(), CancellationToken);

            var response = await client.ReceivePacketAsync(CancellationToken);
            Assert.That((SshMessageNumber) response[0], Is.EqualTo(SshMessageNumber.UserAuthFailure),
                        "a signature whose algorithm disagrees with the request's algorithm must be refused");

            await Assert.ThrowsAsync<SshAuthenticationException>(async () => await serverRun);

        }

        #endregion

    }

}
