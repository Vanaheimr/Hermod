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
                              MeasurementHandler?  OnMeasurement   = null)

        : IMonitor

    {

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
                                        ? HTTPPath.Root
                                        : URL.Path;
                var request       = $"GET {path} HTTP/1.1\r\nHost: {URL.Hostname}\r\nConnection: close\r\nUser-Agent: Vanaheimr Argus/1.0\r\n\r\n";
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
                    if (doc.RootElement.TryGetProperty("diagnostics", out var diagEl))
                        serverDiagnostics = JsonSerializer.Deserialize<ServerDiagnostics>(diagEl.GetRawText(), DiagJsonOpts);
                }
                catch { }
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

            await MeasurementStore.AddAsync(measurement);

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
