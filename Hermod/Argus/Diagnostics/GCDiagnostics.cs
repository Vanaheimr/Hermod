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

    public record GCDiagnostics(

        [property: JsonPropertyName("gen0")]
        UInt32    Gen0              = 0,

        [property: JsonPropertyName("gen1")]
        UInt32    Gen1              = 0,

        [property: JsonPropertyName("gen2")]
        UInt32    Gen2              = 0,

        [property: JsonPropertyName("pauseTotalMs")]
        TimeSpan  PauseTotalMs      = default,

        [property: JsonPropertyName("heapMB")]
        Double    HeapMB            = 0,

        [property: JsonPropertyName("allocatedTotalMB")]
        Double    AllocatedTotalMB  = 0,

        [property: JsonPropertyName("workingSetMB")]
        Double    WorkingSetMB      = 0

    );

}
