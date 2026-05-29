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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Argus
{

    public class MeasurementStore
    {

        private readonly ConcurrentQueue<Measurement>  measurements   = new();
        private readonly String                        logDirectory;
        private readonly SemaphoreSlim                 writeLock      = new(1, 1);

        public MeasurementStore(String logDir)
        {
            this.logDirectory = logDir;
            Directory.CreateDirectory(logDir);
        }

        public async Task AddAsync(Measurement m)
        {

            measurements.Enqueue(m);

            var cutoff = DateTimeOffset.UtcNow.AddHours(-169);

            while (measurements.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
                measurements.TryDequeue(out _);

            await AppendToFileAsync(m);

        }

        public IReadOnlyList<Measurement> GetRange(DateTimeOffset  from,
                                                   DateTimeOffset  to)

             => [.. measurements.Where(m => m.Timestamp >= from && m.Timestamp <= to)];

        public async Task LoadHistoryAsync(TimeSpan window)
        {

            var cutoff   = DateTimeOffset.UtcNow - window;
            var dates    = Enumerable.Range (0, (int)window.TotalDays + 1).
                                      Select(i => DateTimeOffset.UtcNow.AddDays(-i).ToString("yyyy-MM-dd"));

            var count    = 0;
            var skipped  = 0;
            foreach (var date in dates.Reverse())
            {

                var file = Path.Combine(logDirectory, $"Argus-{date}.jsonl");
                if (!File.Exists(file))
                    continue;

                await foreach (var line in File.ReadLinesAsync(file))
                {
                    if (String.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var m = JsonSerializer.Deserialize<Measurement>(line, JSONSerializerOptions.Default);
                        if (m != null && m.Timestamp >= cutoff)
                        {
                            measurements.Enqueue(m);
                            count++;
                        }
                    }
                    catch
                    {
                        skipped++;
                    }
                }

            }

            if (count > 0)
                Debug.WriteLine($"  Loaded:    {count} measurements from log files");

            if (skipped > 0)
                Debug.WriteLine($"  Skipped: {skipped} defective log lines");

        }

        private async Task AppendToFileAsync(Measurement Measurement)
        {

            await writeLock.WaitAsync();

            try
            {

                await File.AppendAllTextAsync(
                          Path.Combine(
                              logDirectory,
                              $"Argus-{Measurement.Timestamp.UtcDateTime:yyyy-MM-dd}.jsonl"
                          ),
                          JsonSerializer.Serialize(
                              Measurement,
                              JSONSerializerOptions.Default
                          ) + "\n"
                      );

            }
            finally
            {
                writeLock.Release();
            }

        }

    }

}
