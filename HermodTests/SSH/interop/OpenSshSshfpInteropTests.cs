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

    /// <summary>M8 SSHFP: our generated zone records match <c>ssh-keygen -r</c> byte-for-byte.</summary>
    [TestFixture]
    [Category("Interop")]
    [Category("Interop.OpenSSH")]
    public class OpenSshSshfpInteropTests
    {

        private static String? FindOnPathOrSystem(String Tool)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                foreach (var name in new[] { Tool, Tool + ".exe" })
                    try { var c = Path.Combine(dir.Trim(), name); if (File.Exists(c)) return c; } catch { }
            var system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", Tool + ".exe");
            return File.Exists(system) ? system : null;
        }

        private static String Normalize(String Line)
            => String.Join(' ', Line.Split((Char[]?) null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();


        #region OurSshfpRecords_MatchSshKeygenDashR

        [Test]
        [CancelAfter(30000)]
        [TestCase("ed25519")]
        [TestCase("ecdsa")]
        [TestCase("rsa")]
        public async Task OurSshfpRecords_MatchSshKeygenDashR(String KeyType, CancellationToken CancellationToken)
        {

            var sshKeygen = FindOnPathOrSystem("ssh-keygen");
            if (sshKeygen is null)
                Assert.Ignore("No 'ssh-keygen' found.");

            var keyPath = Path.Combine(Path.GetTempPath(), "hermod_sshfp_" + Guid.NewGuid().ToString("N"));
            const String hostname = "host.example.";

            try
            {

                using (var keygen = Process.Start(new ProcessStartInfo(sshKeygen!) { ArgumentList = { "-t", KeyType, "-f", keyPath, "-N", "", "-q" }, UseShellExecute = false, CreateNoWindow = true })!)
                    await keygen.WaitForExitAsync(CancellationToken);

                // ssh-keygen -r prints the SSHFP zone records for the key.
                using var rr = new Process { StartInfo = new ProcessStartInfo(sshKeygen!) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true } };
                foreach (var arg in new[] { "-r", hostname, "-f", keyPath + ".pub" })
                    rr.StartInfo.ArgumentList.Add(arg);
                rr.Start();
                var output = await rr.StandardOutput.ReadToEndAsync(CancellationToken);
                await rr.WaitForExitAsync(CancellationToken);

                var theirs = output.Split('\n')
                                   .Where(l => l.Contains("SSHFP", StringComparison.OrdinalIgnoreCase))
                                   .Select(Normalize)
                                   .OrderBy(l => l)
                                   .ToList();

                // Our records from the same public-key blob.
                var pubLine = (await File.ReadAllTextAsync(keyPath + ".pub", CancellationToken)).Split(' ');
                var blob    = Convert.FromBase64String(pubLine[1]);
                var ours    = SshfpRecord.FromBlob(blob)
                                         .Select(r => Normalize(r.ToZoneLine(hostname)))
                                         .OrderBy(l => l)
                                         .ToList();

                Assert.That(ours, Is.EqualTo(theirs), $"our SSHFP records must match ssh-keygen -r.\n ours:   {String.Join(" | ", ours)}\n theirs: {String.Join(" | ", theirs)}");

            }
            finally
            {
                try { File.Delete(keyPath);          } catch { }
                try { File.Delete(keyPath + ".pub"); } catch { }
            }

        }

        #endregion

    }

}
