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

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Interoperability tests for OpenSSH certificates against <c>ssh-keygen</c>: we validate a certificate
    /// it signs (as a CA), and it reads a certificate our mini-CA issues.
    /// </summary>
    [TestFixture]
    [Category("Interop")]
    [Category("Interop.OpenSSH")]
    public class OpenSshCertificateInteropTests
    {

        private static String? FindSshKeygen()
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                foreach (var name in new[] { "ssh-keygen", "ssh-keygen.exe" })
                    try { var c = Path.Combine(dir.Trim(), name); if (File.Exists(c)) return c; } catch { }
            var system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh-keygen.exe");
            return File.Exists(system) ? system : null;
        }

        private static async Task<(Int32 ExitCode, String StdOut, String StdErr)> RunAsync(String Exe, CancellationToken CancellationToken, params String[] Args)
        {
            var startInfo = new ProcessStartInfo(Exe) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in Args) startInfo.ArgumentList.Add(a);
            using var process = Process.Start(startInfo)!;
            var stdout = await process.StandardOutput.ReadToEndAsync(CancellationToken);
            var stderr = await process.StandardError.ReadToEndAsync(CancellationToken);
            await process.WaitForExitAsync(CancellationToken);
            return (process.ExitCode, stdout, stderr);
        }


        #region WeValidate_SshKeygenSignedCertificate

        [Test]
        [CancelAfter(30000)]
        public async Task WeValidate_SshKeygenSignedCertificate(CancellationToken CancellationToken)
        {

            var keygen = FindSshKeygen();
            if (keygen is null)
                Assert.Ignore("No 'ssh-keygen' found.");

            var dir   = Directory.CreateTempSubdirectory("hermod_cert_");
            var ca    = Path.Combine(dir.FullName, "ca");
            var user  = Path.Combine(dir.FullName, "user");

            try
            {

                if ((await RunAsync(keygen!, CancellationToken, "-t", "ed25519", "-f", ca,   "-N", "", "-q", "-C", "ca")).ExitCode   != 0 ||
                    (await RunAsync(keygen!, CancellationToken, "-t", "ed25519", "-f", user, "-N", "", "-q", "-C", "user")).ExitCode != 0)
                    Assert.Ignore("ssh-keygen could not generate keys.");

                // ssh-keygen as a CA: sign the user key with id, principals, serial and a validity window.
                var sign = await RunAsync(keygen!, CancellationToken,
                                          "-s", ca, "-I", "hermod-test-id", "-n", "achim,ops", "-z", "777",
                                          "-V", "-1h:+52w", user + ".pub");
                if (sign.ExitCode != 0)
                    Assert.Ignore($"ssh-keygen -s failed: {sign.StdErr}");

                var certLine = await File.ReadAllTextAsync(user + "-cert.pub", CancellationToken);
                var caLine   = await File.ReadAllTextAsync(ca + ".pub", CancellationToken);

                var cert  = SshCertificate.Parse(SshPublicKey.Parse(certLine).Blob);
                var caKey = SshPublicKey.Parse(caLine);
                var trust = new SshCertificateAuthorityTrust().TrustCA(caKey.Blob);

                var result = SshCertificateValidator.Validate(cert, SshCertType.User, "achim", trust, DateTimeOffset.UtcNow);

                Assert.Multiple(() => {
                    Assert.That(cert.KeyId,       Is.EqualTo("hermod-test-id"));
                    Assert.That(cert.Serial,      Is.EqualTo(777));
                    Assert.That(cert.Principals,  Does.Contain("achim").And.Contains("ops"));
                    Assert.That(cert.VerifyCaSignature(), Is.True, "we must verify ssh-keygen's CA signature");
                    Assert.That(result.IsValid,   Is.True, "ssh-keygen's certificate must pass our validation");
                    Assert.That(SshCertificateValidator.Validate(cert, SshCertType.User, "eve", trust, DateTimeOffset.UtcNow).IsValid, Is.False);
                });

            }
            finally
            {
                try { dir.Delete(recursive: true); } catch { }
            }

        }

        #endregion

        #region SshKeygenReads_OurCertificate

        [Test]
        [CancelAfter(30000)]
        public async Task SshKeygenReads_OurCertificate(CancellationToken CancellationToken)
        {

            var keygen = FindSshKeygen();
            if (keygen is null)
                Assert.Ignore("No 'ssh-keygen' found.");

            var dir  = Directory.CreateTempSubdirectory("hermod_ourcert_");
            var path = Path.Combine(dir.FullName, "id-cert.pub");

            try
            {

                var caKey    = SshHostKey.GenerateEd25519();
                var userKey  = SshHostKey.GenerateEd25519();

                var cert = new OpenSshCertificateBuilder
                {
                    Serial      = 4242,
                    Type        = SshCertType.User,
                    KeyId       = "issued-by-hermod",
                    Principals  = [ "achim", "admin" ],
                    ValidAfter  = DateTimeOffset.UtcNow.AddHours(-1),
                    ValidBefore = DateTimeOffset.UtcNow.AddDays(30)
                }.Sign(userKey.PublicKeyBlob, caKey);

                var line = cert.CertAlgorithm + " " + Convert.ToBase64String(cert.Blob) + " hermod\n";
                await File.WriteAllTextAsync(path, line, CancellationToken);

                // ssh-keygen -L prints certificate details — proof it can parse and verify our structure.
                var view = await RunAsync(keygen!, CancellationToken, "-L", "-f", path);

                Assert.Multiple(() => {
                    Assert.That(view.ExitCode, Is.EqualTo(0), $"ssh-keygen -L failed: {view.StdErr}");
                    Assert.That(view.StdOut,   Does.Contain("issued-by-hermod"));
                    Assert.That(view.StdOut,   Does.Contain("Serial: 4242"));
                    Assert.That(view.StdOut,   Does.Contain("achim"));
                });

            }
            finally
            {
                try { dir.Delete(recursive: true); } catch { }
            }

        }

        #endregion

    }

}
