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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Serves one subsystem on one session's channel - until the client closes it, the server stops, or
    /// the handler is done. The server closes the channel once the handler returns.
    /// </summary>
    /// <remarks>
    /// Before it closes the channel, the server sends the exit-status of RFC 4254, section 6.10: 0 where
    /// the handler returned, 1 where it threw. A handler that has a status of its own to tell sends it
    /// with <see cref="SshMuxChannel.SendExitStatusAsync"/>, and the server then sends none.
    /// </remarks>
    /// <param name="Context">Who asked, for what, and on which channel.</param>
    /// <param name="CancellationToken">Fires when the channel is closed, the server stops, or the connection ends.</param>
    public delegate ValueTask SshSubsystemHandler(SshSubsystemContext  Context,
                                                  CancellationToken    CancellationToken);


    /// <summary>
    /// Asked when a session requests a subsystem: how to serve it for this session, or null to refuse it.
    /// </summary>
    /// <param name="Session">Who signed in: login, method, key, restrictions, peer.</param>
    /// <param name="Name">The subsystem, as the client named it.</param>
    /// <param name="CancellationToken">Fires when the session ends while the answer is being made.</param>
    public delegate ValueTask<SshSubsystemHandler?> SshSubsystemFactory(SshSessionInfo     Session,
                                                                        String             Name,
                                                                        CancellationToken  CancellationToken);


    /// <summary>
    /// What a <see cref="SshSubsystemHandler"/> is given: the session, the subsystem it asked for, and the
    /// channel to serve it on.
    /// </summary>
    /// <param name="Session">Who signed in: login, method, key, restrictions, peer.</param>
    /// <param name="Name">The subsystem, as the client named it.</param>
    /// <param name="Channel">The session's channel: read the client's data from <see cref="SshMuxChannel.Input"/>,
    /// send with <see cref="SshMuxChannel.SendDataAsync"/> - one writer at a time, as the channel does not
    /// keep two writers' data apart.</param>
    /// <param name="Closed">Fires when the client closed the channel, or the connection went.</param>
    /// <param name="Stopping">Fires when the server is stopping: the handler is asked to end.</param>
    public sealed record SshSubsystemContext(SshSessionInfo     Session,
                                             String             Name,
                                             SshMuxChannel      Channel,
                                             CancellationToken  Closed,
                                             CancellationToken  Stopping);

}
