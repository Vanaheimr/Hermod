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

using System;
using System.Linq;
using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public static class Ext3
    {

        public static void WriteLineSMTP(this TCPConnection TCPConn, SMTPStatusCode StatusCode, params String[] Response)
        {

            var n = (UInt64) Response.Where(line => line.IsNotNullOrEmpty()).Count();

            Response.
                Where(line => line.IsNotNullOrEmpty()).
                ForEachCounted((response, i) => {
                    TCPConn.WriteLineToResponseStream(((Int32) StatusCode) + (i < n ? "-" : " ") + response);
                    DebugX.LogT(">> " +           ((Int32) StatusCode) + (i < n ? "-" : " ") + response);
                });

            TCPConn.Flush();

        }

    }

}
