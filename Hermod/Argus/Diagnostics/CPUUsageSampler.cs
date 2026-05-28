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

using System.Diagnostics;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Argus
{

    public sealed class CPUUsageSampler
    {

        private readonly  Lock       sync = new();

        private           TimeSpan?  lastCpuTime;
        private           Int64?     lastTimestamp;

        public CPUUsageSample? Sample()
        {
            lock (sync)
            {

                using var process  = Process.GetCurrentProcess();

                var nowTimestamp   = Stopwatch.GetTimestamp();
                var cpuTime        = process.TotalProcessorTime;

                if (!lastCpuTime.HasValue || !lastTimestamp.HasValue)
                {
                    lastCpuTime    = cpuTime;
                    lastTimestamp  = nowTimestamp;
                    return null;
                }

                var elapsedSeconds  = (nowTimestamp - lastTimestamp.Value) / (Double) Stopwatch.Frequency;
                var cpuSeconds      = (cpuTime      - lastCpuTime.  Value).TotalSeconds;

                lastCpuTime         = cpuTime;
                lastTimestamp       = nowTimestamp;

                if (elapsedSeconds <= 0)
                    return null;

                var cores           = cpuSeconds / elapsedSeconds;

                return new CPUUsageSample(
                           cores / Environment.ProcessorCount * 100.0,
                           cores
                       );

            }
        }

    }

}
