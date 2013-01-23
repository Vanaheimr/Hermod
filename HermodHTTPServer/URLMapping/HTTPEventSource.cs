/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
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
using System.Net;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

using de.ahzf.Vanaheimr.Hermod.Sockets.TCP;
using de.ahzf.Vanaheimr.Hermod.Datastructures;
using de.ahzf.Illias.Commons.Collections;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.HTTP
{

    // In contrast to other popular Comet protocols such as Bayeux or BOSH, Server-Sent Events
    // support a unidirectional server-to-client channel only. The Bayeux protocol on the other
    // side supports a bidirectional communication channel. Furthermore, Bayeux can use HTTP
    // streaming as well as long polling. Like Bayeux, the BOSH protocol is a bidirectional
    // protocol. BOSH is based on the long polling approach.

    /// <summary>
    /// A HTTP event source.
    /// </summary>
    public class HTTPEventSource
    {

        #region Data

        private          Int64              IdCounter;
        private readonly TSQueue<HTTPEvent> ListOfEvents;

        #endregion

        #region Properties

        /// <summary>
        /// The internal identification of the HTTP event.
        /// </summary>
        public String EventIdentification     { get; private set; }

        /// <summary>
        /// Maximum number of cached events.
        /// </summary>
        public UInt64 MaxNumberOfCachedEvents
        {
            
            get
            {
                return ListOfEvents.MaxNumberOfElements;
            }

            set
            {
                ListOfEvents.MaxNumberOfElements = value;
            }

        }

        #endregion

        #region Constructor(s)

        #region HTTPEventSource()

        /// <summary>
        /// Create a new HTTP event source.
        /// </summary>
        /// <param name="EventIdentification">The internal identification of the HTTP event.</param>
        public HTTPEventSource(String EventIdentification)
        {

            if (EventIdentification == null || EventIdentification == "")
                throw new ArgumentNullException("The EventIdentification must not be null or zero!");

            this.EventIdentification = EventIdentification;
            this.ListOfEvents        = new TSQueue<HTTPEvent>();

        }

        #endregion

        #endregion


        #region SubmitEvent(Data)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="Data">The attached event data.</param>
        public void SubmitEvent(params String[] Data)
        {
            ListOfEvents.Push(new HTTPEvent((UInt64) Interlocked.Increment(ref IdCounter), Data));
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
            ListOfEvents.Push(new HTTPEvent(SubEvent, (UInt64) Interlocked.Increment(ref IdCounter), Data));
        }

        #endregion


        #region GetEvents(LastEventId = 0)

        /// <summary>
        /// Get a filtered list of events.
        /// </summary>
        /// <param name="LastEventId">The Last-Event-Id header value.</param>
        public IEnumerable<HTTPEvent> GetEvents(UInt64 LastEventId = 0)
        {
            return from   Eventsss in ListOfEvents
                   where  Eventsss.Id > LastEventId
                   select Eventsss;
        }

        #endregion

    }

}
