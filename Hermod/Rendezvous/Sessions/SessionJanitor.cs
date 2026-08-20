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
using Microsoft.Extensions.Logging.Abstractions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// Closes all rendezvous that ran into their rendezvous or idle timeout,
    /// again and again.
    ///
    /// This is a plain object with a timer, not a hosted service: a library must
    /// not force a hosting framework onto its users. A rendezvous manager creates
    /// one for itself unless <see cref="RendezvousOptions.AutoMaintenance"/> was
    /// disabled, and an application that prefers to drive the maintenance itself
    /// can do so by calling <see cref="RendezvousManager.Sweep"/> whenever it likes.
    /// </summary>
    public sealed class SessionJanitor : IAsyncDisposable
    {

        #region Data

        private readonly RendezvousManager         manager;
        private readonly TimeSpan                  interval;
        private readonly TimeProvider              timeProvider;
        private readonly ILogger                   logger;
        private readonly CancellationTokenSource   stopSource     = new();
        private readonly TaskCompletionSource      runningSource  = new (TaskCreationOptions.RunContinuationsAsynchronously);

        private          Int32                     started;
        private          Task?                     loopTask;

        #endregion

        #region Properties

        /// <summary>
        /// Whether this janitor is running.
        /// </summary>
        public Boolean IsRunning
            => Volatile.Read(ref started) != 0 && !stopSource.IsCancellationRequested;

        /// <summary>
        /// A task that completes as soon as this janitor watches the clock.
        /// Without it a test using a controllable clock could advance the time
        /// before the periodic timer was even created.
        /// </summary>
        internal Task Running
            => runningSource.Task;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new session janitor.
        /// </summary>
        /// <param name="Manager">The rendezvous manager to look after.</param>
        /// <param name="Interval">How often to look for timed out rendezvous.</param>
        /// <param name="TimeProvider">An optional time provider, e.g. for tests.</param>
        /// <param name="Logger">An optional logger.</param>
        public SessionJanitor(RendezvousManager  Manager,
                              TimeSpan           Interval,
                              TimeProvider?      TimeProvider   = null,
                              ILogger?           Logger         = null)
        {

            ArgumentNullException.ThrowIfNull(Manager);

            if (Interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(Interval), "The maintenance interval must be positive!");

            this.manager       = Manager;
            this.interval      = Interval;
            this.timeProvider  = TimeProvider  ?? System.TimeProvider.System;
            this.logger        = Logger        ?? NullLogger.Instance;

        }

        #endregion


        #region Start()

        /// <summary>
        /// Start looking for timed out rendezvous. Calling this more than once
        /// has no effect.
        /// </summary>
        public void Start()
        {

            if (Interlocked.Exchange(ref started, 1) != 0)
                return;

            loopTask = RunAsync(stopSource.Token);

        }

        #endregion

        #region StopAsync()

        /// <summary>
        /// Stop looking for timed out rendezvous and wait until the last sweep is done.
        /// </summary>
        public async Task StopAsync()
        {

            try
            {
                await stopSource.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            { }

            var loop = Volatile.Read(ref loopTask);

            if (loop is not null)
            {
                try
                {
                    await loop.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                { }
            }

        }

        #endregion

        #region (private) RunAsync(CancellationToken)

        private async Task RunAsync(CancellationToken CancellationToken)
        {

            logger.LogDebug("The session janitor is running every {Interval}.", interval);

            using var timer = new PeriodicTimer(interval, timeProvider);

            runningSource.TrySetResult();

            try
            {

                while (await timer.WaitForNextTickAsync(CancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        manager.Sweep();
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "The session janitor failed!");
                    }
                }

            }
            catch (OperationCanceledException)
            { }
            finally
            {
                runningSource.TrySetResult();
            }

        }

        #endregion


        #region DisposeAsync()

        /// <summary>
        /// Stop this janitor.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            await StopAsync().ConfigureAwait(false);

            stopSource.Dispose();

        }

        #endregion

    }

}
