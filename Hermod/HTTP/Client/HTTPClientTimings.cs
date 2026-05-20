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

    public class HTTPClientTimings
    {

        public TimeSpan               Elapsed
            => Timestamp.Now - Start;

        public List<Elapsed<String>>  Errors                  { get; }


        public DateTimeOffset         Start                   { get; }
        public TimeSpan?              Request                 { get; }
        public TimeSpan?              RequestLogging1         { get; internal set; }
        public TimeSpan?              RequestLogging2         { get; internal set; }
        public TimeSpan?              DNSLookup               { get; internal set; }
        public TimeSpan?              Connected               { get; internal set; }
        public TimeSpan?              TLSHandshake            { get; internal set; }
        public Byte                   RestartCounter          { get; internal set; }
        public UInt64?                RequestHeaderLength     { get; internal set; }
        public TimeSpan?              WriteRequestHeader      { get; internal set; }
        public UInt64?                RequestBodyLength       { get; internal set; }
        public TimeSpan?              WriteRequestBody        { get; internal set; }
        public List<Elapsed<UInt64>>  DataReceived            { get; }
        public DateTimeOffset         ResponseTimestamp       { get; internal set; }
        public TimeSpan?              ResponseHeaderParsed    { get; internal set; }
        public TimeSpan?              ResponseLogging1        { get; internal set; }
        public TimeSpan?              ResponseLogging2        { get; internal set; }



        public HTTPClientTimings(HTTPRequest? HTTPRequest = null)
        {

            var now            = Timestamp.Now;

            this.Start         = HTTPRequest is not null
                                     ? HTTPRequest.Timestamp < now
                                           ? HTTPRequest.Timestamp
                                           : now
                                     : now;

            this.Request       = HTTPRequest is not null
                                     ? HTTPRequest.Timestamp - Start
                                     : null;

            this.DataReceived  = [];
            this.Errors        = [];

        }

        public void AddHTTPResponse(HTTPResponse HTTPResponse)
        {
            this.ResponseTimestamp = HTTPResponse.Timestamp;
        }



        public void AddError(String Error)
        {
            Errors.Add(new Elapsed<String>(Timestamp.Now - Start, Error));
        }


        public String ErrorsAsString()

            => Errors.Select(elapsed => elapsed.Time.TotalMilliseconds.ToString("F2") + ": " + elapsed.Value).AggregateWith(Environment.NewLine);


        public override String ToString()

            => String.Concat(
                    "Start: ",                Start.                                      ToISO8601(),                             " > ",
                    "Request: ",              Request?.                                   TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "RequestLogging1: ",      RequestLogging1?.                           TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "RequestLogging2: ",      RequestLogging2?.                           TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "DNSLookup: ",            DNSLookup?.                                 TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "Connected: ",            Connected?.                                 TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "TLSHandshake: ",         TLSHandshake?.                              TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "RestartCounter: ",       RestartCounter - 1,                                                                  " > ",
                    "WriteRequestHeader: ",   WriteRequestHeader?.                        TotalMilliseconds.ToString("F2") ?? "-", $" ({RequestHeaderLength} bytes) > ",
                    "WriteRequestBody: ",     WriteRequestBody?.                          TotalMilliseconds.ToString("F2") ?? "-", $" ({RequestBodyLength} bytes) > ",
                    DataReceived.Select(elapsed => $"DataReceived: {elapsed.Time.TotalMilliseconds:F2} ({elapsed.Value} bytes)").AggregateWith(" > "), " > ",
                    "ResponseTimestamp: ",   (ResponseTimestamp - Start - Request!.Value).TotalMilliseconds.ToString("F2"), " > ",
                    "ResponseHeaderParsed: ", ResponseHeaderParsed?.                      TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "ResponseLogging1: ",     ResponseLogging1?.                          TotalMilliseconds.ToString("F2") ?? "-", " > ",
                    "ResponseLogging2: ",     ResponseLogging2?.                          TotalMilliseconds.ToString("F2") ?? "-"
                );

    }

}
