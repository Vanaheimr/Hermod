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
    using org.GraphDefined.Vanaheimr.Hermod.HTTP;
    using System.Diagnostics;


    /// <summary>
    /// Distributed tracing for the HTTP/2 stack, via the BCL's
    /// <see cref="System.Diagnostics.ActivitySource"/> — which is the API
    /// OpenTelemetry consumes, so a caller gets OTel-shaped spans without this
    /// library referencing OpenTelemetry, or anything else.
    ///
    /// Spans nest the way the protocol does: one per connection, one per request
    /// stream inside it. Tag names follow the OpenTelemetry semantic conventions
    /// where they exist, so an exporter needs no translation layer.
    ///
    /// <see cref="ActivitySource.StartActivity"/> returns null when nobody is
    /// listening, and every call site here treats that as normal — an unobserved
    /// stack pays for one null check per connection and per request, and nothing
    /// else. Events (as opposed to spans) go to <see cref="HTTP2EventSource"/>.
    /// </summary>
    public static class HTTP2Diagnostics
    {

        #region Data

        /// <summary>
        /// The name to subscribe to — <c>AddSource("Vanaheimr.Hermod.HTTP2")</c> in
        /// an OpenTelemetry tracer builder, or an <see cref="ActivityListener"/>
        /// matching it.
        /// </summary>
        public const String ActivitySourceName = "Vanaheimr.Hermod.HTTP2";

        /// <summary>
        /// The source every span in this stack comes from.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

        #endregion

        #region StartConnection (RemoteEndPoint, Role)

        /// <summary>
        /// Begin a span covering one connection's whole life. Null when unobserved.
        /// </summary>
        /// <param name="RemoteEndPoint">The peer, for <c>network.peer.address</c>.</param>
        /// <param name="Role">"server" or "client".</param>
        public static Activity? StartConnection(String? RemoteEndPoint, String Role)
        {

            var activity = ActivitySource.StartActivity(
                               $"HTTP/2 connection {Role}",
                               Role == "server" ? ActivityKind.Server : ActivityKind.Client
                           );

            activity?.SetTag("network.protocol.name",    "http");
            activity?.SetTag("network.protocol.version", "2");
            activity?.SetTag("network.peer.address",     RemoteEndPoint);

            return activity;

        }

        #endregion

        #region StartRequest (Method, Scheme, Authority, Path, StreamId, Role)

        /// <summary>
        /// Begin a span covering one request/response exchange on one stream. Null
        /// when unobserved.
        /// </summary>
        public static Activity? StartRequest(HTTPMethod?  Method,
                                             URIScheme?   Scheme,
                                             String?      Authority,
                                             String?      Path,
                                             UInt32       StreamId,
                                             String       Role)
        {

            // OpenTelemetry names an HTTP span after the method (the route, when
            // known, but this layer has no notion of routes — that is the
            // application's business).
            var activity = ActivitySource.StartActivity(
                               Method?.ToString() ?? "HTTP",
                               Role == "server" ? ActivityKind.Server : ActivityKind.Client
                           );

            if (activity is null)
                return null;

            activity.SetTag("http.request.method",       Method);
            activity.SetTag("url.scheme",                Scheme);
            activity.SetTag("url.path",                  Path);
            activity.SetTag("server.address",            Authority);
            activity.SetTag("network.protocol.name",     "http");
            activity.SetTag("network.protocol.version",  "2");
            activity.SetTag("http2.stream_id",           StreamId);

            return activity;

        }

        #endregion

        #region Complete (Activity, Status) / Fail (Activity, Error)

        /// <summary>
        /// Record the response status and close the span. A 5xx is an error by
        /// OpenTelemetry's convention for *server* spans; a 4xx is not, since the
        /// server behaved correctly by rejecting the request.
        /// </summary>
        public static void Complete(Activity? Activity, Int32 Status)
        {

            if (Activity is null)
                return;

            Activity.SetTag("http.response.status_code", Status);

            if (Status >= 500)
            {
                Activity.SetStatus(ActivityStatusCode.Error);
                Activity.SetTag("error.type", Status.ToString());
            }

        }

        /// <summary>
        /// Mark a span as failed, tagging the error the way OTel expects.
        /// </summary>
        public static void Fail(Activity? Activity, Exception Error)
        {

            if (Activity is null)
                return;

            Activity.SetStatus(ActivityStatusCode.Error, Error.Message);
            Activity.SetTag("error.type", Error.GetType().Name);

        }

        #endregion

    }

}
