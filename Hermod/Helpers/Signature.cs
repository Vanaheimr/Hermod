/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A cryptographical signature.
    /// </summary>
    public class Signature : IEquatable<Signature>
    {

        #region Properties

        /// <summary>
        /// The value of the cryptographical signature as text.
        /// </summary>
        public String SignatureText { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new cryptographical signature.
        /// </summary>
        /// <param name="SignatureText">The value of the signature as text.</param>
        public Signature(String SignatureText)
        {

            this.SignatureText = SignatureText;

        }

        #endregion


        #region Operator overloading

        #region Operator == (Signature1, Signature2)

        /// <summary>
        /// Compares two signatures for equality.
        /// </summary>
        /// <param name="Signature1">A signature.</param>
        /// <param name="Signature2">Another signature.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public static Boolean operator == (Signature Signature1, Signature Signature2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(Signature1, Signature2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) Signature1 == null) || ((Object) Signature2 == null))
                return false;

            return Signature1.Equals(Signature2);

        }

        #endregion

        #region Operator != (Signature1, Signature2)

        /// <summary>
        /// Compares two signatures for inequality.
        /// </summary>
        /// <param name="Signature1">A signature.</param>
        /// <param name="Signature2">Another signature.</param>
        /// <returns>False if both match; True otherwise.</returns>
        public static Boolean operator != (Signature Signature1, Signature Signature2)

            => !(Signature1 == Signature2);

        #endregion

        #endregion

        #region IEquatable<Signature> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object == null)
                return false;

            // Check if the given object is a signature.
            var Signature = Object as Signature;
            if ((Object) Signature == null)
                return false;

            return this.Equals(Signature);

        }

        #endregion

        #region Equals(Signature)

        /// <summary>
        /// Compares two signatures for equality.
        /// </summary>
        /// <param name="Signature">A signature to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(Signature Signature)
        {

            if ((Object) Signature == null)
                return false;

            return SignatureText.Equals(Signature.SignatureText);

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            unchecked
            {

                return SignatureText.GetHashCode();

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()

            => SignatureText;

        #endregion

    }

}
