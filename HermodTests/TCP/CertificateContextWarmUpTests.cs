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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// What a TCP server does at <see cref="ATCPServer.Start"/> so that its
    /// first TLS client does not wait for it.
    /// </summary>
    /// <remarks>
    /// Building the first <see cref="System.Net.Security.SslStreamCertificateContext"/>
    /// of a process costs 62 - 109 ms of CPU, and where the certificate comes
    /// from an authority this machine does not know, 13 - 20 ms of waiting
    /// besides. Measured end to end through a TLS HTTPServer, the two arms
    /// interleaved: the first HTTPS request of a process went from 169 - 740 ms
    /// to 110 - 247 ms.
    ///
    /// <b>What is deliberately not asserted here is a duration.</b> Those
    /// numbers vary by a factor of four with what else the machine is doing, and
    /// by a factor of fifty with how many certificates have piled up in its CA
    /// store - the same probes read seconds rather than milliseconds a few hours
    /// before this was written, off the same code. A test that reads any of that
    /// off a stopwatch says "this machine was busy" at least as often as it says
    /// anything about this code, which is the lesson of the accept loop next
    /// door. What is asserted instead are the two properties the saving rests
    /// on: that the throwaway certificate is one this platform will build a
    /// context for at all, and that the cost is paid once per process however
    /// many servers start.
    /// </remarks>
    [TestFixture]
    public class CertificateContextWarmUpTests
    {

        #region TheWarmUp_BuildsAContext()

        /// <summary>
        /// The warm-up reports that it built a context.
        /// </summary>
        /// <remarks>
        /// This is the assertion that the throwaway certificate is a shape this
        /// platform accepts. It swallows its own failures by design - there is
        /// nobody to report to on a thread nobody waits for - so without this
        /// the warm-up could stop working and nothing would say so: every server
        /// would keep starting, and every first connection would quietly go back
        /// to paying seconds.
        /// </remarks>
        [Test]
        public void TheWarmUp_BuildsAContext()
        {

            Assert.That(TCPConnection.WarmUpCertificateContexts(), Is.True,
                        "The TLS certificate context warm-up did not manage to build a context, " +
                        "so the first TLS connection of a process pays for the chain engine itself.");

        }

        #endregion

        #region TheWarmUp_HappensOncePerProcess()

        /// <summary>
        /// Asked a hundred times over, from as many threads as the pool gives,
        /// the expensive part still happens once.
        /// </summary>
        /// <remarks>
        /// The cost belongs to the process rather than to a certificate or a
        /// server, so a second server starting would spend the half second of
        /// CPU and buy nothing with it. A hundred servers starting together -
        /// which is roughly what this test suite does - would spend it a hundred
        /// times.
        /// </remarks>
        [Test]
        public async Task TheWarmUp_HappensOncePerProcess()
        {

            // Whether this test or an earlier one triggered it does not matter:
            // the count is at most one either way, which is the property. So
            // this does not depend on the order the fixtures run in.
            await Task.WhenAll(
                      Enumerable.Range(0, 100).
                                 Select(_ => Task.Run(TCPConnection.WarmUpCertificateContexts))
                  );

            Assert.That(TCPConnection.CertificateContextWarmUps, Is.EqualTo(1),
                        "The throwaway certificate context was built more than once, " +
                        "so every server that starts spends half a second of CPU on it again.");

        }

        #endregion

        #region AChainWhoseRootIsNowhere_StillBuildsAContext()

        /// <summary>
        /// A leaf signed by a root that is neither installed on this machine nor
        /// sent along with it can still have a TLS context built for it.
        /// </summary>
        /// <remarks>
        /// This is the warm-up's own certificate, minted here in the same shape,
        /// and the reason it has that shape. A <em>self-signed</em> throwaway
        /// warms the chain engine - the CPU half - but not the lookup: its chain
        /// is complete at the leaf, so the platform is never sent looking for an
        /// issuer it has no copy of, and measured that way the first real
        /// certificate still paid 15 - 22 ms - or 0.14 - 5.2 s, on this same
        /// machine when its CA store was still full of leftover test
        /// certificates. A leaf whose issuer is nowhere warms both, and the
        /// first real certificate cost 1.3 - 2.1 ms.
        ///
        /// So if this ever fails, the warm-up has not broken - it falls back to
        /// the self-signed shape and keeps the larger half - but it has quietly
        /// become the lesser of the two, and this is what says so.
        ///
        /// Note the chain only has to be <em>buildable</em>, not trusted, and
        /// nothing is passed as an intermediate: it is the intermediates that
        /// <see cref="System.Net.Security.SslStreamCertificateContext"/> installs
        /// into the user's CA store, which is a thing these tests have poisoned a
        /// machine with before - see ServerCertificateChainTests.UniqueName.
        /// </remarks>
        [Test]
        public void AChainWhoseRootIsNowhere_StillBuildsAContext()
        {

            var now                  = DateTimeOffset.UtcNow;
            var unique               = Guid.NewGuid().ToString("N")[..8];

            using var issuerKey      = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var       issuerRequest  = new CertificateRequest($"CN=Hermod warm-up test issuer {unique}",
                                                              issuerKey,
                                                              HashAlgorithmName.SHA256);

            issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));

            using var issuer         = issuerRequest.CreateSelfSigned(now.AddHours(-1), now.AddHours(1));

            using var leafKey        = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var       leafRequest    = new CertificateRequest($"CN=Hermod warm-up test leaf {unique}",
                                                              leafKey,
                                                              HashAlgorithmName.SHA256);

            leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

            using var signed         = leafRequest.Create(issuer,
                                                          now.AddHours(-1),
                                                          now.AddHours(1),
                                                          Guid.NewGuid().ToByteArray());

            using var leaf           = signed.CopyWithPrivateKey(leafKey);

            var built = new ServerCertificateChain(leaf).TryCreateContext(out var context, out var error);

            Assert.Multiple(() => {

                Assert.That(built,    Is.True, $"A chain whose root is in no store could not have a context built: {error}");
                Assert.That(context,  Is.Not.Null);

            });

        }

        #endregion

    }

}
