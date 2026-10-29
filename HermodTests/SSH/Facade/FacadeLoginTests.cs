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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Logging in through the <see cref="SshClient"/> façade with what is not a key: a password, the
    /// answers to keyboard-interactive prompts, and a key and a password together where the server asks
    /// for both. Until the façade took them, it could only log in with keys.
    /// </summary>
    [TestFixture]
    public class FacadeLoginTests
    {

        #region (class) RecordingAuthenticator

        /// <summary>
        /// A server-side policy that writes down what it was asked, in order - so a test can tell what
        /// the client sent, and what it kept to itself.
        /// </summary>
        private sealed class RecordingAuthenticator(IReadOnlyList<String>   Offered,
                                                    Byte[]?                 AcceptedKey       = null,
                                                    String?                 AcceptedPassword  = null,
                                                    IReadOnlyList<String>?  Chain             = null) : ISshUserAuthenticator
        {

            public ConcurrentQueue<String> Asked { get; } = new();

            public IReadOnlyList<String> OfferedMethods
                => Offered;

            public ValueTask<Boolean> AuthorizePublicKeyAsync(SshPublicKeyAuthRequest Request, CancellationToken CancellationToken = default)
            {
                Asked.Enqueue(UserAuthentication.PublicKeyMethod);
                return ValueTask.FromResult(AcceptedKey is not null && Request.PublicKeyBlob.AsSpan().SequenceEqual(AcceptedKey));
            }

            public ValueTask<Boolean> AuthorizePasswordAsync(String Username, String Password, CancellationToken CancellationToken = default)
            {
                Asked.Enqueue(UserAuthentication.PasswordMethod);
                return ValueTask.FromResult(AcceptedPassword is not null && Password == AcceptedPassword);
            }

            public IReadOnlyList<String> RemainingMethods(String Username, IReadOnlySet<String> CompletedMethods)
                => Chain is null
                       ? []
                       : Chain.Where(method => !CompletedMethods.Contains(method)).ToList();

        }

        #endregion

        #region (class) TwoQuestions

        /// <summary>
        /// A keyboard-interactive factor with two prompts - the shape of a PAM stack asking for a password
        /// and then a code.
        /// </summary>
        private sealed class TwoQuestions : ISshKeyboardInteractiveFactor
        {

            public String                    Name         => "login";
            public String                    Instruction  => "Two questions.";
            public IReadOnlyList<SshPrompt>  Prompts      => [ new SshPrompt("Password: ", Echo: false), new SshPrompt("Code: ", Echo: true) ];

            public ValueTask<Boolean> ValidateAsync(IReadOnlyList<String> Responses, CancellationToken CancellationToken = default)
                => ValueTask.FromResult(Responses.Count == 2 && Responses[0] == "s3cr3t" && Responses[1] == "424242");

        }

        #endregion

        #region (static) WithServerAsync(Authenticator, Test, CancellationToken)

        private static async Task WithServerAsync(ISshUserAuthenticator                        Authenticator,
                                                  Func<UInt16, Func<Byte[], Boolean>, Task>    Test,
                                                  CancellationToken                            CancellationToken)
        {

            var hostKey = SshHostKey.GenerateEd25519();

            var server  = new SshServer(new SshServerOptions {
                              HostKeys       = [ hostKey ],
                              Authenticator  = Authenticator,
                              ExecHandler    = async (ctx, ct) => { await ctx.WriteAsync($"ran: {ctx.Command}\n", ct); return 0; }
                          });

            try
            {
                await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);
                await Test((UInt16) server.LocalEndPoint.Port.ToInt32(), blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob));
            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion


        #region Facade_LogsIn_WithAPassword

        [Test]
        [CancelAfter(25000)]
        public async Task Facade_LogsIn_WithAPassword(CancellationToken CancellationToken)
        {

            var policy = new SshAuthenticationPolicy()
                             .WithPassword((user, pw, _) => ValueTask.FromResult(user == "achim" && pw == "s3cr3t"));

            await WithServerAsync(policy, async (port, verify) => {

                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username       = "achim",
                    VerifyHostKey  = verify,
                    Password       = "s3cr3t"
                }, CancellationToken);

                var result = await client.ExecuteAsync("whoami", CancellationToken);
                Assert.That(result.StandardOutput, Is.EqualTo("ran: whoami\n"));

            }, CancellationToken);

        }

        #endregion

        #region Facade_AWrongPassword_IsAnAuthenticationError

        [Test]
        [CancelAfter(25000)]
        public async Task Facade_AWrongPassword_IsAnAuthenticationError(CancellationToken CancellationToken)
        {

            var policy = new SshAuthenticationPolicy()
                             .WithPassword((user, pw, _) => ValueTask.FromResult(pw == "s3cr3t"));

            await WithServerAsync(policy, async (port, verify) => {

                await Assert.ThrowsAsync<SshAuthenticationException>(async () =>
                    await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                        Username       = "achim",
                        VerifyHostKey  = verify,
                        Password       = "wrong"
                    }, CancellationToken));

            }, CancellationToken);

        }

        #endregion

        #region Facade_ThePassword_NeverGoesToAServerThatTakesNone

        /// <summary>
        /// A server that takes keys only is never sent the password - not even as a last try once the
        /// keys are refused. A password sent there would be a password handed to a server that had no
        /// use for it.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task Facade_ThePassword_NeverGoesToAServerThatTakesNone(CancellationToken CancellationToken)
        {

            var server = new RecordingAuthenticator(Offered:          [ UserAuthentication.PublicKeyMethod ],
                                                    AcceptedKey:      SshHostKey.GenerateEd25519().PublicKeyBlob,
                                                    AcceptedPassword: "s3cr3t");

            await WithServerAsync(server, async (port, verify) => {

                await Assert.ThrowsAsync<SshAuthenticationException>(async () =>
                    await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                        Username       = "achim",
                        VerifyHostKey  = verify,
                        Credentials    = [ SshHostKey.GenerateEd25519() ],
                        Password       = "s3cr3t"
                    }, CancellationToken));

            }, CancellationToken);

            Assert.That(server.Asked, Is.EqualTo(new[] { UserAuthentication.PublicKeyMethod }), "the key was offered, the password kept back");

        }

        #endregion

        #region Facade_AServerThatTakesOnlyPasswords_IsNotOfferedTheKeys

        /// <summary>
        /// The other way round: a server that takes passwords only is not first offered every key -
        /// each would cost an attempt of the few it allows.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task Facade_AServerThatTakesOnlyPasswords_IsNotOfferedTheKeys(CancellationToken CancellationToken)
        {

            var server = new RecordingAuthenticator(Offered:          [ UserAuthentication.PasswordMethod ],
                                                    AcceptedPassword: "s3cr3t");

            await WithServerAsync(server, async (port, verify) => {

                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username       = "achim",
                    VerifyHostKey  = verify,
                    Credentials    = [ SshHostKey.GenerateEd25519(), SshHostKey.GenerateEd25519() ],
                    Password       = "s3cr3t"
                }, CancellationToken);

                var result = await client.ExecuteAsync("whoami", CancellationToken);
                Assert.That(result.StandardOutput, Is.EqualTo("ran: whoami\n"));

            }, CancellationToken);

            Assert.That(server.Asked, Is.EqualTo(new[] { UserAuthentication.PasswordMethod }), "straight to the password");

        }

        #endregion

        #region Facade_AKeyAndAPassword_CompleteAChain

        /// <summary>
        /// OpenSSH's <c>AuthenticationMethods publickey,password</c>: the key is a partial success, and
        /// the password, which the server then asks for, completes the login.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task Facade_AKeyAndAPassword_CompleteAChain(CancellationToken CancellationToken)
        {

            var userKey = SshHostKey.GenerateEd25519();
            var server  = new RecordingAuthenticator(Offered:          [ UserAuthentication.PublicKeyMethod, UserAuthentication.PasswordMethod ],
                                                     AcceptedKey:      userKey.PublicKeyBlob,
                                                     AcceptedPassword: "s3cr3t",
                                                     Chain:            [ UserAuthentication.PublicKeyMethod, UserAuthentication.PasswordMethod ]);

            await WithServerAsync(server, async (port, verify) => {

                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username       = "achim",
                    VerifyHostKey  = verify,
                    Credentials    = [ userKey ],
                    Password       = "s3cr3t"
                }, CancellationToken);

                var result = await client.ExecuteAsync("whoami", CancellationToken);
                Assert.That(result.StandardOutput, Is.EqualTo("ran: whoami\n"));

            }, CancellationToken);

            // The key twice - the query, then the signed request - and then the password.
            Assert.That(server.Asked, Is.EqualTo(new[] { UserAuthentication.PublicKeyMethod, UserAuthentication.PublicKeyMethod, UserAuthentication.PasswordMethod }));

        }

        #endregion

        #region Facade_AKeyAlone_DoesNotCompleteAChainThatAsksForAPassword

        [Test]
        [CancelAfter(25000)]
        public async Task Facade_AKeyAlone_DoesNotCompleteAChainThatAsksForAPassword(CancellationToken CancellationToken)
        {

            var userKey = SshHostKey.GenerateEd25519();
            var server  = new RecordingAuthenticator(Offered:          [ UserAuthentication.PublicKeyMethod, UserAuthentication.PasswordMethod ],
                                                     AcceptedKey:      userKey.PublicKeyBlob,
                                                     AcceptedPassword: "s3cr3t",
                                                     Chain:            [ UserAuthentication.PublicKeyMethod, UserAuthentication.PasswordMethod ]);

            await WithServerAsync(server, async (port, verify) => {

                await Assert.ThrowsAsync<SshAuthenticationException>(async () =>
                    await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                        Username       = "achim",
                        VerifyHostKey  = verify,
                        Credentials    = [ userKey ]
                    }, CancellationToken));

            }, CancellationToken);

        }

        #endregion

        #region Facade_AnswersTheKeyboardInteractivePrompts

        [Test]
        [CancelAfter(25000)]
        public async Task Facade_AnswersTheKeyboardInteractivePrompts(CancellationToken CancellationToken)
        {

            var policy  = new SshAuthenticationPolicy()
                              .WithKeyboardInteractive(_ => new TwoQuestions());

            SshKeyboardInteractiveChallenge? asked = null;

            await WithServerAsync(policy, async (port, verify) => {

                await using var client = await SshClient.ConnectAsync("127.0.0.1", port, new SshClientOptions {
                    Username             = "achim",
                    VerifyHostKey        = verify,
                    KeyboardInteractive  = (challenge, _) => {
                                               asked = challenge;
                                               return ValueTask.FromResult(new[] { "s3cr3t", "424242" });
                                           }
                }, CancellationToken);

                var result = await client.ExecuteAsync("whoami", CancellationToken);
                Assert.That(result.StandardOutput, Is.EqualTo("ran: whoami\n"));

            }, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(asked,                                    Is.Not.Null);
                Assert.That(asked!.Name,                              Is.EqualTo("login"));
                Assert.That(asked.Instruction,                        Is.EqualTo("Two questions."));
                Assert.That(asked.Prompts.Select(prompt => prompt.Text), Is.EqualTo(new[] { "Password: ", "Code: " }));
                Assert.That(asked.Prompts.Select(prompt => prompt.Echo), Is.EqualTo(new[] { false, true }));
            });

        }

        #endregion

    }

}
