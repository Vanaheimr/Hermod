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

using org.GraphDefined.Vanaheimr.Hermod.ACME;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.ACME
{

    /// <summary>
    /// The certificate manager against <see cref="TestACMEServer"/>: a missing
    /// certificate is fetched and written as certbot would, a kept one is used
    /// until it is due, the certificate authority's window decides where it
    /// gives one and a third of the lifetime where it does not, and a failed
    /// renewal leaves the certificate there is.
    /// </summary>
    [TestFixture]
    public class ACMECertificateManagerTests
    {

        #region Data

        private TestACMEServer?            ca;
        private HTTPServer?                port80;
        private HTTP01ChallengeResponder?  http01;
        private ACMEAccountKey?            accountKey;
        private DirectoryInfo?             directory;

        #endregion

        #region Setup / Teardown

        [SetUp]
        public async Task SetUp()
        {

            ca          = await TestACMEServer.StartAsync();

            port80      = new HTTPServer(
                              IPAddress:  IPv4Address.Localhost,
                              TCPPort:    IPPort.Zero,
                              AutoStart:  true
                          );

            http01      = new HTTP01ChallengeResponder();
            http01.RegisterAt(port80);

            ca.HTTP01Port = port80.TCPPort.ToUInt16();

            accountKey  = ACMEAccountKey.Generate();
            directory   = System.IO.Directory.CreateTempSubdirectory("hermod-acme-");

            using var client = NewClient();
            await client.CreateAccountAsync(TermsOfServiceAgreed: true);

        }

        [TearDown]
        public async Task TearDown()
        {
            if (port80 is not null)
                await port80.DisposeAsync();
            if (ca is not null)
                await ca.DisposeAsync();
            accountKey?.Dispose();
            directory?.Delete(true);
        }

        #endregion

        #region (private) NewClient()

        private ACMEClient NewClient()

            => new (ca!.DirectoryURL,
                    accountKey!,
                    PollInterval: TimeSpan.FromMilliseconds(100),
                    PollTimeout:  TimeSpan.FromSeconds(20));

        #endregion

        #region (private) NewManager(Client, Names = null, Responders = null, TimeProvider = null)

        private ACMECertificateManager NewManager(ACMEClient                             Client,
                                                  String[]?                              Names        = null,
                                                  IACMEChallengeResponder[]?             Responders   = null,
                                                  TimeProvider?                          TimeProvider = null)

            => new (Client,
                    Names      ?? [ "time.example.org" ],
                    Responders ?? [ http01! ],
                    Directory:     directory!.FullName,
                    RetryInterval: TimeSpan.FromSeconds(1),
                    TimeProvider:  TimeProvider);

        #endregion


        #region AMissingCertificateIsFetched_WrittenAsCertbotWould_AndSaid()

        [Test]
        public async Task AMissingCertificateIsFetched_WrittenAsCertbotWould_AndSaid()
        {

            using var client   = NewClient();
            await using var manager = NewManager(client);

            var renewed = new List<ACMECertificate>();
            manager.OnRenewed += renewed.Add;

            Assert.That(manager.Current, Is.Null);
            Assert.That(await manager.StartAsync(), Is.True);

            Assert.That(renewed,                   Has.Count.EqualTo(1));
            Assert.That(manager.Current,           Is.SameAs(renewed[0].Chain));
            Assert.That(manager.Selector("any"),   Is.SameAs(manager.Current));
            Assert.That(manager.LastError,         Is.Null);

            // What certbot's files hold, and what ServerCertificateFile reads.
            var files = new ServerCertificateFile(manager.FullChainPath!, manager.PrivateKeyPath!);

            Assert.That(files.LastError,                       Is.Null);
            Assert.That(files.Current?.Certificate.Thumbprint, Is.EqualTo(manager.Current!.Certificate.Thumbprint));
            Assert.That(files.Current?.Intermediates.Count,    Is.EqualTo(1));

            if (!OperatingSystem.IsWindows())
                Assert.That(File.GetUnixFileMode(manager.PrivateKeyPath!), Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));

        }

        #endregion

        #region AKeptCertificateIsUsedUntilItIsDue()

        [Test]
        public async Task AKeptCertificateIsUsedUntilItIsDue()
        {

            using var client = NewClient();

            await using (var first = NewManager(client))
                await first.StartAsync();

            var orders = ca!.Orders;

            // As after a restart: the files are there, and not due.
            await using var second = NewManager(client);

            Assert.That(second.Current, Is.Not.Null, "read from the files in the constructor");
            Assert.That(await second.StartAsync(), Is.True);
            Assert.That(ca.Orders, Is.EqualTo(orders), "nothing ordered");

        }

        #endregion

        #region TheCertificateAuthoritysWindowDecides_AndTheRenewalSaysWhatItReplaces()

        [Test]
        public async Task TheCertificateAuthoritysWindowDecides_AndTheRenewalSaysWhatItReplaces()
        {

            using var client = NewClient();
            await using var manager = NewManager(client);

            await manager.StartAsync();

            var first       = manager.Certificate!;
            var identifier  = ACMEClient.CertificateIdentifier(first.Certificate);

            Assert.That(await manager.RenewIfDueAsync(), Is.Null, "the window lies in the future");

            // The authority moves the window into the past - a revocation is coming.
            ca!.DueAtOnce[first.Certificate.Thumbprint] = 0;

            var renewed = await manager.RenewIfDueAsync();

            Assert.That(renewed,                               Is.Not.Null);
            Assert.That(manager.Certificate,                   Is.SameAs(renewed));
            Assert.That(renewed!.Certificate.Thumbprint,       Is.Not.EqualTo(first.Certificate.Thumbprint));
            Assert.That(renewed.PrivateKeyPem,                 Is.Not.EqualTo(first.PrivateKeyPem), "a new key for every certificate");
            Assert.That(ca.Replaces,                           Is.EqualTo(new[] { identifier }));

            var files = new ServerCertificateFile(manager.FullChainPath!, manager.PrivateKeyPath!);
            Assert.That(files.Current?.Certificate.Thumbprint, Is.EqualTo(renewed.Certificate.Thumbprint));

        }

        #endregion

        #region WithoutRenewalInformation_AThirdOfTheLifetimeDecides()

        [Test]
        public async Task WithoutRenewalInformation_AThirdOfTheLifetimeDecides()
        {

            ca!.OfferRenewalInfo     = false;
            ca.CertificateLifetime   = TimeSpan.FromDays(90);

            var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);

            using var client = NewClient();
            await using var manager = NewManager(client, TimeProvider: clock);

            await manager.StartAsync();

            var issued = manager.Certificate!;
            var due    = await manager.WhenDueAsync();

            Assert.That(due, Is.EqualTo(issued.NotAfter - (issued.NotAfter - issued.NotBefore) / 3).Within(TimeSpan.FromSeconds(1)));

            clock.Now = issued.NotBefore.AddDays(59);
            Assert.That(await manager.RenewIfDueAsync(), Is.Null);

            clock.Now = issued.NotBefore.AddDays(61);
            Assert.That(await manager.RenewIfDueAsync(), Is.Not.Null);

            Assert.That(ca.Replaces, Is.Empty, "without renewal information, nothing is said to be replaced");

        }

        #endregion

        #region ACertificateForOtherNamesIsDueAtOnce()

        [Test]
        public async Task ACertificateForOtherNamesIsDueAtOnce()
        {

            using var client = NewClient();

            await using (var first = NewManager(client))
                await first.StartAsync();

            var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);

            await using var more = NewManager(client, [ "time.example.org", "ntp.example.org" ], TimeProvider: clock);

            Assert.That(await more.WhenDueAsync(), Is.EqualTo(clock.Now));

            await more.StartAsync();

            Assert.That(more.Certificate!.Identifiers.Select(identifier => identifier.Value),
                        Is.EquivalentTo(new[] { "time.example.org", "ntp.example.org" }));

        }

        #endregion

        #region AFailedRenewalKeepsTheCertificateThereIs()

        [Test]
        public async Task AFailedRenewalKeepsTheCertificateThereIs()
        {

            using var client = NewClient();

            await using (var first = NewManager(client))
                await first.StartAsync();

            var kept = File.ReadAllText(Path.Combine(directory!.FullName, ACMECertificateManager.FullChainFile));

            // Nothing answers on "port 80" any more.
            await using var manager = NewManager(client, Responders: [ new HTTP01ChallengeResponder() ]);

            var failures = new List<String>();
            manager.OnRenewalFailed += failures.Add;

            var before = manager.Certificate!;

            await Assert.ThrowsAsync<ACMEException>(() => manager.RenewAsync());

            Assert.That(manager.Certificate,  Is.SameAs(before));
            Assert.That(manager.LastError,    Does.Contain("time.example.org"));
            Assert.That(failures,             Has.Count.EqualTo(1));
            Assert.That(File.ReadAllText(Path.Combine(directory.FullName, ACMECertificateManager.FullChainFile)), Is.EqualTo(kept));

        }

        #endregion

        #region TheBackgroundLoopRenewsWhenTheWindowMoves()

        [Test]
        public async Task TheBackgroundLoopRenewsWhenTheWindowMoves()
        {

            using var client = NewClient();

            await using var manager = new ACMECertificateManager(
                                          client,
                                          [ "time.example.org" ],
                                          [ http01! ],
                                          Directory:      directory!.FullName,
                                          CheckInterval:  TimeSpan.FromMilliseconds(200),
                                          RetryInterval:  TimeSpan.FromMilliseconds(200)
                                      );

            var renewed = new TaskCompletionSource<ACMECertificate>(TaskCreationOptions.RunContinuationsAsynchronously);

            await manager.StartAsync();
            var first = manager.Certificate!;

            manager.OnRenewed += certificate => renewed.TrySetResult(certificate);

            ca!.DueAtOnce[first.Certificate.Thumbprint] = 0;

            var second = await renewed.Task.WaitAsync(TimeSpan.FromSeconds(20));

            Assert.That(second.Certificate.Thumbprint, Is.Not.EqualTo(first.Certificate.Thumbprint));
            Assert.That(manager.Certificate,           Is.SameAs(second));

        }

        #endregion


        #region (private) ManualTimeProvider

        private sealed class ManualTimeProvider(DateTimeOffset Start) : TimeProvider
        {

            public DateTimeOffset Now { get; set; } = Start;

            public override DateTimeOffset GetUtcNow()
                => Now;

        }

        #endregion

    }

}
