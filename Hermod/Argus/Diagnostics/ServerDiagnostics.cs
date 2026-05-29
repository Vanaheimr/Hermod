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

    public record ServerDiagnostics
    {

        [JsonPropertyName("processorCount")]
        public UInt32                 ProcessorCount    { get; init; } = 0;

        [JsonPropertyName("tcp")]
        public TCPDiagnostics         TCP               { get; init; } = new();

        [JsonPropertyName("threadPool")]
        public ThreadPoolDiagnostics  ThreadPool        { get; init; } = new();

        [JsonPropertyName("gc")]
        public GCDiagnostics          GC                { get; init; } = new();

        [JsonPropertyName("process")]
        public ProcessDiagnostics     Process           { get; init; } = new();

        [JsonPropertyName("disc")]
        public DiscDiagnostics?       Disc              { get; init; } = null;

        [JsonPropertyName("availableRAM")]
        public AvailableRAMDiagnostics? AvailableRAM    { get; init; } = null;

        public UInt32                 ActiveConnections    => TCP.ActiveConnections;
        public UInt32                 ThreadPoolThreads    => ThreadPool.Threads;
        public UInt64                 ThreadPoolCompleted  => ThreadPool.Completed;
        public UInt64                 ThreadPoolPending    => ThreadPool.Pending;
        public UInt32                 ThreadPoolBusy       => ThreadPool.Busy;
        public UInt32                 GcGen0               => GC.Gen0;
        public UInt32                 GcGen1               => GC.Gen1;
        public UInt32                 GcGen2               => GC.Gen2;
        public TimeSpan               GcPauseTotalMs       => GC.PauseTotalMs;
        public Double                 HeapMB               => GC.HeapMB;
        public Double                 GcAllocatedTotalMB   => GC.AllocatedTotalMB;
        public Double                 WorkingSetMB         => GC.WorkingSetMB;
        public GCMemoryInfoDiagnostics? GcMemoryInfo       => GC.MemoryInfo;
        public Double?                GcHeapSizeMB         => GC.MemoryInfo?.HeapSizeMB;
        public Double?                GcFragmentedMB       => GC.MemoryInfo?.FragmentedMB;
        public Double?                GcPromotedMB         => GC.MemoryInfo?.PromotedMB;
        public Double?                GcTotalCommittedMB   => GC.MemoryInfo?.TotalCommittedMB;
        public Double?                GcMemoryLoadMB       => GC.MemoryInfo?.MemoryLoadMB;
        public Double?                GcPauseTimePercent   => GC.MemoryInfo?.PauseTimePercentage;
        public Int64?                 GcPinnedObjects      => GC.MemoryInfo?.PinnedObjects;
        public Int64?                 GcFinalizationPending => GC.MemoryInfo?.FinalizationPending;
        public Double?                ProcessCpuPercent    => Process.CPUPercent;
        public Double?                ProcessCpuCores      => Process.CPUCores;
        public UInt32                 ProcessThreads       => Process.Threads;
        public UInt32                 ProcessHandleCount   => Process.HandleCount;
        public Double                 ProcessPrivateMB     => Process.PrivateMB;
        public Double?                DiscFreePercent      => Disc?.FreePercent;
        public Double?                AvailableRAMTotalMB  => AvailableRAM?.Total;
        public Double?                AvailableRAMFreeMB   => AvailableRAM?.Free;
        public Double?                AvailableRAMUsedMB   => AvailableRAM?.Used;
        public Double?                AvailableRAMFreePercent => AvailableRAM?.FreePercent;

    }

}
