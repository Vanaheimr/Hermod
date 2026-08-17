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

using Microsoft.Extensions.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// One way to raise an event, so that a handler which throws is a handler
    /// which throws - and not a connection which dies.
    /// </summary>
    /// <remarks>
    /// There are two ways to get this wrong, and both of them are cheap to
    /// make and expensive to find.
    ///
    /// The first is the synchronous handler. An <c>Action</c> raised from a
    /// read loop runs inside that loop, so an exception in it travels straight
    /// into the loop and takes the connection with it: a debug display that
    /// trips over a null ends somebody's session. And whoever wants to do
    /// something asynchronous in such a handler - store it, answer it, forward
    /// it, which is most of what one does on receiving something - has only
    /// <c>async void</c> left. An exception in an <c>async void</c> lambda is
    /// not caught by the caller, because by then there is no caller any more:
    /// it lands on the thread pool, and the process ends. That is not a
    /// hypothetical; it is what an application hits on its first failed
    /// database write.
    ///
    /// The second is the <c>Task.WhenAll</c> over the invocation list inside
    /// one try/catch. It looks like it covers everything, and it covers less
    /// than it looks - see the remarks on the method.
    ///
    /// Task-returning delegates remove the first, this class the second.
    /// </remarks>
    public static class EventInvocation
    {

        #region InvokeAllAsync(this Handlers, Invocation, Logger, EventName = ...)

        /// <summary>
        /// Calls every registered handler and waits for it.
        /// </summary>
        /// <param name="Handlers">The event; null when nobody has subscribed.</param>
        /// <param name="Invocation">Calls one handler with the arguments of this event.</param>
        /// <param name="Logger">Where a failing handler is reported; without one it stays silent.</param>
        /// <param name="EventName">
        /// The name of the event - supplied by the compiler from the call site, so
        /// that it cannot fall out of step with the event it names.
        /// </param>
        /// <remarks>
        /// <b>One after another, in the order subscribed</b>, and not
        /// <see cref="Task.WhenAll(IEnumerable{Task})"/>. That is what the
        /// <c>Action</c> events did, handlers rely on it, and doing them at once
        /// would buy nothing: the raiser waits for all of them either way, so the
        /// only difference is whether two handlers may see each other's
        /// half-finished state.
        ///
        /// <b>Every handler in its own try/catch</b>, and this is the part that a
        /// single <c>WhenAll</c> in a try/catch gets wrong twice over. A handler
        /// that throws before its first <c>await</c> throws while the list is
        /// still being built, and the handlers behind it are then never called at
        /// all - one subscriber decides for all the others. And of those that do
        /// fail, <c>WhenAll</c> re-throws exactly one; the rest are in the
        /// AggregateException nobody looks at, so a second fault can hide behind
        /// the first for as long as the first is there.
        ///
        /// <b>Nothing comes back out.</b> The alternative would be to let it
        /// through to whatever raised the event, and then a single subscriber
        /// decides whether the connection lives. Whoever wants their exception to
        /// have consequences has to say so in their own handler.
        /// </remarks>
        public static async Task InvokeAllAsync<TDelegate>(this TDelegate?        Handlers,
                                                           Func<TDelegate, Task>  Invocation,
                                                           ILogger?               Logger,

                                                           [CallerArgumentExpression(nameof(Handlers))]
                                                           String?                EventName   = null)

            where TDelegate : Delegate

        {

            if (Handlers is null)
                return;

            foreach (var handler in Handlers.GetInvocationList().OfType<TDelegate>())
            {
                try
                {
                    await Invocation(handler).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Shutting down is not a fault. A handler that passes the
                    // cancellation token on gets this the moment the connection
                    // closes, and an error in the log for every subscriber at
                    // every disconnect would teach the reader to skip the log.
                }
                catch (Exception e)
                {
                    Logger?.LogError(e,
                                     "A handler of {EventName} threw - the event carries on to the remaining handlers",
                                     EventName ?? "an event");
                }
            }

        }

        #endregion

    }

}
