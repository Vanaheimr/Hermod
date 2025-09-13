/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    //public delegate Task OnHTTPRequestDelegate(HTTPRequest        Request,
    //                                           NetworkStream      Stream,
    //                                           CancellationToken  CancellationToken);


    /// <summary>
    /// A simple HTTP test server that listens for incoming TCP connections and processes HTTP requests, supporting pipelining.
    /// </summary>
    /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
    /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
    /// <param name="HTTPServerName">An optional HTTP server name. If null or empty, the default HTTP server name will be used.</param>
    /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
    /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
    /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
    /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
    public abstract class AHTTPPipeline()
    {

        #region (override) ProcessHTTPRequest(Request, Stream, CancellationToken = default)

        public virtual async Task<(HTTPRequest, HTTPResponse?)>

            ProcessHTTPRequest(HTTPRequest        Request,
                               CancellationToken  CancellationToken   = default)

        {

            //=> LogEvent(
            //       OnHTTPRequest,
            //       loggingDelegate => loggingDelegate.Invoke(
            //           Request,
            //           Stream,
            //           CancellationToken
            //       )
            //   );

            await Task.Delay(1, CancellationToken);

            return (Request, null);

        }

        #endregion


        #region (private) LogEvent (Logger, LogHandler, ...)

        //private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
        //                                 Func<TDelegate, Task>                              LogHandler,
        //                                 [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
        //                                 [CallerMemberName()]                       String  OICPCommand   = "")

        //    where TDelegate : Delegate

        //    => LogEvent(
        //           nameof(HTTPTestServer),
        //           Logger,
        //           LogHandler,
        //           EventName,
        //           OICPCommand
        //       );

        #endregion


    }

}
