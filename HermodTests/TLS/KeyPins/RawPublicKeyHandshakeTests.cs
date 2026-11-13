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

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TLS.KeyPins
{

    /// <summary>
    /// Raw public keys (RFC 7250) in Hermod's own TLS 1.3, the stack under
    /// its QUIC and HTTP/3: a peer shows its SubjectPublicKeyInfo and no
    /// certificate, proves the key with CertificateVerify, and is accepted
    /// by its key pin - as a server and as a client. Where the other side
    /// does not offer raw public keys the certificate is shown, and where
    /// an endpoint demands them and does not get them the handshake ends.
    /// </summary>
    /// <remarks>
    /// The handshakes run in process, message by message, so that a test
    /// can look at what is on the wire and put something else there. No
    /// certificate store is touched.
    /// </remarks>
    [TestFixture]
    public class RawPublicKeyHandshakeTests
    {

        #region (helpers)

        private static readonly Byte[] TransportParameters = [ 0x0f, 0x00 ];

        private sealed class Told
        {
            public readonly ConcurrentQueue<KeyPinValidation> Results = new();
            public void Add(KeyPinValidation Validation) => Results.Enqueue(Validation);
            public KeyPinOutcome[] Outcomes => Results.Select(result => result.Outcome).ToArray();
        }

        /// <summary>
        /// The handshake messages that went one way.
        /// </summary>
        private sealed class Wire
        {

            public readonly List<Byte[]> Messages = [];

            public IEnumerable<Byte[]> OfType(HandshakeType Type)
                => Messages.Where(message => message[0] == (Byte) Type);

            /// <summary>
            /// The entries of the one Certificate message that went this way.
            /// </summary>
            public List<Byte[]> CertificateEntries()
            {

                var certificate = OfType(HandshakeType.Certificate).Single();

                Assert.That(CertificateMessage.TryParseWithContext(certificate.AsSpan(4), out var entries, out _), Is.True);

                return entries;

            }

            public Boolean Contains(Byte[] Needle)
                => Messages.Any(message => message.AsSpan().IndexOf(Needle) >= 0);

        }

        /// <summary>
        /// What is put on the wire in place of a message: the message itself,
        /// another one, or nothing.
        /// </summary>
        private delegate Byte[]? Rewrite(Byte[] Message);

        private static void Pump(ITlsHandshake From, ITlsHandshake To, Wire? Wire = null, Rewrite? Rewrite = null)
        {
            while (From.TryGetOutgoingCrypto(out EncryptionLevel level, out Byte[] data))
            {

                var sent = Rewrite is null ? data : Rewrite(data);

                if (sent is null)
                    continue;

                Wire?.Messages.Add(sent);
                To.ProvideCrypto(level, sent);

            }
        }

        /// <summary>
        /// Run a handshake to its end, or to the exception that ends it.
        /// </summary>
        private static void Run(TlsClientHandshake  Client,
                                TlsServerHandshake  Server,
                                Wire?               ToServer        = null,
                                Wire?               ToClient        = null,
                                Rewrite?            RewriteToServer = null,
                                Rewrite?            RewriteToClient = null)
        {

            Client.Start();

            for (var round = 0; round < 10 && !(Client.IsComplete && Server.IsComplete); round++)
            {
                Pump(Client, Server, ToServer, RewriteToServer);
                Pump(Server, Client, ToClient, RewriteToClient);
            }

        }

        private static void RunToCompletion(TlsClientHandshake  Client,
                                            TlsServerHandshake  Server,
                                            Wire?               ToServer   = null,
                                            Wire?               ToClient   = null)
        {

            Run(Client, Server, ToServer, ToClient);

            Assert.That(Client.IsComplete,          Is.True, "Client handshake incomplete.");
            Assert.That(Server.IsComplete,          Is.True, "Server handshake incomplete.");
            Assert.That(Server.ClientFinishedValid, Is.True, "The client's Finished does not verify.");

        }

        /// <summary>
        /// A key that is shown raw, in the given mode. It has a certificate
        /// all the same, as every key of Hermod's TLS has: the carrier that
        /// is shown where a peer does not take raw public keys.
        /// </summary>
        private static ServerCertificate Key(RawPublicKeyMode RawPublicKeys, String CommonName = "carrier.invalid")
        {
            var key = ServerCertificate.CreateSelfSigned(CommonName);
            key.RawPublicKeys = RawPublicKeys;
            return key;
        }

        private static CertificateValidationOptions PinnedTo(ServerCertificate  Key,
                                                             RawPublicKeyMode   RawPublicKeys,
                                                             Told?              Told = null)

            => CertificateValidationOptions.PinnedTo(
                   new KeyPinPolicy([ Key.KeyPin ], RawPublicKeys: RawPublicKeys),
                   Told is null ? null : Told.Add
               );

        private static ClientCertificateOptions ClientsPinnedTo(ServerCertificate      Key,
                                                                RawPublicKeyMode       RawPublicKeys,
                                                                Told?                  Told = null,
                                                                ClientCertificateMode  Mode = ClientCertificateMode.Require)

            => ClientCertificateOptions.RequirePinned(
                   new KeyPinPolicy([ Key.KeyPin ], RawPublicKeys: RawPublicKeys),
                   Told is null ? null : Told.Add
               ) with { Mode = Mode };

        /// <summary>
        /// The given EncryptedExtensions with one more extension.
        /// </summary>
        private static Byte[] WithExtension(Byte[] EncryptedExtensions, ExtensionType Type, params Byte[] Data)
        {

            Assert.That(EncryptedExtensions[0], Is.EqualTo((Byte) HandshakeType.EncryptedExtensions));

            var extensions = EncryptedExtensions.AsSpan(6).ToArray();

            Byte[] added   = [ .. extensions, (Byte) ((UInt16) Type >> 8), (Byte) Type, (Byte) (Data.Length >> 8), (Byte) Data.Length, .. Data ];
            var    body    = added.Length + 2;

            return [ (Byte) HandshakeType.EncryptedExtensions,
                     (Byte) (body >> 16), (Byte) (body >> 8), (Byte) body,
                     (Byte) (added.Length >> 8), (Byte) added.Length,
                     .. added ];

        }

        private static Rewrite Replace(HandshakeType Type, Func<Byte[], Byte[]?> With)
            => message => message[0] == (Byte) Type ? With(message) : message;

        #endregion


        #region The ClientHello: what is offered

        private static ClientHelloInfo ClientHelloOf(TlsClientHandshake Client)
        {

            Client.Start();

            Assert.That(Client.TryGetOutgoingCrypto(out _, out var clientHello),     Is.True);
            Assert.That(ClientHelloParser.TryParse(clientHello, out var parsed),     Is.True);

            return parsed!;

        }

        [Test]
        public void ClientHello_OffersWhatTheEndpointAsksFor()
        {

            using var server  = Key(RawPublicKeyMode.Off);
            using var station = Key(RawPublicKeyMode.Off);

            Byte[] rawAlone    = [ (Byte) TLSCertificateType.RawPublicKey ];
            Byte[] rawThenX509 = [ (Byte) TLSCertificateType.RawPublicKey, (Byte) TLSCertificateType.X509 ];

            // A CA-checked endpoint, and a key-pinned one whose keys come in certificates: no extension at all.
            using (var client = new TlsClientHandshake("localhost", TransportParameters))
            {
                var hello = ClientHelloOf(client);
                Assert.That(hello.ServerCertificateTypes, Is.Null);
                Assert.That(hello.ClientCertificateTypes, Is.Null);
            }

            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       certificateValidation: PinnedTo(server, RawPublicKeyMode.Off),
                                                       clientCertificate:     station))
            {
                var hello = ClientHelloOf(client);
                Assert.That(hello.ServerCertificateTypes, Is.Null);
                Assert.That(hello.ClientCertificateTypes, Is.Null);
            }

            // Prefer: a raw public key before X.509.
            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       certificateValidation: PinnedTo(server, RawPublicKeyMode.Prefer)))
            {
                var hello = ClientHelloOf(client);
                Assert.That(hello.ServerCertificateTypes, Is.EqualTo(rawThenX509));
                Assert.That(hello.ClientCertificateTypes, Is.Null, "There is no client key to show.");
            }

            // Require: a raw public key alone. And the client's own key, as it says itself.
            station.RawPublicKeys = RawPublicKeyMode.Prefer;

            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       certificateValidation: PinnedTo(server, RawPublicKeyMode.Require),
                                                       clientCertificate:     station))
            {
                var hello = ClientHelloOf(client);
                Assert.That(hello.ServerCertificateTypes, Is.EqualTo(rawAlone));
                Assert.That(hello.ClientCertificateTypes, Is.EqualTo(rawThenX509));
            }

            station.RawPublicKeys = RawPublicKeyMode.Require;

            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       clientCertificate: station))
            {
                var hello = ClientHelloOf(client);
                Assert.That(hello.ServerCertificateTypes, Is.Null, "A CA-checked server shows a certificate, whatever the client's own key is.");
                Assert.That(hello.ClientCertificateTypes, Is.EqualTo(rawAlone));
            }

        }

        [Test]
        public void ClientHello_AMalformedCertificateTypeList_IsNotParsed()
        {

            using var kex = EcdheKeyExchange.Create(NamedGroup.Secp256r1);

            var clientHello = ClientHello.Build(new ClientHelloOptions {
                                  ServerName               = "localhost",
                                  KeyShares                = [ new KeyShareEntry(NamedGroup.Secp256r1, kex.PublicKey) ],
                                  QuicTransportParameters  = TransportParameters,
                                  ServerCertificateTypes   = [ TLSCertificateType.RawPublicKey, TLSCertificateType.X509 ]
                              });

            Assert.That(ClientHelloParser.TryParse(clientHello, out var parsed), Is.True);
            Assert.That(parsed!.ServerCertificateTypes, Is.EqualTo(new Byte[] { 2, 0 }));

            // server_certificate_type (20), 3 bytes: a list of 2 types. Cut the list's length to 1: one byte is left over.
            Byte[] extension = [ 0x00, 0x14, 0x00, 0x03, 0x02, 0x02, 0x00 ];
            var    at        = clientHello.AsSpan().IndexOf(extension);

            Assert.That(at, Is.GreaterThan(0));

            clientHello[at + 4] = 0x01;
            Assert.That(ClientHelloParser.TryParse(clientHello, out _), Is.False, "A list that does not fill its extension.");

            // ... and an empty one.
            clientHello[at + 4] = 0x00;
            Assert.That(ClientHelloParser.TryParse(clientHello, out _), Is.False, "An empty list.");

        }

        #endregion

        #region The server's key

        [Test]
        public void TheServersKey_ComesRaw_AndIsAcceptedByItsPin([Values(RawPublicKeyMode.Prefer, RawPublicKeyMode.Require)] RawPublicKeyMode ClientMode,
                                                                [Values(RawPublicKeyMode.Prefer, RawPublicKeyMode.Require)] RawPublicKeyMode ServerMode)
        {

            using var key      = Key(ServerMode);
            var       told     = new Told();
            var       toClient = new Wire();

            // A name that is not the certificate's, and no certificate to have a name: only the key counts.
            using var client = new TlsClientHandshake("somewhere-else.invalid", TransportParameters,
                                                      certificateValidation: PinnedTo(key, ClientMode, told));
            using var server = new TlsServerHandshake(key, TransportParameters);

            RunToCompletion(client, server, ToClient: toClient);

            var entries = toClient.CertificateEntries();

            Assert.Multiple(() => {

                Assert.That(client.ServerCertificateType,   Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(server.ServerCertificateType,   Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(client.ServerCertificateValid,  Is.True);

                // What the client holds: a key, and no certificate.
                Assert.That(client.ServerCertificate,       Is.Null);
                Assert.That(client.ServerKey,               Is.Not.Null);
                Assert.That(client.ServerKey!.IsRawPublicKey, Is.True);
                Assert.That(client.ServerKey!.Certificate,  Is.Null);
                Assert.That(client.ServerKey!.KeyPin,       Is.EqualTo(key.KeyPin));
                Assert.That(client.ServerKeyPin,            Is.EqualTo(key.KeyPin));

                // What was on the wire: one entry, the SubjectPublicKeyInfo, and the certificate nowhere.
                Assert.That(entries,                        Has.Count.EqualTo(1));
                Assert.That(entries[0],                     Is.EqualTo(key.SubjectPublicKeyInfo));
                Assert.That(toClient.Contains(key.Der),     Is.False, "The certificate was sent.");

                Assert.That(told.Outcomes,                  Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                Assert.That(told.Results.Single().CertificateType, Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(told.Results.Single().Pin,      Is.EqualTo(key.KeyPin));

            });

        }

        [Test]
        public void TheServersKey_OfEveryKindARawKeyMayBe_ComesRaw()
        {

            using var p256    = ServerCertificate.CreateSelfSigned       ("carrier.invalid");
            using var ed25519 = ServerCertificate.CreateSelfSignedEd25519("carrier.invalid");
            using var mlDsa   = ServerCertificate.CreateSelfSignedMLDsa  ("carrier.invalid");

            using var p384Key  = ECDsa.Create(ECCurve.NamedCurves.nistP384);
            using var p384Cert = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=carrier.invalid", p384Key, HashAlgorithmName.SHA384).
                                     CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
            using var p384     = ServerCertificate.From(p384Cert);

            foreach (var key in new[] { p256, p384, ed25519, mlDsa })
            {

                key.RawPublicKeys = RawPublicKeyMode.Require;

                using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                          certificateValidation: PinnedTo(key, RawPublicKeyMode.Require));
                using var server = new TlsServerHandshake(key, TransportParameters);

                RunToCompletion(client, server);

                Assert.That(client.ServerKey?.IsRawPublicKey, Is.True,              key.SignatureScheme.ToString());
                Assert.That(client.ServerKey?.KeyPin,         Is.EqualTo(key.KeyPin), key.SignatureScheme.ToString());

            }

        }

        [Test]
        public void TheServersKey_LearnedOnFirstUse_IsARawKey()
        {

            using var key  = Key(RawPublicKeyMode.Prefer);
            var       told = new Told();

            using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                      certificateValidation: CertificateValidationOptions.PinnedTo(
                                                                                 KeyPinPolicy.LearnOnFirstUse(RawPublicKeyMode.Require),
                                                                                 told.Add
                                                                             ));
            using var server = new TlsServerHandshake(key, TransportParameters);

            RunToCompletion(client, server);

            Assert.That(told.Outcomes,                           Is.EqualTo(new[] { KeyPinOutcome.Learned }));
            Assert.That(told.Results.Single().Pin,               Is.EqualTo(key.KeyPin));
            Assert.That(told.Results.Single().CertificateType,   Is.EqualTo(TLSCertificateType.RawPublicKey));

        }

        [Test]
        public void TheServersKey_FallsBackToItsCertificate_WhereTheOtherSideOffersNoRawKeys()
        {

            // A client that prefers raw public keys, and a server that never shows its key raw.
            using (var key = Key(RawPublicKeyMode.Off))
            {

                var       told     = new Told();
                var       toClient = new Wire();
                using var client   = new TlsClientHandshake("localhost", TransportParameters,
                                                            certificateValidation: PinnedTo(key, RawPublicKeyMode.Prefer, told));
                using var server   = new TlsServerHandshake(key, TransportParameters);

                RunToCompletion(client, server, ToClient: toClient);

                Assert.Multiple(() => {
                    Assert.That(client.ServerCertificateType,          Is.EqualTo(TLSCertificateType.X509));
                    Assert.That(client.ServerCertificate,              Is.Not.Null);
                    Assert.That(client.ServerKey!.IsRawPublicKey,      Is.False);
                    Assert.That(client.ServerKey!.KeyPin,              Is.EqualTo(key.KeyPin));
                    Assert.That(toClient.CertificateEntries()[0],      Is.EqualTo(key.Der));
                    Assert.That(told.Outcomes,                         Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                    Assert.That(told.Results.Single().CertificateType, Is.EqualTo(TLSCertificateType.X509));
                });

            }

            // A server that prefers to show its key raw, and clients that do not ask for it:
            // one that is key-pinned with its keys in certificates, and one that checks a CA.
            using (var key = Key(RawPublicKeyMode.Prefer, "localhost"))
            {

                foreach (var validation in new[] {
                                               PinnedTo(key, RawPublicKeyMode.Off),
                                               new CertificateValidationOptions { CustomTrustRoots = [ key.Certificate ] }
                                           })
                {

                    var       toClient = new Wire();
                    using var client   = new TlsClientHandshake("localhost", TransportParameters, certificateValidation: validation);
                    using var server   = new TlsServerHandshake(key, TransportParameters);

                    RunToCompletion(client, server, ToClient: toClient);

                    Assert.Multiple(() => {
                        Assert.That(client.ServerCertificateType,     Is.EqualTo(TLSCertificateType.X509));
                        Assert.That(server.ServerCertificateType,     Is.EqualTo(TLSCertificateType.X509));
                        Assert.That(client.ServerCertificate,         Is.Not.Null);
                        Assert.That(toClient.CertificateEntries()[0], Is.EqualTo(key.Der));
                    });

                }

            }

        }

        [Test]
        public void TheServersKey_WhereOneSideDemandsRawKeys_AndTheOtherHasNone_TheHandshakeEnds()
        {

            // The client takes a raw public key alone; the server never shows its key raw.
            using (var key = Key(RawPublicKeyMode.Off))
            {

                var       toClient = new Wire();
                using var client   = new TlsClientHandshake("localhost", TransportParameters,
                                                            certificateValidation: PinnedTo(key, RawPublicKeyMode.Require));
                using var server   = new TlsServerHandshake(key, TransportParameters);

                var e = Assert.Throws<TlsHandshakeException>(() => Run(client, server, ToClient: toClient));

                Assert.That(e!.Alert,                    Is.EqualTo(TlsAlert.UnsupportedCertificate));
                Assert.That(client.IsComplete,           Is.False);
                Assert.That(toClient.Messages,           Is.Empty, "The server answered a ClientHello it has no certificate type for.");

            }

            // The server shows its key raw alone; the clients do not take raw public keys.
            using (var key = Key(RawPublicKeyMode.Require, "localhost"))
            {

                foreach (var validation in new[] {
                                               PinnedTo(key, RawPublicKeyMode.Off),
                                               new CertificateValidationOptions { CustomTrustRoots = [ key.Certificate ] }
                                           })
                {

                    var       toClient = new Wire();
                    using var client   = new TlsClientHandshake("localhost", TransportParameters, certificateValidation: validation);
                    using var server   = new TlsServerHandshake(key, TransportParameters);

                    var e = Assert.Throws<TlsHandshakeException>(() => Run(client, server, ToClient: toClient));

                    Assert.That(e!.Alert,                 Is.EqualTo(TlsAlert.UnsupportedCertificate));
                    Assert.That(client.IsComplete,        Is.False);
                    Assert.That(toClient.Contains(key.Der), Is.False, "The certificate of a key that is shown raw alone was sent.");

                }

            }

        }

        [Test]
        public void TheServersKey_WithAnotherPin_IsRefused()
        {

            using var key   = Key(RawPublicKeyMode.Prefer);
            using var other = Key(RawPublicKeyMode.Prefer);
            var       told  = new Told();

            using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                      certificateValidation: PinnedTo(other, RawPublicKeyMode.Require, told));
            using var server = new TlsServerHandshake(key, TransportParameters);

            var e = Assert.Throws<CertificateValidationException>(() => Run(client, server));

            Assert.Multiple(() => {
                Assert.That(e!.Message,                Does.Contain(key.KeyPin.ToHex()));
                Assert.That(e.Alert,                   Is.EqualTo(TlsAlert.BadCertificate));
                Assert.That(client.IsComplete,         Is.False);
                Assert.That(client.ServerKey,          Is.Null);
                Assert.That(told.Outcomes,             Is.EqualTo(new[] { KeyPinOutcome.Refused }));
            });

        }

        [Test]
        public void TheServersKey_ThePinnedKey_WithoutItsPrivateKey_IsRefused_AndNothingIsLearned()
        {

            // An impostor knows the public key - everybody may - and signs with a key of its own.
            using var pinned   = Key(RawPublicKeyMode.Prefer);
            using var impostor = Key(RawPublicKeyMode.Prefer);

            var theirKeyInTheirPlace = Replace(HandshakeType.Certificate,
                                               _ => CertificateMessage.BuildRawPublicKey(pinned.SubjectPublicKeyInfo));

            foreach (var policy in new[] {
                                       new KeyPinPolicy([ pinned.KeyPin ], RawPublicKeys: RawPublicKeyMode.Require),
                                       KeyPinPolicy.LearnOnFirstUse(RawPublicKeyMode.Require)
                                   })
            {

                var       told   = new Told();
                using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                          certificateValidation: CertificateValidationOptions.PinnedTo(policy, told.Add));
                using var server = new TlsServerHandshake(impostor, TransportParameters);

                var e = Assert.Throws<CertificateValidationException>(() => Run(client, server, RewriteToClient: theirKeyInTheirPlace));

                Assert.Multiple(() => {
                    Assert.That(e!.Message,        Does.Contain("CertificateVerify"));
                    Assert.That(client.IsComplete, Is.False);
                    Assert.That(client.ServerKey,  Is.Null);
                    Assert.That(told.Outcomes,     Is.Empty, "A key that was not proven was accepted or learned.");
                });

            }

        }

        [Test]
        public void TheServersKey_ThatIsNotOneKey_IsRefused()
        {

            using var key   = Key(RawPublicKeyMode.Prefer);
            using var other = Key(RawPublicKeyMode.Prefer);

            Byte[] twoEntries()
            {

                // Two entries of a certificate chain's layout, each a SubjectPublicKeyInfo.
                var one = CertificateMessage.Build(key.SubjectPublicKeyInfo, [ other.SubjectPublicKeyInfo ]);

                Assert.That(CertificateMessage.TryParseWithContext(one.AsSpan(4), out var entries, out _), Is.True);
                Assert.That(entries, Has.Count.EqualTo(2));

                return one;

            }

            var cases = new (String Name, Func<Byte[]> Certificate, String Says)[] {
                            ("two entries",             twoEntries,                                                                                     "2 entries"),
                            ("trailing data",           () => CertificateMessage.BuildRawPublicKey([ .. key.SubjectPublicKeyInfo, 0x00 ]),              "trailing data"),
                            ("a certificate",           () => CertificateMessage.Build(key.Der),                                                        "SubjectPublicKeyInfo"),
                            ("a point off the curve",   () => CertificateMessage.BuildRawPublicKey(OffTheCurve(key.SubjectPublicKeyInfo)),              "not on the curve"),
                            ("nothing of a key",        () => CertificateMessage.BuildRawPublicKey([ 0x30, 0x00 ]),                                     "SubjectPublicKeyInfo")
                        };

            foreach (var (name, certificate, says) in cases)
            {

                var       told   = new Told();
                using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                          certificateValidation: CertificateValidationOptions.PinnedTo(
                                                                                     KeyPinPolicy.LearnOnFirstUse(RawPublicKeyMode.Require),
                                                                                     told.Add
                                                                                 ));
                using var server = new TlsServerHandshake(key, TransportParameters);

                var e = Assert.Throws<CertificateValidationException>(
                            () => Run(client, server, RewriteToClient: Replace(HandshakeType.Certificate, _ => certificate())),
                            name
                        );

                Assert.Multiple(() => {
                    Assert.That(e!.Message,                                     Does.Contain(says), name);
                    Assert.That(client.IsComplete,                              Is.False,           name);
                    Assert.That(told.Outcomes.Contains(KeyPinOutcome.Learned),  Is.False,           name);
                    Assert.That(told.Outcomes.Contains(KeyPinOutcome.Accepted), Is.False,           name);
                });

            }

        }

        /// <summary>
        /// The given P-256 SubjectPublicKeyInfo with its point moved off the curve.
        /// </summary>
        private static Byte[] OffTheCurve(Byte[] SubjectPublicKeyInfo)
        {
            var moved = SubjectPublicKeyInfo.ToArray();
            moved[^1] ^= 0x01;
            return moved;
        }

        [Test]
        public void TheServersKey_ARawKeyWhereACertificateWasNegotiated_IsRefused()
        {

            using var key = Key(RawPublicKeyMode.Off, "localhost");

            // Nothing was offered, so X.509 it is - and a SubjectPublicKeyInfo is no certificate.
            foreach (var validation in new[] {
                                           PinnedTo(key, RawPublicKeyMode.Off),
                                           new CertificateValidationOptions { CustomTrustRoots = [ key.Certificate ] }
                                       })
            {

                using var client = new TlsClientHandshake("localhost", TransportParameters, certificateValidation: validation);
                using var server = new TlsServerHandshake(key, TransportParameters);

                var e = Assert.Throws<CertificateValidationException>(
                            () => Run(client, server,
                                      RewriteToClient: Replace(HandshakeType.Certificate,
                                                               _ => CertificateMessage.BuildRawPublicKey(key.SubjectPublicKeyInfo)))
                        );

                Assert.That(e!.Message,        Does.Contain("cannot be read"));
                Assert.That(client.IsComplete, Is.False);

            }

        }

        [Test]
        public void TheServersChoice_OfACertificateTypeThatWasNotOffered_IsRefused()
        {

            using var key = Key(RawPublicKeyMode.Off, "localhost");

            // Nothing offered at all: server_certificate_type has no business in EncryptedExtensions.
            foreach (var validation in new[] {
                                           PinnedTo(key, RawPublicKeyMode.Off),
                                           new CertificateValidationOptions { CustomTrustRoots = [ key.Certificate ] }
                                       })
            {

                foreach (var extension in new[] { ExtensionType.ServerCertificateType, ExtensionType.ClientCertificateType })
                {

                    using var client = new TlsClientHandshake("localhost", TransportParameters, certificateValidation: validation);
                    using var server = new TlsServerHandshake(key, TransportParameters);

                    var e = Assert.Throws<TlsHandshakeException>(
                                () => Run(client, server,
                                          RewriteToClient: Replace(HandshakeType.EncryptedExtensions,
                                                                   message => WithExtension(message, extension, (Byte) TLSCertificateType.RawPublicKey)))
                            );

                    Assert.That(e!.Alert,          Is.EqualTo(TlsAlert.UnsupportedExtension), extension.ToString());
                    Assert.That(client.IsComplete, Is.False,                                  extension.ToString());

                }

            }

            // Offered: a raw public key, then X.509. Chosen: OpenPGP (1).
            {

                using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                          certificateValidation: PinnedTo(key, RawPublicKeyMode.Prefer));
                using var server = new TlsServerHandshake(key, TransportParameters);

                var e = Assert.Throws<TlsHandshakeException>(
                            () => Run(client, server,
                                      RewriteToClient: Replace(HandshakeType.EncryptedExtensions,
                                                               message => WithExtension(message, ExtensionType.ServerCertificateType, 0x01)))
                        );

                Assert.That(e!.Alert,          Is.EqualTo(TlsAlert.IllegalParameter));
                Assert.That(client.IsComplete, Is.False);

            }

            // Offered: a raw public key alone. Chosen: X.509.
            using (var rawKey = Key(RawPublicKeyMode.Prefer))
            {

                using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                          certificateValidation: PinnedTo(rawKey, RawPublicKeyMode.Require));
                using var server = new TlsServerHandshake(rawKey, TransportParameters);

                Byte[]? x509InPlaceOfRaw(Byte[] message)
                {

                    Byte[] chosen = [ 0x00, 0x14, 0x00, 0x01, (Byte) TLSCertificateType.RawPublicKey ];
                    var    at     = message.AsSpan().IndexOf(chosen);

                    Assert.That(at, Is.GreaterThan(0), "The server did not choose a raw public key.");

                    message[at + 4] = (Byte) TLSCertificateType.X509;

                    return message;

                }

                var e = Assert.Throws<TlsHandshakeException>(
                            () => Run(client, server, RewriteToClient: Replace(HandshakeType.EncryptedExtensions, x509InPlaceOfRaw))
                        );

                Assert.That(e!.Alert,          Is.EqualTo(TlsAlert.IllegalParameter));
                Assert.That(client.IsComplete, Is.False);

            }

        }

        [Test]
        public void TheServersChoice_IsMadeOnce()
        {

            using var key    = Key(RawPublicKeyMode.Prefer);
            using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                      certificateValidation: PinnedTo(key, RawPublicKeyMode.Prefer));
            using var server = new TlsServerHandshake(key, TransportParameters);

            Byte[]? encryptedExtensions = null;

            // The server's key came raw - and before CertificateVerify the choice is made again, for X.509.
            Byte[]? rewrite(Byte[] message)
            {

                switch ((HandshakeType) message[0])
                {

                    case HandshakeType.EncryptedExtensions:
                        encryptedExtensions = message.ToArray();
                        return message;

                    case HandshakeType.CertificateVerify:
                        Byte[] chosen = [ 0x00, 0x14, 0x00, 0x01, (Byte) TLSCertificateType.RawPublicKey ];
                        var    at     = encryptedExtensions!.AsSpan().IndexOf(chosen);
                        Assert.That(at, Is.GreaterThan(0), "The server did not choose a raw public key.");
                        encryptedExtensions[at + 4] = (Byte) TLSCertificateType.X509;
                        return [ .. encryptedExtensions, .. message ];

                    default:
                        return message;

                }

            }

            var e = Assert.Throws<TlsHandshakeException>(() => Run(client, server, RewriteToClient: rewrite));

            Assert.That(e!.Alert,                       Is.EqualTo(TlsAlert.UnexpectedMessage));
            Assert.That(client.ServerCertificateType,   Is.EqualTo(TLSCertificateType.RawPublicKey));
            Assert.That(client.IsComplete,              Is.False);

        }

        [Test]
        public void AServerThatShowsNoKeyAtAll_IsNotBelieved()
        {

            using var key = Key(RawPublicKeyMode.Prefer, "localhost");

            // Whoever answers in the server's place can do the key exchange, and so it can
            // compute a Finished that verifies. What it cannot do is sign with the server's key -
            // so it leaves Certificate and CertificateVerify out.
            foreach (var validation in new[] {
                                           PinnedTo(key, RawPublicKeyMode.Require),
                                           PinnedTo(key, RawPublicKeyMode.Off),
                                           new CertificateValidationOptions { CustomTrustRoots = [ key.Certificate ] }
                                       })
            {

                using var client = new TlsClientHandshake("localhost", TransportParameters, certificateValidation: validation);
                using var server = new TlsServerHandshake(key, TransportParameters);

                client.Start();

                Assert.That(client.TryGetOutgoingCrypto(out var level, out var clientHello), Is.True);
                server.ProvideCrypto(level, clientHello);

                using var transcript = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                transcript.AppendData(clientHello);

                var e = Assert.Throws<TlsHandshakeException>(() => {

                    while (server.TryGetOutgoingCrypto(out var serverLevel, out var message))
                    {

                        switch ((HandshakeType) message[0])
                        {

                            case HandshakeType.ServerHello:
                            case HandshakeType.EncryptedExtensions:
                                transcript.AppendData(message);
                                client.ProvideCrypto(serverLevel, message);
                                break;

                            case HandshakeType.Finished:
                                Assert.That(server.NegotiatedCipherSuite, Is.EqualTo(CipherSuite.Aes128GcmSha256));
                                var verifyData = new KeySchedule(CipherSuite.Aes128GcmSha256).FinishedVerifyData(
                                                     server.HandshakeSecrets!.ServerHandshakeTrafficSecret,
                                                     transcript.GetCurrentHash()
                                                 );
                                client.ProvideCrypto(serverLevel, Finished.BuildMessage(verifyData));
                                break;

                            // Certificate and CertificateVerify: left out.

                        }

                    }

                });

                Assert.Multiple(() => {
                    Assert.That(e!.Alert,                       Is.EqualTo(TlsAlert.UnexpectedMessage));
                    Assert.That(client.ServerFinishedValid,     Is.True,  "The Finished of this test does not verify: it shows nothing.");
                    Assert.That(client.ServerCertificateValid,  Is.False);
                    Assert.That(client.IsComplete,              Is.False, "A server that showed no key was believed.");
                    Assert.That(client.ApplicationSecrets,      Is.Null);
                    Assert.That(client.TryGetOutgoingCrypto(out _, out _), Is.False, "The client went on to its own Finished.");
                });

            }

        }

        [Test]
        public void AServerWhoseFinishedDoesNotVerify_IsNotBelieved()
        {

            using var key    = Key(RawPublicKeyMode.Prefer);
            using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                      certificateValidation: PinnedTo(key, RawPublicKeyMode.Require));
            using var server = new TlsServerHandshake(key, TransportParameters);

            Byte[]? flipped(Byte[] message)
            {
                message[^1] ^= 0x01;
                return message;
            }

            var e = Assert.Throws<TlsHandshakeException>(() => Run(client, server, RewriteToClient: Replace(HandshakeType.Finished, flipped)));

            Assert.That(e!.Alert,          Is.EqualTo(TlsAlert.DecryptError));
            Assert.That(client.IsComplete, Is.False);

        }

        #endregion

        #region The client's key

        [Test]
        public void TheClientsKey_ComesRaw_AndIsAdmittedByItsPin([Values(RawPublicKeyMode.Prefer, RawPublicKeyMode.Require)] RawPublicKeyMode ClientMode,
                                                                [Values(RawPublicKeyMode.Prefer, RawPublicKeyMode.Require)] RawPublicKeyMode ServerMode)
        {

            using var key      = Key(RawPublicKeyMode.Prefer);
            using var station  = Key(ClientMode, "station.invalid");
            var       told     = new Told();
            var       toServer = new Wire();

            using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                      certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                      clientCertificate:     station);
            using var server = new TlsServerHandshake(key, TransportParameters,
                                                      clientCertificate: ClientsPinnedTo(station, ServerMode, told));

            RunToCompletion(client, server, ToServer: toServer);

            var entries        = toServer.CertificateEntries();
            var authentication = server.ClientAuthentication;

            Assert.Multiple(() => {

                Assert.That(client.ClientCertificateType,       Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(server.ClientCertificateType,       Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(client.ClientCertificateSent,       Is.True);

                // What the server holds: a key, and no certificate - and so no subject to take for an identity.
                Assert.That(server.ClientCertificate,           Is.Null);
                Assert.That(server.ClientKey,                   Is.Not.Null);
                Assert.That(server.ClientKey!.IsRawPublicKey,   Is.True);
                Assert.That(server.ClientKey!.KeyPin,           Is.EqualTo(station.KeyPin));

                Assert.That(authentication.Requested,           Is.True);
                Assert.That(authentication.IsAuthenticated,     Is.True);
                Assert.That(authentication.Certificate,         Is.Null);
                Assert.That(authentication.Key,                 Is.EqualTo(server.ClientKey));
                Assert.That(authentication.FailureReason,       Is.Null);

                Assert.That(entries,                            Has.Count.EqualTo(1));
                Assert.That(entries[0],                         Is.EqualTo(station.SubjectPublicKeyInfo));
                Assert.That(toServer.Contains(station.Der),     Is.False, "The certificate was sent.");

                Assert.That(told.Outcomes,                      Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                Assert.That(told.Results.Single().CertificateType, Is.EqualTo(TLSCertificateType.RawPublicKey));

            });

        }

        [Test]
        public void TheClientsKey_WithAnotherPin_IsRefused()
        {

            using var key      = Key(RawPublicKeyMode.Prefer);
            using var station  = Key(RawPublicKeyMode.Prefer, "station.invalid");
            using var stranger = Key(RawPublicKeyMode.Prefer, "station.invalid");
            var       told     = new Told();

            using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                      certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                      clientCertificate:     stranger);
            using var server = new TlsServerHandshake(key, TransportParameters,
                                                      clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Require, told));

            var e = Assert.Throws<TlsHandshakeException>(() => Run(client, server));

            Assert.Multiple(() => {
                Assert.That(e!.Alert,                               Is.EqualTo(TlsAlert.AccessDenied));
                Assert.That(e.Message,                              Does.Contain(stranger.KeyPin.ToHex()));
                Assert.That(server.IsComplete,                      Is.False);
                Assert.That(server.ClientKey,                       Is.Null);
                Assert.That(server.ClientAuthentication.IsAuthenticated, Is.False);
                Assert.That(told.Outcomes,                          Is.EqualTo(new[] { KeyPinOutcome.Refused }));
            });

        }

        [Test]
        public void TheClientsKey_UnderRequest_ARefusedKeyIsToldAndNotAuthenticated()
        {

            using var key      = Key(RawPublicKeyMode.Prefer);
            using var station  = Key(RawPublicKeyMode.Prefer, "station.invalid");
            using var stranger = Key(RawPublicKeyMode.Prefer, "station.invalid");

            using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                      certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                      clientCertificate:     stranger);
            using var server = new TlsServerHandshake(key, TransportParameters,
                                                      clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Require, Mode: ClientCertificateMode.Request));

            RunToCompletion(client, server);

            var authentication = server.ClientAuthentication;

            Assert.Multiple(() => {
                Assert.That(authentication.Requested,       Is.True);
                Assert.That(authentication.IsAuthenticated, Is.False);
                Assert.That(authentication.Key,             Is.Null);
                Assert.That(authentication.FailureReason,   Does.Contain(stranger.KeyPin.ToHex()));
            });

        }

        [Test]
        public void TheClientsKey_FallsBackToItsCertificate_WhereTheOtherSideOffersNoRawKeys()
        {

            using var key     = Key(RawPublicKeyMode.Prefer);
            using var station = Key(RawPublicKeyMode.Prefer, "station.invalid");

            // A client that prefers to show its key raw, and a server that takes client keys in certificates.
            {

                var       toServer = new Wire();
                using var client   = new TlsClientHandshake("localhost", TransportParameters,
                                                            certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                            clientCertificate:     station);
                using var server   = new TlsServerHandshake(key, TransportParameters,
                                                            clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Off));

                RunToCompletion(client, server, ToServer: toServer);

                Assert.Multiple(() => {
                    Assert.That(client.ClientCertificateType,         Is.EqualTo(TLSCertificateType.X509));
                    Assert.That(server.ClientCertificate,             Is.Not.Null);
                    Assert.That(server.ClientKey!.IsRawPublicKey,     Is.False);
                    Assert.That(server.ClientKey!.KeyPin,             Is.EqualTo(station.KeyPin));
                    Assert.That(toServer.CertificateEntries()[0],     Is.EqualTo(station.Der));
                    // The mixed case: the server's key came raw all the same.
                    Assert.That(client.ServerCertificateType,         Is.EqualTo(TLSCertificateType.RawPublicKey));
                });

            }

            // A server that prefers raw client keys, and a client that shows its key in its certificate.
            station.RawPublicKeys = RawPublicKeyMode.Off;

            {

                var       toServer = new Wire();
                using var client   = new TlsClientHandshake("localhost", TransportParameters,
                                                            certificateValidation: PinnedTo(key, RawPublicKeyMode.Off),
                                                            clientCertificate:     station);
                using var server   = new TlsServerHandshake(key, TransportParameters,
                                                            clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Prefer));

                RunToCompletion(client, server, ToServer: toServer);

                Assert.Multiple(() => {
                    Assert.That(server.ClientCertificateType,         Is.EqualTo(TLSCertificateType.X509));
                    Assert.That(server.ClientCertificate,             Is.Not.Null);
                    Assert.That(server.ClientKey!.KeyPin,             Is.EqualTo(station.KeyPin));
                    Assert.That(toServer.CertificateEntries()[0],     Is.EqualTo(station.Der));
                    Assert.That(client.ServerCertificateType,         Is.EqualTo(TLSCertificateType.X509));
                });

            }

        }

        [Test]
        public void TheClientsKey_WhereTheServerDemandsRawKeys_AndTheClientOffersNone_TheHandshakeEnds()
        {

            using var key     = Key(RawPublicKeyMode.Prefer);
            using var station = Key(RawPublicKeyMode.Off, "station.invalid");

            // A client that has to authenticate, and cannot: unsupported_certificate, before anything is asked.
            {

                var       toClient = new Wire();
                using var client   = new TlsClientHandshake("localhost", TransportParameters,
                                                            certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                            clientCertificate:     station);
                using var server   = new TlsServerHandshake(key, TransportParameters,
                                                            clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Require));

                var e = Assert.Throws<TlsHandshakeException>(() => Run(client, server, ToClient: toClient));

                Assert.That(e!.Alert,          Is.EqualTo(TlsAlert.UnsupportedCertificate));
                Assert.That(toClient.Messages, Is.Empty);
                Assert.That(server.IsComplete, Is.False);

            }

            // A client that only may: it is asked, shows its certificate, and that is refused - the right key, in the wrong type.
            {

                var       told   = new Told();
                using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                          certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                          clientCertificate:     station);
                using var server = new TlsServerHandshake(key, TransportParameters,
                                                          clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Require, told, ClientCertificateMode.Request));

                RunToCompletion(client, server);

                Assert.That(server.ClientAuthentication.IsAuthenticated, Is.False);
                Assert.That(server.ClientAuthentication.FailureReason,   Does.Contain("requires a raw public key"));
                Assert.That(told.Outcomes,                               Is.EqualTo(new[] { KeyPinOutcome.Refused }));

            }

        }

        [Test]
        public void TheClientsKey_ThatIsShownRawAlone_IsNotShownInItsCertificate()
        {

            using var key     = Key(RawPublicKeyMode.Prefer);
            using var station = Key(RawPublicKeyMode.Require, "station.invalid");

            // A server that takes client keys in certificates and insists on one.
            {

                using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                          certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                          clientCertificate:     station);
                using var server = new TlsServerHandshake(key, TransportParameters,
                                                          clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Off));

                var e = Assert.Throws<TlsHandshakeException>(() => Run(client, server));

                Assert.That(e!.Alert, Is.EqualTo(TlsAlert.UnsupportedCertificate));

            }

            // One that only asks: the client declines with an empty Certificate, as without a key.
            {

                var       toServer = new Wire();
                using var client   = new TlsClientHandshake("localhost", TransportParameters,
                                                            certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                            clientCertificate:     station);
                using var server   = new TlsServerHandshake(key, TransportParameters,
                                                            clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Off, Mode: ClientCertificateMode.Request));

                RunToCompletion(client, server, ToServer: toServer);

                Assert.Multiple(() => {
                    Assert.That(client.ClientCertificateRequested,           Is.True);
                    Assert.That(client.ClientCertificateSent,                Is.False);
                    Assert.That(toServer.CertificateEntries(),               Is.Empty);
                    Assert.That(toServer.Contains(station.Der),              Is.False, "The certificate was sent.");
                    Assert.That(toServer.OfType(HandshakeType.CertificateVerify), Is.Empty);
                    Assert.That(server.ClientAuthentication.IsAuthenticated, Is.False);
                });

            }

        }

        [Test]
        public void TheClientsKey_OfAnotherTypeThanTheServerChose_IsRefused()
        {

            using var key     = Key(RawPublicKeyMode.Prefer);
            using var station = Key(RawPublicKeyMode.Prefer, "station.invalid");

            // Chosen: a raw public key. Sent: the certificate.
            {

                var       told   = new Told();
                using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                          certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                          clientCertificate:     station);
                using var server = new TlsServerHandshake(key, TransportParameters,
                                                          clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Require, told));

                var e = Assert.Throws<TlsHandshakeException>(
                            () => Run(client, server,
                                      RewriteToServer: Replace(HandshakeType.Certificate, _ => CertificateMessage.Build(station.Der)))
                        );

                Assert.Multiple(() => {
                    Assert.That(e!.Alert,          Is.EqualTo(TlsAlert.BadCertificate));
                    Assert.That(server.IsComplete, Is.False);
                    Assert.That(server.ClientKey,  Is.Null);
                    Assert.That(told.Outcomes,     Is.EqualTo(new[] { KeyPinOutcome.Refused }));
                });

            }

            // Chosen: X.509. Sent: the raw public key.
            station.RawPublicKeys = RawPublicKeyMode.Off;

            {

                using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                          certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                          clientCertificate:     station);
                using var server = new TlsServerHandshake(key, TransportParameters,
                                                          clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Off));

                var e = Assert.Throws<TlsHandshakeException>(
                            () => Run(client, server,
                                      RewriteToServer: Replace(HandshakeType.Certificate, _ => CertificateMessage.BuildRawPublicKey(station.SubjectPublicKeyInfo)))
                        );

                Assert.Multiple(() => {
                    Assert.That(e!.Alert,          Is.EqualTo(TlsAlert.BadCertificate));
                    Assert.That(e.Message,         Does.Contain("cannot be read"));
                    Assert.That(server.IsComplete, Is.False);
                    Assert.That(server.ClientKey,  Is.Null);
                });

            }

            // Chosen: a raw public key. Sent: two of them.
            station.RawPublicKeys = RawPublicKeyMode.Prefer;

            {

                using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                          certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                          clientCertificate:     station);
                using var server = new TlsServerHandshake(key, TransportParameters,
                                                          clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Require));

                var e = Assert.Throws<TlsHandshakeException>(
                            () => Run(client, server,
                                      RewriteToServer: Replace(HandshakeType.Certificate,
                                                               _ => CertificateMessage.Build(station.SubjectPublicKeyInfo, [ station.SubjectPublicKeyInfo ])))
                        );

                Assert.That(e!.Alert,          Is.EqualTo(TlsAlert.BadCertificate));
                Assert.That(server.IsComplete, Is.False);

            }

        }

        [Test]
        public void TheClientsKey_SignedWithASchemeThatWasNotOffered_IsRefused()
        {

            using var key     = Key(RawPublicKeyMode.Prefer);
            using var station = Key(RawPublicKeyMode.Prefer, "station.invalid");

            using var client = new TlsClientHandshake("localhost", TransportParameters,
                                                      certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                      clientCertificate:     station);
            using var server = new TlsServerHandshake(key, TransportParameters,
                                                      clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Require)
                                                                             with { SignatureSchemes = [ SignatureScheme.Ed25519 ] });

            var e = Assert.Throws<TlsHandshakeException>(() => Run(client, server));

            Assert.That(e!.Alert,          Is.EqualTo(TlsAlert.IllegalParameter));
            Assert.That(server.ClientKey,  Is.Null);
            Assert.That(server.IsComplete, Is.False);

        }

        #endregion

        #region Resumption

        private static ResumptionTicket TicketOf(TlsClientHandshake Client, TlsServerHandshake Server)
        {

            // The NewSessionTicket arrives after the handshake.
            Pump(Server, Client);

            Assert.That(Client.NewSessionTickets, Is.Not.Empty);

            return Client.NewSessionTickets[0];

        }

        [Test]
        public void AResumption_IsOffered_WhileTheServersPinIsAmongThePins_AndNotAfterwards()
        {

            using var key   = Key(RawPublicKeyMode.Prefer);
            using var other = Key(RawPublicKeyMode.Prefer);
            var       cache = new ServerResumptionCache();

            ResumptionTicket ticket1, ticket2;

            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       certificateValidation: PinnedTo(key, RawPublicKeyMode.Require)))
            using (var server = new TlsServerHandshake(key, TransportParameters, resumptionCache: cache))
            {
                RunToCompletion(client, server);
                ticket1 = TicketOf(client, server);
                Assert.That(ticket1.ServerKeyPin, Is.EqualTo(key.KeyPin));
            }

            // The pin still stands: resumed, without a key being shown, and the pin is known all the same.
            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                       resumptionTicket:      ticket1))
            using (var server = new TlsServerHandshake(key, TransportParameters, resumptionCache: cache))
            {

                RunToCompletion(client, server);

                Assert.That(client.ResumptionAccepted,  Is.True);
                Assert.That(client.ServerKey,           Is.Null);
                Assert.That(client.ServerKeyPin,        Is.EqualTo(key.KeyPin));

                ticket2 = TicketOf(client, server);
                Assert.That(ticket2.ServerKeyPin, Is.EqualTo(key.KeyPin), "A ticket of a resumed handshake has forgotten its key.");

            }

            // The pin was removed: the ticket is not offered, the handshake is a full one, and the key is refused.
            var told = new Told();

            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       certificateValidation: PinnedTo(other, RawPublicKeyMode.Require, told),
                                                       resumptionTicket:      ticket2))
            using (var server = new TlsServerHandshake(key, TransportParameters, resumptionCache: cache))
            {

                Assert.Throws<CertificateValidationException>(() => Run(client, server));

                Assert.That(client.ResumptionAccepted, Is.False);
                Assert.That(server.ResumptionAccepted, Is.False, "A ticket of a server whose pin was removed was offered.");
                Assert.That(client.IsComplete,         Is.False);
                Assert.That(told.Outcomes,             Is.EqualTo(new[] { KeyPinOutcome.Refused }));

            }

        }

        [Test]
        public void ATicketOfUnknownOrigin_IsNotOfferedAtAKeyPinnedEndpoint()
        {

            using var key   = Key(RawPublicKeyMode.Prefer, "localhost");
            var       cache = new ServerResumptionCache();

            ResumptionTicket ticket;

            // A ticket of a CA-checked handshake remembers the pin of the key it saw ...
            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       certificateValidation: new CertificateValidationOptions { CustomTrustRoots = [ key.Certificate ] }))
            using (var server = new TlsServerHandshake(key, TransportParameters, resumptionCache: cache))
            {
                RunToCompletion(client, server);
                ticket = TicketOf(client, server);
            }

            // ... and one that does not, as a ticket made elsewhere, is not offered where keys are pinned.
            var unknown = new ResumptionTicket(ticket.Psk, ticket.Identity, ticket.AgeAdd, ticket.CipherSuite, ticket.ServerName,
                                               ticket.LifetimeSeconds, ticket.MaxEarlyDataSize, ticket.PeerTransportParameters, ticket.ReceivedAt);

            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       certificateValidation: PinnedTo(key, RawPublicKeyMode.Prefer),
                                                       resumptionTicket:      unknown))
            using (var server = new TlsServerHandshake(key, TransportParameters, resumptionCache: cache))
            {

                RunToCompletion(client, server);

                Assert.That(client.ResumptionAccepted,         Is.False);
                Assert.That(client.ServerKey?.IsRawPublicKey,  Is.True, "The handshake was not a full one.");

            }

        }

        [Test]
        public void AResumption_IsTaken_WhileTheClientsPinIsAmongThePins_AndNotAfterwards()
        {

            using var key      = Key(RawPublicKeyMode.Prefer);
            using var station  = Key(RawPublicKeyMode.Prefer, "station.invalid");
            using var another  = Key(RawPublicKeyMode.Prefer, "station.invalid");
            var       cache    = new ServerResumptionCache();

            ResumptionTicket ticket1, ticket2;

            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                       clientCertificate:     station))
            using (var server = new TlsServerHandshake(key, TransportParameters, resumptionCache: cache,
                                                       clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Require)))
            {
                RunToCompletion(client, server);
                ticket1 = TicketOf(client, server);
            }

            // The station's pin still stands: resumed, and the server knows whose ticket it is.
            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                       clientCertificate:     station,
                                                       resumptionTicket:      ticket1))
            using (var server = new TlsServerHandshake(key, TransportParameters, resumptionCache: cache,
                                                       clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Require)))
            {

                RunToCompletion(client, server);

                var authentication = server.ClientAuthentication;

                Assert.Multiple(() => {
                    Assert.That(server.ResumptionAccepted,       Is.True);
                    Assert.That(client.ClientCertificateRequested, Is.False);
                    Assert.That(authentication.IsAuthenticated,  Is.True);
                    Assert.That(authentication.Key?.KeyPin,      Is.EqualTo(station.KeyPin));
                    Assert.That(authentication.Key?.IsRawPublicKey, Is.True);
                });

                ticket2 = TicketOf(client, server);

            }

            // The station's pin was removed: its ticket is not taken, the handshake is a full one, and its key is refused.
            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                       clientCertificate:     station,
                                                       resumptionTicket:      ticket2))
            using (var server = new TlsServerHandshake(key, TransportParameters, resumptionCache: cache,
                                                       clientCertificate: ClientsPinnedTo(another, RawPublicKeyMode.Require)))
            {

                var e = Assert.Throws<TlsHandshakeException>(() => Run(client, server));

                Assert.Multiple(() => {
                    Assert.That(e!.Alert,                  Is.EqualTo(TlsAlert.AccessDenied));
                    Assert.That(server.ResumptionAccepted, Is.False, "A ticket of a client whose pin was removed was taken.");
                    Assert.That(server.IsComplete,         Is.False);
                    Assert.That(server.ClientAuthentication.IsAuthenticated, Is.False);
                });

            }

        }

        [Test]
        public void AResumption_IsNotTaken_FromAClientThatWasNotAskedForItsKeyThen()
        {

            using var key     = Key(RawPublicKeyMode.Prefer);
            using var station = Key(RawPublicKeyMode.Prefer, "station.invalid");
            var       cache   = new ServerResumptionCache();

            ResumptionTicket ticket;

            // A ticket of a handshake in which no client key was asked for ...
            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                       clientCertificate:     station))
            using (var server = new TlsServerHandshake(key, TransportParameters, resumptionCache: cache))
            {
                RunToCompletion(client, server);
                ticket = TicketOf(client, server);
            }

            // ... does not get its holder past a server that asks for one now.
            using (var client = new TlsClientHandshake("localhost", TransportParameters,
                                                       certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                       clientCertificate:     station,
                                                       resumptionTicket:      ticket))
            using (var server = new TlsServerHandshake(key, TransportParameters, resumptionCache: cache,
                                                       clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Require)))
            {

                RunToCompletion(client, server);

                Assert.Multiple(() => {
                    Assert.That(server.ResumptionAccepted,          Is.False);
                    Assert.That(client.ClientCertificateRequested,  Is.True);
                    Assert.That(server.ClientAuthentication.Requested, Is.True);
                    Assert.That(server.ClientKey?.KeyPin,           Is.EqualTo(station.KeyPin));
                });

            }

        }

        #endregion

        #region HTTP/3

        private static Http3Response? Get(Http3ClientConnection Client, Http3ServerConnection Server)
        {

            Client.Start();

            UInt64         requestStream = 0;
            var            requestSent   = false;
            Http3Response? response      = null;

            for (var round = 0; round < 20 && response is null; round++)
            {

                Client.CheckTimeouts();

                foreach (var datagram in Client.GetDatagramsToSend())
                    Server.ProcessDatagram(datagram);

                foreach (var datagram in Server.GetDatagramsToSend())
                    Client.ProcessDatagram(datagram);

                if (Client.HandshakeConfirmed && !requestSent)
                {
                    Client.InitializeHttp3();
                    requestStream = Client.SendRequest(Http3Request.Get("localhost", "/"));
                    requestSent   = true;
                }

                if (requestSent)
                    Client.TryGetResponse(requestStream, out response);

            }

            return response;

        }

        private static Http3Response Hello(Http3Request Request)

            => new() {
                   Status  = 200,
                   Body    = Encoding.UTF8.GetBytes("raw")
               };

        [Test]
        public void HTTP3_GetsAnAnswer_WithRawPublicKeysInBothDirections()
        {

            using var key     = Key(RawPublicKeyMode.Require);
            using var station = Key(RawPublicKeyMode.Require, "station.invalid");

            var toldClient = new Told();
            var toldServer = new Told();

            using var server = new Http3ServerConnection(key, Hello,
                                                         clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Require, toldServer));
            using var client = new Http3ClientConnection("localhost",
                                                         certificateValidation: PinnedTo(key, RawPublicKeyMode.Require, toldClient),
                                                         clientCertificate:     station);

            var response       = Get(client, server);
            var authentication = server.ClientAuthentication;

            Assert.Multiple(() => {

                Assert.That(response,                            Is.Not.Null);
                Assert.That(response?.BodyText,                  Is.EqualTo("raw"));

                Assert.That(client.ServerCertificateType,        Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(client.ClientCertificateType,        Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(server.ServerCertificateType,        Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(server.ClientCertificateType,        Is.EqualTo(TLSCertificateType.RawPublicKey));

                Assert.That(client.ServerKey?.IsRawPublicKey,    Is.True);
                Assert.That(client.ServerKey?.KeyPin,            Is.EqualTo(key.KeyPin));

                Assert.That(authentication.IsAuthenticated,      Is.True);
                Assert.That(authentication.Certificate,          Is.Null);
                Assert.That(authentication.Key?.IsRawPublicKey,  Is.True);
                Assert.That(authentication.Key?.KeyPin,          Is.EqualTo(station.KeyPin));

                Assert.That(toldClient.Outcomes,                 Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                Assert.That(toldServer.Outcomes,                 Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                Assert.That(toldClient.Results.Single().CertificateType, Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(toldServer.Results.Single().CertificateType, Is.EqualTo(TLSCertificateType.RawPublicKey));

            });

        }

        [Test]
        public void HTTP3_FallsBackToCertificates_WithAClientThatOffersNoRawKeys()
        {

            using var key     = Key(RawPublicKeyMode.Prefer);
            using var station = Key(RawPublicKeyMode.Off, "station.invalid");

            using var server = new Http3ServerConnection(key, Hello,
                                                         clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Prefer));
            using var client = new Http3ClientConnection("localhost",
                                                         certificateValidation: PinnedTo(key, RawPublicKeyMode.Off),
                                                         clientCertificate:     station);

            var response = Get(client, server);

            Assert.Multiple(() => {
                Assert.That(response?.BodyText,                           Is.EqualTo("raw"));
                Assert.That(client.ServerCertificateType,                 Is.EqualTo(TLSCertificateType.X509));
                Assert.That(server.ClientCertificateType,                 Is.EqualTo(TLSCertificateType.X509));
                Assert.That(client.ServerKey?.KeyPin,                     Is.EqualTo(key.KeyPin));
                Assert.That(server.ClientAuthentication.Certificate,      Is.Not.Null);
                Assert.That(server.ClientAuthentication.Key?.KeyPin,      Is.EqualTo(station.KeyPin));
            });

        }

        [Test]
        public void HTTP3_GetsNoAnswer_WhereRawKeysAreDemandedAndNotOffered()
        {

            // The client takes a raw public key alone, and the server shows a certificate alone.
            using (var key = Key(RawPublicKeyMode.Off))
            {

                using var server = new Http3ServerConnection(key, Hello);
                using var client = new Http3ClientConnection("localhost",
                                                             certificateValidation: PinnedTo(key, RawPublicKeyMode.Require));

                Assert.That(Get(client, server),         Is.Null);
                Assert.That(client.HandshakeConfirmed,   Is.False);
                Assert.That(client.PeerCloseFrame?.ErrorCode,
                            Is.EqualTo((UInt64) Quic.TransportError.CryptoErrorBase + (UInt64) TlsAlert.UnsupportedCertificate));

            }

            // The server shows its key raw alone, and the client does not take raw public keys.
            using (var key = Key(RawPublicKeyMode.Require))
            {

                using var server = new Http3ServerConnection(key, Hello);
                using var client = new Http3ClientConnection("localhost",
                                                             certificateValidation: PinnedTo(key, RawPublicKeyMode.Off));

                Assert.That(Get(client, server),         Is.Null);
                Assert.That(client.HandshakeConfirmed,   Is.False);
                Assert.That(client.PeerCloseFrame?.ErrorCode,
                            Is.EqualTo((UInt64) Quic.TransportError.CryptoErrorBase + (UInt64) TlsAlert.UnsupportedCertificate));

            }

        }

        [Test]
        public void HTTP3_GetsNoAnswer_FromAKeyThatIsNotPinned_AndGivesNoneToOne()
        {

            using var key      = Key(RawPublicKeyMode.Require);
            using var other    = Key(RawPublicKeyMode.Require);
            using var station  = Key(RawPublicKeyMode.Require, "station.invalid");
            using var stranger = Key(RawPublicKeyMode.Require, "station.invalid");

            // The server's key is not the pinned one: bad_certificate, and no request is sent.
            {

                var served = 0;

                using var server = new Http3ServerConnection(key, request => { served++; return Hello(request); });
                using var client = new Http3ClientConnection("localhost",
                                                             certificateValidation: PinnedTo(other, RawPublicKeyMode.Require));

                Assert.That(Get(client, server),        Is.Null);
                Assert.That(served,                     Is.Zero);
                Assert.That(client.HandshakeConfirmed,  Is.False);
                Assert.That(server.PeerCloseFrame?.ErrorCode,
                            Is.EqualTo((UInt64) Quic.TransportError.CryptoErrorBase + (UInt64) TlsAlert.BadCertificate));

            }

            // The client's key is not a pinned one: access_denied, and no request is answered.
            {

                var served = 0;

                using var server = new Http3ServerConnection(key, request => { served++; return Hello(request); },
                                                             clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Require));
                using var client = new Http3ClientConnection("localhost",
                                                             certificateValidation: PinnedTo(key, RawPublicKeyMode.Require),
                                                             clientCertificate:     stranger);

                Assert.That(Get(client, server),        Is.Null);
                Assert.That(served,                     Is.Zero);
                Assert.That(server.HandshakeComplete,   Is.False);
                Assert.That(client.PeerCloseFrame?.ErrorCode,
                            Is.EqualTo((UInt64) Quic.TransportError.CryptoErrorBase + (UInt64) TlsAlert.AccessDenied));

            }

        }

        [Test]
        public async Task HTTP3_OverSockets_WithRawPublicKeysInBothDirections()
        {

            using var key     = Key(RawPublicKeyMode.Require);
            using var station = Key(RawPublicKeyMode.Require, "station.invalid");

            var toldServer = new Told();

            await using var server = new Http3Server(key,
                                                     (request, token) => Task.FromResult(Hello(request)),
                                                     port:              0,
                                                     clientCertificate: ClientsPinnedTo(station, RawPublicKeyMode.Require, toldServer));
            server.Start();

            await using (var client = new Http3Client("localhost", server.Port,
                                                      PinnedTo(key, RawPublicKeyMode.Require),
                                                      clientCertificate: station))
            {

                await client.ConnectAsync(TimeSpan.FromSeconds(10));

                var response = await client.GetAsync("/").WaitAsync(TimeSpan.FromSeconds(10));

                Assert.Multiple(() => {
                    Assert.That(response.BodyText,                              Is.EqualTo("raw"));
                    Assert.That(client.Connection.ServerCertificateType,        Is.EqualTo(TLSCertificateType.RawPublicKey));
                    Assert.That(client.Connection.ClientCertificateType,        Is.EqualTo(TLSCertificateType.RawPublicKey));
                    Assert.That(client.Connection.ServerKey?.KeyPin,            Is.EqualTo(key.KeyPin));
                    Assert.That(client.Connection.ServerKey?.Certificate,       Is.Null);
                    Assert.That(toldServer.Outcomes,                            Is.EqualTo(new[] { KeyPinOutcome.Accepted }));
                    Assert.That(toldServer.Results.Single().Pin,                Is.EqualTo(station.KeyPin));
                    Assert.That(toldServer.Results.Single().CertificateType,    Is.EqualTo(TLSCertificateType.RawPublicKey));
                });

            }

            // A client whose key is not pinned does not get in.
            using var stranger = Key(RawPublicKeyMode.Require, "station.invalid");

            await using (var client = new Http3Client("localhost", server.Port,
                                                      PinnedTo(key, RawPublicKeyMode.Require),
                                                      clientCertificate: stranger))
            {

                Assert.That(async () => {
                                await client.ConnectAsync(TimeSpan.FromSeconds(10));
                                await client.GetAsync("/").WaitAsync(TimeSpan.FromSeconds(10));
                            },
                            Throws.Exception);

                Assert.That(toldServer.Outcomes, Is.EqualTo(new[] { KeyPinOutcome.Accepted, KeyPinOutcome.Refused }));

            }

        }

        #endregion

    }

}
