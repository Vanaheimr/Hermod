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
using org.GraphDefined.Vanaheimr.Hermod.HTTPTest;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    //public delegate Task OnHTTPRequestDelegate(HTTPRequest        Request,
    //                                           NetworkStream      Stream,
    //                                           CancellationToken  CancellationToken);


    public static class HTTPAuthPipelineExtension
    {

        public static void AddHTTPAuthPipeline(this HTTPExtAPIX  HTTPAPI,
                                               IUser?            DefaultUser                    = null,
                                               Byte?             RemoteAuthServersMaxHopCount   = null)

            => HTTPAPI.HTTPServer.AddPipeline(
                   new HTTPAuthPipeline(
                       HTTPAPI,
                       DefaultUser,
                       RemoteAuthServersMaxHopCount
                   )
               );

    }


    public class HTTPAuthPipeline(HTTPExtAPIX  HTTPAPI,
                                  IUser?       DefaultUser                    = null,
                                  Byte?        RemoteAuthServersMaxHopCount   = null) : AHTTPPipeline()
    {

        #region Data

        private readonly HTTPExtAPIX HTTPAPI = HTTPAPI;

        #endregion

        #region Properties

        public IUser? DefaultUser                     { get; } = DefaultUser;
        public Byte   RemoteAuthServersMaxHopCount    { get; } = RemoteAuthServersMaxHopCount ?? 0;

        #endregion


        #region (override) ProcessHTTPRequest(Request, Stream, CancellationToken = default)

        public override async Task<(HTTPRequest, HTTPResponse?)>

            ProcessHTTPRequest(HTTPRequest        Request,
                               CancellationToken  CancellationToken   = default)

        {

            var user = await HTTPAPI.CheckHTTPCookie(
                                 Request,
                                 RemoteAuthServersMaxHopCount
                             );

            user ??= HTTPAPI.CheckHTTPAPIKey   (Request) ??
                     HTTPAPI.CheckHTTPBasicAuth(Request);

            Request.User = user ?? DefaultUser;

            //=> LogEvent(
            //       OnHTTPRequest,
            //       loggingDelegate => loggingDelegate.Invoke(
            //           Request,
            //           Stream,
            //           CancellationToken
            //       )
            //   );

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
