/*
 * Copyright (c) 2010-2014, GraphDefined GmbH
 * Author: Achim Friedland <achim.friedland@graphdefined.com>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using eu.Vanaheimr.Illias.Commons;
using eu.Vanaheimr.Illias.Commons.Collections;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    // In contrast to other popular Comet protocols such as Bayeux or BOSH, Server-Sent Events
    // support a unidirectional server-to-client channel only. The Bayeux protocol on the other
    // side supports a bidirectional communication channel. Furthermore, Bayeux can use HTTP
    // streaming as well as long polling. Like Bayeux, the BOSH protocol is a bidirectional
    // protocol. BOSH is based on the long polling approach.

    /// <summary>
    /// A HTTP event source.
    /// </summary>
    public class HTTPEventSource : IEnumerable<HTTPEvent>
    {

        #region Data

        private          Int64               IdCounter;
        private readonly TSQueue<HTTPEvent>  QueueOfEvents;

        #endregion

        #region Properties

        #region EventIdentification

        private readonly String _EventIdentification;

        /// <summary>
        /// The internal identification of the HTTP event.
        /// </summary>
        public String EventIdentification
        {
            get
            {
                return _EventIdentification;
            }
        }

        #endregion

        #region MaxNumberOfCachedEvents

        /// <summary>
        /// Maximum number of cached events.
        /// </summary>
        public UInt64 MaxNumberOfCachedEvents
        {

            get
            {
                return QueueOfEvents.MaxNumberOfElements;
            }

            set
            {
                QueueOfEvents.MaxNumberOfElements = value;
            }

        }

        #endregion

        #region RetryTime

        /// <summary>
        /// The retry time of this HTTP event.
        /// </summary>
        public TimeSpan RetryTime { get; private set; }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP event source.
        /// </summary>
        /// <param name="EventIdentification">The internal identification of the HTTP event.</param>
        public HTTPEventSource(String EventIdentification)
        {

            EventIdentification.FailIfNullOrEmpty();

            this._EventIdentification  = EventIdentification;
            this.QueueOfEvents         = new TSQueue<HTTPEvent>();
            this.RetryTime             = TimeSpan.FromSeconds(5);
            this.IdCounter             = 0;

        }

        #endregion


        #region SubmitEvent(Data)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="Data">The attached event data.</param>
        public void SubmitEvent(params String[] Data)
        {
            QueueOfEvents.Push(new HTTPEvent((UInt64) Interlocked.Increment(ref IdCounter), Data));
        }

        #endregion

        #region SubmitSubEvent(SubEvent, Data)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="Data">The attached event data.</param>
        public void SubmitSubEvent(String SubEvent, params String[] Data)
        {
            QueueOfEvents.Push(new HTTPEvent(SubEvent, (UInt64) Interlocked.Increment(ref IdCounter), Data));
        }

        #endregion


        #region SubmitSubEventWithTimestamp(SubEvent, Data)

        /// <summary>
        /// Submit a new subevent, using the current time as timestamp.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="Data">The attached event data.</param>
        public void SubmitSubEventWithTimestamp(String SubEvent, params String[] Data)
        {
            SubmitSubEventWithTimestamp(SubEvent, DateTime.Now, Data);
        }

        #endregion

        #region SubmitSubEventWithTimestamp(SubEvent, Timestamp, Data)

        /// <summary>
        /// Submit a new subevent with a timestamp.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="Timestamp">The timestamp of the event.</param>
        /// <param name="Data">The attached event data.</param>
        public void SubmitSubEventWithTimestamp(String SubEvent, DateTime Timestamp, params String[] Data)
        {

            SubmitSubEvent(SubEvent,
                           new JObject(
                               new JProperty("Timestamp", Timestamp),
                               new JProperty("Message",   Data.Aggregate((a, b) => a + " " + b))
                           ).
                           ToString().
                           Replace(Environment.NewLine, " ")
                          );

        }

        #endregion


        #region GetEvents(LastEventId = 0)

        /// <summary>
        /// Get a list of events filtered by the event id.
        /// </summary>
        /// <param name="LastEventId">The Last-Event-Id header value.</param>
        public IEnumerable<HTTPEvent> GetEvents(UInt64 LastEventId = 0)
        {

            return from    Events in QueueOfEvents
                   where   Events.Id > LastEventId
                   orderby Events.Id
                   select  Events;

        }

        #endregion

        #region GetEventsSince(Timestamp)

        /// <summary>
        /// Get a list of events filtered by a minimal timestamp.
        /// </summary>
        /// <param name="Timestamp">The earlierst timestamp of the events.</param>
        public IEnumerable<HTTPEvent> GetEventsSince(DateTime Timestamp)
        {

            return from    Events in QueueOfEvents
                   where   Events.Timestamp >= Timestamp
                   orderby Events.Timestamp
                   select  Events;

        }

        #endregion


        #region IEnumerable Members

        public IEnumerator<HTTPEvent> GetEnumerator()
        {
            return QueueOfEvents.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return QueueOfEvents.GetEnumerator();
        }

        #endregion

    }

}
