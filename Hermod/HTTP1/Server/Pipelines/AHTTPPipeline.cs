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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    //public delegate Task OnHTTPRequestDelegate(HTTPRequest        Request,
    //                                           NetworkStream      Stream,
    //                                           CancellationToken  CancellationToken);


    /// <summary>
    /// The base of the HTTP pipelines an <see cref="HTTPServer"/> runs on every request, before the URL is matched to a handler.
    /// </summary>
    /// <remarks>
    /// <see cref="HTTPServer.AddPipeline"/> adds a pipeline, and the server runs its pipelines in the order they were added.
    /// A pipeline that returns no response lets the request carry on. The first one that returns a response is the last:
    /// the server answers the request with it, and neither the remaining pipelines nor a handler see the request.
    /// </remarks>
    public abstract class AHTTPPipeline()
    {

        #region (virtual) ProcessHTTPRequest(Request, CancellationToken = default)

        /// <summary>
        /// Process the given HTTP request. This one passes it on unchanged.
        /// </summary>
        /// <param name="Request">The HTTP request to process.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel the processing of the HTTP request.</param>
        /// <returns>The HTTP request to pass on, and an HTTP response to answer it with, or null to let it carry on.</returns>
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
