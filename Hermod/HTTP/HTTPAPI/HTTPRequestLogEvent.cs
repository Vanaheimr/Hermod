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
    /// An async event notifying about HTTP requests.
    /// </summary>
    public class HTTPRequestLogEvent
    {

        #region Data

        private readonly List<HTTPRequestLogHandlerX> subscribers = [];

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new async event notifying about incoming HTTP requests.
        /// </summary>
        public HTTPRequestLogEvent()
        { }

        #endregion


        #region + / Add

        public static HTTPRequestLogEvent operator + (HTTPRequestLogEvent e, HTTPRequestLogHandlerX callback)
        {

            lock (e.subscribers)
            {
                e.subscribers.Add(callback);
            }

            return e;

        }

        public HTTPRequestLogEvent Add(HTTPRequestLogHandlerX callback)
        {

            lock (subscribers)
            {
                subscribers.Add(callback);
            }

            return this;

        }

        #endregion

        #region - / Remove

        public static HTTPRequestLogEvent operator - (HTTPRequestLogEvent e, HTTPRequestLogHandlerX callback)
        {

            lock (e.subscribers)
            {
                e.subscribers.Remove(callback);
            }

            return e;

        }

        public HTTPRequestLogEvent Remove(HTTPRequestLogHandlerX callback)
        {

            lock (subscribers)
            {
                subscribers.Remove(callback);
            }

            return this;

        }

        #endregion


        #region InvokeAsync (ServerTimestamp, HTTPAPI, Request)

        /// <summary>
        /// Call all subscribers sequentially.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        public async Task InvokeAsync(DateTimeOffset     ServerTimestamp,
                                      HTTPAPI            HTTPAPI,
                                      HTTPRequest        Request,
                                      CancellationToken  CancellationToken)
        {

            HTTPRequestLogHandlerX[] invocationList;

            lock (subscribers)
            {
                invocationList = [.. subscribers];
            }

            foreach (var callback in invocationList)
                await callback(ServerTimestamp, HTTPAPI, Request, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region WhenAny     (ServerTimestamp, HTTPAPI, Request,               Timeout = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for any to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        public Task WhenAny(DateTimeOffset     ServerTimestamp,
                            HTTPAPI            HTTPAPI,
                            HTTPRequest        Request,
                            CancellationToken  CancellationToken,
                            TimeSpan?          Timeout   = null)
        {

            List<Task> invocationList;

            lock (subscribers)
            {

                invocationList = [.. subscribers.Select(callback => callback(ServerTimestamp, HTTPAPI, Request, CancellationToken))];

                if (Timeout.HasValue)
                    invocationList.Add(Task.Delay(Timeout.Value));

            }

            return Task.WhenAny(invocationList);

        }

        #endregion

        #region WhenFirst   (ServerTimestamp, HTTPAPI, Request, VerifyResult, Timeout = null, DefaultResult = null)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <typeparam name="T">The type of the results.</typeparam>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        /// <param name="VerifyResult">A delegate to verify and filter results.</param>
        /// <param name="Timeout">A timeout for this operation.</param>
        /// <param name="DefaultResult">A default result in case of errors or a timeout.</param>
        public Task<T> WhenFirst<T>(DateTimeOffset      ServerTimestamp,
                                    HTTPAPI            HTTPAPI,
                                    HTTPRequest         Request,
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

                invocationList = [.. subscribers.Select(callback => callback(ServerTimestamp, HTTPAPI, Request, CancellationToken))];

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

        #region WhenAll     (ServerTimestamp, HTTPAPI, Request)

        /// <summary>
        /// Call all subscribers in parallel and wait for all to complete.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the event.</param>
        /// <param name="HTTPAPI">The sending HTTP API.</param>
        /// <param name="Request">The HTTP request.</param>
        public Task WhenAll(DateTimeOffset     ServerTimestamp,
                            HTTPAPI            HTTPAPI,
                            HTTPRequest        Request,
                            CancellationToken  CancellationToken)
        {

            Task[] invocationList;

            lock (subscribers)
            {
                invocationList = [.. subscribers.Select(callback => callback(ServerTimestamp, HTTPAPI, Request, CancellationToken))];
            }

            return Task.WhenAll(invocationList);

        }

        #endregion

    }

}
