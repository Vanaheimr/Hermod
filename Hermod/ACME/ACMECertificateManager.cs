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

using Microsoft.Extensions.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.ACME
{

    /// <summary>
    /// Keeps a certificate from an ACME certificate authority: fetches it when
    /// there is none, renews it before it runs out, writes it into files and
    /// says so - so that a server presents the new one from its next handshake
    /// on, without a restart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where it goes.</b> Into <see cref="Current"/> and
    /// <see cref="Selector"/> - what a Hermod TLS server asks at every
    /// handshake, as with <see cref="ServerCertificateFile"/> - into
    /// <see cref="OnRenewed"/> for whoever holds the certificate elsewhere, and,
    /// where a directory is given, into its fullchain.pem and privkey.pem, the
    /// names certbot uses. A <see cref="ServerCertificateFile"/> of another
    /// process reads them as it reads certbot's; and the next start of this
    /// one finds them, and asks the certificate authority for nothing while
    /// they are good.
    /// </para>
    /// <para>
    /// <b>When it is renewed.</b> When the certificate authority says, where it
    /// does: ACME Renewal Information (RFC 9773) suggests a window, and a time
    /// in it is chosen at random, so that not every server of the world asks at
    /// the same second. A window that moves - because the certificate is to be
    /// revoked, say - is noticed at the next check, every
    /// <see cref="CheckInterval"/>. Where the authority says nothing, when
    /// <see cref="RenewWhenRemaining"/> of the certificate's lifetime is left:
    /// a third, by default, which is 30 days of Let's Encrypt's 90, and
    /// works the same for its six-day certificates.
    /// </para>
    /// <para>
    /// <b>What a failure does: nothing to the certificate there is.</b> It is
    /// kept and presented, <see cref="LastError"/> and
    /// <see cref="OnRenewalFailed"/> say why the new one did not come, and it
    /// is tried again after <see cref="RetryInterval"/> - which a third of the
    /// lifetime leaves room for many times over.
    /// </para>
    /// </remarks>
    public sealed class ACMECertificateManager : IAsyncDisposable
    {

        #region Data

        /// <summary>The file of the certificate and its intermediates, as certbot calls it.</summary>
        public const String  FullChainFile   = "fullchain.pem";

        /// <summary>The file of its private key, as certbot calls it.</summary>
        public const String  PrivateKeyFile  = "privkey.pem";

        private readonly SemaphoreSlim             renewing      = new (1, 1);
        private readonly CancellationTokenSource   stopping      = new ();
        private          Task?                     loop;
        private volatile ACMECertificate?          current;
        private volatile String?                   lastError;
        private          ACMERenewalInfo?          renewalInfo;
        private          DateTimeOffset?           chosenRenewalTime;

        #endregion

        #region Properties

        /// <summary>
        /// The ACME client that orders the certificates.
        /// </summary>
        public ACMEClient                             Client                 { get; }

        /// <summary>
        /// The names and addresses the certificate is for.
        /// </summary>
        public IReadOnlyList<ACMEIdentifier>          Identifiers            { get; }

        /// <summary>
        /// What answers the challenges.
        /// </summary>
        public IReadOnlyList<IACMEChallengeResponder> Responders             { get; }

        /// <summary>
        /// The directory the certificate is kept in, if any.
        /// </summary>
        public String?                                Directory              { get; }

        /// <summary>
        /// The kind of key of every new certificate.
        /// </summary>
        public ACMEKeyType                            KeyType                { get; }

        /// <summary>
        /// The certificate profile, where the server offers several.
        /// </summary>
        public String?                                Profile                { get; }

        /// <summary>
        /// The share of a certificate's lifetime that is left when it is renewed,
        /// where the certificate authority does not say when.
        /// </summary>
        public Double                                 RenewWhenRemaining     { get; }

        /// <summary>
        /// How often to look whether the certificate is due.
        /// </summary>
        public TimeSpan                               CheckInterval          { get; }

        /// <summary>
        /// How long to wait after a renewal failed.
        /// </summary>
        public TimeSpan                               RetryInterval          { get; }

        /// <summary>
        /// The clock.
        /// </summary>
        public TimeProvider                           TimeProvider           { get; }

        /// <summary>
        /// Where a subscriber of an event that throws is reported, if anywhere.
        /// </summary>
        public ILogger?                               Logger                 { get; }

        /// <summary>
        /// The certificate there is now, or null while there has never been one.
        /// </summary>
        public ACMECertificate?                       Certificate
            => current;

        /// <summary>
        /// The chain to present now, or null while there has never been one.
        /// </summary>
        public ServerCertificateChain?                Current
            => current?.Chain;

        /// <summary>
        /// Why the last renewal did not bring a certificate, or null when it did.
        /// </summary>
        public String?                                LastError
            => lastError;

        /// <summary>
        /// The path of the certificate file, if there is a directory.
        /// </summary>
        public String?                                FullChainPath
            => Directory is not null ? Path.Combine(Directory, FullChainFile)  : null;

        /// <summary>
        /// The path of the key file, if there is a directory.
        /// </summary>
        public String?                                PrivateKeyPath
            => Directory is not null ? Path.Combine(Directory, PrivateKeyFile) : null;

        #endregion

        #region Events

        /// <summary>
        /// A new certificate came, and is presented from now on.
        /// </summary>
        public event Action<ACMECertificate>?  OnRenewed;

        /// <summary>
        /// A renewal failed; the certificate before stays.
        /// </summary>
        public event Action<String>?           OnRenewalFailed;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Keep a certificate for the given names. The certificate kept in the
        /// directory before, if any, is read here; nothing is asked of the
        /// certificate authority before <see cref="StartAsync"/> or
        /// <see cref="RenewAsync"/>.
        /// </summary>
        /// <param name="Client">The ACME client, with an account (ACMEClient.CreateAccountAsync).</param>
        /// <param name="Names">The DNS names and IP addresses the certificate is for.</param>
        /// <param name="Responders">What answers the challenges.</param>
        /// <param name="Directory">Where to keep fullchain.pem and privkey.pem; in memory only when null.</param>
        /// <param name="KeyType">The kind of key of every new certificate.</param>
        /// <param name="Profile">The certificate profile, where the server offers several.</param>
        /// <param name="RenewWhenRemaining">The share of the lifetime left at renewal, where the certificate authority does not say; a third by default.</param>
        /// <param name="CheckInterval">How often to look whether it is due; every six hours by default.</param>
        /// <param name="RetryInterval">How long to wait after a failure; an hour by default.</param>
        /// <param name="TimeProvider">The clock.</param>
        /// <param name="Logger">Where a subscriber of an event that throws is reported, if anywhere.</param>
        public ACMECertificateManager(ACMEClient                            Client,
                                      IEnumerable<String>                   Names,
                                      IEnumerable<IACMEChallengeResponder>  Responders,
                                      String?                               Directory            = null,
                                      ACMEKeyType                           KeyType              = ACMEKeyType.ECDSA_P256,
                                      String?                               Profile              = null,
                                      Double?                               RenewWhenRemaining   = null,
                                      TimeSpan?                             CheckInterval        = null,
                                      TimeSpan?                             RetryInterval        = null,
                                      TimeProvider?                         TimeProvider         = null,
                                      ILogger?                              Logger               = null)
        {

            this.Client              = Client ?? throw new ArgumentNullException(nameof(Client));
            this.Identifiers         = Names.Select(ACMEIdentifier.From).Distinct().ToArray();
            this.Responders          = Responders.ToArray();
            this.Directory           = Directory;
            this.KeyType             = KeyType;
            this.Profile             = Profile;
            this.RenewWhenRemaining  = RenewWhenRemaining ?? 1.0 / 3;
            this.CheckInterval       = CheckInterval      ?? TimeSpan.FromHours(6);
            this.RetryInterval       = RetryInterval      ?? TimeSpan.FromHours(1);
            this.TimeProvider        = TimeProvider       ?? TimeProvider.System;
            this.Logger              = Logger;

            if (this.Identifiers.Count == 0)
                throw new ArgumentException("A certificate is for at least one name.", nameof(Names));

            if (this.RenewWhenRemaining is <= 0 or >= 1)
                throw new ArgumentOutOfRangeException(nameof(RenewWhenRemaining), "The share of the lifetime left at renewal lies between 0 and 1.");

            LoadKept();

        }

        #endregion


        #region Selector(ServerName)

        /// <summary>
        /// What a TLS server asks at every handshake: the same certificate for
        /// every name.
        /// </summary>
        public ServerCertificateChain? Selector(String? ServerName)
            => Current;

        #endregion

        #region StartAsync(CancellationToken = default)

        /// <summary>
        /// Renew now if the certificate is due - or missing - and go on looking
        /// every <see cref="CheckInterval"/> in the background.
        /// </summary>
        /// <returns>Whether there is a certificate to present.</returns>
        public async Task<Boolean> StartAsync(CancellationToken CancellationToken = default)
        {

            if (loop is not null)
                return current is not null;

            await RenewIfDueAsync(CancellationToken);

            loop = Task.Run(() => LoopAsync(stopping.Token), CancellationToken.None);

            return current is not null;

        }

        #endregion

        #region WhenDueAsync(CancellationToken = default)

        /// <summary>
        /// When the certificate is to be renewed: now, where there is none or it
        /// is for other names than it should be; when the certificate authority
        /// suggests, where it does; when <see cref="RenewWhenRemaining"/> of its
        /// lifetime is left otherwise.
        /// </summary>
        public async Task<DateTimeOffset> WhenDueAsync(CancellationToken CancellationToken = default)
        {

            var now          = TimeProvider.GetUtcNow();
            var certificate  = current;

            if (certificate is null || !certificate.IsFor(Identifiers) || certificate.NotAfter <= now)
                return now;

            try
            {

                var info = await Client.GetRenewalInfoAsync(certificate.Certificate, CancellationToken);

                if (info is not null)
                {

                    // RFC 9773, section 4.2: a time chosen at random in the window - the
                    // same one as long as the window stays where it is.
                    if (renewalInfo is null || renewalInfo.Start != info.Start || renewalInfo.End != info.End || chosenRenewalTime is null)
                    {
                        renewalInfo        = info;
                        chosenRenewalTime  = info.Start + (info.End - info.Start) * Random.Shared.NextDouble();
                    }

                    return chosenRenewalTime.Value;

                }

            }
            catch (Exception e) when (e is ACMEException or HttpRequestException or TaskCanceledException && !CancellationToken.IsCancellationRequested)
            {
                // No window to be had: then by the certificate's own lifetime.
            }

            var lifetime = certificate.NotAfter - certificate.NotBefore;

            return certificate.NotAfter - lifetime * RenewWhenRemaining;

        }

        #endregion

        #region RenewIfDueAsync(CancellationToken = default)

        /// <summary>
        /// Renew the certificate if it is due.
        /// </summary>
        /// <returns>The new certificate, or null where it was not due or the renewal failed.</returns>
        /// <remarks>
        /// Whether it is due is asked once the renewal before - of the
        /// background loop, say - is over: otherwise both would find the old
        /// certificate due, and the second would replace the one the first had
        /// just fetched.
        /// </remarks>
        public async Task<ACMECertificate?> RenewIfDueAsync(CancellationToken CancellationToken = default)
        {

            await renewing.WaitAsync(CancellationToken);

            try
            {

                if (await WhenDueAsync(CancellationToken) > TimeProvider.GetUtcNow())
                    return null;

                return await RenewWhileLockedAsync(CancellationToken);

            }
            catch (Exception) when (!CancellationToken.IsCancellationRequested)
            {
                // Said already: LastError and OnRenewalFailed.
                return null;
            }
            finally
            {
                renewing.Release();
            }

        }

        #endregion

        #region RenewAsync(CancellationToken = default)

        /// <summary>
        /// Renew the certificate now, whether it is due or not.
        /// </summary>
        /// <exception cref="ACMEException">When it could not be had; the certificate before stays.</exception>
        public async Task<ACMECertificate> RenewAsync(CancellationToken CancellationToken = default)
        {

            await renewing.WaitAsync(CancellationToken);

            try
            {
                return await RenewWhileLockedAsync(CancellationToken);
            }
            finally
            {
                renewing.Release();
            }

        }

        #endregion

        #region (private) RenewWhileLockedAsync(CancellationToken)

        private async Task<ACMECertificate> RenewWhileLockedAsync(CancellationToken CancellationToken)
        {

            ACMECertificate certificate;

            try
            {

                var replacing    = current;

                certificate      = await Client.ObtainCertificateAsync(
                                             Identifiers.Select(identifier => identifier.Value),
                                             Responders,
                                             KeyType,
                                             Profile,
                                             replacing?.Certificate,
                                             CancellationToken
                                         );

                // The key first: a reader that sees the new chain beside the old key
                // keeps what it has, and finds both a moment later.
                if (Directory is not null)
                {
                    ACMEFiles.WritePrivate(PrivateKeyPath!, certificate.PrivateKeyPem);
                    ACMEFiles.Write       (FullChainPath!,  certificate.FullChainPem);
                }

                current            = certificate;
                lastError          = null;
                renewalInfo        = null;
                chosenRenewalTime  = null;

            }
            catch (Exception e) when (!CancellationToken.IsCancellationRequested)
            {

                lastError = e.Message;
                await OnRenewalFailed.InvokeAllAsync(handler => { handler(e.Message); return Task.CompletedTask; }, Logger);

                throw e as ACMEException ?? new ACMEException($"The certificate for {String.Join(", ", Identifiers)} could not be had: {e.Message}", e);

            }

            // Outside of the try: the renewal is done, and nothing a subscriber does
            // makes it fail - the certificate is there, and presented.
            await OnRenewed.InvokeAllAsync(handler => { handler(certificate); return Task.CompletedTask; }, Logger);

            return certificate;

        }

        #endregion


        #region (private) LoopAsync(CancellationToken)

        private async Task LoopAsync(CancellationToken CancellationToken)
        {

            while (!CancellationToken.IsCancellationRequested)
            {

                try
                {

                    var now    = TimeProvider.GetUtcNow();
                    var due    = await WhenDueAsync(CancellationToken);
                    var wait   = due - now;

                    if (wait <= TimeSpan.Zero)
                    {
                        wait = await RenewIfDueAsync(CancellationToken) is not null
                                   ? CheckInterval
                                   : RetryInterval;
                    }
                    else if (wait > CheckInterval)
                        wait = CheckInterval;

                    await Task.Delay(wait, TimeProvider, CancellationToken);

                }
                catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e)
                {
                    lastError = e.Message;
                    try
                    {
                        await Task.Delay(RetryInterval, TimeProvider, CancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }

            }

        }

        #endregion

        #region (private) LoadKept()

        /// <summary>
        /// The certificate in the directory, if there is one that can be read.
        /// One that cannot be read is no error here: it is what the next renewal
        /// replaces.
        /// </summary>
        private void LoadKept()
        {

            if (FullChainPath is null || PrivateKeyPath is null ||
                !File.Exists(FullChainPath) || !File.Exists(PrivateKeyPath))
                return;

            try
            {
                current = ACMECertificate.FromPem(File.ReadAllText(FullChainPath),
                                                  File.ReadAllText(PrivateKeyPath),
                                                  FullChainPath);
            }
            catch (Exception e)
            {
                lastError = $"The certificate kept in '{Directory}' cannot be used: {e.Message}";
            }

        }

        #endregion

        #region DisposeAsync()

        /// <summary>
        /// Stop looking.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            stopping.Cancel();

            if (loop is not null)
            {
                try
                {
                    await loop;
                }
                catch (OperationCanceledException)
                { }
            }

            stopping.Dispose();

        }

        #endregion

    }

}
