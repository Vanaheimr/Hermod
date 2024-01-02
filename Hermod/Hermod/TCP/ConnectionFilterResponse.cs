/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A (TCP) connection filter for e.g. simple firewalling.
    /// </summary>
    public enum ConnectionFilterResult
    {

        /// <summary>
        /// The connection was rejected
        /// </summary>
        Rejected,

        /// <summary>
        /// The connection was accepted
        /// </summary>
        Accepted,

        /// <summary>
        /// No operation
        /// </summary>
        NoOperation

    }


    /// <summary>
    /// A connection filter response.
    /// </summary>
    public class ConnectionFilterResponse
    {

        #region Properties

        /// <summary>
        /// The result of the filter operation.
        /// </summary>
        public ConnectionFilterResult  Result    { get; }

        /// <summary>
        /// An optional multi-language reason for this result.
        /// </summary>
        public I18NString?             Reason    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new connection filter response.
        /// </summary>
        /// <param name="Result">The result of the filter operation.</param>
        /// <param name="Reason">A reason for this result.</param>
        public ConnectionFilterResponse(ConnectionFilterResult  Result,
                                        String                  Reason)
        {
            this.Result  = Result;
            this.Reason  = Reason.ToI18NString();
        }

        /// <summary>
        /// Create a new connection filter response.
        /// </summary>
        /// <param name="Result">The result of the filter operation.</param>
        /// <param name="Reason">An optional multi-language reason for this result.</param>
        public ConnectionFilterResponse(ConnectionFilterResult  Result,
                                        I18NString?             Reason   = null)
        {
            this.Result  = Result;
            this.Reason  = Reason;
        }

        #endregion


        #region (static) Rejected

        /// <summary>
        /// The connection was rejected
        /// </summary>
        /// <param name="Reason">A reason for this result.</param>
        public static ConnectionFilterResponse Rejected(String Reason)

            => new (ConnectionFilterResult.Rejected,
                    Reason);

        /// <summary>
        /// The connection was rejected
        /// </summary>
        /// <param name="Reason">An optional multi-language reason for this result.</param>
        public static ConnectionFilterResponse Rejected(I18NString? Reason = null)

            => new (ConnectionFilterResult.Rejected,
                    Reason);

        #endregion

        #region (static) Accepted

        /// <summary>
        /// The connection was accepted
        /// </summary>
        /// <param name="Reason">A reason for this result.</param>
        public static ConnectionFilterResponse Accepted(String Reason)

            => new (ConnectionFilterResult.Accepted,
                    Reason);

        /// <summary>
        /// The connection was accepted
        /// </summary>
        /// <param name="Reason">An optional multi-language reason for this result.</param>
        public static ConnectionFilterResponse Accepted(I18NString? Reason = null)

            => new (ConnectionFilterResult.Accepted,
                    Reason);

        #endregion

        #region (static) NoOperation

        /// <summary>
        /// No operation
        /// </summary>
        /// <param name="Reason">A reason for this result.</param>
        public static ConnectionFilterResponse NoOperation(String Reason)

            => new (ConnectionFilterResult.NoOperation,
                    Reason);

        /// <summary>
        /// No operation
        /// </summary>
        /// <param name="Reason">An optional multi-language reason for this result.</param>
        public static ConnectionFilterResponse NoOperation(I18NString? Reason = null)

            => new (ConnectionFilterResult.NoOperation,
                    Reason);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{Result}{(Reason is not null ? Reason : "")}";

        #endregion


    }

}
