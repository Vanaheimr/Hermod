/*
 * Copyright (c) 2010-2016, GraphDefined GmbH
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private static   SemaphoreSlim       LogfileLock  = new SemaphoreSlim(1,1);

        #endregion

        #region Properties

        /// <summary>
        /// The internal identification of the HTTP event.
        /// </summary>
        public String  EventIdentification   { get; }

        /// <summary>
        /// Maximum number of cached events.
        /// </summary>
        public UInt64  MaxNumberOfCachedEvents
        {
            get
            {
                return QueueOfEvents.MaxNumberOfElements;
            }
        }

        /// <summary>
        /// The retry intervall of this HTTP event.
        /// </summary>
        public TimeSpan  RetryIntervall   { get; set; }

        /// <summary>
        /// The delegate to create a filename for storing and reloading events.
        /// </summary>
        public Func<String, DateTime, String>  LogfileName   { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP event source.
        /// </summary>
        /// <param name="EventIdentification">The internal identification of the HTTP event.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryIntervall">The retry intervall.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing and reloading events.</param>
        public HTTPEventSource(String                          EventIdentification,
                               UInt64                          MaxNumberOfCachedEvents   = 500,
                               TimeSpan?                       RetryIntervall            = null,
                               Func<String, DateTime, String>  LogfileName               = null)
        {

            #region Initial checks

            EventIdentification.FailIfNullOrEmpty("The HTTP Server Sent Event must have a name!");

            #endregion

            this.EventIdentification  = EventIdentification;
            this.QueueOfEvents        = new TSQueue<HTTPEvent>(MaxNumberOfCachedEvents);
            this.RetryIntervall       = RetryIntervall ?? TimeSpan.FromSeconds(30);
            this.LogfileName          = LogfileName;
            this.IdCounter            = 0;

            #region Setup Logfile(s)

            if (LogfileName != null)
            {

                Int64 CurrentCounter;

                //ToDo: Reverse the order!
                foreach (var logfilename in Directory.EnumerateFiles(Directory.GetCurrentDirectory(),
                                                                     this.EventIdentification + "*.log",
                                                                     SearchOption.TopDirectoryOnly))
                {

                    //ToDo: Read files backwards!
                    File.ReadLines(logfilename).
                         Take   ((Int64) MaxNumberOfCachedEvents - (Int64) QueueOfEvents.Count).
                         Select (line => line.Split((Char) 0x1E)).
                         ForEach(line => {

                             try
                             {

                                 CurrentCounter = Int64.Parse(line[0]);

                                 if (IdCounter < CurrentCounter)
                                     IdCounter = CurrentCounter;

                                 QueueOfEvents.Push(new HTTPEvent((UInt64) CurrentCounter,
                                                                  DateTime.Parse(line[1]),
                                                                  line[2],
                                                                  line[3].Split((Char) 0x1F))).
                                               Wait();

                             }
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
                             catch (Exception)
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.
                             { }

                         });

                    if (QueueOfEvents.Count >= MaxNumberOfCachedEvents)
                        break;

                }

                // Note: Do not attach this event handler before the data
                //       is reread from the logfiles above!
                QueueOfEvents.OnAdded += async (Sender, Value) => {

                    await LogfileLock.WaitAsync();

                    try
                    {

                        using (var logfile = File.AppendText(this.LogfileName(this.EventIdentification,
                                                                              DateTime.Now)))
                        {

                            logfile.WriteLine(String.Concat(Value.Id,                    (Char) 0x1E,
                                                            Value.Timestamp.ToIso8601(), (Char) 0x1E,
                                                            Value.Subevent,              (Char) 0x1E,
                                                            Value.Data.AggregateWith(    (Char) 0x1F)));

                        }

                    }
                    finally
                    {
                        LogfileLock.Release();
                    }

                };

            }

            #endregion

        }

        #endregion


        #region SubmitEvent(params Data)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="Data">The attached event data.</param>
        public async Task SubmitEvent(params String[] Data)
        {

            QueueOfEvents.Push(new HTTPEvent((UInt64) Interlocked.Increment(ref IdCounter),
                                             Data));

        }

        #endregion

        #region SubmitTimestampedEvent(Timestamp, params Data)

        /// <summary>
        /// Submit a new subevent with a timestamp.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the event.</param>
        /// <param name="Data">The attached event data.</param>
        public async Task SubmitTimestampedEvent(DateTime Timestamp, params String[] Data)
        {

            await SubmitEvent(new JObject(
                                  new JProperty("Timestamp",  Timestamp),
                                  new JProperty("Message",    Data?.Length > 0
                                                                  ? Data.Aggregate((a, b) => a.Trim() + " " + b.Trim())
                                                                  : "")
                              ).
                              ToString().
                              Replace(Environment.NewLine, " ")
                             ).
                      ConfigureAwait(false);

        }

        #endregion


        #region SubmitSubEvent(SubEvent, params Data)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="Data">The attached event data.</param>
        public async Task SubmitSubEvent(String SubEvent, params String[] Data)
        {

            if (SubEvent.IsNotNullOrEmpty())
                SubEvent = SubEvent.Trim().Replace(",", "");

            if (SubEvent.IsNullOrEmpty())
                QueueOfEvents.Push(new HTTPEvent((UInt64) Interlocked.Increment(ref IdCounter),
                                                 Data));

            else
                QueueOfEvents.Push(new HTTPEvent((UInt64) Interlocked.Increment(ref IdCounter),
                                                 SubEvent,
                                                 Data));

        }

        #endregion

        #region SubmitTimestampedSubEvent(SubEvent, params Data)

        /// <summary>
        /// Submit a new subevent, using the current time as timestamp.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="Data">The attached event data.</param>
        public async Task SubmitSubEventWithTimestamp(String SubEvent, params String[] Data)
        {

            if (SubEvent.IsNotNullOrEmpty())
                SubEvent = SubEvent.Trim().Replace(",", "");

            if (SubEvent.IsNullOrEmpty())
                await SubmitTimestampedEvent(DateTime.Now,
                                             Data).
                          ConfigureAwait(false);

            else
                await SubmitTimestampedSubEvent(SubEvent,
                                                DateTime.Now,
                                                Data).
                          ConfigureAwait(false);

        }

        #endregion

        #region SubmitTimestampedSubEvent(SubEvent, Timestamp, params Data)

        /// <summary>
        /// Submit a new subevent with a timestamp.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="Timestamp">The timestamp of the event.</param>
        /// <param name="Data">The attached event data.</param>
        public async Task SubmitTimestampedSubEvent(String           SubEvent,
                                                    DateTime         Timestamp,
                                                    params String[]  Data)
        {

            if (SubEvent.IsNotNullOrEmpty())
                SubEvent = SubEvent.Trim().Replace(",", "");

            if (SubEvent.IsNullOrEmpty())
                await SubmitTimestampedEvent(Timestamp,
                                             Data).
                          ConfigureAwait(false);

            else
                await SubmitSubEvent(SubEvent,
                                     new JObject(
                                         new JProperty("Timestamp",  Timestamp),
                                         new JProperty("Message",    Data?.Length > 0
                                                                         ? Data.Aggregate((a, b) => a.Trim() + " " + b.Trim())
                                                                         : "")
                                     ).
                                     ToString().
                                     Replace(Environment.NewLine, " ")
                                    ).
                          ConfigureAwait(false);

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
