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

using System.Text.Json.Serialization;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Argus
{

    public record Measurement(
                                                   DateTimeOffset      Timestamp,

        [property: JsonPropertyName("dnsMs")]      TimeSpan            DNS_Delay,
        [property: JsonPropertyName("tcpMs")]      TimeSpan            TCP_Delay,
        [property: JsonPropertyName("tlsMs")]      TimeSpan            TLS_Delay,
        [property: JsonPropertyName("ttfbMs")]     TimeSpan            TTFB_Delay,
        [property: JsonPropertyName("downloadMs")] TimeSpan            Download_Delay,
        [property: JsonPropertyName("totalMs")]    TimeSpan            TotalTime,

                                                   Int32               StatusCode,
                                                   Boolean             Success,
                                                   String?             Error,

        [property: JsonPropertyName("signature")] SignatureVerification? SignatureVerification = null,

        [property: JsonPropertyName("diag")]       ServerDiagnostics?  ServerDiagnostics = null)
    {

        /// <summary>
        /// The timestamp when the check started. Older log files do not contain this value;
        /// consumers can derive it from Timestamp - TotalTime when needed for display.
        /// </summary>
        [JsonPropertyName("startedAt")]
        public DateTimeOffset? StartedAt { get; init; }

    }

}
