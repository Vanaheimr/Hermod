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

using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The value of an Alt-Svc field (RFC 7838, Section 3): either the
    /// alternatives of the origin, most preferred first, or "clear".
    /// </summary>
    /// <remarks>
    /// Two shapes rather than one list, because an empty list and "clear" mean
    /// opposite things to a client: "nothing new" and "forget what you have".
    /// The grammar has no empty list, so a value with no alternative that can
    /// be used is refused rather than read as either of them.
    /// </remarks>
    public sealed class AlternativeServices
    {

        #region Properties

        /// <summary>
        /// The alternatives, in the sender's order of preference. Empty when
        /// <see cref="IsClear"/>.
        /// </summary>
        public IReadOnlyList<HTTPAlternativeService>  Services    { get; }

        /// <summary>
        /// Whether the origin asks for all of its alternatives to be
        /// invalidated.
        /// </summary>
        public Boolean                                IsClear     { get; }

        #endregion

        #region Constructor(s)

        private AlternativeServices(IReadOnlyList<HTTPAlternativeService>  Services,
                                    Boolean                                IsClear)
        {
            this.Services  = Services;
            this.IsClear   = IsClear;
        }

        #endregion


        #region Clear

        /// <summary>
        /// "clear": invalidate every alternative of this origin.
        /// </summary>
        public static AlternativeServices Clear { get; }

            = new ([], true);

        #endregion

        #region Of(Service, params MoreServices)

        /// <summary>
        /// The given alternatives, most preferred first.
        /// </summary>
        /// <param name="Service">The most preferred alternative.</param>
        /// <param name="MoreServices">Further alternatives, in decreasing preference.</param>
        public static AlternativeServices Of(HTTPAlternativeService           Service,
                                             params HTTPAlternativeService[]  MoreServices)

            => new ([ Service, .. MoreServices ], false);

        #endregion

        #region TryParse(Text, out AlternativeServices)

        /// <summary>
        /// Parse an Alt-Svc field value.
        /// </summary>
        /// <remarks>
        /// A malformed alternative is skipped and the well-formed ones beside it
        /// are kept, as HTTPAlternativeService.Parse does, because this is
        /// advisory routing information. Only a value with nothing usable in it
        /// at all is refused.
        /// </remarks>
        /// <param name="Text">The field value, with repeated field lines already joined by commas.</param>
        /// <param name="AlternativeServices">The parsed value.</param>
        public static Boolean TryParse(String                                         Text,
                                       [NotNullWhen(true)] out AlternativeServices?  AlternativeServices)
        {

            AlternativeServices = null;

            if (String.IsNullOrWhiteSpace(Text))
                return false;

            var services = HTTPAlternativeService.Parse(Text, out var clear);

            if (clear)
            {
                AlternativeServices = Clear;
                return true;
            }

            if (services.Count == 0)
                return false;

            AlternativeServices = new AlternativeServices(services, false);
            return true;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// The field value: "clear", or the alternatives separated by commas.
        /// </summary>
        public override String ToString()

            => IsClear
                   ? "clear"
                   : String.Join(", ", Services.Select(service => service.ToFieldValue()));

        #endregion

    }

}
