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

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A crypto key usage with a relation.
    /// </summary>
    public class CryptoKeyUsageTuple : IEquatable <CryptoKeyUsageTuple>,
                                       IComparable<CryptoKeyUsageTuple>,
                                       IComparable
    {

        #region Properties

        /// <summary>
        /// The crypto key usage.
        /// </summary>
        public CryptoKeyUsage          Usage       { get; }

        /// <summary>
        /// The crypto key usage relation.
        /// </summary>
        public CryptoKeyUsageRelation  Relation    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new unique crypto key usage tuple.
        /// </summary>
        /// <param name="Usage">The crypto key usage.</param>
        /// <param name="Relation">The crypto key usage relation.</param>
        public CryptoKeyUsageTuple(CryptoKeyUsage          Usage,
                                   CryptoKeyUsageRelation  Relation)
        {

            this.Usage     = Usage;
            this.Relation  = Relation;

        }

        #endregion


        #region Operator overloading

        #region Operator == (CryptoKeyUsageTuple1, CryptoKeyUsageTuple2)

        /// <summary>
        /// Compares two crypto key usage tuples for equality.
        /// </summary>
        /// <param name="CryptoKeyUsageTuple1">A crypto key usage tuple.</param>
        /// <param name="CryptoKeyUsageTuple2">Another crypto key usage tuple.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (CryptoKeyUsageTuple? CryptoKeyUsageTuple1,
                                           CryptoKeyUsageTuple? CryptoKeyUsageTuple2)
        {

            if (Object.ReferenceEquals(CryptoKeyUsageTuple1, CryptoKeyUsageTuple2))
                return true;

            if (CryptoKeyUsageTuple1 is null || CryptoKeyUsageTuple2 is null)
                return false;

            return CryptoKeyUsageTuple1.Equals(CryptoKeyUsageTuple2);

        }

        #endregion

        #region Operator != (CryptoKeyUsageTuple1, CryptoKeyUsageTuple2)

        /// <summary>
        /// Compares two crypto key usage tuples for inequality.
        /// </summary>
        /// <param name="CryptoKeyUsageTuple1">A crypto key usage tuple.</param>
        /// <param name="CryptoKeyUsageTuple2">Another crypto key usage tuple.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (CryptoKeyUsageTuple? CryptoKeyUsageTuple1,
                                           CryptoKeyUsageTuple? CryptoKeyUsageTuple2)

            => !(CryptoKeyUsageTuple1 == CryptoKeyUsageTuple2);

        #endregion

        #region Operator <  (CryptoKeyUsageTuple1, CryptoKeyUsageTuple2)

        /// <summary>
        /// Compares two crypto key usage tuples.
        /// </summary>
        /// <param name="CryptoKeyUsageTuple1">A crypto key usage tuple.</param>
        /// <param name="CryptoKeyUsageTuple2">Another crypto key usage tuple.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (CryptoKeyUsageTuple? CryptoKeyUsageTuple1,
                                          CryptoKeyUsageTuple? CryptoKeyUsageTuple2)

            => CryptoKeyUsageTuple1 is null
                   ? throw new ArgumentNullException(nameof(CryptoKeyUsageTuple1), "The given crypto key usage tuple must not be null!")
                   : CryptoKeyUsageTuple1.CompareTo(CryptoKeyUsageTuple2) < 0;

        #endregion

        #region Operator <= (CryptoKeyUsageTuple1, CryptoKeyUsageTuple2)

        /// <summary>
        /// Compares two crypto key usage tuples.
        /// </summary>
        /// <param name="CryptoKeyUsageTuple1">A crypto key usage tuple.</param>
        /// <param name="CryptoKeyUsageTuple2">Another crypto key usage tuple.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (CryptoKeyUsageTuple? CryptoKeyUsageTuple1,
                                           CryptoKeyUsageTuple? CryptoKeyUsageTuple2)

            => !(CryptoKeyUsageTuple1 > CryptoKeyUsageTuple2);

        #endregion

        #region Operator >  (CryptoKeyUsageTuple1, CryptoKeyUsageTuple2)

        /// <summary>
        /// Compares two crypto key usage tuples.
        /// </summary>
        /// <param name="CryptoKeyUsageTuple1">A crypto key usage tuple.</param>
        /// <param name="CryptoKeyUsageTuple2">Another crypto key usage tuple.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (CryptoKeyUsageTuple? CryptoKeyUsageTuple1,
                                          CryptoKeyUsageTuple? CryptoKeyUsageTuple2)

            => CryptoKeyUsageTuple1 is null
                   ? throw new ArgumentNullException(nameof(CryptoKeyUsageTuple1), "The given crypto key usage tuple must not be null!")
                   : CryptoKeyUsageTuple1.CompareTo(CryptoKeyUsageTuple2) > 0;

        #endregion

        #region Operator >= (CryptoKeyUsageTuple1, CryptoKeyUsageTuple2)

        /// <summary>
        /// Compares two crypto key usage tuples.
        /// </summary>
        /// <param name="CryptoKeyUsageTuple1">A crypto key usage tuple.</param>
        /// <param name="CryptoKeyUsageTuple2">Another crypto key usage tuple.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (CryptoKeyUsageTuple? CryptoKeyUsageTuple1,
                                           CryptoKeyUsageTuple? CryptoKeyUsageTuple2)

            => !(CryptoKeyUsageTuple1 < CryptoKeyUsageTuple2);

        #endregion

        #endregion

        #region IComparable<CryptoKeyUsageTuple> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two crypto key usage tuples.
        /// </summary>
        /// <param name="Object">A crypto key usage tuple to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is CryptoKeyUsageTuple cryptoKeyUsageTuple
                   ? CompareTo(cryptoKeyUsageTuple)
                   : throw new ArgumentException("The given object is not a crypto key usage tuple!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(CryptoKeyUsageTuple)

        /// <summary>
        /// Compares two crypto key usage tuples.
        /// </summary>
        /// <param name="CryptoKeyUsageTuple">A crypto key usage tuple to compare with.</param>
        public Int32 CompareTo(CryptoKeyUsageTuple? CryptoKeyUsageTuple)
        {

            if (CryptoKeyUsageTuple is null)
                throw new ArgumentNullException(nameof(CryptoKeyUsageTuple), "The given crypto key usage tuple must not be null!");

            var c = Usage.   CompareTo(CryptoKeyUsageTuple.Usage);

            if (c == 0)
                c = Relation.CompareTo(CryptoKeyUsageTuple.Relation);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<CryptoKeyUsageTuple> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two crypto key usage tuples for equality.
        /// </summary>
        /// <param name="Object">A crypto key usage tuple to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is CryptoKeyUsageTuple cryptoKeyUsageTuple &&
                   Equals(cryptoKeyUsageTuple);

        #endregion

        #region Equals(CryptoKeyUsageTuple)

        /// <summary>
        /// Compares two crypto key usage tuples for equality.
        /// </summary>
        /// <param name="CryptoKeyUsageTuple">A crypto key usage tuple to compare with.</param>
        public Boolean Equals(CryptoKeyUsageTuple? CryptoKeyUsageTuple)


            => CryptoKeyUsageTuple is not null &&

               Usage.   Equals(CryptoKeyUsageTuple.Usage) &&
               Relation.Equals(CryptoKeyUsageTuple.Relation);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override Int32 GetHashCode()

            => Usage.   GetHashCode() ^
               Relation.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{Usage} -> {Relation}";

        #endregion

    }

}
