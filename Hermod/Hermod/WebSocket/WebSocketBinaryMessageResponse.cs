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

#region Usings

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// A HTTP WebSocket binary message response.
    /// </summary>
    /// <param name="RequestTimestamp">The request timestamp.</param>
    /// <param name="RequestMessage">The request message.</param>
    /// <param name="ResponseTimestamp">The response timestamp.</param>
    /// <param name="ResponseMessage">The response message.</param>
    /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
    public class WebSocketBinaryMessageResponse(DateTime           RequestTimestamp,
                                                Byte[]             RequestMessage,
                                                DateTime           ResponseTimestamp,
                                                Byte[]             ResponseMessage,
                                                EventTracking_Id   EventTrackingId,
                                                CancellationToken  CancellationToken)
    {

        /// <summary>
        /// The request timestamp.
        /// </summary>
        public DateTime           RequestTimestamp     { get; } = RequestTimestamp;

        /// <summary>
        /// The request message.
        /// </summary>
        public Byte[]             RequestMessage       { get; } = RequestMessage;

        /// <summary>
        /// The response timestamp.
        /// </summary>
        public DateTime           ResponseTimestamp    { get; } = ResponseTimestamp;

        /// <summary>
        /// The response message.
        /// </summary>
        public Byte[]             ResponseMessage      { get; } = ResponseMessage;

        /// <summary>
        /// The event tracking identification for correlating this request with other events.
        /// </summary>
        public EventTracking_Id   EventTrackingId      { get; } = EventTrackingId;


        public CancellationToken  CancellationToken    { get; } = CancellationToken;


    }

}
