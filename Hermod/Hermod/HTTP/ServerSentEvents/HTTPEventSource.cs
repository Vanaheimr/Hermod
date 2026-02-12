/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * Author: Achim Friedland <achim.friedland@graphdefined.com>
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

using System.Text;
using System.Threading.Channels;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTPTest;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for event source extensions.
    /// </summary>
    public static class HTTPEventSourceExtensions
    {

        #region SubmitEvent(SubEvent, HTTPRequest)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="HTTPRequest">The attached HTTP request.</param>
        public static Task SubmitEvent(this HTTPEventSource<JObject>  HTTPEventSource,
                                       String                         SubEvent,
                                       HTTPRequest                    HTTPRequest,
                                       CancellationToken              CancellationToken = default)

            => HTTPEventSource.SubmitEvent(
                   SubEvent,
                   Timestamp.Now,
                   new JObject(
                       new JProperty("httpRequest", HTTPRequest.EntirePDU)
                   ),
                   CancellationToken
               );

        #endregion

        #region SubmitEvent(SubEvent, HTTPResponse)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="HTTPResponse">The attached HTTP request.</param>
        public static Task SubmitEvent(this HTTPEventSource<JObject>  HTTPEventSource,
                                       String                         SubEvent,
                                       HTTPResponse                   HTTPResponse,
                                       CancellationToken              CancellationToken = default)

            => HTTPEventSource.SubmitEvent(
                   SubEvent,
                   Timestamp.Now,
                   new JObject(
                       new JProperty("httpRequest", HTTPResponse.EntirePDU)
                   ),
                   CancellationToken
               );

        #endregion

    }


    // In contrast to other popular Comet protocols such as Bayeux or BOSH, Server-Sent Events
    // support a unidirectional server-to-client channel only. The Bayeux protocol on the other
    // side supports a bidirectional communication channel. Furthermore, Bayeux can use HTTP
    // streaming as well as long polling. Like Bayeux, the BOSH protocol is a bidirectional
    // protocol. BOSH is based on the long polling approach.

    /// <summary>
    /// An HTTP event source.
    /// </summary>
    public class HTTPEventSource<T> : IHTTPEventSource<T>
    {

        #region Data

        /// <summary>
        /// ASCII unit/cell separator
        /// </summary>
        protected const Char US = (Char) 0x1F;

        /// <summary>
        /// ASCII record/row separator
        /// </summary>
        protected const Char RS = (Char) 0x1E;

        /// <summary>
        /// ASCII group separator
        /// </summary>
        protected const Char GS = (Char) 0x1D;


        private static readonly String[]                                             Splitter         = [ Environment.NewLine ];

        private                 Int64                                                IdCounter;
        private        readonly ConcurrentQueue                     <HTTPEvent<T>>   eventHistory     = new();
        private        readonly Channel                             <HTTPEvent<T>>   liveChannel;
        private        readonly ConcurrentDictionary<String, Channel<HTTPEvent<T>>>  clientChannels   = [];
        private static readonly SemaphoreSlim                                        LogfileLock      = new (1,1);

        private        readonly Func<T, String>                                      DataSerializer;
        private        readonly Func<String, T?>                                     DataDeserializer;

        #endregion

        #region Properties

        /// <summary>
        /// The attached HTTP API.
        /// </summary>
        //public HTTPAPI                               HTTPAPI                { get; }

        /// <summary>
        /// The attached HTTP API.
        /// </summary>
        public HTTPAPIX                              HTTPAPIX                   { get; }

        /// <summary>
        /// The internal identification of the HTTP event.
        /// </summary>
        public HTTPEventSource_Id                    Id        { get; }

        /// <summary>
        /// The number of currently connected clients.
        /// </summary>
        public UInt32                                NumberOfConnectedClients
            => (UInt32) clientChannels.Count;

        /// <summary>
        /// Maximum number of cached events.
        /// </summary>
        public UInt32                                MaxNumberOfCachedEvents    { get; }

        /// <summary>
        /// The retry interval of this HTTP event.
        /// </summary>
        public TimeSpan                              RetryInterval              { get; set; }

        /// <summary>
        /// The path to the log file.
        /// </summary>
        public String                                LogfilePath                { get; }

        /// <summary>
        /// The delegate to create a filename for storing and reloading events.
        /// </summary>
        public Func<String, DateTimeOffset, String>  LogfileName                { get; }

        #endregion

        #region Constructor(s)

        ///// <summary>
        ///// Create a new HTTP event source.
        ///// </summary>
        ///// <param name="EventIdentification">The internal identification of the HTTP event.</param>
        ///// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        ///// <param name="RetryInterval ">The retry interval.</param>
        ///// <param name="DataSerializer">A delegate to serialize the stored events.</param>
        ///// <param name="DataDeserializer">A delegate to deserialize stored events.</param>
        ///// <param name="EnableLogging">Whether to enable event logging.</param>
        ///// <param name="LogfileName">A delegate to create a filename for storing events.</param>
        ///// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        //public HTTPEventSource(HTTPEventSource_Id                     EventIdentification,
        //                       HTTPAPI                                HTTPAPI,
        //                       UInt32                                 MaxNumberOfCachedEvents      = 500,
        //                       TimeSpan?                              RetryInterval                = null,
        //                       Func<T, String>?                       DataSerializer               = null,
        //                       Func<String, T?>?                      DataDeserializer             = null,
        //                       Boolean                                EnableLogging                = true,
        //                       String?                                LogfilePath                  = null,
        //                       Func<String, DateTimeOffset, String>?  LogfileName                  = null,
        //                       String?                                LogfileReloadSearchPattern   = null)
        //{

        //    this.liveChannel              = Channel.CreateBounded<HTTPEvent<T>>(
        //                                        new BoundedChannelOptions(capacity: (Int32) MaxNumberOfCachedEvents) {
        //                                            FullMode      = BoundedChannelFullMode.DropOldest,
        //                                            SingleReader  = false,
        //                                            SingleWriter  = false
        //                                        }
        //                                    );

        //    this.HTTPAPI                  = HTTPAPI;
        //    this.EventIdentification      = EventIdentification;
        //    this.MaxNumberOfCachedEvents  = MaxNumberOfCachedEvents;
        //    this.RetryInterval            = RetryInterval    ?? TimeSpan.FromSeconds(30);
        //    this.DataSerializer           = DataSerializer   ?? (data => data?.ToString() ?? "");
        //    this.DataDeserializer         = DataDeserializer ?? (data => default);
        //    this.LogfilePath              = LogfilePath      ?? AppContext.BaseDirectory;
        //    this.LogfileName              = LogfileName      ?? ((text, timestamp) => $"{text}_{timestamp:yyyyMMdd}.log");
        //    this.IdCounter                = 0;

        //    if (EnableLogging)
        //    {

        //        #region Reload old data from logfile(s)...

        //        if (LogfileReloadSearchPattern is not null)
        //        {

        //            var httpSSEs = new List<String[]>();

        //            try
        //            {

        //                foreach (var logfilename in Directory.EnumerateFiles(this.LogfilePath,
        //                                                                     LogfileReloadSearchPattern,
        //                                                                     SearchOption.TopDirectoryOnly).
        //                                                      OrderByDescending(file => file))
        //                {

        //                    DebugX.LogT("Reloading: HTTP SSE logfile: " + logfilename);

        //                    File.ReadAllLines(logfilename).
        //                         Reverse().
        //                         Where  (line => line.IsNotNullOrEmpty() &&
        //                                        !line.StartsWith("//")   &&
        //                                        !line.StartsWith("#")).
        //                         Take   ((Int64) MaxNumberOfCachedEvents - httpSSEs.Count).
        //                         Select (line => line.Split(RS)).
        //                         ForEach(line => {

        //                                             if (line.Length >= 3           &&
        //                                                 line.Length <= 4           &&
        //                                                 line[0].IsNotNullOrEmpty() &&
        //                                                 line[2].IsNotNullOrEmpty())
        //                                             {
        //                                                 httpSSEs.Add(line);
        //                                             }

        //                                             else
        //                                                 DebugX.Log("Invalid HTTP event source data in file '", logfilename, "'!");

        //                                         });

        //                    if (httpSSEs.ULongCount() >= MaxNumberOfCachedEvents)
        //                        break;

        //                }

        //            }
        //            catch (DirectoryNotFoundException)
        //            {
        //                // Will fail, when part of the file system path is not accessible!
        //            }
        //            catch (Exception e)
        //            {
        //                DebugX.LogException(e, "While creating a HTTP Event Source!");
        //            }

        //            httpSSEs.Reverse();

        //            httpSSEs.ForEach(line => {

        //                try
        //                {

        //                    liveChannel.Writer.TryWrite(
        //                        new HTTPEvent<T>(
        //                            Id:                (UInt64) IdCounter++,
        //                            Timestamp:         DateTime.Parse(line[0]).ToUniversalTime(),
        //                            Subevent:          line[1],
        //                            Data:              DataDeserializer(line[2]),
        //                            SerializedHeader:  String.Concat(
        //                                                   line[1].IsNotNullOrEmpty()
        //                                                       ? "event: " + line[1] + Environment.NewLine
        //                                                       : String.Empty,
        //                                                   "id: ",   IdCounter,        Environment.NewLine,
        //                                                   "c"
        //                                               ),
        //                            SerializedData:    line[2]
        //                        )
        //                    );

        //                }
        //                catch (Exception e)
        //                {
        //                    DebugX.Log("Reloading HTTP event source data led to an exception: ", Environment.NewLine,
        //                               e.Message);
        //                }

        //            });

        //        }

        //        #endregion

        //        #region Write new data to logfile(s)...

        //        if (LogfileName is not null)
        //        {

        //            //// Note: Do not attach this event handler before the data
        //            ////       is reread from the logfiles above!
        //            //QueueOfEvents.OnAdded += async (Sender, httpEvent, ct) => {

        //            //    await LogfileLock.WaitAsync(ct);

        //            //    try
        //            //    {

        //            //        using (var logfile = File.AppendText(
        //            //                                 Path.Combine(
        //            //                                     this.LogfilePath,
        //            //                                     this.LogfileName(
        //            //                                         this.EventIdentification.ToString(),
        //            //                                         Timestamp.Now
        //            //                                     )
        //            //                                 )
        //            //                             ))
        //            //        {

        //            //            await logfile.WriteLineAsync(String.Concat(httpEvent.Timestamp.ToISO8601(),
        //            //                                                       RS,
        //            //                                                       httpEvent.Subevent,
        //            //                                                       RS,
        //            //                                                       DataSerializer(httpEvent.Data))).
        //            //                          ConfigureAwait(false);

        //            //        }

        //            //    }
        //            //    finally
        //            //    {
        //            //        LogfileLock.Release();
        //            //    }

        //            //};

        //        }

        //        #endregion

        //    }

        //}


        /// <summary>
        /// Create a new HTTP event source.
        /// </summary>
        /// <param name="EventIdentification">The internal identification of the HTTP event.</param>
        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events.</param>
        /// <param name="RetryInterval ">The retry interval.</param>
        /// <param name="DataSerializer">A delegate to serialize the stored events.</param>
        /// <param name="DataDeserializer">A delegate to deserialize stored events.</param>
        /// <param name="EnableLogging">Whether to enable event logging.</param>
        /// <param name="LogfileName">A delegate to create a filename for storing events.</param>
        /// <param name="LogfileReloadSearchPattern">The logfile search pattern for reloading events.</param>
        public HTTPEventSource(HTTPEventSource_Id                     EventIdentification,
                               HTTPAPIX                               HTTPAPIX,
                               UInt32                                 MaxNumberOfCachedEvents      = 500,
                               TimeSpan?                              RetryInterval                = null,
                               Func<T, String>?                       DataSerializer               = null,
                               Func<String, T?>?                      DataDeserializer             = null,
                               Boolean                                EnableLogging                = true,
                               String?                                LogfilePath                  = null,
                               Func<String, DateTimeOffset, String>?  LogfileName                  = null,
                               String?                                LogfileReloadSearchPattern   = null)
        {

            this.liveChannel              = Channel.CreateBounded<HTTPEvent<T>>(
                                                new BoundedChannelOptions(capacity: (Int32) MaxNumberOfCachedEvents) {
                                                    FullMode      = BoundedChannelFullMode.DropOldest,
                                                    SingleReader  = false,
                                                    SingleWriter  = false
                                                }
                                            );

            this.HTTPAPIX                 = HTTPAPIX;
            this.Id      = EventIdentification;
            this.MaxNumberOfCachedEvents  = MaxNumberOfCachedEvents;
            this.RetryInterval            = RetryInterval    ?? TimeSpan.FromSeconds(30);
            this.DataSerializer           = DataSerializer   ?? (data => data?.ToString() ?? "");
            this.DataDeserializer         = DataDeserializer ?? (data => default);
            this.LogfilePath              = LogfilePath      ?? AppContext.BaseDirectory;
            this.LogfileName              = LogfileName      ?? ((text, timestamp) => $"{text}_{timestamp:yyyyMMdd}.log");
            this.IdCounter                = 0;

            if (EnableLogging)
            {

                #region Reload old data from logfile(s)...

                if (LogfileReloadSearchPattern is not null)
                {

                    var httpSSEs = new List<String[]>();

                    try
                    {

                        foreach (var logfilename in Directory.EnumerateFiles(this.LogfilePath,
                                                                             LogfileReloadSearchPattern,
                                                                             SearchOption.TopDirectoryOnly).
                                                              OrderByDescending(file => file))
                        {

                            DebugX.LogT("Reloading: HTTP SSE logfile: " + logfilename);

                            File.ReadAllLines(logfilename).
                                 Reverse().
                                 Where  (line => line.IsNotNullOrEmpty() &&
                                                !line.StartsWith("//")   &&
                                                !line.StartsWith("#")).
                                 Take   ((Int64) MaxNumberOfCachedEvents - httpSSEs.Count).
                                 Select (line => line.Split(RS)).
                                 ForEach(line => {

                                                     if (line.Length >= 3           &&
                                                         line.Length <= 4           &&
                                                         line[0].IsNotNullOrEmpty() &&
                                                         line[2].IsNotNullOrEmpty())
                                                     {
                                                         httpSSEs.Add(line);
                                                     }

                                                     else
                                                         DebugX.Log("Invalid HTTP event source data in file '", logfilename, "'!");

                                                 });

                            if (httpSSEs.ULongCount() >= MaxNumberOfCachedEvents)
                                break;

                        }

                    }
                    catch (DirectoryNotFoundException)
                    {
                        // Will fail, when part of the file system path is not accessible!
                    }
                    catch (Exception e)
                    {
                        DebugX.LogException(e, "While creating a HTTP Event Source!");
                    }

                    httpSSEs.Reverse();

                    httpSSEs.ForEach(line => {

                        try
                        {

                            liveChannel.Writer.TryWrite(
                                new HTTPEvent<T>(
                                    Id:                (UInt64) IdCounter++,
                                    Timestamp:         DateTimeOffset.Parse(line[0]),
                                    Subevent:          line[1],
                                    Data:              DataDeserializer(line[2]),
                                    SerializedHeader:  String.Concat(
                                                           line[1].IsNotNullOrEmpty()
                                                               ? "event: " + line[1] + Environment.NewLine
                                                               : String.Empty,
                                                           "id: ",   IdCounter,        Environment.NewLine,
                                                           "data: "
                                                       ),
                                    SerializedData:    line[2]
                                )
                            );

                        }
                        catch (Exception e)
                        {
                            DebugX.Log("Reloading HTTP event source data led to an exception: ", Environment.NewLine,
                                       e.Message);
                        }

                    });

                }

                #endregion

                #region Write new data to logfile(s)...

                if (LogfileName is not null)
                {

                    //// Note: Do not attach this event handler before the data
                    ////       is reread from the logfiles above!
                    //QueueOfEvents.OnAdded += async (Sender, httpEvent, ct) => {

                    //    await LogfileLock.WaitAsync(ct);

                    //    try
                    //    {

                    //        using (var logfile = File.AppendText(
                    //                                 Path.Combine(
                    //                                     this.LogfilePath,
                    //                                     this.LogfileName(
                    //                                         this.EventIdentification.ToString(),
                    //                                         Timestamp.Now
                    //                                     )
                    //                                 )
                    //                             ))
                    //        {

                    //            await logfile.WriteLineAsync(String.Concat(httpEvent.Timestamp.ToISO8601(),
                    //                                                       RS,
                    //                                                       httpEvent.Subevent,
                    //                                                       RS,
                    //                                                       DataSerializer(httpEvent.Data))).
                    //                          ConfigureAwait(false);

                    //        }

                    //    }
                    //    finally
                    //    {
                    //        LogfileLock.Release();
                    //    }

                    //};

                }

                #endregion

            }

            _ = Task.Run(async () => {
                    try
                    {
                        await foreach (var item in liveChannel.Reader.ReadAllAsync())
                        {
                            // Enumerate a snapshot — ConcurrentBag allows this safely
                            foreach (var clientChannel in clientChannels)
                            {
                                // Use TryWrite → non-blocking, safe even if client is slow/closed
                                if (!clientChannel.Value.Writer.TryWrite(item))
                                {
                                    // Optional: if TryWrite fails often → client is slow or closed
                                    // You can remove it here, but usually better to let Completion handle it
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (ChannelClosedException) { }
                    catch (Exception ex)
                    {
                        // Log unexpected error
                        Console.Error.WriteLine($"Fan-out loop failed: {ex.Message}");
                    }
                });

        }

        #endregion


        #region Subscribe           (ClientId)

        /// <summary>
        /// Subscribe the given client to this event source and
        /// return its own channel reader for receiving live events.
        /// </summary>
        /// <param name="ClientId">The unique client identification.</param>
        public ChannelReader<HTTPEvent<T>> Subscribe(String ClientId)
        {

            var clientChannel = Channel.CreateUnbounded<HTTPEvent<T>>(
                new UnboundedChannelOptions {
                    SingleReader = true,
                    SingleWriter = true
                }
            );

            clientChannels.TryAdd(
                ClientId,
                clientChannel
            );

            // Clean up when client channel is completed/closed
            _ = clientChannel.Reader.Completion.ContinueWith(_ => {
                    // remove one instance (best-effort)
                    clientChannels.TryRemove(ClientId, out var _);
                    clientChannel.Writer.TryComplete();
                }, TaskScheduler.Default);

            return clientChannel.Reader;

        }

        #endregion

        #region Unsubscribe         (ClientId)

        /// <summary>
        /// Unsubscribe the given client.
        /// </summary>
        /// <param name="ClientId">The unique client identification.</param>
        public async Task Unsubscribe(String ClientId)
        {

            if (clientChannels.TryRemove(ClientId,
                                         out var closedChannel))
            {
                closedChannel.Writer.TryComplete();
            }

            //DebugX.LogT($"HTTP SEE client '{ClientId}' unsubscribed!");

        }

        #endregion

        #region GetAllEventsGreater (ClientId, LastEventId = 0)

        /// <summary>
        /// Get a list of events filtered by the event id.
        /// </summary>
        /// <param name="LastEventId">The Last-Event-Id header value.</param>
        public async IAsyncEnumerable<HTTPEvent<T>> GetAllEventsGreater(String                                      ClientId,
                                                                        UInt64?                                     LastEventId         = 0,
                                                                        [EnumeratorCancellation] CancellationToken  CancellationToken   = default)
        {

            var lastEventId  = LastEventId ?? 0;
            var lastEventId2 = lastEventId;

            foreach (var httpEvent in eventHistory)
            {

                if (httpEvent.Id > lastEventId)
                {
                    yield return httpEvent;
                    lastEventId2 = httpEvent.Id;
                }

                if (CancellationToken.IsCancellationRequested)
                    yield break;

            }

            //await foreach (var httpEvent in liveChannel.Reader.ReadAllAsync(CancellationToken))
            //{

            //    // We already sent everything <= current ID from history,
            //    // so live events should all be > last seen
            //    if (httpEvent.Id > lastEventId2)
            //        yield return httpEvent;

            //}

            var reader = Subscribe(ClientId);

            await foreach (var httpEvent in reader.ReadAllAsync(CancellationToken))
            {
                // We already sent everything <= current ID from history,
                // so live events should all be > last seen
                if (httpEvent.Id > lastEventId2)
                    yield return httpEvent;
            }

        }

        #endregion

        #region GetAllEventsSince   (ClientId, Timestamp)

        /// <summary>
        /// Get a list of events filtered by a minimal timestamp.
        /// </summary>
        /// <param name="Timestamp">The earliest timestamp of the events.</param>
        public async IAsyncEnumerable<HTTPEvent<T>> GetAllEventsSince(String                                      ClientId,
                                                                      DateTimeOffset                              Timestamp,
                                                                      [EnumeratorCancellation] CancellationToken  CancellationToken = default)
        {

            // 1. Replay missed events from history (oldest to newest)
            foreach (var evt in eventHistory)
            {

                if (evt.Timestamp >= Timestamp)
                    yield return evt;

                if (CancellationToken.IsCancellationRequested)
                    yield break;

            }

            // 2. Then switch to live events
            await foreach (var evt in liveChannel.Reader.ReadAllAsync(CancellationToken))
            {

                // We already sent everything <= current ID from history,
                // so live events should all be > last seen
                yield return evt;

            }

        }

        #endregion



        #region SubmitEvent (                     Data)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="Data">The attached event data.</param>
        public Task SubmitEvent(T                  Data,
                                CancellationToken  CancellationToken = default)

            => SubmitEvent(
                   String.Empty,
                   Timestamp.Now,
                   Data,
                   CancellationToken
               );

        #endregion

        #region SubmitEvent (          Timestamp, Data)

        /// <summary>
        /// Submit a new subevent with a timestamp.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the event.</param>
        /// <param name="Data">The attached event data.</param>
        public Task SubmitEvent(DateTimeOffset     Timestamp,
                                T                  Data,
                                CancellationToken  CancellationToken = default)

            => SubmitEvent(
                   String.Empty,
                   Timestamp,
                   Data,
                   CancellationToken
               );

        #endregion

        #region SubmitEvent (SubEvent,            Data)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="Data">The attached event data.</param>
        public Task SubmitEvent(String             SubEvent,
                                T                  Data,
                                CancellationToken  CancellationToken = default)

            => SubmitEvent(
                   SubEvent,
                   Timestamp.Now,
                   Data,
                   CancellationToken
               );

        #endregion

        #region SubmitEvent (SubEvent, Timestamp, Data)

        /// <summary>
        /// Submit a new subevent with a timestamp.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="Timestamp">The timestamp of the event.</param>
        /// <param name="Data">The attached event data.</param>
        public async Task SubmitEvent(String             SubEvent,
                                      DateTimeOffset     Timestamp,
                                      T                  Data,
                                      CancellationToken  CancellationToken = default)
        {

            if (SubEvent.IsNotNullOrEmpty())
                SubEvent = SubEvent.Trim().Replace(",", "");

            var data = DataSerializer(Data);

            // Multi-line data must be prefixed with "data: " for each line,
            // and end with an empty line!
            if (data.Contains(Environment.NewLine))
            {

                var sb = new StringBuilder();

                foreach (var item in data.Split(Splitter, StringSplitOptions.RemoveEmptyEntries))
                    sb.AppendLine("data: " + item);

                data = sb.ToString().Trim();

            }
            else
                data = "data: " + data.Trim();

            var httpEvent  = new HTTPEvent<T>(
                                 (UInt64) Interlocked.Increment(ref IdCounter),
                                 Timestamp,
                                 SubEvent,
                                 Data,
                                 String.Concat(
                                     SubEvent.IsNotNullOrEmpty()
                                         ? "event: " + SubEvent + Environment.NewLine
                                         : String.Empty,
                                     "id: ",   IdCounter,         Environment.NewLine
                                 ),
                                 data
                             );

            // Add to history (bounded)
            eventHistory.Enqueue(httpEvent);

            while (eventHistory.Count > MaxNumberOfCachedEvents)
                eventHistory.TryDequeue(out _);

            await liveChannel.Writer.WriteAsync(
                      httpEvent,
                      CancellationToken
                  );

        }

        #endregion


        // Signal without payload!

        #region SubmitEvent (SubEvent)

        /// <summary>
        /// Submit a new event.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="Data">The attached event data.</param>
        //public Task SubmitEvent(String             SubEvent,
        //                        CancellationToken  CancellationToken = default)

        //    => SubmitEvent(
        //           SubEvent,
        //           Timestamp.Now,
        //           null,
        //           CancellationToken
        //       );

        #endregion

        #region SubmitEvent (SubEvent, Timestamp)

        /// <summary>
        /// Submit a new subevent with a timestamp.
        /// </summary>
        /// <param name="SubEvent">A subevent identification.</param>
        /// <param name="Timestamp">The timestamp of the event.</param>
        //public async Task SubmitEvent(String             SubEvent,
        //                              DateTimeOffset     Timestamp,
        //                              CancellationToken  CancellationToken = default)
        //{

        //    if (SubEvent.IsNotNullOrEmpty())
        //        SubEvent = SubEvent.Trim().Replace(",", "");

        //    var httpEvent  = new HTTPEvent<T>(
        //                         (UInt64) Interlocked.Increment(ref IdCounter),
        //                         Timestamp,
        //                         SubEvent,
        //                         null,
        //                         String.Concat(
        //                             SubEvent.IsNotNullOrEmpty()
        //                                 ? "event: " + SubEvent + Environment.NewLine
        //                                 : String.Empty,
        //                             "id: ",   IdCounter,         Environment.NewLine,
        //                             "data: "
        //                         ),
        //                         DataSerializer(Data)
        //                     );

        //    // Add to history (bounded)
        //    eventHistory.Enqueue(httpEvent);

        //    while (eventHistory.Count > MaxNumberOfCachedEvents)
        //        eventHistory.TryDequeue(out _);

        //    await liveChannel.Writer.WriteAsync(
        //              httpEvent,
        //              CancellationToken
        //          );

        //}

        #endregion



        public static async Task<List<HTTPEvent<JObject>>> ParseHTTPResponseStream(HTTPResponse       HTTPResponse,
                                                                                   TimeSpan?          lineTimeout       = null,
                                                                                   CancellationToken  cancellationToken = default)
        {

            if (HTTPResponse?.HTTPBodyStream is null)
                return [];

            var reader = new StreamReader(HTTPResponse.HTTPBodyStream);

            if (!lineTimeout.HasValue)
                lineTimeout = TimeSpan.FromSeconds(120);

            var events        = new List<HTTPEvent<JObject>>();
            var currentEvent  = new HTTPEvent<JObject>.EventBuilder();
            var isData        = false;

            string? line;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    line = await reader.ReadLineWithTimeoutAsync(lineTimeout.Value, cancellationToken);
                }
                catch (TimeoutException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Loggen oder werfen – hier einfach abbrechen
                    Console.Error.WriteLine($"Fehler beim Lesen: {ex.Message}");
                    break;
                }

                if (line is null)
                {
                    // Stream-Ende
                    break;
                }

                line = line.TrimEnd('\r', '\n');

                // Leere Zeile → Event abschließen
                if (string.IsNullOrEmpty(line))
                {
                    if (currentEvent.IsValid())
                    {
                        var parsedEvent = currentEvent.Build(JObject.Parse);
                        if (parsedEvent is not null)
                        {
                            events.Add(parsedEvent);
                        }
                    }
                    currentEvent.Reset();
                    isData = false;
                    continue;
                }

                if (isData)
                {
                    currentEvent.AppendData(line);
                    continue;
                }

                // Kommentar ignorieren
                if (line.StartsWith(":"))
                    continue;

                // Feld parsen
                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0)
                    continue; // ungültige Zeile

                var field = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].TrimStart();

                switch (field.ToLowerInvariant())
                {

                    case "event":
                        currentEvent.Subevent = value;
                        break;

                    case "id":
                        if (ulong.TryParse(value, out var id))
                            currentEvent.Id = id;
                        break;

                    case "data":
                        isData = true;
                        currentEvent.AppendData(value);
                        break;

                    case "retry":
                        // Optional: currentEvent.RetryMs = int.Parse(value);
                        break;

                }
            }

            // Letztes Event (falls kein abschließendes Leerzeichen)
            if (currentEvent.IsValid())
            {
                var lastEvent = currentEvent.Build(JObject.Parse);
                if (lastEvent is not null)
                    events.Add(lastEvent);
            }

            return events;

        }



        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => Id.ToString();

        #endregion

        #region Dispose()

        /// <summary>
        /// Dispose this event source and complete all channels.
        /// </summary>
        public void Dispose()
        {

            liveChannel.Writer.Complete();

            foreach (var clientChannel in clientChannels)
                clientChannel.Value.Writer.Complete();

        }

        #endregion

    }

}
