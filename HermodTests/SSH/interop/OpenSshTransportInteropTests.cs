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

using System.Diagnostics;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{


    /// <summary>
    /// Interoperability tests against the real OpenSSH client. These prove that our transport (KEX,
    /// key derivation, AES-GCM framing) matches OpenSSH byte-for-byte: the ultimate M1 acceptance test.
    /// Skipped (not failed) when no <c>ssh</c> client is available.
    /// </summary>
    [TestFixture]
    [Category("Interop")]
    [Category("Interop.OpenSSH")]
    public class OpenSshTransportInteropTests
    {

        #region (static) FindSshClients() / SshSupportsKex() / FindSshClientSupporting()

        // All candidate ssh clients, PATH first (often the newest) then the Windows-bundled one, deduplicated.
        private static IEnumerable<String> FindSshClients()
        {

            var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                foreach (var name in new[] { "ssh", "ssh.exe" })
                {
                    String? candidate = null;
                    try   { candidate = Path.Combine(dir.Trim(), name); }
                    catch { /* ignore malformed PATH entries */ }
                    if (candidate is not null && File.Exists(candidate) && seen.Add(candidate))
                        yield return candidate;
                }
            }

            var windowsOpenSsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe");
            if (File.Exists(windowsOpenSsh) && seen.Add(windowsOpenSsh))
                yield return windowsOpenSsh;

        }

        // Whether the given ssh client advertises the key exchange (via "ssh -Q kex"). Older Windows-bundled
        // OpenSSH (e.g. 9.5) lacks the post-quantum methods, so PQ interop must select a newer client.
        private static Boolean SshSupportsKex(String SshClient, String Kex)
        {
            try
            {
                using var probe = Process.Start(new ProcessStartInfo(SshClient, "-Q kex")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                })!;
                var output = probe.StandardOutput.ReadToEnd();
                probe.WaitForExit(5000);
                return output.Split('\n').Any(line => line.Trim() == Kex);
            }
            catch
            {
                return false;
            }
        }

        // The first available ssh client that supports the required key exchange (null if none).
        private static String? FindSshClientSupporting(String Kex)
        {
            foreach (var client in FindSshClients())
                if (SshSupportsKex(client, Kex))
                    return client;
            return null;
        }

        // Any available ssh client (for tests whose KEX every OpenSSH supports).
        private static String? FindSshClient()
            => FindSshClients().FirstOrDefault();

        #endregion


        #region OurServer_CompletesTransport_WithRealOpenSshClient

        [Test]
        [CancelAfter(30000)]
        [TestCase("curve25519-sha256",   "ssh-ed25519",         "chacha20-poly1305@openssh.com", "hmac-sha2-256",          "chacha20-poly1305@openssh.com")]
        [TestCase("curve25519-sha256",   "ssh-ed25519",         "aes256-gcm@openssh.com", "hmac-sha2-256",                 "aes256-gcm@openssh.com")]
        [TestCase("curve25519-sha256",   "ssh-ed25519",         "aes256-ctr",             "hmac-sha2-256-etm@openssh.com", "aes256-ctr")]
        [TestCase("ecdh-sha2-nistp256",  "ssh-ed25519",         "aes256-gcm@openssh.com", "hmac-sha2-256",                 "aes256-gcm@openssh.com")]
        [TestCase("ecdh-sha2-nistp521",  "ssh-ed25519",         "aes256-ctr",             "hmac-sha2-512-etm@openssh.com", "aes256-ctr")]
        [TestCase("curve25519-sha256",   "ecdsa-sha2-nistp256", "aes256-gcm@openssh.com", "hmac-sha2-256",                 "aes256-gcm@openssh.com")]
        [TestCase("curve25519-sha256",   "rsa-sha2-512",        "aes256-gcm@openssh.com", "hmac-sha2-256",                 "aes256-gcm@openssh.com")]
        [TestCase("diffie-hellman-group14-sha256", "ssh-ed25519", "aes256-gcm@openssh.com", "hmac-sha2-256",              "aes256-gcm@openssh.com")]
        [TestCase("diffie-hellman-group16-sha512", "ssh-ed25519", "aes256-ctr",             "hmac-sha2-512-etm@openssh.com", "aes256-ctr")]
        [TestCase("mlkem768x25519-sha256",  "ssh-ed25519", "aes256-gcm@openssh.com",        "hmac-sha2-256",                 "aes256-gcm@openssh.com")]
        [TestCase("sntrup761x25519-sha512", "ssh-ed25519", "chacha20-poly1305@openssh.com", "hmac-sha2-256",                 "chacha20-poly1305@openssh.com")]
        public async Task OurServer_CompletesTransport_WithRealOpenSshClient(String             SshKex,
                                                                             String             SshHostKeyAlg,
                                                                             String             SshCipher,
                                                                             String             SshMac,
                                                                             String             ExpectedCipher,
                                                                             CancellationToken  CancellationToken)
        {

            var sshClient = FindSshClientSupporting(SshKex);
            if (sshClient is null)
                Assert.Ignore($"No 'ssh' client supporting {SshKex} found — skipping (e.g. Windows-bundled OpenSSH 9.5 lacks the post-quantum methods).");

            var hostKey = HostKeyMatrixTests.MakeHostKey(SshHostKeyAlg);

            using var listener = SshTcpListener.Start(new IPSocket(IPv4Address.Localhost, IPPort.Auto));
            var port = listener.LocalEndPoint.Port.ToInt32();

            // The server accepts one connection, completes the handshake, and reads the client's first
            // application packet — which must decrypt to SSH_MSG_SERVICE_REQUEST("ssh-userauth"). Modern
            // OpenSSH clients send their own SSH_MSG_EXT_INFO first (post-NEWKEYS), so skip that if present.
            var serverTask = Task.Run(async () =>
            {
                var pipe = await listener.AcceptAsync(CancellationToken);
                using var context = await SshHandshake.ServerHandshakeAsync(pipe, hostKey, CancellationToken: CancellationToken);

                // Post-NEWKEYS packets start at sequence number 0 (strict-KEX). ReceiveMac is null for AEAD.
                UInt32 sequenceNumber = 0;
                var payload = await SshPacketFraming.ReadPacketAsync(pipe.Input, context.ReceiveCipher, sequenceNumber++, context.ReceiveMac, CancellationToken: CancellationToken);

                // Modern OpenSSH clients send their own SSH_MSG_EXT_INFO first (post-NEWKEYS); skip it.
                if (payload.Length > 0 && payload[0] == (Byte) SshMessageNumber.ExtInfo)
                    payload = await SshPacketFraming.ReadPacketAsync(pipe.Input, context.ReceiveCipher, sequenceNumber++, context.ReceiveMac, CancellationToken: CancellationToken);

                return (context.Algorithms, payload);
            }, CancellationToken);

            var knownHosts = Path.GetTempFileName();
            var emptyConf  = Path.GetTempFileName();

            using var ssh = new Process { StartInfo = new ProcessStartInfo(sshClient!)
            {
                RedirectStandardError   = true,
                RedirectStandardOutput  = true,
                UseShellExecute         = false,
                CreateNoWindow          = true
            }};

            foreach (var arg in new[]
            {
                "-F", emptyConf,                                       // ignore the user's ssh_config
                "-p", port.ToString(),
                "-o", "StrictHostKeyChecking=no",
                "-o", $"UserKnownHostsFile={knownHosts}",
                "-o", $"KexAlgorithms={SshKex}",
                "-o", $"HostKeyAlgorithms={SshHostKeyAlg}",
                "-o", $"Ciphers={SshCipher}",
                "-o", $"MACs={SshMac}",
                "-o", "PreferredAuthentications=none",
                "-o", "PubkeyAuthentication=no",
                "-o", "PasswordAuthentication=no",
                "-o", "KbdInteractiveAuthentication=no",
                "-o", "BatchMode=yes",
                "-o", "ConnectTimeout=10",
                "-vv",
                "hermod@127.0.0.1",
                "exit"
            })
                ssh.StartInfo.ArgumentList.Add(arg);

            String stderr = "";

            try
            {

                ssh.Start();
                var stderrTask = ssh.StandardError.ReadToEndAsync(CancellationToken);

                var (algorithms, payload) = await serverTask;

                // We have the proof (the decrypted SERVICE_REQUEST). The client would otherwise hang
                // waiting for a SERVICE_ACCEPT we don't send in M1, so stop it now and read its log.
                try { if (!ssh.HasExited) ssh.Kill(entireProcessTree: true); } catch { }
                try { stderr = await stderrTask; } catch { /* torn down */ }

                var reader   = new SshPacketReader(payload);
                var message  = (SshMessageNumber) reader.ReadByte();
                var service  = message == SshMessageNumber.ServiceRequest ? reader.ReadString() : "";

                Assert.Multiple(() => {
                    Assert.That(algorithms.KeyExchange,          Is.EqualTo(SshKex));
                    Assert.That(algorithms.CipherClientToServer, Is.EqualTo(ExpectedCipher));
                    Assert.That(algorithms.StrictKex,            Is.True, "OpenSSH 9.6+ must negotiate strict-KEX with us.");
                    Assert.That(message,                         Is.EqualTo(SshMessageNumber.ServiceRequest),
                                "The real OpenSSH client's first encrypted packet must decrypt to SSH_MSG_SERVICE_REQUEST.");
                    Assert.That(service,                         Is.EqualTo("ssh-userauth"));
                });

            }
            catch (Exception e)
            {
                try   { if (!ssh.HasExited) ssh.Kill(entireProcessTree: true); } catch { }
                try   { stderr = await ssh.StandardError.ReadToEndAsync(CancellationToken); }
                catch { }
                TestContext.Out.WriteLine("ssh -vv stderr:\n" + stderr);
                throw new AssertionException("The OpenSSH transport interop failed. ssh -vv output:\n" + stderr, e);
            }
            finally
            {
                try { if (!ssh.HasExited) ssh.Kill(entireProcessTree: true); } catch { }
                try { File.Delete(knownHosts); } catch { }
                try { File.Delete(emptyConf);  } catch { }
            }

        }

        #endregion

        #region OurServer_SendsExtInfo_RealOpenSshClientReceivesServerSigAlgs

        [Test]
        [CancelAfter(30000)]
        public async Task OurServer_SendsExtInfo_RealOpenSshClientReceivesServerSigAlgs(CancellationToken CancellationToken)
        {

            var sshClient = FindSshClient();
            if (sshClient is null)
                Assert.Ignore("No 'ssh' client found — skipping OpenSSH ext-info interop.");

            var hostKey = HostKeyMatrixTests.MakeHostKey("ssh-ed25519");

            using var listener = SshTcpListener.Start(new IPSocket(IPv4Address.Localhost, IPPort.Auto));
            var port = listener.LocalEndPoint.Port.ToInt32();

            // Our stateful transport completes the handshake and — because ext-info is negotiated —
            // sends SSH_MSG_EXT_INFO(server-sig-algs) as the first packet after NEWKEYS, then reads the
            // client's SERVICE_REQUEST. The real ssh client must log that it received the EXT_INFO.
            var serverTask = Task.Run(async () =>
            {
                var pipe      = await listener.AcceptAsync(CancellationToken);
                var transport = await SshTransport.ServerHandshakeAsync(pipe, hostKey, CancellationToken: CancellationToken);

                // Modern OpenSSH clients send their own EXT_INFO first; consume it, then read SERVICE_REQUEST.
                var payload = await transport.ReceivePacketAsync(CancellationToken);
                if (payload.Length > 0 && payload[0] == (Byte) SshMessageNumber.ExtInfo)
                    payload = await transport.ReceivePacketAsync(CancellationToken);

                return (transport.Algorithms, payload);
            }, CancellationToken);

            var knownHosts = Path.GetTempFileName();
            var emptyConf  = Path.GetTempFileName();

            using var ssh = new Process { StartInfo = new ProcessStartInfo(sshClient!)
            {
                RedirectStandardError   = true,
                RedirectStandardOutput  = true,
                UseShellExecute         = false,
                CreateNoWindow          = true
            }};

            foreach (var arg in new[]
            {
                "-F", emptyConf,
                "-p", port.ToString(),
                "-o", "StrictHostKeyChecking=no",
                "-o", $"UserKnownHostsFile={knownHosts}",
                "-o", "KexAlgorithms=curve25519-sha256",
                "-o", "HostKeyAlgorithms=ssh-ed25519",
                "-o", "Ciphers=chacha20-poly1305@openssh.com",
                "-o", "PreferredAuthentications=none",
                "-o", "PubkeyAuthentication=no",
                "-o", "PasswordAuthentication=no",
                "-o", "KbdInteractiveAuthentication=no",
                "-o", "BatchMode=yes",
                "-o", "ConnectTimeout=10",
                "-vv",
                "hermod@127.0.0.1",
                "exit"
            })
                ssh.StartInfo.ArgumentList.Add(arg);

            String stderr = "";

            try
            {

                ssh.Start();
                var stderrTask = ssh.StandardError.ReadToEndAsync(CancellationToken);

                var (algorithms, payload) = await serverTask;

                try { if (!ssh.HasExited) ssh.Kill(entireProcessTree: true); } catch { }
                try { stderr = await stderrTask; } catch { }

                var reader   = new SshPacketReader(payload);
                var message  = (SshMessageNumber) reader.ReadByte();

                Assert.Multiple(() => {
                    Assert.That(algorithms.ExtensionInfo, Is.True, "OpenSSH must negotiate ext-info with us.");
                    Assert.That(message,                  Is.EqualTo(SshMessageNumber.ServiceRequest));
                    // The proof: the real client logs receipt of our EXT_INFO and parses server-sig-algs.
                    Assert.That(stderr, Does.Contain("SSH2_MSG_EXT_INFO received"),
                                "The real OpenSSH client must report receiving our SSH_MSG_EXT_INFO.");
                    Assert.That(stderr, Does.Contain("server-sig-algs"),
                                "The client's ext-info parse must mention server-sig-algs.");
                });

            }
            catch (Exception e)
            {
                try   { stderr = await ssh.StandardError.ReadToEndAsync(CancellationToken); }
                catch { }
                TestContext.Out.WriteLine("ssh -vv stderr:\n" + stderr);
                throw new AssertionException("The OpenSSH ext-info interop failed. ssh -vv output:\n" + stderr, e);
            }
            finally
            {
                try { if (!ssh.HasExited) ssh.Kill(entireProcessTree: true); } catch { }
                try { File.Delete(knownHosts); } catch { }
                try { File.Delete(emptyConf);  } catch { }
            }

        }

        #endregion

    }

}
