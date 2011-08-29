/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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

using de.ahzf.Hermod.Sockets.TCP;
using de.ahzf.Hermod.Datastructures;

#endregion

namespace de.ahzf.Hermod.HTTP
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

        private          Int64                 IdCounter;
        private readonly LinkedList<HTTPEvent> Eventlist;

        #endregion

        #region Properties

        /// <summary>
        /// The internal identification of the HTTP event.
        /// </summary>
        public String EventIdentification     { get; private set; }

        /// <summary>
        /// Maximum number of cached events.
        /// </summary>
        public UInt32 MaxNumberOfCachedEvents { get; set; }

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

            Eventlist = new LinkedList<HTTPEvent>();

        }

        #endregion

        #endregion


        #region Submit(Data)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="Data">The attached event data.</param>
        public void Submit(String Data)
        {
            Eventlist.AddLast(new HTTPEvent((UInt64) Interlocked.Increment(ref IdCounter), Data));
        }

        #endregion

        #region Submit(Subevent, Data)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="Subevent">A subevent identification.</param>
        /// <param name="Data">The attached event data.</param>
        public void Submit(String Subevent, String Data)
        {
            Eventlist.AddLast(new HTTPEvent(Subevent, (UInt64) Interlocked.Increment(ref IdCounter), Data));
        }

        #endregion


        #region GetEvents(LastEventId = 0)

        /// <summary>
        /// Get a filtered list of events.
        /// </summary>
        /// <param name="LastEventId">The Last-Event-Id header value.</param>
        public IEnumerable<HTTPEvent> GetEvents(UInt64 LastEventId = 0)
        {
            return from   Eventsss in Eventlist
                   where  Eventsss.Id > LastEventId
                   select Eventsss;
        }

        #endregion

    }

}
