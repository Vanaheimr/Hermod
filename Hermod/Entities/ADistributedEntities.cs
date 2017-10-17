/*
 * Copyright (c) 2010-2016 Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Illias <http://www.github.com/Vanaheimr/Illias>
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

using System;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Distributed
{

    /// <summary>
    /// An abstract entity.
    /// </summary>
    /// <typeparam name="TId">The type of the entity identification.</typeparam>
    public abstract class ADistributedEntity<TId> : AEntity<TId>
        where TId : IId
    {

        #region Data

        private static readonly Regex JSONWhitespaceRegEx = new Regex(@"(\s)+", RegexOptions.IgnorePatternWhitespace);

        #endregion

        #region Properties

        /// <summary>
        /// The hash value of this object.
        /// </summary>
        public String  CurrentCryptoHash   { get; protected set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract entity.
        /// </summary>
        /// <param name="Id">The unique entity identification.</param>
        /// <param name="DataSource">The source of this information, e.g. an automatic importer.</param>
        protected ADistributedEntity(TId     Id,
                                     String  DataSource)

            : base(Id,
                   DataSource)

        { }

        #endregion


        #region (protected) CalcHash()

        /// <summary>
        /// Calculate the hash value of this object.
        /// </summary>
        protected void CalcHash()
        {

            CurrentCryptoHash = "json:sha256:" +
                          new SHA256Managed().ComputeHash(Encoding.Unicode.GetBytes(JSONWhitespaceRegEx.Replace(ToJSON(IncludeCryptoHash: false).ToString(), " "))).
                                              Select(value => String.Format("{0:x2}", value)).
                                              Aggregate();

        }

        #endregion

        #region ToJSON(IncludeCryptoHash = true)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="IncludeCryptoHash">Include the crypto hash value of this object.</param>
        public abstract JObject ToJSON(Boolean IncludeCryptoHash = true);

        #endregion

    }

}
