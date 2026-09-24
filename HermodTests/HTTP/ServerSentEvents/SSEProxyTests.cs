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

using System.Runtime.CompilerServices;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.ServerSentEvents;

/// <summary>
/// An event stream as a proxy in front of the server sees it.
/// </summary>
/// <remarks>
/// Measured with a vehicle of the EV project behind nginx as it comes, whose own event stream had the
/// shape of MapEventSource's: nginx buffers what it proxies, and a buffered event stream reached the
/// browser as nothing at all - not even its header - until nginx gave up on the upstream after 60
/// silent seconds. The header arrived after 72 seconds, the stream ended 98 ms later, and the browser
/// began the same wait again. Two things put it right, and these tests hold both: the stream tells nginx
/// not to buffer it, and a stream with nothing to say says so every heartbeat, so that the 60 seconds
/// are never reached.
/// </remarks>
[TestFixture]
public class SSEProxyTests
{

    #region Data

    private HTTPServer?                     httpServer;
    private List<HTTPClient>                clients  = [];
    private List<HTTPEventSource<JObject>>  sources  = [];

    #endregion

    #region TearDown

    [TearDown]
    public async Task StopEverything()
    {

        foreach (var client in clients)
            await client.DisposeAsync();

        // A stream notices that its client has gone only when it next writes, and a server waits for
        // its streams before it reports itself stopped - so each is given something to write.
        foreach (var source in sources)
            await source.SubmitEvent("goodbye", new JObject(), CancellationToken.None);

        clients.Clear();
        sources.Clear();

        // Bounded all the same: not stopping in time is not what any of these tests are about.
        if (httpServer is not null)
        {
            try
            {
                await httpServer.Stop().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            { }
        }

        httpServer = null;

    }

    #endregion


    #region (helpers)

    private HTTPEventSource<JObject> AddSource(HTTPAPI API, String Name)
    {

        var source = API.AddEventSource<JObject>(
                         EventSourceId:                HTTPEventSource_Id.Parse(Name),
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

        sources.Add(source);

        return source;

    }

    /// <summary>
    /// Open the event stream at the given path, as a browser would.
    /// </summary>
    private async Task<HTTPResponse> Open(String Path)
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

        return response;

    }

    /// <summary>
    /// Read line by line until a line satisfies the condition; whether one did in time. Every line
    /// read is added to Seen, when given.
    /// </summary>
    private static async Task<Boolean> ReadUntil(StreamReader           Reader,
                                                 Func<String, Boolean>  Wanted,
                                                 TimeSpan               Within,
                                                 List<String>?          Seen   = null)
    {

        using var timeout = new CancellationTokenSource(Within);

        try
        {
            while (await Reader.ReadLineAsync(timeout.Token) is String line)
            {

                Seen?.Add(line);

                if (Wanted(line))
                    return true;

            }
        }
        catch (OperationCanceledException)
        { }

        return false;

    }

    #endregion


    #region The stream tells nginx not to buffer it

    [Test]
    public async Task TheStreamAsksAProxyNotToBufferIt()
    {

        httpServer      = await HTTPServer.StartNew();
        var httpAPI     = httpServer.AddHTTPAPI();
        var eventSource = AddSource(httpAPI, "quiet");

        Assert.That(httpAPI.MapEventSource<JObject>(eventSource, HTTPPath.Parse("/quiet"), null), Is.True);

        var response    = await Open("/quiet");

        Assert.Multiple(() =>
        {
            Assert.That(response.ContentType,                                             Is.EqualTo(HTTPContentType.Text.EVENTSTREAM));
            Assert.That(response.GetHeaderField(HTTPResponseHeaderField.X_AccelBuffering), Is.EqualTo("no"));
        });

    }

    /// <summary>
    /// The same for the other MapEventSource, the one behind which the accounts live. Not signed in:
    /// what is tested is the stream, and a stream that requires a user is still the same stream.
    /// </summary>
    [Test]
    public async Task TheExtAPIStreamAsksAProxyNotToBufferItToo()
    {

        httpServer      = new HTTPServer(
                              IPAddress:  IPv4Address.Localhost,
                              TCPPort:    IPPort.Zero,
                              AutoStart:  false
                          );

        var extAPI      = new HTTPExtAPI(
                              httpServer,
                              SkipURLTemplates:      true,
                              DisableLogging:        true,
                              DisableNotifications:  true
                          );

        await httpServer.Start();

        var eventSource = AddSource(extAPI, "quiet");

        Assert.That(extAPI.MapEventSource<JObject>(eventSource, HTTPPath.Parse("/quiet"), RequireAuthentication: false), Is.True);

        var response    = await Open("/quiet");

        Assert.That(response.GetHeaderField(HTTPResponseHeaderField.X_AccelBuffering), Is.EqualTo("no"));

    }

    #endregion

    #region A silent stream says so, and carries on

    [Test]
    public async Task ASilentStreamSendsAHeartbeat()
    {

        httpServer      = await HTTPServer.StartNew();
        var httpAPI     = httpServer.AddHTTPAPI();
        var eventSource = AddSource(httpAPI, "quiet");

        Assert.That(httpAPI.MapEventSource<JObject>(eventSource, HTTPPath.Parse("/quiet"), null,
                                                    Heartbeat: TimeSpan.FromMilliseconds(300)), Is.True);

        using var reader = new StreamReader((await Open("/quiet")).HTTPBodyStream!);

        Assert.That(await ReadUntil(reader, line => line == ": keep-alive", TimeSpan.FromSeconds(5)),
                    Is.True,
                    "a comment within 5 s on a stream with nothing to say and a heartbeat of 300 ms");

    }

    /// <summary>
    /// The event after a heartbeat, once - and the one after that, so that the stream is known to be
    /// waiting for events and not only for its heartbeat.
    /// </summary>
    /// <remarks>
    /// This is the half that could go wrong: the next event is waited for across heartbeats rather
    /// than asked for again, and an enumerator asked a second time while the first question is still
    /// open throws, or in one that does not check, loses an event or delivers it twice.
    /// </remarks>
    [Test]
    public async Task AnEventAfterAHeartbeatArrivesExactlyOnce()
    {

        httpServer      = await HTTPServer.StartNew();
        var httpAPI     = httpServer.AddHTTPAPI();
        var eventSource = AddSource(httpAPI, "quiet");

        Assert.That(httpAPI.MapEventSource<JObject>(eventSource, HTTPPath.Parse("/quiet"), null,
                                                    Heartbeat: TimeSpan.FromMilliseconds(300)), Is.True);

        using var reader = new StreamReader((await Open("/quiet")).HTTPBodyStream!);

        // Two heartbeats, so that the question to the enumerator has stayed open across more than one.
        var heartbeats   = 0;
        var twice        = await ReadUntil(reader, line => line == ": keep-alive" && ++heartbeats == 2, TimeSpan.FromSeconds(5));

        var seen         = new List<String>();

        await eventSource.SubmitEvent("sub", new JObject(new JProperty("n", "first")), CancellationToken.None);
        var first        = await ReadUntil(reader, line => line.Contains("\"first\""),  TimeSpan.FromSeconds(5), seen);

        await eventSource.SubmitEvent("sub", new JObject(new JProperty("n", "second")), CancellationToken.None);
        var second       = await ReadUntil(reader, line => line.Contains("\"second\""), TimeSpan.FromSeconds(5), seen);

        Assert.Multiple(() =>
        {
            Assert.That(twice,                                       Is.True,       "two heartbeats before the first event");
            Assert.That(first,                                       Is.True,       "the event submitted after them arrived");
            Assert.That(second,                                      Is.True,       "and so did the one after it");
            Assert.That(seen.Count(line => line.Contains("\"first\"")), Is.EqualTo(1), "the first event, once");
        });

    }

    /// <summary>
    /// Zero switches the heartbeat off, for a client that would rather have the silence.
    /// </summary>
    [Test]
    public async Task ZeroMeansNoHeartbeat()
    {

        httpServer      = await HTTPServer.StartNew();
        var httpAPI     = httpServer.AddHTTPAPI();
        var eventSource = AddSource(httpAPI, "quiet");

        Assert.That(httpAPI.MapEventSource<JObject>(eventSource, HTTPPath.Parse("/quiet"), null,
                                                    Heartbeat: TimeSpan.Zero), Is.True);

        using var reader = new StreamReader((await Open("/quiet")).HTTPBodyStream!);

        var preamble     = await ReadUntil(reader, line => line.StartsWith("retry:"), TimeSpan.FromSeconds(5));
        var comment      = await ReadUntil(reader, line => line.StartsWith(':'),      TimeSpan.FromMilliseconds(1500));

        Assert.Multiple(() =>
        {
            Assert.That(preamble,  Is.True,   "the stream started");
            Assert.That(comment,   Is.False,  "and said nothing more");
        });

    }

    #endregion

    #region A stream that cannot be written lets go of its events

    /// <summary>
    /// A heartbeat that cannot be written ends the stream with that error - and the enumerator, which
    /// was waiting for its next event at that moment, is stopped and disposed rather than left
    /// running or disposed while it runs.
    /// </summary>
    [Test]
    public async Task AFailedHeartbeatLetsGoOfTheWaitingEnumerator()
    {

        var finished = new StrongBox<Boolean>(false);
        var writer   = new StreamWriter(new BrokenStream());

        // Bounded, because the way this goes wrong is a stream that never returns.
        var failure  = Assert.ThrowsAsync<IOException>(async () => await writer.WriteEvents(
                                                                             Endless(finished),
                                                                             TimeSpan.FromMilliseconds(50)
                                                                         ).WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Multiple(() =>
        {
            Assert.That(failure?.Message,  Does.Contain("broken"),  "the error the stream gave");
            Assert.That(finished.Value,    Is.True,                 "the enumerator ran its finally");
        });

    }

    /// <summary>
    /// Waits for an event that never comes, and says when it has stopped waiting.
    /// </summary>
    private static async IAsyncEnumerable<HTTPEvent<JObject>> Endless(StrongBox<Boolean>                          Finished,
                                                                      [EnumeratorCancellation] CancellationToken  CancellationToken = default)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, CancellationToken);
            yield break;
        }
        finally
        {
            Finished.Value = true;
        }
    }

    /// <summary>
    /// A connection the client has gone from.
    /// </summary>
    private sealed class BrokenStream : Stream
    {

        public override Boolean  CanRead   => false;
        public override Boolean  CanSeek   => false;
        public override Boolean  CanWrite  => true;
        public override Int64    Length    => throw new NotSupportedException();
        public override Int64    Position  { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void  Flush() { }
        public override Int32 Read (Byte[] Buffer, Int32 Offset, Int32 Count) => throw new NotSupportedException();
        public override Int64 Seek (Int64 Offset, SeekOrigin Origin)          => throw new NotSupportedException();
        public override void  SetLength(Int64 Value)                          => throw new NotSupportedException();
        public override void  Write(Byte[] Buffer, Int32 Offset, Int32 Count) => throw new IOException("The pipe is broken.");

    }

    #endregion

}
