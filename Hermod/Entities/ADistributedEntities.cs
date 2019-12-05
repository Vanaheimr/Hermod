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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Distributed
{

    //public static class ADistributedEntity
    //{
    //    public static readonly Regex JSONWhitespaceRegEx = new Regex(@"(\s)+", RegexOptions.IgnorePatternWhitespace);
    //}

    /// <summary>
    /// An abstract entity.
    /// </summary>
    /// <typeparam name="TId">The type of the entity identification.</typeparam>
    public abstract class ADistributedEntity<TId> : ADistributedEntity<TId, String>
        where TId : IId
    {

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract entity.
        /// </summary>
        /// <param name="Id">The unique entity identification.</param>
        /// <param name="DataSource">The source of this information, e.g. an automatic importer.</param>
        protected ADistributedEntity(TId     Id,
                                     String  DataSource)

            : base(Id,
                   (DataSource ?? "").Trim())

        { }

        #endregion

    }

    /// <summary>
    /// An abstract entity.
    /// </summary>
    /// <typeparam name="TId">The type of the entity identification.</typeparam>
    /// <typeparam name="TDataSource">The type of the data source.</typeparam>
    public abstract class ADistributedEntity<TId, TDataSource> : AEntity<TId, TDataSource>
        where TId : IId
    {

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
        protected ADistributedEntity(TId          Id,
                                     TDataSource  DataSource)

            : base(Id,
                   DataSource)

        { }

        #endregion


        #region CalcHash()

        /// <summary>
        /// Calculate the hash value of this object.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "<Pending>")]
        public void CalcHash()
        {

            using (var SHA256 = new SHA256Managed())
            {

                CurrentCryptoHash = "json:sha256:" +
                                        SHA256.ComputeHash(Encoding.Unicode.GetBytes(
                                                               ToJSON  (IncludeCryptoHash: false).
                                                               ToString(Newtonsoft.Json.Formatting.None)
                                                           )).
                                               Select(value => String.Format("{0:x2}", value)).
                                               Aggregate();

            }

        }

        #endregion

        #region ToJSON(Embedded = false, IncludeCryptoHash = false)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data is embedded into another data structure.</param>
        /// <param name="IncludeCryptoHash">Include the crypto hash value of this object.</param>
        public abstract JObject ToJSON(Boolean Embedded           = false,
                                       Boolean IncludeCryptoHash  = false);

        #endregion

    }

}
