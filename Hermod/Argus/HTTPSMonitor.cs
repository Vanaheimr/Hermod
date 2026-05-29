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

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.Json;
using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Argus
{

    public class HTTPSMonitor(String               Name,
                              URL                  URL,
                              MeasurementStore     MeasurementStore,
                              IReadOnlyDictionary<String, String>? RequestHeaders = null,
                              MeasurementHandler?  OnMeasurement   = null)

        : IMonitor

    {

        private const Int32   MaxBaselineSamples                    = 120;
        private const Double  PostTimeoutDiscardFactor              = 8.0;

        private static readonly TimeSpan MinPostTimeoutDiscardDelay  = TimeSpan.FromSeconds(2);

        private readonly Queue<TimeSpan> recentSuccessfulTotals      = new();
        private Boolean                  previousCheckTimedOut       = false;
        private readonly IReadOnlyDictionary<String, String> requestHeaders = RequestHeaders ?? new Dictionary<String, String>();

        public async Task RunAsync(TimeSpan interval, CancellationToken ct)
        {
            using var timer = new PeriodicTimer(interval);
            await CheckOnce(ct);
            while (!ct.IsCancellationRequested)
            {
                try { await timer.WaitForNextTickAsync(ct); }
                catch (OperationCanceledException) { break; }
                await CheckOnce(ct);
            }
        }

        private static readonly JsonSerializerOptions DiagJsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = {
                new TimeSpanMillisecondsJsonConverter()
            }
        };

        private static Boolean IsTimeoutError(String? error)
        {

            if (String.IsNullOrWhiteSpace(error))
                return false;

            return error.Contains("timeout",            StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("timed out",          StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("Zeitüberschreitung", StringComparison.OrdinalIgnoreCase);

        }

        private static TimeSpan? Median(IReadOnlyCollection<TimeSpan> values)
        {

            if (values.Count == 0)
                return null;

            var sorted = values.
                             Select(value => value.TotalMilliseconds).
                             Order().
                             ToArray();

            var middle = sorted.Length / 2;

            return TimeSpan.FromMilliseconds(sorted.Length % 2 == 0
                                                 ? (sorted[middle - 1] + sorted[middle]) / 2
                                                 : sorted[middle]);

        }

        private Boolean ShouldDiscardFirstSuccessAfterTimeout(Measurement  measurement,
                                                              out String   reason)
        {

            reason = "";

            if (!previousCheckTimedOut || !measurement.Success || recentSuccessfulTotals.Count < 5)
                return false;

            var median = Median(recentSuccessfulTotals);
            if (median is null)
                return false;

            var threshold = TimeSpan.FromMilliseconds(
                                Math.Max(
                                    MinPostTimeoutDiscardDelay.TotalMilliseconds,
                                    median.Value.TotalMilliseconds * PostTimeoutDiscardFactor
                                )
                            );

            if (measurement.TotalTime <= threshold)
                return false;

            reason = $"first success after timeout took {measurement.TotalTime.TotalMilliseconds:F1} ms; " +
                     $"recent median is {median.Value.TotalMilliseconds:F1} ms, discard threshold is {threshold.TotalMilliseconds:F1} ms";

            return true;

        }

        private void TrackSuccessfulBaseline(Measurement measurement)
        {

            if (!measurement.Success)
                return;

            recentSuccessfulTotals.Enqueue(measurement.TotalTime);

            while (recentSuccessfulTotals.Count > MaxBaselineSamples)
                recentSuccessfulTotals.Dequeue();

        }

        private async Task CheckOnce(CancellationToken ct)
        {

         //   var uri            = new Uri(URL.ToString());
            var stopwatch      = new Stopwatch();
            var dnsDelay       = TimeSpan.Zero;
            var tcpDelay       = TimeSpan.Zero;
            var tlsDelay       = TimeSpan.Zero;
            var ttfbDelay      = TimeSpan.Zero;
            var downloadDelay  = TimeSpan.Zero;
            var statusCode     = 0;
            var success        = false;

            String?            error              = null;
            String             responseBody       = "";
            ServerDiagnostics? serverDiagnostics  = null;
            String?            diagnosticsError   = null;

            try
            {

                stopwatch.Restart();
                var addresses = await Dns.GetHostAddressesAsync(URL.Hostname.ToString(), ct);
                dnsDelay = stopwatch.Elapsed;

                if (addresses.Length == 0)
                    throw new Exception("DNS: keine Adressen");

                var ipAddress  = addresses[0];
                var port       = URL.Port ?? (URL.Protocol == URLProtocols.https
                                                  ? IPPort.HTTPS
                                                  : IPPort.HTTP);

                using var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                stopwatch.Restart();
                await socket.ConnectAsync(new IPEndPoint(ipAddress, port.ToUInt16()), ct);
                tcpDelay = stopwatch.Elapsed;

                Stream stream = new NetworkStream(socket, ownsSocket: false);

                if (URL.Protocol == URLProtocols.https)
                {
                    var sslStream = new SslStream(stream, leaveInnerStreamOpen: false);
                    stopwatch.Restart();
                    await sslStream.AuthenticateAsClientAsync(
                              new SslClientAuthenticationOptions {
                                  TargetHost = URL.Hostname.ToString()
                              },
                              ct
                          );
                    tlsDelay  = stopwatch.Elapsed;
                    stream    = sslStream;
                }

                var path          = URL.Path.IsNotNullOrEmpty
                                        ? URL.Path
                                        : HTTPPath.Root;
                var additionalHeaders = String.Join(
                                            "",
                                            requestHeaders.Select(header => $"{header.Key}: {header.Value}\r\n")
                                        );
                var request       = $"GET {path} HTTP/1.1\r\nHost: {URL.Hostname}\r\nConnection: close\r\nUser-Agent: Vanaheimr Argus/1.0\r\n{additionalHeaders}\r\n";
                var requestBytes  = System.Text.Encoding.ASCII.GetBytes(request);

                stopwatch.Restart();
                await stream.WriteAsync(requestBytes, ct);
                await stream.FlushAsync(ct);

                using var ms = new MemoryStream();
                var buffer = new byte[8192];
                var firstRead = await stream.ReadAsync(buffer, ct);
                ttfbDelay = stopwatch.Elapsed;

                stopwatch.Restart();
                if (firstRead > 0) ms.Write(buffer, 0, firstRead);
                int read;
                while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                    ms.Write(buffer, 0, read);
                downloadDelay = stopwatch.Elapsed;

                var responseText = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                var statusLine = responseText.Split('\r', '\n')[0];
                var parts = statusLine.Split(' ');
                if (parts.Length >= 2 && int.TryParse(parts[1], out var code))
                    statusCode = code;

                var bodyStart = responseText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (bodyStart >= 0)
                    responseBody = responseText[(bodyStart + 4)..];

                success = statusCode == 200;
                stream.Dispose();

            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            if (!String.IsNullOrEmpty(responseBody))
            {
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    var diagEl = doc.RootElement.TryGetProperty("diagnostics", out var nestedDiagnostics)
                                     ? nestedDiagnostics
                                     : doc.RootElement;

                    if (diagEl.TryGetProperty("processorCount", out _) ||
                        diagEl.TryGetProperty("tcp",            out _) ||
                        diagEl.TryGetProperty("threadPool",     out _) ||
                        diagEl.TryGetProperty("gc",             out _) ||
                        diagEl.TryGetProperty("process",        out _) ||
                        diagEl.TryGetProperty("disc",           out _) ||
                        diagEl.TryGetProperty("availableRAM",   out _))
                    {
                        serverDiagnostics = JsonSerializer.Deserialize<ServerDiagnostics>(
                                                diagEl.GetRawText(),
                                                DiagJsonOpts
                                            );
                    }
                }
                catch (Exception ex)
                {
                    diagnosticsError = ex.Message;
                }
            }

            var total        = dnsDelay + tcpDelay + tlsDelay + ttfbDelay + downloadDelay;
            var measurement  = new Measurement(
                                   DateTimeOffset.UtcNow,
                                   dnsDelay,
                                   tcpDelay,
                                   tlsDelay,
                                   ttfbDelay,
                                   downloadDelay,
                                   total,
                                   statusCode,
                                   success,
                                   error,
                                   serverDiagnostics
                               );

            // A timeout can leave the next successful request measuring recovery noise rather than
            // service latency: DNS/TCP/TLS setup, server warm-up, cold caches, or a just-freed queue
            // can dominate that single sample.  We only discard the first successful sample after an
            // actual timeout, only when we already have a small baseline, and only when it is far
            // above both an absolute floor and the recent median.  Normal latency spikes that are not
            // immediately after a timeout remain logged and visible.
            if (ShouldDiscardFirstSuccessAfterTimeout(measurement, out var discardReason))
            {

                previousCheckTimedOut = false;

                OnMeasurement?.Invoke(
                    measurement,
                    Name,
                    URL,
                    "SKIP",
                    "",
                    $" | discarded recovery sample: {discardReason}"
                );

                return;

            }

            await MeasurementStore.AddAsync(measurement);

            TrackSuccessfulBaseline(measurement);
            previousCheckTimedOut = !measurement.Success && IsTimeoutError(measurement.Error);

            var statusIcon   = measurement.Success
                                   ? "OK"
                                   : "FAIL";

            var diagPart     = "";

            if (serverDiagnostics is not null)
            {

                static String FormatNullable(Double? value, String suffix = "", String format = "F1")

                    => value.HasValue
                           ? $"{value.Value.ToString(format)}{suffix}"
                           : "-";

                diagPart = $" | CPU:{FormatNullable(serverDiagnostics.ProcessCpuPercent, "%")}/{FormatNullable(serverDiagnostics.ProcessCpuCores, " cores", "F2")} Proc:{serverDiagnostics.ProcessorCount}" +
                           $" TCP:{serverDiagnostics.ActiveConnections}" +
                           $" TP:{serverDiagnostics.ThreadPoolBusy}/{serverDiagnostics.ThreadPoolThreads} pending:{serverDiagnostics.ThreadPoolPending} completed:{serverDiagnostics.ThreadPoolCompleted}" +
                           $" GC:{serverDiagnostics.GcGen0}/{serverDiagnostics.GcGen1}/{serverDiagnostics.GcGen2} pause:{serverDiagnostics.GcPauseTotalMs.TotalMilliseconds:F1}ms alloc:{serverDiagnostics.GcAllocatedTotalMB:F0}MB heap:{serverDiagnostics.HeapMB:F0}MB ws:{serverDiagnostics.WorkingSetMB:F0}MB" +
                           $" Process:threads:{serverDiagnostics.ProcessThreads} handles:{serverDiagnostics.ProcessHandleCount} private:{serverDiagnostics.ProcessPrivateMB:F0}MB";

            }
            else if (diagnosticsError is not null)
            {
                diagPart = $" | Diagnostics parse failed: {diagnosticsError[..Math.Min(80, diagnosticsError.Length)]}";
            }

            var errorPart    = measurement.Error is not null
                                   ? $" | {measurement.Error[..Math.Min(60, measurement.Error.Length)]}"
                                   : "";

            OnMeasurement?.Invoke(
                measurement,
                Name,
                URL,
                statusIcon,
                diagPart,
                errorPart
            );

        }

    }

}
