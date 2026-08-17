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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.ServerSentEvents;

/// <summary>
/// What a Server-Sent-Events client sees at the START of the stream, before any event exists.
/// </summary>
/// <remarks>
/// These come out of a CI failure that could not be reproduced at the time: ClientServer_HTTPServerSentEvents_Test02
/// hung for its full 30-second read timeout and reported 0 of 3 events, once, on the Debian leg.
///
/// The first fix was to the server: the SSE worker wrote its "retry:" preamble into a StreamWriter with
/// AutoFlush off and only flushed inside the event loop, so an event source with nothing to send never
/// reached the client at all. That was real, but it was not the flake - the test below then went red on
/// the Debian leg twice, with the flush in place.
///
/// The cause is now known, and it is on the client side: PrefixStream.ReadAsync. When a response header
/// and the first body bytes arrive in the same read, AHTTPClient hands the leftover bytes to a
/// PrefixStream. That stream copied them out and then, because the caller's buffer was not yet full,
/// went on to read the inner socket for the rest - and on a quiet SSE stream there is no rest. The bytes
/// it was already holding were never delivered. Whether it happened came down to whether the server's
/// header write and preamble write were coalesced into one TCP segment, which is why it needed load and
/// only ever showed on one platform. Read must return what it has; see PrefixStreamTests.
///
/// So the 5-second bound below is not impatience. On both legs this test takes ~50 ms when it works, and
/// the failures were never slow - they were silent, and would have stayed silent for any timeout.
/// </remarks>
[TestFixture]
public class SSEStreamStartTests
{

    #region An event source with nothing to send still starts its stream

    [Test]
    public async Task EmptyEventSource_StillSendsTheRetryPreamble()
    {

        var httpServer  = await HTTPServer.StartNew();
        var httpAPI     = httpServer.AddHTTPAPI();

        var sourceId    = HTTPEventSource_Id.Parse("quiet");
        var eventSource = httpAPI.AddEventSource<JObject>(
                              EventSourceId:                sourceId,
                              MaxNumberOfCachedEvents:      100,
                              RetryInterval:                TimeSpan.FromSeconds(7),
                              DataSerializer:               json => json.ToString(Newtonsoft.Json.Formatting.None),
                              DataDeserializer:             null,
                              EnableLogging:                false,
                              LogfilePath:                  null,
                              LogfilePrefix:                null,
                              LogfileName:                  null,
                              LogfileReloadSearchPattern:   null
                          );

        Assert.That(httpAPI.MapEventSource<JObject>(eventSource, HTTPPath.Parse("/quiet"), null), Is.True);

        // Deliberately no SubmitEvent: there is no history and no live event, which is the normal
        // state of an event source a client subscribes to before anything has happened.
        var client = await HTTPClient.ConnectNew(IPv4Address.Localhost, httpServer.TCPPort);
        Assert.That(client.Item1, Is.Not.Null);

        var httpResponse = await client.Item1!.SendRequest(
                                     client.Item1.CreateRequest(
                                         HTTPMethod.GET,
                                         HTTPPath.Parse("/quiet"),
                                         Accept: AcceptTypes.FromHTTPContentTypes(HTTPContentType.Text.EVENTSTREAM)
                                     )
                                 );

        Assert.That(httpResponse, Is.Not.Null);
        Assert.That(httpResponse.HTTPBodyStream, Is.Not.Null, "an SSE response must carry a body stream");

        // A short timeout on purpose. The preamble is written before the server looks at the event
        // source at all, so it owes us those bytes immediately; waiting 30 seconds for them would
        // only reproduce the problem this test exists to prevent.
        using var reader = new StreamReader(httpResponse.HTTPBodyStream!);
        using var cts    = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        string? firstLine;
        try
        {
            firstLine = await reader.ReadLineAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Assert.Fail("no byte arrived within 5 s - the stream never started, which is the silence "
                      + "that turns any early failure into a client-side timeout");
            return;
        }

        Assert.That(firstLine, Is.Not.Null);
        Assert.That(firstLine, Does.StartWith("retry:"), $"expected the retry preamble, got \"{firstLine}\"");
        // 7 s, as configured above - proof the value is the source's own and not a default.
        Assert.That(firstLine, Does.Contain("7000"));

    }

    #endregion

    #region Concurrent submitters keep the wire id and the event id in agreement

    [Test]
    public async Task ConcurrentSubmits_SerializeTheirOwnEventId()
    {

        var httpServer  = await HTTPServer.StartNew();
        var httpAPI     = httpServer.AddHTTPAPI();

        var eventSource = httpAPI.AddEventSource<JObject>(
                              EventSourceId:                HTTPEventSource_Id.Parse("busy"),
                              // Above the number submitted below: the history is a bounded queue, and
                              // a cache smaller than the run would silently drop the early events and
                              // turn this into a test of the cache size instead.
                              MaxNumberOfCachedEvents:      100_000,
                              RetryInterval:                TimeSpan.FromSeconds(1),
                              DataSerializer:               json => json.ToString(Newtonsoft.Json.Formatting.None),
                              DataDeserializer:             null,
                              EnableLogging:                false,
                              LogfilePath:                  null,
                              LogfilePrefix:                null,
                              LogfileName:                  null,
                              LogfileReloadSearchPattern:   null
                          );

        // The counter used to be incremented for the Id and then read again for the "id:" line. With
        // one submitter those are always equal; with several they are equal only by luck, and the
        // wire value is the one a client resumes from.
        //
        // Task.Run, not a bare Select: SubmitEvent assigns the id synchronously, before its first
        // await, so `Select(i => SubmitEvent(i))` would run all 200 assignments back to back on the
        // calling thread and never overlap at all. That version of this test passed against the bug.
        var barrier = new Barrier(Environment.ProcessorCount);
        await Task.WhenAll(Enumerable.Range(0, Environment.ProcessorCount).Select(_ => Task.Run(async () => {
            barrier.SignalAndWait();   // release them together, into the same few instructions
            for (var i = 0; i < 500; i++)
                await eventSource.SubmitEvent("sub", new JObject(new JProperty("i", i)), CancellationToken.None);
        })));

        var expected = Environment.ProcessorCount * 500;

        // GetAllEventsGreater yields the history and then subscribes for live events forever, so it
        // is left once the history is in - and the token bounds the case where it never is.
        var events = new List<HTTPEvent<JObject>>();
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            try
            {
                await foreach (var httpEvent in eventSource.GetAllEventsGreater("test-client", 0, cts.Token))
                {
                    events.Add(httpEvent);
                    if (events.Count == expected)
                        break;
                }
            }
            catch (OperationCanceledException) { }
        }

        Assert.That(events, Has.Count.EqualTo(expected));

        // Assert on the COUNT, not the collection: NUnit renders the whole thing on failure, and
        // thousands of events would bury the one example that explains what went wrong.
        var mismatches = events.Where(e => !e.SerializedHeader.Contains($"id: {e.Id}")).ToArray();
        var example    = mismatches.FirstOrDefault();
        Assert.That(mismatches.Length, Is.Zero,
                    $"{mismatches.Length} of {expected} events serialize an id they do not carry, e.g. event "
                  + $"{example?.Id}: \"{example?.SerializedHeader.Replace(Environment.NewLine, "\\n")}\"");

        // And the ids cover 1..N exactly once each - no duplicates, no gaps.
        Assert.That(events.Select(e => e.Id).Order(),
                    Is.EqualTo(Enumerable.Range(1, expected).Select(i => (UInt64) i)));

    }

    #endregion

}
