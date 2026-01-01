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
    public partial class Warden
    {

        internal class WardenCheck<TResult> : IWardenCheck<TResult>
        {

            #region Data

            private ServiceCheckDelegate<TResult>  ServiceCheck    { get; }

            #endregion

            #region Properties

            /// <summary>
            /// A delegate for checking whether it is time to run a service check.
            /// </summary>
            public RunCheckDelegate   RunCheck           { get; }

            /// <summary>
            /// An additional sleeping time after every check.
            /// </summary>
            public TimeSpan           SleepTime          { get; }

            /// <summary>
            /// An entity to check.
            /// </summary>
            public Object             Entity             { get; }


            public Action<TResult>[]  ResultConsumers    { get; }

            /// <summary>
            /// The timestamp of the last run.
            /// </summary>
            public DateTimeOffset     LastRun            { private set; get; }

            #endregion

            #region Constructor(s)

            internal WardenCheck()
            { }

            #region WardenCheck(RunCheck, SleepTime, ServiceChecker)

            public WardenCheck(RunCheckDelegate                                        RunCheck,
                               TimeSpan                                                SleepTime,
                               Func<DateTimeOffset, CancellationToken, Task<TResult>>  ServiceChecker,
                               params Action<TResult>[]                                ResultConsumers)
            {

                this.RunCheck         = RunCheck;
                this.SleepTime        = SleepTime;
                this.ServiceCheck     = (ts, dns, obj, ct) => ServiceChecker(ts, ct);
                this.ResultConsumers  = ResultConsumers;

            }

            #endregion

            #region WardenCheck(RunCheck, SleepTime, ServiceChecker)

            public WardenCheck(RunCheckDelegate                                                    RunCheck,
                               TimeSpan                                                            SleepTime,
                               Func<DateTimeOffset, IDNSClient, CancellationToken, Task<TResult>>  ServiceChecker,
                               params Action<TResult>[]                                            ResultConsumers)
            {

                this.RunCheck         = RunCheck;
                this.SleepTime        = SleepTime;
                this.ServiceCheck     = (ts, dns, obj, ct) => ServiceChecker(ts, dns, ct);
                this.ResultConsumers  = ResultConsumers;

            }

            #endregion

            #region WardenCheck(RunCheck, SleepTime, Entity, ServiceChecker)

            public WardenCheck(RunCheckDelegate                                                RunCheck,
                               TimeSpan                                                        SleepTime,
                               Object                                                          Entity,
                               Func<DateTimeOffset, Object, CancellationToken, Task<TResult>>  ServiceChecker,
                               params Action<Object, TResult>[]                                ResultConsumers)
            {

                this.RunCheck         = RunCheck;
                this.SleepTime        = SleepTime;
                this.Entity           = Entity;
                this.ServiceCheck     = (ts, dns, obj, ct) => ServiceChecker(ts, obj, ct);
                //this.ResultConsumers  = new Action<TResult>[] { result => ResultConsumers.ForEach(consumer => consumer(Entity, result)) };
                var thisResultConsumers = new List<Action<TResult>>();

                foreach (var consumer in ResultConsumers)
                {
                    thisResultConsumers.Add(_result => consumer(Entity, _result));
                }

                this.ResultConsumers = thisResultConsumers.ToArray();

            }

            #endregion

            #region WardenCheck(RunCheck, SleepTime, Entity, ServiceChecker)

            public WardenCheck(RunCheckDelegate                                                            RunCheck,
                               TimeSpan                                                                    SleepTime,
                               Object                                                                      Entity,
                               Func<DateTimeOffset, IDNSClient, Object, CancellationToken, Task<TResult>>  ServiceChecker,
                               params Action<Object, TResult>[]                                            ResultConsumers)
            {

                this.RunCheck         = RunCheck;
                this.SleepTime        = SleepTime;
                this.Entity           = Entity;
                this.ServiceCheck     = (ts, dns, obj, ct) => ServiceChecker(ts, dns, obj, ct);

                var thisResultConsumers = new List<Action<TResult>>();

                foreach ( var consumer in ResultConsumers)
                {
                    thisResultConsumers.Add(_result => consumer(Entity, _result));
                }

                this.ResultConsumers = thisResultConsumers.ToArray();
                //this.ResultConsumers  = new Action<TResult>[] { result => ResultConsumers.ForEach(consumer => consumer(Entity, result)) };

            }

            #endregion

            #endregion


            #region Run(CommonTimestamp, DNSClient, CancellationToken)

            /// <summary>
            /// Run this Warden check.
            /// </summary>
            /// <param name="CommonTimestamp">The common timestamp of all current/parallel Warden checks.</param>
            /// <param name="DNSClient">The DNS client to use.</param>
            /// <param name="CancellationToken">The cancellation token to use.</param>
            public Task Run(DateTimeOffset     CommonTimestamp,
                            IDNSClient         DNSClient,
                            CancellationToken  CancellationToken)

            {

                if (CommonTimestamp >= LastRun + SleepTime)
                {

                    try
                    {

                        LastRun          = CommonTimestamp;

                        var result       = ServiceCheck(CommonTimestamp, DNSClient, Entity, CancellationToken);

                        //foreach (var consumer in ResultConsumers)
                        //{

                        //    try
                        //    {
                        //        consumer(result.Result);
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        DebugX.LogException(ex);
                        //    }

                        //}

                        var resultTasks  = ResultConsumers.Select(consumer => result.ContinueWith(task => {
                                               try
                                               {
                                                   consumer(task.Result);
                                               }
                                               catch (Exception e)
                                               {

                                                   while (e.InnerException is not null)
                                                       e = e.InnerException;

                                                   DebugX.LogException(e, nameof(WardenCheck));

                                               }
                                           })).ToArray();

                        return Task.WhenAll(resultTasks);

                    }
                    catch (Exception e)
                    {

                        while (e.InnerException is not null)
                            e = e.InnerException;

                        return Task.FromException(e);

                    }

                }

                return Task.FromResult(false);

            }

            #endregion

        }

        internal class WardenCheck : WardenCheck<Boolean>
        {

            #region WardenCheck(RunCheck, SleepTime, ServiceChecker)

            public WardenCheck(RunCheckDelegate                               RunCheck,
                               TimeSpan                                       SleepTime,
                               Func<DateTimeOffset, CancellationToken, Task>  ServiceChecker)

                : base(RunCheck,
                       SleepTime,
                       (ts, ct) => {
                           ServiceChecker(ts, ct);
                           return Task.FromResult(false);
                       })

            { }

            #endregion

            #region WardenCheck(RunCheck, SleepTime, ServiceChecker)

            public WardenCheck(RunCheckDelegate                                           RunCheck,
                               TimeSpan                                                   SleepTime,
                               Func<DateTimeOffset, IDNSClient, CancellationToken, Task>  ServiceChecker)

                : base(RunCheck,
                       SleepTime,
                       (ts, dns, ct) => {
                           ServiceChecker(ts, dns, ct);
                           return Task.FromResult(false);
                       })

            { }

            #endregion

            #region WardenCheck(RunCheck, SleepTime, Entity, ServiceChecker)

            public WardenCheck(RunCheckDelegate                                       RunCheck,
                               TimeSpan                                               SleepTime,
                               Object                                                 Entity,
                               Func<DateTimeOffset, Object, CancellationToken, Task>  ServiceChecker)

                : base(RunCheck,
                       SleepTime,
                       Entity,
                       (ts, obj, ct) => {
                           ServiceChecker(ts, obj, ct);
                           return Task.FromResult(false);
                       })

            { }

            #endregion

            #region WardenCheck(RunCheck, SleepTime, Entity, ServiceChecker)

            public WardenCheck(RunCheckDelegate      RunCheck,
                               TimeSpan              SleepTime,
                               Object                Entity,
                               ServiceCheckDelegate  ServiceChecker)

                : base(RunCheck,
                       SleepTime,
                       Entity,
                       (ts, dns, obj, ct) => {
                           ServiceChecker(ts, dns, obj, ct);
                           return Task.FromResult(false);
                       })

            { }

            #endregion

        }

    }

}
