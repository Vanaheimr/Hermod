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

namespace org.GraphDefined.Vanaheimr.Hermod.Argus
{

    public static class MeasurementAggregator
    {
        public static List<Measurement> Aggregate(IReadOnlyList<Measurement>  Measurements,
                                                  UInt32                      maxPoints)
        {

            if (Measurements.Count <= maxPoints)
                return [.. Measurements];

            var bucketSize  = (Int32) Math.Ceiling(Measurements.Count / (Double) maxPoints);
            var result      = new List<Measurement>((Int32) Math.Ceiling(Measurements.Count / (Double) bucketSize));

            for (var i = 0; i < Measurements.Count; i += bucketSize)
            {

                var bucket           = Measurements.Skip(i).Take(bucketSize).ToList();
                var worst            = bucket.      MaxBy(measurement => measurement.TotalTime) ?? bucket[0];
                var diagItems        = bucket.      Where(measurement => measurement.ServerDiagnostics is not null).ToList();
                var lastDiag         = diagItems.   LastOrDefault()?.ServerDiagnostics;
                var bucketTimestamp  = bucket[0].Timestamp + TimeSpan.FromTicks((bucket[^1].Timestamp - bucket[0].Timestamp).Ticks / 2);

                ServerDiagnostics? serverDiagnostics = null;

                if (lastDiag is not null)
                {

                    static Double Avg(List<Measurement> items, Func<ServerDiagnostics, Double> selector)

                         => items.Count == 0
                                ? 0
                                : items.Average(m => selector(m.ServerDiagnostics!));

                    static Double? AvgNullable(List<Measurement> items, Func<ServerDiagnostics, Double?> selector)
                    {

                        var values = items.
                                         Select(m => selector(m.ServerDiagnostics!)).
                                         Where(value => value.HasValue).
                                         Select(value => value!.Value).
                                         ToList();

                        return values.Count == 0
                                   ? null
                                   : values.Average();

                    }

                    serverDiagnostics = new ServerDiagnostics(
                                            ProcessorCount:  lastDiag.ProcessorCount,
                                            TCP:             new TCPDiagnostics(
                                                                 ActiveConnections:  (UInt32) Math.Round(Avg(diagItems, serverDiagnostics => serverDiagnostics.ActiveConnections))
                                                             ),
                                            ThreadPool:      new ThreadPoolDiagnostics(
                                                                 Threads:            (UInt32) Math.Round(Avg(diagItems, serverDiagnostics => serverDiagnostics.ThreadPoolThreads)),
                                                                 Completed:          lastDiag.ThreadPoolCompleted,
                                                                 Pending:            (UInt64) Math.Round(Avg(diagItems, serverDiagnostics => serverDiagnostics.ThreadPoolPending)),
                                                                 Busy:               (UInt32) Math.Round(Avg(diagItems, serverDiagnostics => serverDiagnostics.ThreadPoolBusy))
                                                             ),
                                            GC:              new GCDiagnostics(
                                                                 Gen0:               lastDiag.GcGen0,
                                                                 Gen1:               lastDiag.GcGen1,
                                                                 Gen2:               lastDiag.GcGen2,
                                                                 PauseTotalMs:       lastDiag.GcPauseTotalMs,
                                                                 HeapMB:             Avg(diagItems, serverDiagnostics => serverDiagnostics.HeapMB),
                                                                 AllocatedTotalMB:   lastDiag.GcAllocatedTotalMB,
                                                                 WorkingSetMB:       Avg(diagItems, serverDiagnostics => serverDiagnostics.WorkingSetMB)
                                                             ),
                                            Process:         new ProcessDiagnostics(
                                                                 CPUPercent:         AvgNullable(diagItems, serverDiagnostics => serverDiagnostics.ProcessCpuPercent),
                                                                 CPUCores:           AvgNullable(diagItems, serverDiagnostics => serverDiagnostics.ProcessCpuCores),
                                                                 Threads:            (UInt32) Math.Round(Avg(diagItems, serverDiagnostics => serverDiagnostics.ProcessThreads)),
                                                                 HandleCount:        (UInt32) Math.Round(Avg(diagItems, serverDiagnostics => serverDiagnostics.ProcessHandleCount)),
                                                                 PrivateMB:          Avg(diagItems, serverDiagnostics => serverDiagnostics.ProcessPrivateMB)
                                                             )
                                        );

                }

                result.Add(worst with {
                    Timestamp          = bucketTimestamp,
                    Success            = bucket.All(m => m.Success),
                    Error              = bucket.FirstOrDefault(m => m.Error is not null)?.Error,
                    ServerDiagnostics  = serverDiagnostics
                });

            }

            return result;

        }

    }

}
