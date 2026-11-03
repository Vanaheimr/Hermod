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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP
{

    /// <summary>
    /// What one session's <c>sftp</c> subsystem is served over: a file system made for that session, and
    /// the profile, limits and end that go with it - what an <see cref="SftpSessionFactory"/> answers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file system belongs to the session from here on. Where it is <see cref="IAsyncDisposable"/> or
    /// <see cref="IDisposable"/>, it is disposed once the subsystem has ended, however it ended - the
    /// client closing it, <see cref="Ends"/>, the connection going - so that it can close what the client
    /// left open and say how the session went. A file system shared by several sessions must therefore
    /// not be disposable, or be wrapped in one that is not.
    /// </para>
    /// <para>
    /// <see cref="Ends"/> is for whoever let the session in and may change their mind while it lasts: an
    /// account switched off, a key withdrawn. When it fires, the request in flight is cancelled and the
    /// channel is closed, which every client reads as the end of the session.
    /// </para>
    /// </remarks>
    /// <param name="FileSystem">The file system this session sees.</param>
    public sealed record SftpSessionOptions(ISftpFileSystem FileSystem)
    {

        /// <summary>
        /// The operations this session may do at all, or null for every one the file system serves.
        /// </summary>
        public SshAccessProfile?   Profile  { get; init; }

        /// <summary>
        /// This session's quotas and bandwidth caps, or null for none.
        /// </summary>
        public SftpLimits?         Limits   { get; init; }

        /// <summary>
        /// Fires when the session is to end before its client ends it; none by default.
        /// </summary>
        public CancellationToken   Ends     { get; init; }

    }


    /// <summary>
    /// Asked when a session requests the <c>sftp</c> subsystem, with who signed in on it: what that
    /// session is served over, or null to refuse it.
    /// </summary>
    /// <param name="Session">Who signed in, from where, with what.</param>
    /// <param name="CancellationToken">Fires when the connection ends while the answer is being made.</param>
    public delegate ValueTask<SftpSessionOptions?> SftpSessionFactory(SshSessionInfo     Session,
                                                                      CancellationToken  CancellationToken);

}
