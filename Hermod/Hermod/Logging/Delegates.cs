/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.Logging
{

    /// <summary>
    /// The delegate for logging the request send by a client.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the outgoing request.</param>
    /// <param name="Client">The client sending the request.</param>
    /// <param name="Request">The outgoing HTTP request.</param>
    public delegate Task APIClientRequestLogHandler(DateTime  Timestamp,
                                                    Object    Client,
                                                    String?   Request);


    /// <summary>
    /// The delegate for logging the response received by a client.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the incoming response.</param>
    /// <param name="Client">The client receiving the request.</param>
    /// <param name="Request">The outgoing request.</param>
    /// <param name="Response">The incoming response.</param>
    public delegate Task APIClientResponseLogHandler(DateTime  Timestamp,
                                                     Object    Client,
                                                     String?   Request,
                                                     String?   Response,
                                                     TimeSpan  Runtime);


    public delegate Task   RequestLoggerDelegate (String LoggingPath, String Context, String LogEventName, String? Request);
    public delegate Task   ResponseLoggerDelegate(String LoggingPath, String Context, String LogEventName, String? Request, String? Response, TimeSpan Runtime);

}
