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
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TLS.KeyPins
{

    /// <summary>
    /// An OpenSSL to test raw public keys against: one that knows RFC 7250,
    /// which OpenSSL does since 3.2.
    /// </summary>
    /// <remarks>
    /// Looked for in the environment variable HERMOD_OPENSSL, on the PATH,
    /// and where Git for Windows keeps its own. Where there is none, or an
    /// older one, the tests that need it are ignored and say so: nothing is
    /// shown about interoperability then.
    /// </remarks>
    public static class OpenSSLForTests
    {

        private static readonly Lazy<(String? Path, String Says)> found = new (Find);

        private static (String?, String) Find()
        {

            var candidates = new List<String>();

            if (Environment.GetEnvironmentVariable("HERMOD_OPENSSL") is { Length: > 0 } named)
                candidates.Add(named);

            candidates.Add("openssl");

            if (OperatingSystem.IsWindows())
            {
                candidates.Add(@"C:\Program Files\Git\ucrt64\bin\openssl.exe");
                candidates.Add(@"C:\Program Files\Git\mingw64\bin\openssl.exe");
                candidates.Add(@"C:\Program Files\Git\usr\bin\openssl.exe");
            }

            var seen = new List<String>();

            foreach (var candidate in candidates)
            {

                try
                {

                    var (exitCode, output) = Run(candidate, [ "version" ], TimeSpan.FromSeconds(20));
                    var version            = Regex.Match(output, @"OpenSSL (\d+)\.(\d+)\.(\d+)");

                    if (exitCode != 0 || !version.Success)
                    {
                        seen.Add($"{candidate}: {output.Trim()}");
                        continue;
                    }

                    var major = Int32.Parse(version.Groups[1].Value);
                    var minor = Int32.Parse(version.Groups[2].Value);

                    if (major > 3 || (major == 3 && minor >= 2))
                        return (candidate, output.Trim());

                    seen.Add($"{candidate}: {version.Value}, which has no raw public keys");

                }
                catch (Exception e)
                {
                    seen.Add($"{candidate}: {e.Message}");
                }

            }

            return (null, $"No OpenSSL 3.2 or newer was found ({String.Join("; ", seen)}). Set HERMOD_OPENSSL to one.");

        }

        /// <summary>
        /// The OpenSSL to use, or the test is ignored.
        /// </summary>
        public static String Required()
        {

            if (found.Value.Path is null)
                Assert.Ignore(found.Value.Says);

            return found.Value.Path!;

        }

        /// <summary>
        /// Run a program to its end with nothing on its standard input.
        /// </summary>
        public static (Int32 ExitCode, String Output) Run(String FileName, IEnumerable<String> Arguments, TimeSpan Timeout)
        {

            var startInfo = new ProcessStartInfo(FileName) {
                                RedirectStandardInput   = true,
                                RedirectStandardOutput  = true,
                                RedirectStandardError   = true,
                                UseShellExecute         = false,
                                CreateNoWindow          = true
                            };

            foreach (var argument in Arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo)!;

            var output = new StringBuilder();

            process.OutputDataReceived += (_, line) => { if (line.Data is not null) lock (output) output.AppendLine(line.Data); };
            process.ErrorDataReceived  += (_, line) => { if (line.Data is not null) lock (output) output.AppendLine(line.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.StandardInput.Close();

            if (!process.WaitForExit(Timeout))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                lock (output)
                    return (-1, output + $"{Environment.NewLine}(killed after {Timeout.TotalSeconds} s)");
            }

            process.WaitForExit();

            lock (output)
                return (process.ExitCode, output.ToString());

        }

    }


    /// <summary>
    /// Raw public keys (RFC 7250) between Hermod's own TLS 1.3 under QUIC
    /// and OpenSSL: "openssl s_client -quic" with -enable_server_rpk and
    /// -enable_client_rpk against an HTTP/3 server of Hermod.
    /// </summary>
    /// <remarks>
    /// OpenSSL's s_server speaks no QUIC, so the other direction - Hermod's
    /// QUIC client against an OpenSSL server - is not tested here.
    /// </remarks>
    [TestFixture]
    public class RawPublicKeyOpenSSLTests
    {

        #region (helpers)

        private sealed class Told
        {
            public readonly ConcurrentQueue<KeyPinValidation> Results = new();
            public void Add(KeyPinValidation Validation) => Results.Enqueue(Validation);
        }

        private static Http3Response Hello(Http3Request Request)

            => new() {
                   Status  = 200,
                   Body    = Encoding.UTF8.GetBytes("raw")
               };

        /// <summary>
        /// A P-256 key in a self-signed certificate, as files OpenSSL reads.
        /// </summary>
        private sealed class KeyFiles : IDisposable
        {

            public String  Directory      { get; }
            public String  KeyFile        { get; }
            public String  CertificateFile { get; }
            public KeyPin  KeyPin         { get; }

            public KeyFiles()
            {

                Directory        = Path.Combine(Path.GetTempPath(), "hermod-rpk-" + Guid.NewGuid().ToString("N"));
                KeyFile          = Path.Combine(Directory, "key.pem");
                CertificateFile  = Path.Combine(Directory, "certificate.pem");

                System.IO.Directory.CreateDirectory(Directory);

                using var key         = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                using var certificate = new CertificateRequest("CN=station.invalid", key, HashAlgorithmName.SHA256).
                                            CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

                File.WriteAllText(KeyFile,         key.ExportPkcs8PrivateKeyPem());
                File.WriteAllText(CertificateFile, certificate.ExportCertificatePem());

                KeyPin = KeyPin.FromKey(key);

            }

            public void Dispose()
            {
                try { System.IO.Directory.Delete(Directory, recursive: true); } catch { }
            }

        }

        /// <summary>
        /// An HTTP/3 server whose connections can be looked at afterwards.
        /// </summary>
        private static Http3Server ServerOf(ServerCertificate                       Key,
                                            ConcurrentQueue<Http3ServerConnection>  Connections,
                                            ClientCertificateOptions?               ClientCertificate = null)

            => new (0,
                    retry => {
                        var connection = new Http3ServerConnection(Key, Hello, validatedRetry: retry, clientCertificate: ClientCertificate);
                        Connections.Enqueue(connection);
                        return connection;
                    });

        private static async Task<Boolean> Until(Func<Boolean> Condition)
        {

            for (var i = 0; i < 100 && !Condition(); i++)
                await Task.Delay(50);

            return Condition();

        }

        #endregion


        [Test]
        public async Task OpenSSL_TakesTheRawPublicKey_OfAnHTTP3Server()
        {

            var openssl = OpenSSLForTests.Required();

            using var key = ServerCertificate.CreateSelfSigned("carrier.invalid");
            key.RawPublicKeys = RawPublicKeyMode.Prefer;

            var connections = new ConcurrentQueue<Http3ServerConnection>();

            await using var server = ServerOf(key, connections);
            server.Start();

            var (_, output) = OpenSSLForTests.Run(openssl,
                                                  [ "s_client", "-quic", "-alpn", "h3",
                                                    "-connect", $"127.0.0.1:{server.Port}",
                                                    "-enable_server_rpk" ],
                                                  TimeSpan.FromSeconds(30));

            TestContext.Out.WriteLine(output);

            Assert.That(await Until(() => connections.Any(connection => connection.HandshakeComplete)), Is.True, output);

            var connection = connections.First(connection => connection.HandshakeComplete);

            Assert.Multiple(() => {
                Assert.That(connection.ServerCertificateType, Is.EqualTo(TLSCertificateType.RawPublicKey));
                Assert.That(output,                           Does.Contain("Server-to-client raw public key negotiated"));
                Assert.That(output,                           Does.Contain("Server raw public key"));
                Assert.That(output,                           Does.Not.Contain("Client-to-server raw public key negotiated"), "No client key was asked for.");
            });

        }

        [Test]
        public async Task OpenSSL_WithoutRawPublicKeys_IsShownTheCertificate_OrRefused()
        {

            var openssl = OpenSSLForTests.Required();

            // Prefer: the certificate.
            using (var key = ServerCertificate.CreateSelfSigned("carrier.invalid"))
            {

                key.RawPublicKeys = RawPublicKeyMode.Prefer;

                var connections = new ConcurrentQueue<Http3ServerConnection>();

                await using var server = ServerOf(key, connections);
                server.Start();

                var (_, output) = OpenSSLForTests.Run(openssl,
                                                      [ "s_client", "-quic", "-alpn", "h3", "-connect", $"127.0.0.1:{server.Port}" ],
                                                      TimeSpan.FromSeconds(30));

                TestContext.Out.WriteLine(output);

                Assert.That(await Until(() => connections.Any(connection => connection.HandshakeComplete)), Is.True, output);
                Assert.That(connections.First(connection => connection.HandshakeComplete).ServerCertificateType, Is.EqualTo(TLSCertificateType.X509));
                Assert.That(output, Does.Contain("carrier.invalid"));

            }

            // Require: no handshake.
            using (var key = ServerCertificate.CreateSelfSigned("carrier.invalid"))
            {

                key.RawPublicKeys = RawPublicKeyMode.Require;

                var connections = new ConcurrentQueue<Http3ServerConnection>();

                await using var server = ServerOf(key, connections);
                server.Start();

                var (exitCode, output) = OpenSSLForTests.Run(openssl,
                                                             [ "s_client", "-quic", "-alpn", "h3", "-connect", $"127.0.0.1:{server.Port}" ],
                                                             TimeSpan.FromSeconds(30));

                TestContext.Out.WriteLine(output);

                Assert.That(await Until(() => !connections.IsEmpty),                         Is.True, output);
                Assert.That(connections.Any(connection => connection.HandshakeComplete),     Is.False);
                Assert.That(exitCode,                                                        Is.Not.Zero);
                Assert.That(output,                                                          Does.Not.Contain("carrier.invalid"), "The certificate of a key that is shown raw alone was sent.");

            }

        }

        [Test]
        public async Task OpenSSL_ShowsItsRawPublicKey_AndIsAdmittedByItsPin_OrRefused()
        {

            var openssl = OpenSSLForTests.Required();

            using var station = new KeyFiles();
            using var key     = ServerCertificate.CreateSelfSigned("carrier.invalid");
            key.RawPublicKeys = RawPublicKeyMode.Prefer;

            String[] arguments(Int32 port) => [ "s_client", "-quic", "-alpn", "h3",
                                                "-connect", $"127.0.0.1:{port}",
                                                "-enable_server_rpk", "-enable_client_rpk",
                                                "-key", station.KeyFile, "-cert", station.CertificateFile ];

            // Its key is pinned.
            {

                var told        = new Told();
                var connections = new ConcurrentQueue<Http3ServerConnection>();

                await using var server = ServerOf(key, connections,
                                                  ClientCertificateOptions.RequirePinned(
                                                      new KeyPinPolicy([ station.KeyPin ], RawPublicKeys: RawPublicKeyMode.Require),
                                                      told.Add
                                                  ));
                server.Start();

                var (_, output) = OpenSSLForTests.Run(openssl, arguments(server.Port), TimeSpan.FromSeconds(30));

                TestContext.Out.WriteLine(output);

                Assert.That(await Until(() => connections.Any(connection => connection.HandshakeComplete)), Is.True, output);

                var connection     = connections.First(connection => connection.HandshakeComplete);
                var authentication = connection.ClientAuthentication;

                Assert.Multiple(() => {
                    Assert.That(connection.ServerCertificateType,     Is.EqualTo(TLSCertificateType.RawPublicKey));
                    Assert.That(connection.ClientCertificateType,     Is.EqualTo(TLSCertificateType.RawPublicKey));
                    Assert.That(authentication.IsAuthenticated,       Is.True, authentication.FailureReason);
                    Assert.That(authentication.Certificate,           Is.Null);
                    Assert.That(authentication.Key?.IsRawPublicKey,   Is.True);
                    Assert.That(authentication.Key?.KeyPin,           Is.EqualTo(station.KeyPin));
                    Assert.That(told.Results.Single().Outcome,        Is.EqualTo(KeyPinOutcome.Accepted));
                });

            }

            // Another key is.
            {

                using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);

                var told        = new Told();
                var connections = new ConcurrentQueue<Http3ServerConnection>();

                await using var server = ServerOf(key, connections,
                                                  ClientCertificateOptions.RequirePinned(
                                                      new KeyPinPolicy([ KeyPin.FromKey(other) ], RawPublicKeys: RawPublicKeyMode.Require),
                                                      told.Add
                                                  ));
                server.Start();

                var (_, output) = OpenSSLForTests.Run(openssl, arguments(server.Port), TimeSpan.FromSeconds(30));

                TestContext.Out.WriteLine(output);

                Assert.That(await Until(() => !told.Results.IsEmpty), Is.True, output);

                Assert.Multiple(() => {
                    Assert.That(connections.Any(connection => connection.HandshakeComplete), Is.False);
                    Assert.That(told.Results.Single().Outcome,                               Is.EqualTo(KeyPinOutcome.Refused));
                    Assert.That(told.Results.Single().Pin,                                   Is.EqualTo(station.KeyPin));
                    Assert.That(told.Results.Single().CertificateType,                       Is.EqualTo(TLSCertificateType.RawPublicKey));
                });

            }

        }

    }

}
