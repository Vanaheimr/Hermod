/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Threading;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    public class WebSocketBinaryMessageResponse
    {

        public EventTracking_Id  EventTrackingId      { get; }

        public DateTime          RequestTimestamp     { get; }

        public Byte[]            Request              { get; }

        public DateTime          ResponseTimestamp    { get; }

        public Byte[]            Response             { get; }


        public WebSocketBinaryMessageResponse(EventTracking_Id  EventTrackingId,
                                              DateTime          RequestTimestamp,
                                              Byte[]            Request,
                                              DateTime          ResponseTimestamp,
                                              Byte[]            Response)
        {

            this.EventTrackingId    = EventTrackingId;
            this.RequestTimestamp   = RequestTimestamp;
            this.Request            = Request;
            this.ResponseTimestamp  = ResponseTimestamp;
            this.Response           = Response;

        }

    }

}
