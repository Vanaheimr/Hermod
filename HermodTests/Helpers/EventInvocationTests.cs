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

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests
{

    /// <summary>
    /// An event in the house shape, for the fixture below to raise.
    /// </summary>
    public delegate Task OnSomethingHappenedDelegate(DateTimeOffset     Timestamp,
                                                     Object             Sender,
                                                     String             Text,
                                                     CancellationToken  CancellationToken);


    /// <summary>
    /// What <see cref="EventInvocation.InvokeAllAsync"/> promises: a handler
    /// which throws is a handler which throws, and not a connection which dies.
    /// </summary>
    /// <remarks>
    /// Counter-checked rather than asserted: against the obvious alternative -
    /// <c>await Task.WhenAll(handlers.GetInvocationList().Select(…))</c> inside
    /// one try/catch, which is how a dozen places in this library still raise
    /// their events - four of these go red. Two for the throw before the first
    /// await (the handlers behind it are never called, and the cancelled one
    /// takes them down with it), one because <c>WhenAll</c> re-throws only the
    /// first exception, and one for the ordering.
    ///
    /// That is the point of writing them down: the difference is invisible
    /// until a subscriber misbehaves, and by then it is somebody else's process
    /// that ended.
    /// </remarks>
    [TestFixture]
    public class EventInvocationTests
    {

        #region Data

        public event OnSomethingHappenedDelegate? OnSomethingHappened;

        private RecordingLogger logger = new ();

        #endregion

        #region SetUp()

        [SetUp]
        public void SetUp()
        {
            OnSomethingHappened  = null;
            logger               = new RecordingLogger();
        }

        #endregion

        #region (private) RaiseAsync()

        private Task RaiseAsync()

            => OnSomethingHappened.InvokeAllAsync(
                   handler => handler(
                                  Timestamp.Now,
                                  this,
                                  "something",
                                  CancellationToken.None
                              ),
                   logger
               );

        #endregion


        #region NobodySubscribed_IsNotAFault()

        /// <summary>
        /// An event nobody listens to is null, and raising it does nothing.
        /// </summary>
        [Test]
        public async Task NobodySubscribed_IsNotAFault()
        {

            await RaiseAsync();

            Assert.That(logger.Errors, Is.Empty);

        }

        #endregion

        #region AHandlerThatThrows_DoesNotReachTheRaiser()

        /// <summary>
        /// The whole purpose: whoever raises the event carries on.
        /// </summary>
        [Test]
        public async Task AHandlerThatThrows_DoesNotReachTheRaiser()
        {

            OnSomethingHappened += (timestamp, sender, text, ct)
                => throw new InvalidOperationException("a subscriber's problem");

            await RaiseAsync();

            Assert.That(logger.Errors,        Has.Count.EqualTo(1));
            Assert.That(logger.Errors[0].Exception?.Message, Is.EqualTo("a subscriber's problem"));

        }

        #endregion

        #region AHandlerThatThrowsBeforeItsFirstAwait_DoesNotSilenceTheOnesBehindIt()

        /// <summary>
        /// The first of the two faults a single <c>WhenAll</c> makes.
        /// </summary>
        /// <remarks>
        /// This handler throws <b>synchronously</b> - before it has returned a
        /// task at all. Under <c>WhenAll(…Select(…))</c> the throw therefore
        /// happens while the list is still being built, so the handlers behind
        /// it are never invoked: one subscriber decides for all the others, and
        /// the two that did nothing wrong are the ones that go quiet.
        /// </remarks>
        [Test]
        public async Task AHandlerThatThrowsBeforeItsFirstAwait_DoesNotSilenceTheOnesBehindIt()
        {

            var second  = false;
            var third   = false;

            OnSomethingHappened += (timestamp, sender, text, ct)
                => throw new InvalidOperationException("before the first await");

            OnSomethingHappened += (timestamp, sender, text, ct) => {
                second = true;
                return Task.CompletedTask;
            };

            OnSomethingHappened += async (timestamp, sender, text, ct) => {
                await Task.Yield();
                third = true;
            };

            await RaiseAsync();

            Assert.That(second,         Is.True,  "The handler after the throwing one was not called!");
            Assert.That(third,          Is.True,  "The handler after the throwing one was not called!");
            Assert.That(logger.Errors,  Has.Count.EqualTo(1));

        }

        #endregion

        #region EveryFailingHandlerIsReported()

        /// <summary>
        /// The second of the two faults a single <c>WhenAll</c> makes.
        /// </summary>
        /// <remarks>
        /// <c>WhenAll</c> re-throws exactly one of the exceptions; the rest are
        /// in an AggregateException nobody looks at. So the second fault hides
        /// behind the first for as long as the first is there - and the first
        /// is usually the one somebody is already working on.
        /// </remarks>
        [Test]
        public async Task EveryFailingHandlerIsReported()
        {

            OnSomethingHappened += async (timestamp, sender, text, ct) => {
                await Task.Yield();
                throw new InvalidOperationException("the first fault");
            };

            OnSomethingHappened += async (timestamp, sender, text, ct) => {
                await Task.Yield();
                throw new InvalidOperationException("the second fault");
            };

            await RaiseAsync();

            Assert.That(logger.Errors.Select(error => error.Exception?.Message),
                        Is.EquivalentTo(new[] { "the first fault", "the second fault" }));

        }

        #endregion

        #region HandlersRunOneAfterAnother_InTheOrderSubscribed()

        /// <summary>
        /// What the <c>Action</c> events did, and what handlers rely on.
        /// </summary>
        /// <remarks>
        /// Each handler waits a little longer than the one after it, so an
        /// order that came out of the scheduler rather than out of the
        /// subscription would be the reverse of this one.
        /// </remarks>
        [Test]
        public async Task HandlersRunOneAfterAnother_InTheOrderSubscribed()
        {

            var order = new List<String>();

            OnSomethingHappened += async (timestamp, sender, text, ct) => {
                await Task.Delay(60, ct);
                lock (order) order.Add("first");
            };

            OnSomethingHappened += async (timestamp, sender, text, ct) => {
                await Task.Delay(30, ct);
                lock (order) order.Add("second");
            };

            OnSomethingHappened += (timestamp, sender, text, ct) => {
                lock (order) order.Add("third");
                return Task.CompletedTask;
            };

            await RaiseAsync();

            Assert.That(order, Is.EqualTo(new[] { "first", "second", "third" }));

        }

        #endregion

        #region AllHandlersHaveFinishedWhenTheRaiserContinues()

        /// <summary>
        /// Awaited, not fired and forgotten.
        /// </summary>
        [Test]
        public async Task AllHandlersHaveFinishedWhenTheRaiserContinues()
        {

            var arrived = false;

            OnSomethingHappened += async (timestamp, sender, text, ct) => {
                await Task.Delay(50, ct);
                arrived = true;
            };

            await RaiseAsync();

            Assert.That(arrived, Is.True);

        }

        #endregion

        #region ACancelledHandlerIsNotAnError()

        /// <summary>
        /// Shutting down is not a fault.
        /// </summary>
        /// <remarks>
        /// A handler that passes the cancellation token on gets this the moment
        /// the connection closes. An error in the log for every subscriber at
        /// every disconnect would teach the reader to skip the log.
        /// </remarks>
        [Test]
        public async Task ACancelledHandlerIsNotAnError()
        {

            var behind = false;

            OnSomethingHappened += (timestamp, sender, text, ct)
                => throw new OperationCanceledException();

            OnSomethingHappened += (timestamp, sender, text, ct) => {
                behind = true;
                return Task.CompletedTask;
            };

            await RaiseAsync();

            Assert.That(logger.Errors,  Is.Empty);
            Assert.That(behind,         Is.True);

        }

        #endregion

        #region TheEventNamesItselfInTheLog()

        /// <summary>
        /// The name comes from the compiler, out of the call site, so that it
        /// cannot fall out of step with the event it names.
        /// </summary>
        [Test]
        public async Task TheEventNamesItselfInTheLog()
        {

            OnSomethingHappened += (timestamp, sender, text, ct)
                => throw new InvalidOperationException("boom");

            await RaiseAsync();

            Assert.That(logger.Errors,             Has.Count.EqualTo(1));
            Assert.That(logger.Errors[0].Message,  Does.Contain(nameof(OnSomethingHappened)));

        }

        #endregion

        #region WithoutALogger_AFailingHandlerIsStillContained()

        /// <summary>
        /// The logger is optional; the containment is not.
        /// </summary>
        [Test]
        public async Task WithoutALogger_AFailingHandlerIsStillContained()
        {

            var behind = false;

            OnSomethingHappened += (timestamp, sender, text, ct)
                => throw new InvalidOperationException("boom");

            OnSomethingHappened += (timestamp, sender, text, ct) => {
                behind = true;
                return Task.CompletedTask;
            };

            await OnSomethingHappened.InvokeAllAsync(
                      handler => handler(
                                     Timestamp.Now,
                                     this,
                                     "something",
                                     CancellationToken.None
                                 ),
                      null
                  );

            Assert.That(behind, Is.True);

        }

        #endregion


        #region (class) RecordingLogger

        /// <summary>
        /// An ILogger that keeps what it was told instead of writing it.
        /// </summary>
        private sealed class RecordingLogger : ILogger
        {

            public List<(LogLevel Level, String Message, Exception? Exception)> Entries { get; } = [];

            public List<(LogLevel Level, String Message, Exception? Exception)> Errors
                => [.. Entries.Where(entry => entry.Level >= LogLevel.Error)];

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull

                => null;

            public Boolean IsEnabled(LogLevel logLevel)
                => true;

            public void Log<TState>(LogLevel                         logLevel,
                                    EventId                          eventId,
                                    TState                           state,
                                    Exception?                       exception,
                                    Func<TState, Exception?, String> formatter)

                => Entries.Add((logLevel, formatter(state, exception), exception));

        }

        #endregion

    }

}
