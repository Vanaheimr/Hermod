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

using System.Diagnostics;
using System.Collections.Concurrent;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.ServerSentEvents;

/// <summary>
/// A server that is stopped while event streams are open stops at once, and is done with every
/// stream by the time it says it has stopped.
/// </summary>
/// <remarks>
/// A stream waits for its next event or its next heartbeat - fifteen seconds, unless the mapping
/// says otherwise - and finds out that its client has gone only when it next writes. Stop() closed
/// one connection, waited for its handler, and only then closed the next; the token that ends the
/// wait was cancelled after the last. That was up to fifteen seconds for every open stream, one
/// after another. HTTPTestServerTests' event stream tests took 15, 15 and 63 seconds once they
/// disposed of their server, and SSEProxyTests gave every stream an event to write before it
/// stopped its own.
/// </remarks>
[TestFixture]
public class SSEServerStopTests
{

    #region Data

    private HTTPServer?               httpServer;
    private readonly List<HTTPClient> clients = [];

    #endregion

    #region TearDown

    [TearDown]
    public async Task DisposeEverything()
    {

        foreach (var client in clients)
            await client.DisposeAsync();

        clients.Clear();

        if (httpServer is not null)
            await httpServer.DisposeAsync();

        httpServer = null;

    }

    #endregion


    #region Three open streams, and the server stops at once

    /// <summary>
    /// Three streams on a quiet event source, with the heartbeat as it comes: Stop() returns well
    /// before the first of the heartbeats it used to wait for.
    /// </summary>
    [Test]
    public Task AServerWithThreeOpenStreamsStopsAtOnce()

        => ThreeOpenStreamsStopAtOnce(server => {

               var httpAPI = (RecordingHTTPAPI) server.AddHTTPAPI(
                                                    HTTPAPICreator: (sameServer, path) => new RecordingHTTPAPI(sameServer, path)
                                                );

               var source  = AddSource(httpAPI);

               Assert.That(httpAPI.MapEventSource<JObject>(source, HTTPPath.Parse("/events"), null), Is.True);

               return (source, httpAPI.Errors);

           });

    /// <summary>
    /// The same for the other MapEventSource, the one behind which the accounts live. Not signed in,
    /// as in SSEProxyTests: what is tested is the stream.
    /// </summary>
    [Test]
    public Task TheExtAPIStreamsStopAtOnceToo()

        => ThreeOpenStreamsStopAtOnce(server => {

               var extAPI  = new RecordingHTTPExtAPI(server);

               var source  = AddSource(extAPI);

               Assert.That(extAPI.MapEventSource<JObject>(source, HTTPPath.Parse("/events"), RequireAuthentication: false), Is.True);

               return (source, extAPI.Errors);

           });

    #endregion


    #region (private) ThreeOpenStreamsStopAtOnce(MapEvents)

    /// <summary>
    /// Open three streams on the event source the given mapping makes, stop the server, and check
    /// that Stop() returns at once, with every stream ended on both sides and no error reported.
    /// </summary>
    /// <param name="MapEvents">Maps an event source at /events, and returns it together with the errors its API reports.</param>
    private async Task ThreeOpenStreamsStopAtOnce(Func<HTTPServer, (HTTPEventSource<JObject> Source, ConcurrentQueue<Exception> Errors)> MapEvents)
    {

        httpServer               = new HTTPServer(
                                       IPAddress:  IPv4Address.Localhost,
                                       TCPPort:    IPPort.Zero,
                                       AutoStart:  false
                                   );

        var (eventSource, errors) = MapEvents(httpServer);

        await httpServer.Start();

        var readers = new List<StreamReader>();

        for (var i = 0; i < 3; i++)
            readers.Add(await Open("/events"));

        // Each stream past its preamble and subscribed, that is, waiting for its next event.
        Assert.That(await Eventually(() => eventSource.NumberOfConnectedClients == 3, TimeSpan.FromSeconds(5)),
                    Is.True,
                    $"three streams waiting for events, not {eventSource.NumberOfConnectedClients}");

        var stopwatch  = Stopwatch.StartNew();
        var stopping   = httpServer.Stop();
        var inTime     = await Task.WhenAny(stopping, Task.Delay(TimeSpan.FromSeconds(2))) == stopping;

        // Where it was not in time, the streams are let go the way these tests used to, by giving
        // them something to write, rather than waiting for a heartbeat for each of them in turn.
        using (var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
        {
            while (!stopping.IsCompleted && !giveUp.IsCancellationRequested)
            {
                await eventSource.SubmitEvent("goodbye", new JObject(), CancellationToken.None);
                await Task.WhenAny(stopping, Task.Delay(100));
            }
        }

        if (!stopping.IsCompleted)
            Assert.Fail("Stop() had not returned after 60 seconds, not even with events to write.");

        await stopping;

        var tookStop   = stopwatch.Elapsed;

        TestContext.Out.WriteLine($"Stop() returned after {tookStop.TotalMilliseconds:F0} ms.");

        var ended      = new List<Boolean>();

        foreach (var reader in readers)
            ended.Add(await Ends(reader, TimeSpan.FromSeconds(2)));

        Assert.Multiple(() => {

            Assert.That(inTime,                             Is.True,   $"Stop() returned within 2 s, long before any stream's heartbeat; it returned after {tookStop.TotalSeconds:F1} s" +
                                                                       (inTime ? "" : ", once the streams had been given events to write"));
            Assert.That(httpServer.NumberOfConnectedClients, Is.Zero,  "no connection left on the server's books");

            // A stream lets go of its subscription on its way out, before its handler returns. That
            // Stop() waits for the handlers themselves is StopWaitsForEveryHandlerTests' to check:
            // these end too soon after their streams for a Stop() that did not wait to be caught.
            Assert.That(eventSource.NumberOfConnectedClients, Is.Zero, "every stream had let go of its subscription when Stop() returned");

            Assert.That(ended,                              Is.All.True, "every client found its stream ended");

            // The stop ends the wait for the next event by cancelling it, and that is no error.
            Assert.That(errors.Select(error => error.GetType().Name + ": " + error.Message),
                        Is.Empty,
                        "errors reported by the API");

        });

    }

    #endregion

    #region (private) AddSource(API)

    private static HTTPEventSource<JObject> AddSource(HTTPAPI API)

        => API.AddEventSource<JObject>(
               EventSourceId:                HTTPEventSource_Id.Parse("events"),
               MaxNumberOfCachedEvents:      100,
               RetryInterval:                TimeSpan.FromSeconds(1),
               DataSerializer:               json => json.ToString(Newtonsoft.Json.Formatting.None),
               DataDeserializer:             null,
               EnableLogging:                false,
               LogfilePath:                  null,
               LogfilePrefix:                null,
               LogfileName:                  null,
               LogfileReloadSearchPattern:   null
           );

    #endregion

    #region (private) Open(Path)

    /// <summary>
    /// Open the event stream at the given path as a browser would, and read it up to its preamble.
    /// </summary>
    private async Task<StreamReader> Open(String Path)
    {

        var client = await HTTPClient.ConnectNew(IPv4Address.Localhost, httpServer!.TCPPort);
        Assert.That(client.Item1, Is.Not.Null);

        clients.Add(client.Item1!);

        var response = await client.Item1!.SendRequest(
                                 client.Item1.CreateRequest(
                                     HTTPMethod.GET,
                                     HTTPPath.Parse(Path),
                                     Accept: AcceptTypes.FromHTTPContentTypes(HTTPContentType.Text.EVENTSTREAM)
                                 )
                             );

        Assert.That(response,                 Is.Not.Null);
        Assert.That(response.HTTPBodyStream,  Is.Not.Null, "an event stream carries a body stream");

        var reader = new StreamReader(response.HTTPBodyStream!);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        Assert.That(await reader.ReadLineAsync(timeout.Token), Does.StartWith("retry:"), "the stream's preamble");

        return reader;

    }

    #endregion

    #region (private static) Ends(Reader, Within)

    /// <summary>
    /// Whether the stream ends within the given time: whatever is still on its way is read, and
    /// then the end of the stream - or a reset, which ends a stream as well as a close does.
    /// </summary>
    private static async Task<Boolean> Ends(StreamReader  Reader,
                                            TimeSpan      Within)
    {

        using var timeout = new CancellationTokenSource(Within);

        try
        {

            while (await Reader.ReadLineAsync(timeout.Token) is not null)
            { }

            return true;

        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (IOException)
        {
            return true;
        }

    }

    #endregion

    #region (private static) Eventually(Condition, Within)

    private static async Task<Boolean> Eventually(Func<Boolean>  Condition,
                                                  TimeSpan       Within)
    {

        var stopwatch = Stopwatch.StartNew();

        while (!Condition())
        {

            if (stopwatch.Elapsed > Within)
                return false;

            await Task.Delay(20);

        }

        return true;

    }

    #endregion


    #region (private classes) RecordingHTTPAPI, RecordingHTTPExtAPI

    /// <summary>
    /// An HTTP API that keeps what it reports as an error.
    /// </summary>
    private sealed class RecordingHTTPAPI(HTTPServer  Server,
                                          HTTPPath    RootPath)

        : HTTPAPI(Server,
                  RootPath:                  RootPath,
                  RegisterWithinHTTPServer:  false)

    {

        public ConcurrentQueue<Exception> Errors { get; } = new();

        public override Task HandleErrors(String     Module,
                                          String     Caller,
                                          Exception  ExceptionOccurred)
        {
            Errors.Enqueue(ExceptionOccurred);
            return base.HandleErrors(Module, Caller, ExceptionOccurred);
        }

    }

    /// <summary>
    /// The same for the HTTP API behind which the accounts live.
    /// </summary>
    private sealed class RecordingHTTPExtAPI(HTTPServer Server)

        : HTTPExtAPI(Server,
                     SkipURLTemplates:      true,
                     DisableLogging:        true,
                     DisableNotifications:  true)

    {

        public ConcurrentQueue<Exception> Errors { get; } = new();

        public override Task HandleErrors(String     Module,
                                          String     Caller,
                                          Exception  ExceptionOccurred)
        {
            Errors.Enqueue(ExceptionOccurred);
            return base.HandleErrors(Module, Caller, ExceptionOccurred);
        }

    }

    #endregion

}
