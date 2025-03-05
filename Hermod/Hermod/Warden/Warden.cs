/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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


        /// <summary>
        /// The default initial delay before the first Warden checks will be run.
        /// </summary>
        public static readonly TimeSpan             DefaultInitialDelay  = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The default delay between the runs of the Warden check timer.
        /// </summary>
        public static readonly TimeSpan             DefaultCheckEvery    = TimeSpan.FromSeconds(10);

        #endregion

        #region Properties

        /// <summary>
        /// An enumeration of all Warden checks.
        /// </summary>
        public IEnumerable<IWardenCheck> AllWardenChecks
            => AllWardenChecks;

        /// <summary>
        /// The Warden check properties.
        /// </summary>
        public WardenProperties  WardenCheckProperties    { get; }

        /// <summary>
        /// Run the warden checks every...
        /// </summary>
        public TimeSpan          CheckEvery               { get; }

        /// <summary>
        /// The initial start-up delay.
        /// </summary>
        public TimeSpan          InitialDelay             { get; }


        /// <summary>
        /// An optional DNS client for warden checks requiring network access.
        /// </summary>
        public DNSClient         DNSClient                { get; }

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

            this.InitialDelay           = InitialDelay ?? DefaultInitialDelay;
            this.CheckEvery             = CheckEvery   ?? DefaultCheckEvery;
            this.DNSClient              = DNSClient    ?? new DNSClient();

            this._AllWardenChecks       = new List<IWardenCheck>();
            this.WardenCheckProperties  = new WardenProperties();

            this.ServiceCheckLock       = new Object();

            this.ServiceCheckTimer      = new Timer(RunWardenChecks,
                                                    null,
                                                    this.InitialDelay,
                                                    this.CheckEvery);

        }

        #endregion


        #region (private, Timer) RunWardenChecks(Status)

        private void RunWardenChecks(Object Status)
        {

            //DebugX.LogT("'Warden' called!");

            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            if (Monitor.TryEnter(ServiceCheckLock))
            {

                try
                {

                    var Now       = Timestamp.Now;
                    var TS        = new CancellationTokenSource();

                    var allTasks  = _AllWardenChecks.Where (check => check.RunCheck(Now, WardenCheckProperties)).
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

                    if (allTasks.Length > 0)
                        Task.WaitAll(allTasks);

                    //ToDo: Log exceptions!

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

            //else
            //    DebugX.LogT("'Warden' skipped!");

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

    }

}
