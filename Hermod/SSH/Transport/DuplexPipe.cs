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

using System.IO.Pipelines;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A minimal <see cref="IDuplexPipe"/> pairing a reader and a writer. The transport is written
    /// against this interface rather than a socket, which makes fully in-memory loopback handshakes
    /// (client ↔ server, no network) possible for testing.
    /// </summary>
    public sealed class DuplexPipe : IDuplexPipe
    {

        /// <summary>The reader half.</summary>
        public PipeReader  Input     { get; }

        /// <summary>The writer half.</summary>
        public PipeWriter  Output    { get; }

        /// <summary>Create a duplex pipe from a reader and a writer.</summary>
        public DuplexPipe(PipeReader Input, PipeWriter Output)
        {
            this.Input   = Input;
            this.Output  = Output;
        }


        /// <summary>
        /// Wrap a bidirectional <see cref="Stream"/> as an <see cref="IDuplexPipe"/> — used to run a fresh SSH
        /// transport over a tunneled channel stream (the ProxyJump / SSH-over-SSH mechanism).
        /// </summary>
        public static IDuplexPipe FromStream(Stream Stream)
            => new DuplexPipe(PipeReader.Create(Stream), PipeWriter.Create(Stream));


        /// <summary>
        /// Create two cross-wired duplex pipes: whatever is written to one endpoint's output can be
        /// read from the other endpoint's input, and vice versa.
        /// </summary>
        /// <param name="Options">Optional pipe options.</param>
        public static (IDuplexPipe A, IDuplexPipe B) CreateConnectedPair(PipeOptions? Options = null)
        {

            var options  = Options ?? new PipeOptions(useSynchronizationContext: false);
            var aToB      = new Pipe(options);
            var bToA      = new Pipe(options);

            return (
                       new DuplexPipe(bToA.Reader, aToB.Writer),
                       new DuplexPipe(aToB.Reader, bToA.Writer)
                   );

        }

    }

}
