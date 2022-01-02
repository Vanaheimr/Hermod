/*
 * Copyright (c) 2010-2022, Achim Friedland <achim.friedland@graphdefined.com>
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
using System.Threading;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Warden
{

    /// <summary>
    /// The common interface of all Warden checks.
    /// </summary>
    public interface IWardenCheck
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

        /// <summary>
        /// Run this Warden check.
        /// </summary>
        /// <param name="CommonTimestamp">The common timestamp of all current/parallel Warden checks.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        /// <param name="CancellationToken">The cancellation token to use.</param>
        Task Run(DateTime           CommonTimestamp,
                 DNSClient          DNSClient,
                 CancellationToken  CancellationToken);

    }

    /// <summary>
    /// The common generic interface of all Warden checks.
    /// </summary>
    /// <typeparam name="TResult">The type of the Warden check result.</typeparam>
    public interface IWardenCheck<TResult> : IWardenCheck
    {

        /// <summary>
        /// An array of Warden check result consumers.
        /// </summary>
        Action<TResult>[]  ResultConsumers    { get; }

    }

}
