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
    /// The specification of a remote command to start over a session channel: the command line plus
    /// optional standard input, environment variables and a pseudo-terminal request. Passed to
    /// <see cref="SshConnection.StartCommandAsync"/>, which returns a live <see cref="SshCommandProcess"/>
    /// for streaming interaction (in contrast to the one-shot, fully-buffered
    /// <see cref="SshConnection.ExecuteAsync"/>).
    /// </summary>
    public sealed record SshCommand
    {

        /// <summary>The command line to run (empty string requests an interactive shell instead).</summary>
        public String                       CommandLine           { get; init; }

        /// <summary>An optional stream piped to the remote command's standard input; null for no input.</summary>
        public Stream?                      Input                 { get; init; }

        /// <summary>
        /// Environment variables to set via <c>env</c> channel requests before starting the command (subject
        /// to the peer's <c>AcceptEnv</c> policy — unaccepted variables are silently ignored by the server).
        /// </summary>
        public Dictionary<String, String>   EnvironmentVariables  { get; init; } = [];

        /// <summary>Whether to request a pseudo-terminal (<c>pty-req</c>); without a PTY stdout/stderr stay separate.</summary>
        public Boolean                      UsePty                { get; init; }

        /// <summary>The terminal type advertised in the <c>pty-req</c> (default <c>xterm-256color</c>).</summary>
        public String                       TerminalType          { get; init; } = "xterm-256color";

        /// <summary>The terminal width in columns for the <c>pty-req</c>.</summary>
        public UInt32                       TerminalColumns       { get; init; } = 80;

        /// <summary>The terminal height in rows for the <c>pty-req</c>.</summary>
        public UInt32                       TerminalRows          { get; init; } = 24;


        /// <summary>Create a command specification for the given command line.</summary>
        public SshCommand(String CommandLine)
        {
            this.CommandLine = CommandLine;
        }

    }

}
