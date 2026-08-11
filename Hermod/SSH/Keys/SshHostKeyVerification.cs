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
    /// Host-key verification callbacks for an SSH client.
    ///
    /// <para>
    /// Verifying the server's host key is what makes an SSH connection meaningful. The signature the
    /// server produces during key exchange only proves it holds the private half of the key it just
    /// presented — possession, not <i>identity</i>. Without a check binding that key to the host you
    /// meant to reach, any machine on the path can complete the handshake with a key it generated a
    /// moment ago, and then read and rewrite the entire session.
    /// </para>
    ///
    /// <para>
    /// Build the real thing with <see cref="HostKeyPolicy"/> (pinned fingerprints, <c>known_hosts</c>,
    /// host certificates, SSHFP, TOFU) and hand its <c>ForHost(host, port)</c> delegate to the client.
    /// </para>
    /// </summary>
    public static class SshHostKeyVerification
    {

        /// <summary>
        /// Accepts <b>any</b> host key — i.e. performs no host authentication at all, leaving the
        /// connection open to a machine-in-the-middle. Intended for loopback tests and demos where both
        /// ends are known; never for talking to a real remote host. It exists so that skipping
        /// verification has to be written down deliberately rather than happening by omission.
        /// </summary>
        public static Func<Byte[], Boolean> AcceptAnyUnsafe { get; } = _ => true;

    }

}
