/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

        internal interface IWardenCheck
        {

            #region Properties

            /// <summary>
            /// A delegate for checking whether it is time to run a serive check.
            /// </summary>
            RunCheckDelegate   RunCheck    { get; }

            /// <summary>
            /// An additional sleeping time after every check.
            /// </summary>
            TimeSpan           SleepTime   { get; }

            /// <summary>
            /// An entity to check.
            /// </summary>
            Object             Entity      { get; }

            /// <summary>
            /// The timestamp of the last run.
            /// </summary>
            DateTime           LastRun     { get; }

            #endregion

            Task Run(DateTime           Timestamp,
                     DNSClient          DNSClient,
                     CancellationToken  CancellationToken);

        }

        internal interface IWardenCheck<TResult> : IWardenCheck
        {

            Action<TResult>[] ResultConsumers { get; }

        }

    }

}
