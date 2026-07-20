/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP2
{

    /// <summary>
    /// A real .NET Kestrel HTTP/2 server on an ephemeral loopback port, used as
    /// the strict production reference peer for our hand-rolled client (the same
    /// <c>WebApplication</c>-on-a-real-port pattern the HTTP/1.1 tests use, but
    /// with <see cref="HttpProtocols.Http2"/> — TLS with a self-signed cert, or
    /// cleartext h2c). Routes are mapped via the <c>Configure</c> callback.
    /// Disposed at end of test.
    /// </summary>
    internal sealed class KestrelH2Server : IAsyncDisposable
    {

        private readonly WebApplication app;

        /// <summary>The ephemeral loopback port Kestrel is listening on.</summary>
        public Int32 Port { get; }

        private KestrelH2Server(WebApplication App, Int32 Port)
        {
            app       = App;
            this.Port = Port;
        }

        /// <summary>
        /// Start Kestrel (HTTP/2 only) on a free loopback port — TLS with a fresh
        /// self-signed cert unless <paramref name="Cleartext"/> (then h2c
        /// prior-knowledge) — and map routes via <paramref name="Configure"/>.
        /// </summary>
        public static async Task<KestrelH2Server> StartAsync(Action<WebApplication> Configure, Boolean Cleartext = false)
        {

            var port    = H2.FreePort();
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenLocalhost(port, listen =>
                {
                    listen.Protocols = HttpProtocols.Http2;   // HTTP/2 only — forces our client to speak it
                    if (!Cleartext)
                        listen.UseHttps(H2.MakeCert());
                });
            });

            var app = builder.Build();
            Configure(app);
            await app.StartAsync();

            return new KestrelH2Server(app, port);

        }

        public async ValueTask DisposeAsync()
        {
            try { await app.StopAsync();    } catch { }
            try { await app.DisposeAsync(); } catch { }
        }

    }

}
