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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Warden
{

    /// <summary>
    /// A warden for service checking and monitoring.
    /// </summary>
    public partial class Warden : IDisposable,
                                  IAsyncDisposable
    {

        #region Data

        private readonly        List<IWardenCheck>   allWardenChecks;

        //private readonly        Lock                 ServiceCheckLock;
        private readonly        Timer                ServiceCheckTimer;
        private readonly        SemaphoreSlim        ServiceCheckLock     = new (1, 1);


        /// <summary>
        /// The default initial delay before the first Warden checks will be run.
        /// </summary>
        public static readonly  TimeSpan             DefaultInitialDelay  = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The default delay between the runs of the Warden check timer.
        /// </summary>
        public static readonly  TimeSpan             DefaultCheckEvery    = TimeSpan.FromSeconds(10);

        private                 Boolean              disposed;

        #endregion

        #region Properties

        public String                     Id                       { get; }

        /// <summary>
        /// An enumeration of all Warden checks.
        /// </summary>
        public IEnumerable<IWardenCheck>  AllWardenChecks
            => AllWardenChecks;

        /// <summary>
        /// The Warden check properties.
        /// </summary>
        public WardenProperties           WardenCheckProperties    { get; }

        /// <summary>
        /// Run the warden checks every...
        /// </summary>
        public TimeSpan                   CheckEvery               { get; }

        /// <summary>
        /// The initial start-up delay.
        /// </summary>
        public TimeSpan                   InitialDelay             { get; }


        /// <summary>
        /// An optional DNS client for warden checks requiring network access.
        /// </summary>
        public IDNSClient                 DNSClient                { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new warden.
        /// </summary>
        /// <param name="InitialDelay">The initial start-up delay.</param>
        /// <param name="CheckEvery">Run the warden checks every...</param>
        /// <param name="DNSClient">An optional DNS client for warden checks requiring network access.</param>
        public Warden(String       Id,
                      TimeSpan?    InitialDelay   = null,
                      TimeSpan?    CheckEvery     = null,
                      IDNSClient?  DNSClient      = null)
        {

            this.Id                     = Id;
            this.InitialDelay           = InitialDelay ?? DefaultInitialDelay;
            this.CheckEvery             = CheckEvery   ?? DefaultCheckEvery;
            this.DNSClient              = DNSClient    ?? new DNSClient();

            this.allWardenChecks        = [];
            this.WardenCheckProperties  = new WardenProperties();

            //this.ServiceCheckLock       = new Lock();

            this.ServiceCheckTimer      = new Timer(
                                              state => Task.Run(() => RunWardenChecks(state)).GetAwaiter().GetResult(),
                                              null,
                                              this.InitialDelay,
                                              this.CheckEvery
                                          );

        }

        #endregion


        #region (private, Timer) RunWardenChecks(Status)

        private async Task RunWardenChecks(Object? Status)
        {

            DebugX.LogT($"Warden '{Id}' called!");

            //Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            if (await ServiceCheckLock.WaitAsync(0).ConfigureAwait(false))
            {

                try
                {

                    DebugX.LogT($"Warden '{Id}' entered!");

                    var Now       = Timestamp.Now;
                    var TS        = new CancellationTokenSource();

                    //var allTasks  = allWardenChecks.Where (check => check.RunCheck(Now, WardenCheckProperties)).
                    //                                Select(check => {
                    //                                    try
                    //                                    {
                    //                                        return check.Run(Now, DNSClient, TS.Token);
                    //                                    }
                    //                                    catch (Exception e)
                    //                                    {

                    //                                        while (e.InnerException is not null)
                    //                                            e = e.InnerException;

                    //                                        return Task.FromException(e);

                    //                                    }
                    //                                }).
                    //                                Where (task  => task is not null).
                    //                                ToArray();

                    //if (allTasks.Length > 0)
                    //    Task.WaitAll(allTasks);

                    //ToDo: Log exceptions!

                    var allTasks = new List<Task>();

                    foreach (var check in allWardenChecks)
                    {

                        try
                        {

                            var shouldRunNow = check.RunCheck(Now, WardenCheckProperties);

                            if (shouldRunNow)
                            {
                                try
                                {
                                    allTasks.Add(
                                        check.Run(
                                            Now,
                                            DNSClient,
                                            TS.Token
                                        )
                                    );
                                }
                                catch (Exception e)
                                {
                                    DebugX.LogT($"Warden '{Id}': {e.Message}{Environment.NewLine}{e.StackTrace}");
                                }
                            }

                        }
                        catch (Exception e)
                        {
                            DebugX.LogT($"Warden '{Id}': {e.Message}{Environment.NewLine}{e.StackTrace}");
                        }

                    }

                    Task.WaitAll(allTasks);

                }
                catch (Exception e)
                {
                    DebugX.LogT($"Warden '{Id}' led to an exception: {e.Message}{Environment.NewLine}{e.StackTrace}");
                }
                finally
                {
                    DebugX.LogT($"Warden '{Id}' exited!");
                    ServiceCheckLock.Release();
                }

            }

            else
                DebugX.LogT($"Warden '{Id}' skipped!");

        }

        #endregion


        #region Check(RunCheck, SleepTime, ServiceChecker, ...)

        public Warden Check(RunCheckDelegate                               RunCheck,
                            TimeSpan                                       SleepTime,
                            Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)
        {

            allWardenChecks.Add(
                new WardenCheck(
                    RunCheck,
                    SleepTime,
                    ServiceChecker
                )
            );

            return this;

        }

        public Warden Check<TResult>(RunCheckDelegate                                        RunCheck,
                                     TimeSpan                                                SleepTime,
                                     Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                                     params Action<TResult>[]                                ResultConsumers)
        {

            allWardenChecks.Add(
                new WardenCheck<TResult>(
                    RunCheck,
                    SleepTime,
                    ServiceChecker,
                    ResultConsumers
                )
            );

            return this;

        }

        #endregion

        #region Check(RunCheck, SleepTime, ServiceChecker, ...)

        public Warden Check(RunCheckDelegate                                           RunCheck,
                            TimeSpan                                                   SleepTime,
                            Func<DateTimeOffset, IDNSClient, CancellationToken, Task>  ServiceChecker)
        {

            allWardenChecks.Add(
                new WardenCheck(
                    RunCheck,
                    SleepTime,
                    ServiceChecker
                )
            );

            return this;

        }

        public Warden Check<TResult>(RunCheckDelegate                                                    RunCheck,
                                     TimeSpan                                                            SleepTime,
                                     Func<DateTimeOffset, IDNSClient, CancellationToken, Task<TResult>>  ServiceChecker,
                                     params Action<TResult>[]                                            ResultConsumers)
        {

            allWardenChecks.Add(
                new WardenCheck<TResult>(
                    RunCheck,
                    SleepTime,
                    ServiceChecker,
                    ResultConsumers
                )
            );

            return this;

        }

        #endregion

        #region Check(RunCheck, SleepTime, Entity, ServiceChecker)

        public Warden Check<TEntity>(RunCheckDelegate                                        RunCheck,
                                     TimeSpan                                                SleepTime,
                                     TEntity                                                 Entity,
                                     Func<DateTimeOffset, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

        {

            allWardenChecks.Add(
                new WardenCheck(
                    RunCheck,
                    SleepTime,
                    Entity,
                    (ts, obj, ct) => ServiceChecker(ts, obj as TEntity, ct)
                )
            );

            return this;

        }

        public Warden Check<TEntity, TResult>(RunCheckDelegate                                                 RunCheck,
                                              TimeSpan                                                         SleepTime,
                                              TEntity                                                          Entity,
                                              Func<DateTimeOffset, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                              params Action<TEntity, TResult>[]                                ResultConsumers)

            where TEntity : class

        {

            var thisResultConsumers = new List<Action<Object, TResult>>();

            foreach (var consumer in ResultConsumers)
            {
                thisResultConsumers.Add(
                    (entity, _result) => consumer(Entity, _result)
                );
            }

            allWardenChecks.Add(
                new WardenCheck<TResult>(
                    RunCheck,
                    SleepTime,
                    Entity,
                    (ts, obj, ct)    => ServiceChecker(ts, obj as TEntity, ct),
                    thisResultConsumers.ToArray()
                )
            );

            return this;

        }

        #endregion

        #region Check(RunCheck, SleepTime, Entity, ServiceChecker)

        public Warden Check<TEntity>(RunCheckDelegate                                                    RunCheck,
                                     TimeSpan                                                            SleepTime,
                                     TEntity                                                             Entity,
                                     Func<DateTimeOffset, IDNSClient, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

        {

            allWardenChecks.Add(
                new WardenCheck(
                    RunCheck,
                    SleepTime,
                    Entity,
                    (ts, dns, obj, ct) => ServiceChecker(ts, dns, obj as TEntity, ct)
                )
            );

            return this;

        }

        public Warden Check<TEntity, TResult>(RunCheckDelegate                                                             RunCheck,
                                              TimeSpan                                                                     SleepTime,
                                              TEntity                                                                      Entity,
                                              Func<DateTimeOffset, IDNSClient, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                              params Action<TEntity, TResult>[]                                            ResultConsumers)

            where TEntity : class

        {

            var thisResultConsumers = new List<Action<Object, TResult>>();

            foreach (var consumer in ResultConsumers)
            {
                thisResultConsumers.Add(
                    (entity, _result) => consumer(Entity, _result)
                );
            }


            allWardenChecks.Add(
                new WardenCheck<TResult>(
                    RunCheck,
                    SleepTime,
                    Entity,
                    (ts, dns, obj, ct) => ServiceChecker(ts, dns, obj as TEntity, ct),
                    thisResultConsumers.ToArray()
                )
            );

            return this;

        }

        #endregion



        public WardenProperties Set(String Key, Object Value)
        {
            return WardenCheckProperties.Set(Key, Value);
        }

        public Object Get(String Key)
        {
            return WardenCheckProperties.Get(Key);
        }

        public WardenProperties Remove(String Key)
        {
            return WardenCheckProperties.Remove(Key);
        }



        public async ValueTask DisposeAsync()
        {
            await DisposeAsync(true).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsync(Boolean disposing)
        {

            if (disposed)
                return;

            if (disposing)
            {
                try
                {
                    // Stop new callbacks
                    ServiceCheckTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                }
                catch
                {
                    // Ignore timer change exceptions during disposal
                }

                // Ensure all timer callbacks are completed before continuing
                if (ServiceCheckTimer is IAsyncDisposable asyncTimer)
                {
                    await asyncTimer.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    using var mre = new ManualResetEvent(false);
                    ServiceCheckTimer?.Dispose(mre);
                    await Task.Run(() => mre.WaitOne()).ConfigureAwait(false);
                }

                // Dispose DNS client
                if (DNSClient is IAsyncDisposable asyncDNSClient)
                    await asyncDNSClient.DisposeAsync().ConfigureAwait(false);
                else
                    DNSClient?.Dispose();

                // Dispose semaphore
                ServiceCheckLock?.Dispose();

            }

            disposed = true;
        }



        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(Boolean disposing)
        {

            if (disposed)
                return;

            if (disposing)
            {
                // Dispose managed resources
                ServiceCheckTimer?.Dispose(new ManualResetEvent(true)); // Wait for callbacks to complete
                ServiceCheckLock?. Dispose();
            }

            // No unmanaged resources to clean up in this case
            // If DNSClient or allWardenChecks hold unmanaged resources, dispose them here

            disposed = true;

        }

        ~Warden()
        {
            Dispose(false);
        }

    }

}
