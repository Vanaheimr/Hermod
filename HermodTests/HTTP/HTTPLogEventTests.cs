/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Subscribing to and unsubscribing from the HTTP request and response log events.
    ///
    /// HTTPServerLogger subscribes a log target with + and unsubscribes it with -,
    /// so - has to take off what + put on, just as Remove() takes off what Add() put on.
    /// </summary>
    [TestFixture]
    public class HTTPLogEventTests
    {

        /// <summary>
        /// How a handler is put on or taken off: by the operator or by the method.
        /// </summary>
        public enum Way
        {
            Operator,
            Method
        }


        #region RequestLogEvent_RemovedHandlerIsNotCalled  (Added, Removed)

        /// <summary>
        /// A handler taken off the request log event is not called any more,
        /// whichever way it was put on and taken off.
        /// </summary>
        [TestCase(Way.Operator, Way.Operator)]
        [TestCase(Way.Operator, Way.Method)]
        [TestCase(Way.Method,   Way.Operator)]
        [TestCase(Way.Method,   Way.Method)]
        public async Task RequestLogEvent_RemovedHandlerIsNotCalled(Way Added, Way Removed)
        {

            var logEvent  = new HTTPRequestLogEvent();
            var calls     = 0;

            HTTPRequestLogHandlerX handler = (timestamp, api, request, cancellationToken) => {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            };

            if (Added == Way.Operator)
                logEvent += handler;
            else
                logEvent.Add(handler);

            await logEvent.InvokeAsync(Timestamp.Now, null!, null!, CancellationToken.None);

            Assert.That(calls, Is.EqualTo(1), "The handler is called while it is on.");

            if (Removed == Way.Operator)
                logEvent -= handler;
            else
                logEvent.Remove(handler);

            await logEvent.InvokeAsync(Timestamp.Now, null!, null!, CancellationToken.None);
            await logEvent.WhenAll    (Timestamp.Now, null!, null!, CancellationToken.None);

            Assert.That(calls, Is.EqualTo(1), "The handler is not called once it is off.");

        }

        #endregion

        #region ResponseLogEvent_RemovedHandlerIsNotCalled (Added, Removed)

        /// <summary>
        /// A handler taken off the response log event is not called any more,
        /// whichever way it was put on and taken off.
        /// </summary>
        [TestCase(Way.Operator, Way.Operator)]
        [TestCase(Way.Operator, Way.Method)]
        [TestCase(Way.Method,   Way.Operator)]
        [TestCase(Way.Method,   Way.Method)]
        public async Task ResponseLogEvent_RemovedHandlerIsNotCalled(Way Added, Way Removed)
        {

            var logEvent  = new HTTPResponseLogEvent();
            var calls     = 0;

            HTTPResponseLogHandlerX handler = (timestamp, api, request, response, cancellationToken) => {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            };

            if (Added == Way.Operator)
                logEvent += handler;
            else
                logEvent.Add(handler);

            await logEvent.InvokeAsync(Timestamp.Now, null!, null!, null!, CancellationToken.None);

            Assert.That(calls, Is.EqualTo(1), "The handler is called while it is on.");

            if (Removed == Way.Operator)
                logEvent -= handler;
            else
                logEvent.Remove(handler);

            await logEvent.InvokeAsync(Timestamp.Now, null!, null!, null!, CancellationToken.None);
            await logEvent.WhenAll    (Timestamp.Now, null!, null!, null!, CancellationToken.None);

            Assert.That(calls, Is.EqualTo(1), "The handler is not called once it is off.");

        }

        #endregion

    }

}
