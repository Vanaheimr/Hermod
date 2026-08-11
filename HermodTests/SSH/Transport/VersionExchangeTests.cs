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

using System.Text;
using System.IO.Pipelines;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// Unit tests for the SSH version / identification string exchange (RFC 4253, section 4.2).
    /// </summary>
    [TestFixture]
    public class VersionExchangeTests
    {

        #region Parsing

        [Test]
        public void Parse_WithoutComments()
        {

            var id = SshIdentificationString.Parse("SSH-2.0-OpenSSH_9.6");

            Assert.Multiple(() => {
                Assert.That(id.ProtocolVersion, Is.EqualTo("2.0"));
                Assert.That(id.SoftwareVersion, Is.EqualTo("OpenSSH_9.6"));
                Assert.That(id.Comments,        Is.Null);
                Assert.That(id.Line,            Is.EqualTo("SSH-2.0-OpenSSH_9.6"));
            });

        }

        [Test]
        public void Parse_WithComments()
        {

            var id = SshIdentificationString.Parse("SSH-2.0-OpenSSH_9.6p1 Debian-4\r\n");

            Assert.Multiple(() => {
                Assert.That(id.SoftwareVersion, Is.EqualTo("OpenSSH_9.6p1"));
                Assert.That(id.Comments,        Is.EqualTo("Debian-4"));
                Assert.That(id.Line,            Is.EqualTo("SSH-2.0-OpenSSH_9.6p1 Debian-4"));
            });

        }

        [Test]
        public void Parse_Accepts199CompatVersion()
        {
            Assert.That(SshIdentificationString.Parse("SSH-1.99-Server").ProtocolVersion, Is.EqualTo("1.99"));
        }

        [TestCase("SSH-1.5-OldServer")]      // SSH-1 only
        [TestCase("HTTP/1.1 200 OK")]        // not SSH at all
        [TestCase("SSH-2.0-")]               // empty software version
        [TestCase("SSH-2.0")]                // missing second dash
        public void Parse_RejectsInvalid(String Text)
        {
            Assert.That(SshIdentificationString.TryParse(Text, out _, out _), Is.False);
        }

        [Test]
        public void Construct_RejectsSpaceInSoftwareVersion()
        {
            Assert.Throws<ArgumentException>(() => new SshIdentificationString("Bad Version"));
        }

        [Test]
        public void ToWireBytes_HasNoCrLf()
        {
            var bytes = new SshIdentificationString("HermodSSH_0.1").ToWireBytes();
            Assert.That(bytes, Is.EqualTo(Encoding.ASCII.GetBytes("SSH-2.0-HermodSSH_0.1")));
        }

        #endregion

        #region Exchange over a pipe

        [Test]
        [CancelAfter(5000)]
        public async Task Exchange_RoundTrip(CancellationToken CancellationToken)
        {

            var pipe = new Pipe();

            var ours = new SshIdentificationString("HermodSSH_0.1");
            await SshVersionExchange.WriteAsync(pipe.Writer, ours, CancellationToken);

            var peer = await SshVersionExchange.ReadAsync(pipe.Reader, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(peer.Identification.SoftwareVersion, Is.EqualTo("HermodSSH_0.1"));
                Assert.That(peer.WireBytes,                      Is.EqualTo(ours.ToWireBytes()));
            });

        }

        [Test]
        [CancelAfter(5000)]
        public async Task Exchange_SkipsPreBannerLines(CancellationToken CancellationToken)
        {

            var pipe = new Pipe();

            // A server may emit banner lines before its "SSH-" identification string.
            var payload = "Welcome to the jungle\r\n" +
                          "Unauthorized access is prohibited\r\n" +
                          "SSH-2.0-OpenSSH_9.6\r\n";

            await pipe.Writer.WriteAsync(Encoding.ASCII.GetBytes(payload), CancellationToken);
            await pipe.Writer.FlushAsync(CancellationToken);

            var peer = await SshVersionExchange.ReadAsync(pipe.Reader, CancellationToken);

            Assert.That(peer.Identification.SoftwareVersion, Is.EqualTo("OpenSSH_9.6"));

        }

        [Test]
        [CancelAfter(5000)]
        public async Task Exchange_ToleratesBareLineFeed(CancellationToken CancellationToken)
        {

            // Some peers terminate with LF only rather than CR LF.
            var pipe = new Pipe();
            await pipe.Writer.WriteAsync(Encoding.ASCII.GetBytes("SSH-2.0-Dropbear_2022.83\n"), CancellationToken);
            await pipe.Writer.FlushAsync(CancellationToken);

            var peer = await SshVersionExchange.ReadAsync(pipe.Reader, CancellationToken);

            Assert.Multiple(() => {
                Assert.That(peer.Identification.SoftwareVersion, Is.EqualTo("Dropbear_2022.83"));
                Assert.That(peer.WireBytes,                      Is.EqualTo(Encoding.ASCII.GetBytes("SSH-2.0-Dropbear_2022.83")));
            });

        }

        #endregion

    }

}
