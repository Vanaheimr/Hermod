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
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using org.GraphDefined.Vanaheimr.Illias;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Distributed
{

    public static class Helpers
    {

        public static readonly Regex UserDB_RegEx = new Regex(@"(\s)+", RegexOptions.IgnorePatternWhitespace);

    }


    //public interface IEntityClass<TClass> : IEquatable<TClass>,
    //                                        IComparable<TClass>,
    //                                        IComparable
    //{ }

    /// <summary>
    /// An abstract entity.
    /// </summary>
    public abstract class ADistributedEntity<TId> : AEntity<TId>
        where TId : IId
    {

        #region Properties

        public String  CurrentCryptoHash   { get; protected set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract entity.
        /// </summary>
        /// <param name="Id">The unique entity identification.</param>
        public ADistributedEntity(TId Id)

            : base(Id)

        {


        }

        #endregion


        #region (protected) CalcHash()

        protected void CalcHash()
        {

            CurrentCryptoHash = "json:sha256:" +
                          new SHA256Managed().ComputeHash(Encoding.Unicode.GetBytes(Helpers.UserDB_RegEx.Replace(ToJSON(IncludeCryptoHash: false).ToString(), " "))).
                                              Select(value => String.Format("{0:x2}", value)).
                                              Aggregate();

        }

        #endregion

        #region ToJSON(IncludeHash = true)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="IncludeCryptoHash">Include the cryptograhical hash value of this object.</param>
        public abstract JObject ToJSON(Boolean IncludeCryptoHash = true);

        #endregion

    }

}
