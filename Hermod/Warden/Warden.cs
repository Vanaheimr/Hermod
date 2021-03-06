﻿/*
 * Copyright (c) 2010-2021, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Warden
{

    /// <summary>
    /// A warden for service checking and monitoring.
    /// </summary>
    public partial class Warden
    {

        #region Data

        private readonly       List<IWardenCheck>   _AllWardenChecks;

        private readonly       Object               ServiceCheckLock;
        private readonly       Timer                ServiceCheckTimer;

        public static readonly TimeSpan             DefaultInitialDelay  = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan             DefaultCheckEvery    = TimeSpan.FromSeconds(10);

        private readonly       WardenProperties     _Properties;

        #endregion

        #region Properties

        /// <summary>
        /// Run the warden checks every...
        /// </summary>
        public TimeSpan   CheckEvery    { get; }

        /// <summary>
        /// The initial start-up delay.
        /// </summary>
        public TimeSpan   InitialDelay  { get; }


        /// <summary>
        /// An optional DNS client for warden checks requiring network access.
        /// </summary>
        public DNSClient  DNSClient     { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new warden.
        /// </summary>
        /// <param name="InitialDelay">The initial start-up delay.</param>
        /// <param name="CheckEvery">Run the warden checks every...</param>
        /// <param name="DNSClient">An optional DNS client for warden checks requiring network access.</param>
        public Warden(TimeSpan?  InitialDelay  = null,
                      TimeSpan?  CheckEvery    = null,
                      DNSClient  DNSClient     = null)
        {

            this.InitialDelay      = InitialDelay ?? DefaultInitialDelay;
            this.CheckEvery        = CheckEvery   ?? DefaultCheckEvery;
            this.DNSClient         = DNSClient    ?? new DNSClient(SearchForIPv6DNSServers: false);

            this._AllWardenChecks  = new List<IWardenCheck>();
            this._Properties       = new WardenProperties();

            ServiceCheckLock       = new Object();

            ServiceCheckTimer      = new Timer(RunWardenChecks,
                                               null,
                                               this.InitialDelay,
                                               this.CheckEvery);

        }

        #endregion


        #region (private, Timer) RunWardenChecks(Status)

        private void RunWardenChecks(Object Status)
        {

            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            if (Monitor.TryEnter(ServiceCheckLock))
            {

                var StopWatch = new Stopwatch();
                StopWatch.Start();

                try
                {

                    var Now  = DateTime.UtcNow;
                    var TS   = new CancellationTokenSource();

                    var AllTasks = _AllWardenChecks.Where (check => check.RunCheck(Now, _Properties)).
                                                    Select(check =>
                                                    {
                                                        try
                                                        {
                                                            return check.Run(Now, DNSClient, TS.Token);
                                                        }
                                                        catch (Exception e)
                                                        {

                                                            while (e.InnerException != null)
                                                                e = e.InnerException;

                                                            return Task.FromException(e);

                                                        }
                                                    }).
                                                    Where (task  => task != null).
                                                    ToArray();

                    if (AllTasks.Length > 0)
                        Task.WaitAll(AllTasks);

                    //ToDo: Log exceptions!

                    #region Debug info

                    #if DEBUG

                    StopWatch.Stop();

                 //   DebugX.LogT("'Warden' finished after " + StopWatch.Elapsed.TotalSeconds + " seconds!");

                    #endif

                    #endregion

                }
                catch (Exception e)
                {
                    DebugX.LogT("'Warden' led to an exception: " + e.Message + Environment.NewLine + e.StackTrace);
                }

                finally
                {
                    Monitor.Exit(ServiceCheckLock);
                }

            }

            else
                DebugX.LogT("'Warden' skipped!");

        }

        #endregion


        #region Check(RunCheck, SleepTime, ServiceChecker, ...)

        public Warden Check(RunCheckDelegate                         RunCheck,
                            TimeSpan                                 SleepTime,
                            Func<DateTime, CancellationToken, Task>  ServiceChecker)
        {

            _AllWardenChecks.Add(new WardenCheck(RunCheck,
                                                 SleepTime,
                                                 ServiceChecker));

            return this;

        }

        public Warden Check<TResult>(RunCheckDelegate                                  RunCheck,
                                     TimeSpan                                          SleepTime,
                                     Func<DateTime, CancellationToken, Task<TResult>>  ServiceChecker,
                                     params Action<TResult>[]                          ResultConsumers)
        {

            _AllWardenChecks.Add(new WardenCheck<TResult>(RunCheck,
                                                          SleepTime,
                                                          ServiceChecker,
                                                          ResultConsumers));

            return this;

        }

        #endregion

        #region Check(RunCheck, SleepTime, ServiceChecker, ...)

        public Warden Check(RunCheckDelegate                                    RunCheck,
                            TimeSpan                                            SleepTime,
                            Func<DateTime, DNSClient, CancellationToken, Task>  ServiceChecker)
        {

            _AllWardenChecks.Add(new WardenCheck(RunCheck,
                                                 SleepTime,
                                                 ServiceChecker));

            return this;

        }

        public Warden Check<TResult>(RunCheckDelegate                                             RunCheck,
                                     TimeSpan                                                     SleepTime,
                                     Func<DateTime, DNSClient, CancellationToken, Task<TResult>>  ServiceChecker,
                                     params Action<TResult>[]                                     ResultConsumers)
        {

            _AllWardenChecks.Add(new WardenCheck<TResult>(RunCheck,
                                                          SleepTime,
                                                          ServiceChecker,
                                                          ResultConsumers));

            return this;

        }

        #endregion

        #region Check(RunCheck, SleepTime, Entity, ServiceChecker)

        public Warden Check<TEntity>(RunCheckDelegate                                  RunCheck,
                                     TimeSpan                                          SleepTime,
                                     TEntity                                           Entity,
                                     Func<DateTime, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

        {

            _AllWardenChecks.Add(new WardenCheck(RunCheck,
                                                 SleepTime,
                                                 Entity,
                                                 (ts, obj, ct) => ServiceChecker(ts, obj as TEntity, ct)));

            return this;

        }

        public Warden Check<TEntity, TResult>(RunCheckDelegate                                           RunCheck,
                                              TimeSpan                                                   SleepTime,
                                              TEntity                                                    Entity,
                                              Func<DateTime, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                              params Action<TEntity, TResult>[]                          ResultConsumers)

            where TEntity : class

        {

            var thisResultConsumers = new List<Action<Object, TResult>>();

            foreach (var consumer in ResultConsumers)
            {
                thisResultConsumers.Add((entity, _result) => consumer(Entity, _result));
            }

            _AllWardenChecks.Add(new WardenCheck<TResult>(RunCheck,
                                                          SleepTime,
                                                          Entity,
                                                          (ts, obj, ct)    => ServiceChecker(ts, obj as TEntity, ct),
                                                          thisResultConsumers.ToArray()
                                                         ));

            return this;

        }

        #endregion

        #region Check(RunCheck, SleepTime, Entity, ServiceChecker)

        public Warden Check<TEntity>(RunCheckDelegate                                             RunCheck,
                                     TimeSpan                                                     SleepTime,
                                     TEntity                                                      Entity,
                                     Func<DateTime, DNSClient, TEntity, CancellationToken, Task>  ServiceChecker)

            where TEntity : class

        {

            _AllWardenChecks.Add(new WardenCheck(RunCheck,
                                                 SleepTime,
                                                 Entity,
                                                 (ts, dns, obj, ct) => ServiceChecker(ts, dns, obj as TEntity, ct)));

            return this;

        }

        public Warden Check<TEntity, TResult>(RunCheckDelegate                                                      RunCheck,
                                              TimeSpan                                                              SleepTime,
                                              TEntity                                                               Entity,
                                              Func<DateTime, DNSClient, TEntity, CancellationToken, Task<TResult>>  ServiceChecker,
                                              params Action<TEntity, TResult>[]                                     ResultConsumers)

            where TEntity : class

        {

            var thisResultConsumers = new List<Action<Object, TResult>>();

            foreach (var consumer in ResultConsumers)
            {
                thisResultConsumers.Add((entity, _result) => consumer(Entity, _result));
            }


            _AllWardenChecks.Add(new WardenCheck<TResult>(RunCheck,
                                                          SleepTime,
                                                          Entity,
                                                          (ts, dns, obj, ct) => ServiceChecker(ts, dns, obj as TEntity, ct),
                                                          thisResultConsumers.ToArray()
                                                         ));

            return this;

        }

        #endregion



        public WardenProperties Set(String Key, Object Value)
        {
            return _Properties.Set(Key, Value);
        }

        public Object Get(String Key)
        {
            return _Properties.Get(Key);
        }

        public WardenProperties Remove(String Key)
        {
            return _Properties.Remove(Key);
        }

    }

}
