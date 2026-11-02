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

using org.GraphDefined.Vanaheimr.Hermod.SSH;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Client
{

    /// <summary>
    /// Reaching a jump host of <see cref="SshClientOptions.ProxyJump"/> failed: which one, and why
    /// (<see cref="Exception.InnerException"/> - a refused host key, a refused login, a tunnel the
    /// previous hop would not open, a timeout). A failure at the server itself is not wrapped.
    /// </summary>
    public sealed class SshJumpHostException : Exception
    {

        #region Properties

        /// <summary>
        /// The jump host that could not be reached.
        /// </summary>
        public SshJumpHost  JumpHost    { get; }

        /// <summary>
        /// Its place in the chain, from 1.
        /// </summary>
        public Int32        Hop         { get; }

        /// <summary>
        /// How many jump hosts the chain has.
        /// </summary>
        public Int32        Hops        { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Reaching <paramref name="JumpHost"/>, hop <paramref name="Hop"/> of <paramref name="Hops"/>, failed.
        /// </summary>
        public SshJumpHostException(SshJumpHost  JumpHost,
                                    Int32        Hop,
                                    Int32        Hops,
                                    Exception    InnerException)

            : base($"Jump host {Hop} of {Hops} ({JumpHost}): {InnerException.Message}",
                   InnerException)

        {

            this.JumpHost  = JumpHost;
            this.Hop       = Hop;
            this.Hops      = Hops;

        }

        #endregion

    }

}
