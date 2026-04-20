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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An async event notifying about HTTP responses.
    /// </summary>
    public class HTTPResponseLogEvent
    {

        #region Data

        private readonly List<HTTPResponseLogHandlerX> subscribers = [];

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new async event notifying about HTTP responses.
        /// </summary>
        public HTTPResponseLogEvent()
        { }

        #endregion


        #region + / Add

        public static HTTPResponseLogEvent operator + (HTTPResponseLogEvent e, HTTPResponseLogHandlerX callback)
        {

            lock (e.subscribers)
            {
                e.subscribers.Add(
                    (timestamp, api, request, response, cancellationToken)
                        => callback(timestamp, api, request, response, cancellationToken)
                );
            }

            return e;

        }

        public HTTPResponseLogEvent Add(HTTPResponseLogHandlerX callback)
        {

            lock (subscribers)
            {
                subscribers.Add(callback);
            }

            return this;

        }

        #endregion

        #region - / Remove

        public static HTTPResponseLogEvent operator - (HTTPResponseLogEvent e, HTTPResponseLogHandlerX callback)
        {

            lock (e.subscribers)
            {
                e.subscribers.Remove(callback);
            }

            return e;

        }

        public HTTPResponseLogEvent Remove(HTTPResponseLogHandlerX callback)
        {

            lock (subscribers)
            {
                subscribers.Remove(callback);
            }

            return this;

        }

        #endregion


        #region InvokeAsync(ServerTimestamp, HTTPAPI, Request, Response)

        /// <summary>
        /// Call all subscribers sequentially.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        public async Task InvokeAsync(DateTimeOffset     ServerTimestamp,
                                      HTTPAPI            HTTPAPI,
                                      HTTPRequest        Request,
                                      HTTPResponse       Response,
                                      CancellationToken  CancellationToken)
        {

            HTTPResponseLogHandlerX[] invocationList;

            lock (subscribers)
            {
                invocationList = [.. subscribers];
            }

            foreach (var callback in invocationList)
                await callback(ServerTimestamp, HTTPAPI, Request, Response, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region WhenAny    (ServerTimestamp, HTTPAPI, Request, Response,               Timeout = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for any to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        public Task WhenAny(DateTimeOffset     ServerTimestamp,
                            HTTPAPI            HTTPAPI,
                            HTTPRequest        Request,
                            HTTPResponse       Response,
                            CancellationToken  CancellationToken,
                            TimeSpan?          Timeout = null)
        {

            List<Task> invocationList;

            lock (subscribers)
            {

                invocationList = [.. subscribers.Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response, CancellationToken))];

                if (Timeout.HasValue)
                    invocationList.Add(Task.Delay(Timeout.Value));

            }

            return Task.WhenAny(invocationList);

        }

        #endregion

        #region WhenFirst  (ServerTimestamp, HTTPAPI, Request, Response, VerifyResult, Timeout = null, DefaultResult = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <typeparam name="T">The type of the results.</typeparam>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        /// <param name="VerifyResult">A delegate to verify and filter results.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        /// <param name="DefaultResult">A default result in case of errors or a timeout.</param>
        public Task<T> WhenFirst<T>(DateTimeOffset      ServerTimestamp,
                                    HTTPAPI            HTTPAPI,
                                    HTTPRequest         Request,
                                    HTTPResponse        Response,
                                    Func<T, Boolean>    VerifyResult,
                                    CancellationToken   CancellationToken,
                                    TimeSpan?           Timeout         = null,
                                    Func<TimeSpan, T>?  DefaultResult   = null)
        {

            #region Data

            List<Task>      invocationList;
            Task?           WorkDone;
            Task<T>?        Result;
            DateTimeOffset  StartTime     = Timestamp.Now;
            Task?           TimeoutTask   = null;

            #endregion

            lock (subscribers)
            {

                invocationList = [.. subscribers.Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response, CancellationToken))];

                if (Timeout.HasValue)
                    invocationList.Add(TimeoutTask = Task.Run(() => Thread.Sleep(Timeout.Value)));

            }

            do
            {

                try
                {

                    WorkDone = Task.WhenAny(invocationList);

                    invocationList.Remove(WorkDone);

                    if (WorkDone != TimeoutTask)
                    {

                        Result = WorkDone as Task<T>;

                        if (Result is not null &&
                            !EqualityComparer<T>.Default.Equals(Result.Result, default) &&
                            VerifyResult(Result.Result))
                        {
                            return Result;
                        }

                    }

                }
                catch (Exception e)
                {
                    DebugX.LogT(e.Message);
                    WorkDone = null;
                }

            }
            while (!(WorkDone == TimeoutTask || invocationList.Count == 0));

            return Task.FromResult(DefaultResult(Timestamp.Now - StartTime));

        }

        #endregion

        #region WhenAll    (ServerTimestamp, HTTPAPI, Request, Response)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Response">The HTTP response.</param>
        public Task WhenAll(DateTimeOffset     ServerTimestamp,
                            HTTPAPI            HTTPAPI,
                            HTTPRequest        Request,
                            HTTPResponse       Response,
                            CancellationToken  CancellationToken)
        {

            Task[] invocationList;

            lock (subscribers)
            {
                invocationList = [.. subscribers.Select(callback => callback(ServerTimestamp, HTTPAPI, Request, Response, CancellationToken))];
            }

            return Task.WhenAll(invocationList);

        }

        #endregion

    }

}
