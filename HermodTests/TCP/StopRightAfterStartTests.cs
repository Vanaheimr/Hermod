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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// A TCP server that is stopped right after it was started.
    ///
    /// Start() hands the accept loop to a task on a thread of its own and
    /// returns; Stop() cancels the server's token and awaits that task. The
    /// token went to Task.Factory.StartNew as well, and there it decides
    /// something else: whether the task runs at all. A Stop() that came
    /// before the new thread had got going cancelled the task before its first
    /// line, Unwrap() passed that on, and Stop() threw TaskCanceledException
    /// from its await instead of stopping - and DisposeAsync with it. Nor had
    /// the server said that it had started: OnTCPServerStarted is raised at
    /// the start of that task.
    ///
    /// Seen in WWCP_Node's suite, where a node that is started and disposed at
    /// once threw from its DisposeAsync: once in a full run, never in ten runs
    /// of its class alone. How soon a new thread gets going is up to the
    /// scheduler, so one server is started and stopped over and over here,
    /// for a few seconds. Before the fix, each of ten such runs on a 16-core
    /// developer machine had between 65 and 184 stops that threw, in 4800 to
    /// 10400 rounds - and a catch around the await in Stop() instead of the
    /// fix still left as many rounds in which the server had stopped without
    /// having said that it had started.
    ///
    /// Where other work keeps every core busy, the rounds are fewer - a few
    /// hundred, and 69 in one run of the whole suite - and a new thread tends
    /// to get going before Stop() does, so the race is met less often. How
    /// many rounds a run had is in its output.
    /// </summary>
    [TestFixture]
    public class StopRightAfterStartTests
    {

        #region AServerStoppedRightAfterItWasStartedStopsAndHadSaidItStarted()

        [Test]
        public async Task AServerStoppedRightAfterItWasStartedStopsAndHadSaidItStarted()
        {

            // For as long as this rather than for a number of rounds: how many
            // rounds fit in it depends on the machine, and so does how many of
            // them it takes to meet the race once.
            var timeBox    = TimeSpan.FromSeconds(3);

            // IPv4 alone: one listener, bound at every start to a port of the
            // system's choosing. The dual-stack default binds its IPv4 socket
            // at every start to the port it was given in the constructor, and
            // that port is free for anybody between one round and the next.
            var server     = new TCPEchoTestServer(
                                 IPAddress:  IPv4Address.Localhost,
                                 TCPPort:    IPPort.Parse(0)
                             );

            var events     = new ConcurrentQueue<String>();

            server.OnTCPServerStarted += (tcpServer, timestamp, eventTrackingId, message) => {
                events.Enqueue("started");
                return Task.CompletedTask;
            };

            server.OnTCPServerStopped += (tcpServer, timestamp, eventTrackingId, message) => {
                events.Enqueue("stopped");
                return Task.CompletedTask;
            };

            var threw      = new List<Exception>();
            var unstarted  = 0;
            var rounds     = 0;
            var stopwatch  = Stopwatch.StartNew();

            try
            {
                while (stopwatch.Elapsed < timeBox)
                {

                    rounds++;

                    events.Clear();

                    await server.Start();

                    try
                    {
                        await server.Stop();
                    }
                    catch (Exception e)
                    {
                        threw.Add(e);
                    }

                    if (!events.SequenceEqual(["started", "stopped"]))
                        unstarted++;

                }
            }
            finally
            {
                // DisposeAsync stops the server once more, and throws again
                // where the last round's Stop() threw - which would stand in
                // for the counts below with an exception that says less.
                try
                {
                    await server.DisposeAsync();
                }
                catch
                { }
            }

            // A pass says as much as the rounds behind it.
            TestContext.Out.WriteLine($"{rounds} rounds in {stopwatch.Elapsed.TotalSeconds:F1} s");

            Assert.Multiple(() => {

                Assert.That(
                    threw.Count,
                    Is.Zero,
                    $"{threw.Count} of {rounds} stops threw, the first with {threw.FirstOrDefault()?.GetType().Name}: {threw.FirstOrDefault()?.Message}"
                );

                // Counted apart: a Stop() that caught the cancellation would
                // bring the count above down to zero and leave this one where
                // it was.
                Assert.That(
                    unstarted,
                    Is.Zero,
                    $"in {unstarted} of {rounds} rounds the server did not say that it had started before it said that it had stopped"
                );

            });

        }

        #endregion

    }

}
