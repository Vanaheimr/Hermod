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
    /// M6 session recording, end-to-end over a loopback pipe: a recorded exec session produces a valid
    /// asciicast whose output matches the actual channel I/O and whose sidecar carries the command and exit
    /// status. Recording only wraps the (post-authentication) channel, so no credential ever reaches it.
    /// </summary>
    [TestFixture]
    public class RecordingLoopbackTests
    {

        #region RecordedExec_ProducesValidAsciicast_MatchingActualIO

        [Test]
        [CancelAfter(20000)]
        public async Task RecordedExec_ProducesValidAsciicast_MatchingActualIO(CancellationToken CancellationToken)
        {

            var (clientPipe, serverPipe) = DuplexPipe.CreateConnectedPair();
            var hostKey = Ed25519KeyPair.Generate();
            var userKey = SshHostKey.GenerateEd25519();

            var sink = new InMemoryRecordingSink();

            var server = Task.Run(async () =>
            {
                try
                {
                    using var t = await SshTransport.ServerHandshakeAsync(serverPipe, hostKey, CancellationToken: CancellationToken);
                    await UserAuthentication.ServerAuthenticateAsync(t, SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob), CancellationToken: CancellationToken);

                    var meta = new SessionRecordingMetadata {
                        Username       = "achim",
                        SessionId      = Convert.ToHexStringLower(t.SessionId),
                        AccessProfile  = "FullSftp"
                    };

                    var handle   = await sink.BeginAsync(meta, RecordingFormat.AsciicastV2, CancellationToken);
                    var recorder = new SessionRecorder(handle, meta);

                    await SshConnection.ServeCommandAsync(t, "achim", async (context, ct) =>
                    {
                        await context.WriteLineAsync("boot: ok", ct);
                        await context.WriteLineAsync("temp: 41C", ct);
                        return 3;
                    }, recorder, CancellationToken);

                    await recorder.DisposeAsync();
                }
                catch { }
            }, CancellationToken);

            using var client = await SshTransport.ClientHandshakeAsync(clientPipe, VerifyHostKey: SshHostKeyVerification.AcceptAnyUnsafe, CancellationToken: CancellationToken);
            await UserAuthentication.ClientPublicKeyAuthenticateAsync(client, "achim", userKey, CancellationToken: CancellationToken);

            await using var cmd = await SshConnection.StartCommandAsync(client, new SshCommand("status --all"), CancellationToken: CancellationToken);

            var output = await new StreamReader(cmd.StandardOutput).ReadToEndAsync(CancellationToken);
            var exit   = await cmd.WaitForExitAsync(CancellationToken);

            await server;

            var recording = sink.Recordings.Single();
            var parsed    = AsciicastReader.Parse(recording.Text);

            Assert.Multiple(() => {
                // The channel really carried this.
                Assert.That(output, Is.EqualTo("boot: ok\ntemp: 41C\n"));
                Assert.That(exit,   Is.EqualTo(3));

                // … and the recording faithfully reproduces it.
                Assert.That(parsed.Header.Command,        Is.EqualTo("status --all"));
                Assert.That(parsed.OutputText,            Is.EqualTo("boot: ok\ntemp: 41C\n"));
                Assert.That(parsed.ExitStatus,            Is.EqualTo(3));
                Assert.That(parsed.Events.Any(e => e.Code == AsciicastEventCode.Input), Is.False, "output-only recording: no input captured");

                // The sidecar ties it to who/when.
                Assert.That(recording.FinalMetadata.Username,      Is.EqualTo("achim"));
                Assert.That(recording.FinalMetadata.Command,       Is.EqualTo("status --all"));
                Assert.That(recording.FinalMetadata.ExitStatus,    Is.EqualTo(3));
                Assert.That(recording.FinalMetadata.SessionId,     Is.Not.Null.And.Not.Empty);
                Assert.That(recording.FinalMetadata.DisconnectReason, Is.EqualTo("normal"));
            });

        }

        #endregion

    }

}
