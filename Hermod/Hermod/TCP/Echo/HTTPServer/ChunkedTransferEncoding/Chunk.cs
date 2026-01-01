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

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A HTTP Chunk with a timestamp and data.
    /// </summary>
    public abstract class Chunk(DateTimeOffset  Timestamp,
                                TimeSpan        Elapsed,
                                UInt64          Counter,
                                Byte[]          Data)
    {

        #region Properties

        public DateTimeOffset  Timestamp    { get; } = Timestamp;
        public TimeSpan        Elapsed      { get; } = Elapsed;
        public UInt64          Counter      { get; } = Counter;
        public Byte[]          Data         { get; } = Data;

        #endregion


    }


    /// <summary>
    /// A HTTP Request Chunk with a timestamp and data.
    /// </summary>
    public class HTTPRequestChunk(HTTPRequest     Request,
                                  DateTimeOffset  Timestamp,
                                  TimeSpan        Elapsed,
                                  UInt64          Counter,
                                  Byte[]          Data)

        : Chunk(Timestamp,
                Elapsed,
                Counter,
                Data)

    {

        #region Properties

        public HTTPRequest  Request    { get; } = Request;

        #endregion


    }


    /// <summary>
    /// A HTTP Response Chunk with a timestamp and data.
    /// </summary>
    public class HTTPResponseChunk(HTTPResponse    Response,
                                   DateTimeOffset  Timestamp,
                                   TimeSpan        Elapsed,
                                   UInt64          Counter,
                                   Byte[]          Data)

        : Chunk(Timestamp,
                Elapsed,
                Counter,
                Data)

    {

        #region Properties

        public HTTPResponse  Response    { get; } = Response;

        #endregion


    }

}
