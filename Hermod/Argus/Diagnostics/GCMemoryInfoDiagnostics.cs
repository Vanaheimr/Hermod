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

    public record GCMemoryInfoDiagnostics(

        [property: JsonPropertyName("index")]
        Int64                                      Index                       = 0,

        [property: JsonPropertyName("generation")]
        Int32                                      Generation                  = 0,

        [property: JsonPropertyName("compacted")]
        Boolean                                    Compacted                   = false,

        [property: JsonPropertyName("concurrent")]
        Boolean                                    Concurrent                  = false,

        [property: JsonPropertyName("heapSizeMB")]
        Double                                     HeapSizeMB                  = 0,

        [property: JsonPropertyName("fragmentedMB")]
        Double                                     FragmentedMB                = 0,

        [property: JsonPropertyName("promotedMB")]
        Double                                     PromotedMB                  = 0,

        [property: JsonPropertyName("totalCommittedMB")]
        Double                                     TotalCommittedMB            = 0,

        [property: JsonPropertyName("totalAvailableMemoryMB")]
        Double                                     TotalAvailableMemoryMB      = 0,

        [property: JsonPropertyName("memoryLoadMB")]
        Double                                     MemoryLoadMB                = 0,

        [property: JsonPropertyName("highMemoryLoadThresholdMB")]
        Double                                     HighMemoryLoadThresholdMB   = 0,

        [property: JsonPropertyName("pauseTimePercentage")]
        Double                                     PauseTimePercentage         = 0,

        [property: JsonPropertyName("pinnedObjects")]
        Int64                                      PinnedObjects               = 0,

        [property: JsonPropertyName("finalizationPending")]
        Int64                                      FinalizationPending         = 0,

        [property: JsonPropertyName("generations")]
        IReadOnlyList<GCGenerationDiagnostics>?    Generations                 = null

    );

}
