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

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// Who authorized a rendezvous command.
    ///
    /// There are two kinds of callers, and they are trusted very differently:
    ///
    ///   - A caller within the same process already holds the rendezvous manager
    ///     and could close every rendezvous by hand anyway. Asking it for a proof
    ///     would be theatre, therefore <see cref="Trusted"/> may do everything.
    ///     Use <see cref="TrustedAs"/> to still record who opened a rendezvous.
    ///
    ///   - A caller of the control endpoint is whoever reached an open TCP port.
    ///     It is known by the keys that signed its request, and it may only close
    ///     the rendezvous those keys opened - unless one of them is an administrator.
    ///
    /// </summary>
    public sealed class ControlAuthorization
    {

        #region Data

        private readonly ControlKey[]  keys;
        private readonly String[]      keyIds;

        #endregion

        #region Properties

        /// <summary>
        /// Whether this caller lives within the same process and is therefore
        /// allowed to do everything.
        /// </summary>
        public Boolean                    IsTrusted         { get; }

        /// <summary>
        /// The keys that signed the request, empty for a caller within the same process.
        /// </summary>
        public IReadOnlyList<ControlKey>  Keys
            => keys;

        /// <summary>
        /// The identifications of the keys that signed the request, or the name a
        /// caller within the same process gave itself.
        /// </summary>
        public IReadOnlyList<String>      KeyIds
            => keyIds;

        /// <summary>
        /// Whether at least one of the keys is an administrator key, which may
        /// also close the rendezvous of somebody else.
        /// </summary>
        public Boolean                    IsAdministrator   { get; }

        #endregion

        #region Constructor(s)

        private ControlAuthorization(Boolean       IsTrusted,
                                     ControlKey[]  Keys,
                                     String[]      KeyIds)
        {

            this.IsTrusted        = IsTrusted;
            this.keys             = Keys;
            this.keyIds           = KeyIds;
            this.IsAdministrator  = Keys.Any(key => key.IsAdministrator);

        }

        /// <summary>
        /// Create a new authorization from the keys that signed a request.
        /// </summary>
        /// <param name="Keys">The keys that signed the request.</param>
        public ControlAuthorization(IEnumerable<ControlKey> Keys)

            : this(false,
                   [.. Keys],
                   [.. Keys.Select(key => key.Id)])

        { }

        /// <summary>
        /// Create a new authorization from the keys that signed a request.
        /// </summary>
        /// <param name="Keys">The keys that signed the request.</param>
        public ControlAuthorization(params ControlKey[] Keys)

            : this((IEnumerable<ControlKey>) Keys)

        { }

        #endregion


        #region (static) Trusted

        /// <summary>
        /// A caller within the same process: it may open and close every rendezvous.
        /// </summary>
        public static ControlAuthorization Trusted { get; }

            = new (true, [], []);

        #endregion

        #region (static) TrustedAs(Name)

        /// <summary>
        /// A caller within the same process that names itself, so that the
        /// rendezvous it opens record who did so.
        /// </summary>
        /// <param name="Name">How this caller wants to be remembered, e.g. "maintenance-job".</param>
        public static ControlAuthorization TrustedAs(String Name)
        {

            ArgumentException.ThrowIfNullOrWhiteSpace(Name);

            return new (true, [], [Name]);

        }

        #endregion


        #region ToString()

        /// <summary>
        /// Return a text representation of this authorization.
        /// </summary>
        public override String ToString()

            => (keyIds.Length > 0 ? String.Join(", ", keyIds) : IsTrusted ? "in-process" : "nobody") +
               (IsAdministrator ? " (administrator)" : "");

        #endregion

    }

}
