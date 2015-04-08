/*
 * Copyright (c) 2010-2015, GraphDefined GmbH
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
using System.Linq;
using System.Threading;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Illias.Collections;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
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

        #region RetryIntervall

        /// <summary>
        /// The retry intervall of this HTTP event.
        /// </summary>
        public TimeSpan RetryIntervall { get; set; }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP event source.
        /// </summary>
        /// <param name="EventIdentification">The internal identification of the HTTP event.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        public HTTPEventSource(String     EventIdentification,
                               UInt64     MaxNumberOfCachedEvents = 500,
                               TimeSpan?  RetryIntervall          = null)
        {

            EventIdentification.FailIfNullOrEmpty();

            this.QueueOfEvents            = new TSQueue<HTTPEvent>(MaxNumberOfCachedEvents);
            this._EventIdentification     = EventIdentification;
            this.MaxNumberOfCachedEvents  = MaxNumberOfCachedEvents;
            this.RetryIntervall           = (RetryIntervall.HasValue) ? RetryIntervall.Value : TimeSpan.FromSeconds(30);
            this.IdCounter                = 0;

        }

        #endregion


        #region SubmitEvent(params Data)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="Data">The attached event data.</param>
        public void SubmitEvent(params String[] Data)
        {
            QueueOfEvents.Push(new HTTPEvent((UInt64) Interlocked.Increment(ref IdCounter), Data));
        }

        #endregion

        #region SubmitTimestampedEvent(Timestamp, params Data)

        /// <summary>
        /// Submit a new subevent with a timestamp.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the event.</param>
        /// <param name="Data">The attached event data.</param>
        public void SubmitTimestampedEvent(DateTime Timestamp, params String[] Data)
        {

            SubmitSubEvent(new JObject(
                               new JProperty("Timestamp", Timestamp),
                               new JProperty("Message", Data.Aggregate((a, b) => a + " " + b))
                           ).
                           ToString().
                           Replace(Environment.NewLine, " ")
                          );

        }

        #endregion


        #region SubmitSubEvent(SubEvent, params Data)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="Data">The attached event data.</param>
        public void SubmitSubEvent(String SubEvent, params String[] Data)
        {
            if (SubEvent.IsNullOrEmpty())
                QueueOfEvents.Push(new HTTPEvent((UInt64) Interlocked.Increment(ref IdCounter), Data));
            else
                QueueOfEvents.Push(new HTTPEvent(SubEvent, (UInt64) Interlocked.Increment(ref IdCounter), Data));
        }

        #endregion

        #region SubmitTimestampedSubEvent(SubEvent, params Data)

        /// <summary>
        /// Submit a new subevent, using the current time as timestamp.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="Data">The attached event data.</param>
        public void SubmitSubEventWithTimestamp(String SubEvent, params String[] Data)
        {
            if (SubEvent.IsNullOrEmpty())
                SubmitTimestampedEvent(DateTime.Now, Data);
            else
                SubmitTimestampedSubEvent(SubEvent, DateTime.Now, Data);
        }

        #endregion

        #region SubmitTimestampedSubEvent(SubEvent, Timestamp, params Data)

        /// <summary>
        /// Submit a new subevent with a timestamp.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="Timestamp">The timestamp of the event.</param>
        /// <param name="Data">The attached event data.</param>
        public void SubmitTimestampedSubEvent(String SubEvent, DateTime Timestamp, params String[] Data)
        {

            if (SubEvent.IsNullOrEmpty())
                SubmitEvent(new JObject(
                                new JProperty("Timestamp", Timestamp),
                                new JProperty("Message",   Data.Aggregate((a, b) => a + " " + b))
                            ).
                            ToString().
                            Replace(Environment.NewLine, " ")
                           );

            else
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


        #region GetAllEventsGreater(LastEventId = 0)

        /// <summary>
        /// Get a list of events filtered by the event id.
        /// </summary>
        /// <param name="LastEventId">The Last-Event-Id header value.</param>
        public IEnumerable<HTTPEvent> GetAllEventsGreater(UInt64 LastEventId = 0)
        {

            return from    Events in QueueOfEvents
                   where   Events.Id > LastEventId
                   orderby Events.Id
                   select  Events;

        }

        #endregion

        #region GetAllEventsSince(Timestamp)

        /// <summary>
        /// Get a list of events filtered by a minimal timestamp.
        /// </summary>
        /// <param name="Timestamp">The earlierst timestamp of the events.</param>
        public IEnumerable<HTTPEvent> GetAllEventsSince(DateTime Timestamp)
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
