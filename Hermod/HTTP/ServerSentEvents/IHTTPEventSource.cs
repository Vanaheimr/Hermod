/*
 * Copyright (c) 2010-2021, GraphDefined GmbH
 * Author: Achim Friedland <achim.friedland@graphdefined.com>
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
using System.Threading.Tasks;
using System.Collections.Generic;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public interface IHTTPEventSource
    {

        HTTPEventSource_Id              EventIdentification        { get; }
        Func<String, DateTime, String>  LogfileName                { get; }
        UInt64                          MaxNumberOfCachedEvents    { get; }
        TimeSpan                        RetryIntervall             { get; set; }

        String ToString();

    }

    public interface IHTTPEventSource<T> : IHTTPEventSource, IEnumerable<HTTPEvent<T>>
    {

        Task SubmitEvent(                                     T Data);
        Task SubmitEvent(String SubEvent,                     T Data);
        Task SubmitEvent(                 DateTime Timestamp, T Data);
        Task SubmitEvent(String SubEvent, DateTime Timestamp, T Data);


        /// <summary>
        /// Get a list of events filtered by the event id.
        /// </summary>
        /// <param name="LastEventId">The Last-Event-Id header value.</param>
        IEnumerable<HTTPEvent<T>> GetAllEventsGreater(UInt64? LastEventId = 0);

        /// <summary>
        /// Get a list of events filtered by a minimal timestamp.
        /// </summary>
        /// <param name="Timestamp">The earlierst timestamp of the events.</param>
        IEnumerable<HTTPEvent<T>> GetAllEventsSince(DateTime Timestamp);


        String ToString();

    }

}