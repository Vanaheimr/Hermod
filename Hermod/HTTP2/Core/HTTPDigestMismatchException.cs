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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// The octets received do not match the digest the sender claimed for them
    /// (RFC 9530). Not a protocol error — the framing was impeccable — but the
    /// payload is not what it says it is, so it is surfaced instead of returned:
    /// handing corrupt bytes to a caller who asked for them to be verified would
    /// defeat the point of asking.
    /// </summary>
    /// <param name="FieldName">Which field disagreed — <c>content-digest</c> or <c>repr-digest</c>.</param>
    public class HTTPDigestMismatchException(String FieldName, String Message)
        : Exception(Message)
    {

        public String  FieldName  { get; } = FieldName;

    }

}
