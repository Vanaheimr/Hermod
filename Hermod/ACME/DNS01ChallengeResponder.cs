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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.ACME
{

    /// <summary>
    /// Put a TXT record into a DNS zone, or take it out again.
    /// </summary>
    /// <param name="Name">The name of the record, as "_acme-challenge.time.example.org".</param>
    /// <param name="Value">Its text.</param>
    /// <param name="CancellationToken">To give up.</param>
    public delegate Task DNSTXTRecordDelegate(String             Name,
                                              String             Value,
                                              CancellationToken  CancellationToken);

    /// <summary>
    /// Whether a TXT record can be seen yet where the certificate authority
    /// will look - at every authoritative server of the zone, say.
    /// </summary>
    public delegate Task<Boolean> DNSTXTRecordVisibleDelegate(String             Name,
                                                             String             Value,
                                                             CancellationToken  CancellationToken);


    /// <summary>
    /// Answers dns-01 challenges (RFC 8555, section 8.4) through a DNS zone
    /// that the caller knows how to change: a TXT record
    /// "_acme-challenge.{name}" with the digest of the key authorization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The one challenge for a server nobody can reach.</b> The certificate
    /// authority asks the public DNS, not the server - so a time server in a
    /// local network, without a port 80 on the internet, gets a certificate
    /// for its public name all the same. And it is the only one for a
    /// wildcard certificate.
    /// </para>
    /// <para>
    /// <b>How the zone is changed is the caller's.</b> An API of a DNS provider,
    /// an RFC 2136 update, a file of a zone that is reloaded: whatever it is
    /// goes into <see cref="Publish"/> and <see cref="Withdraw"/>. A zone that
    /// is not to be given to a server at all can be left alone with a CNAME
    /// from "_acme-challenge.time.example.org" to a name in a zone kept only
    /// for this; the certificate authority follows it.
    /// </para>
    /// <para>
    /// <b>Waiting.</b> A record is not seen by the certificate authority the
    /// moment it is written: the zone's secondaries have to load it first.
    /// <see cref="IsVisible"/> is asked for every record until it says yes, and
    /// then <see cref="PropagationDelay"/> is waited, once per order.
    /// </para>
    /// </remarks>
    public sealed class DNS01ChallengeResponder : IACMEChallengeResponder
    {

        #region Data

        private readonly ConcurrentDictionary<ACMEChallengeTask, Byte> published = new ();

        #endregion

        #region Properties

        /// <inheritdoc/>
        public String                        ChallengeType       => "dns-01";

        /// <summary>
        /// Puts a TXT record into the zone.
        /// </summary>
        public DNSTXTRecordDelegate          Publish             { get; }

        /// <summary>
        /// Takes it out again.
        /// </summary>
        public DNSTXTRecordDelegate          Withdraw            { get; }

        /// <summary>
        /// Whether a record can be seen yet, if that can be found out.
        /// </summary>
        public DNSTXTRecordVisibleDelegate?  IsVisible           { get; }

        /// <summary>
        /// How long to wait after every record was published, and seen.
        /// </summary>
        public TimeSpan                      PropagationDelay    { get; }

        /// <summary>
        /// How long to ask <see cref="IsVisible"/> before giving up.
        /// </summary>
        public TimeSpan                      VisibilityTimeout   { get; }

        /// <summary>
        /// How often to ask it.
        /// </summary>
        public TimeSpan                      VisibilityInterval  { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Answer dns-01 challenges through the given delegates.
        /// </summary>
        /// <param name="Publish">Puts a TXT record into the zone.</param>
        /// <param name="Withdraw">Takes it out again.</param>
        /// <param name="IsVisible">Whether a record can be seen yet, if that can be found out.</param>
        /// <param name="PropagationDelay">How long to wait after every record was published, and seen; none by default.</param>
        /// <param name="VisibilityTimeout">How long to ask IsVisible before giving up; five minutes by default.</param>
        /// <param name="VisibilityInterval">How often to ask it; every five seconds by default.</param>
        public DNS01ChallengeResponder(DNSTXTRecordDelegate          Publish,
                                       DNSTXTRecordDelegate          Withdraw,
                                       DNSTXTRecordVisibleDelegate?  IsVisible            = null,
                                       TimeSpan?                     PropagationDelay     = null,
                                       TimeSpan?                     VisibilityTimeout    = null,
                                       TimeSpan?                     VisibilityInterval   = null)
        {

            this.Publish             = Publish  ?? throw new ArgumentNullException(nameof(Publish));
            this.Withdraw            = Withdraw ?? throw new ArgumentNullException(nameof(Withdraw));
            this.IsVisible           = IsVisible;
            this.PropagationDelay    = PropagationDelay   ?? TimeSpan.Zero;
            this.VisibilityTimeout   = VisibilityTimeout  ?? TimeSpan.FromMinutes(5);
            this.VisibilityInterval  = VisibilityInterval ?? TimeSpan.FromSeconds(5);

        }

        #endregion


        #region CanAnswer(Identifier, Wildcard)

        /// <inheritdoc/>
        public Boolean CanAnswer(ACMEIdentifier  Identifier,
                                 Boolean         Wildcard)

            => Identifier.Type == "dns";

        #endregion

        #region PrepareAsync(Challenge, CancellationToken)

        /// <inheritdoc/>
        public async Task PrepareAsync(ACMEChallengeTask  Challenge,
                                       CancellationToken  CancellationToken)
        {
            await Publish(Challenge.DNSRecordName, Challenge.DNSRecordValue, CancellationToken);
            published[Challenge] = 0;
        }

        #endregion

        #region AwaitPublicationAsync(CancellationToken)

        /// <inheritdoc/>
        public async Task AwaitPublicationAsync(CancellationToken CancellationToken)
        {

            if (IsVisible is not null)
            {

                using var timeout  = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                timeout.CancelAfter(VisibilityTimeout);

                foreach (var challenge in published.Keys)
                {
                    try
                    {
                        while (!await IsVisible(challenge.DNSRecordName, challenge.DNSRecordValue, timeout.Token))
                            await Task.Delay(VisibilityInterval, timeout.Token);
                    }
                    catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
                    {
                        throw new ACMEException($"The TXT record {challenge.DNSRecordName} could not be seen within {VisibilityTimeout}.");
                    }
                }

            }

            if (PropagationDelay > TimeSpan.Zero && !published.IsEmpty)
                await Task.Delay(PropagationDelay, CancellationToken);

        }

        #endregion

        #region CleanUpAsync(Challenge, CancellationToken)

        /// <inheritdoc/>
        public async Task CleanUpAsync(ACMEChallengeTask  Challenge,
                                       CancellationToken  CancellationToken)
        {
            if (published.TryRemove(Challenge, out _))
                await Withdraw(Challenge.DNSRecordName, Challenge.DNSRecordValue, CancellationToken);
        }

        #endregion

    }

}
