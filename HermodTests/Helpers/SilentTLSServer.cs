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

using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests
{

    /// <summary>
    /// A TLS port that takes every connection, reads the ClientHello and then says
    /// nothing, until the client hangs up: a hung server, a load balancer with nothing
    /// behind it, or someone on the path who swallows the ServerHello.
    /// </summary>
    public sealed class SilentTLSServer : IDisposable
    {

        #region Data

        private readonly TcpListener              listener  = new (System.Net.IPAddress.Loopback, 0);
        private readonly CancellationTokenSource  stop      = new ();
        private          Int32                    clientHellos;

        #endregion

        #region Properties

        public IPPort  Port
            => IPPort.Parse((UInt16) ((System.Net.IPEndPoint) listener.LocalEndpoint).Port);

        /// <summary>
        /// How many connections have sent their first bytes so far.
        /// </summary>
        public Int32   ClientHellos
            => Volatile.Read(ref clientHellos);

        /// <summary>
        /// Completed when the first ClientHello has arrived.
        /// </summary>
        public TaskCompletionSource  FirstClientHello { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);

        #endregion

        #region Constructor(s)

        public SilentTLSServer()
        {
            listener.Start();
            _ = Task.Run(AcceptAsync);
        }

        #endregion


        #region (private) AcceptAsync()

        private async Task AcceptAsync()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(stop.Token);
                    _ = Task.Run(() => ServeAsync(client));
                }
            }
            catch
            {
                // stopped
            }
        }

        #endregion

        #region (private) ServeAsync(Client)

        private async Task ServeAsync(TcpClient Client)
        {
            try
            {

                using var client = Client;
                using var stream = client.GetStream();

                var buffer = new Byte[16 * 1024];

                if (await stream.ReadAsync(buffer, stop.Token) > 0)
                {
                    Interlocked.Increment(ref clientHellos);
                    FirstClientHello.TrySetResult();
                }

                // ...and never a ServerHello.
                while (await stream.ReadAsync(buffer, stop.Token) > 0) { }

            }
            catch
            {
                // the client hung up, or the test is over
            }
        }

        #endregion


        #region Dispose()

        public void Dispose()
        {
            stop.Cancel();
            listener.Stop();
        }

        #endregion

    }

}
