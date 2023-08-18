/*
 * Copyright (c) 2014-2023 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of UsersAPI <https://www.github.com/Vanaheimr/UsersAPI>
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
using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for login & passwords.
    /// </summary>
    public static class LoginPasswordExtensions
    {

        /// <summary>
        /// Indicates whether this login & password is null or empty.
        /// </summary>
        /// <param name="LoginPassword">A login & password.</param>
        public static Boolean IsNullOrEmpty(this LoginPassword? LoginPassword)
            => !LoginPassword.HasValue || LoginPassword.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this login & password is null or empty.
        /// </summary>
        /// <param name="LoginPassword">A login & password.</param>
        public static Boolean IsNotNullOrEmpty(this LoginPassword? LoginPassword)
            => LoginPassword.HasValue && LoginPassword.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A user identification and password combination.
    /// </summary>
    public readonly struct LoginPassword : IEquatable<LoginPassword>,
                                           IComparable<LoginPassword>,
                                           IComparable
    {

        #region Properties

        /// <summary>
        /// Indicates whether this login & password is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => Login.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this login & password is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => Login.IsNotNullOrEmpty;

        /// <summary>
        /// The unique user identification.
        /// </summary>
        public User_Id   Login      { get; }

        /// <summary>
        /// The password of the user.
        /// </summary>
        public Password  Password   { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new user identification and password combination.
        /// </summary>
        /// <param name="Login">The unique user identification.</param>
        /// <param name="Password">The password of the user.</param>
        public LoginPassword(User_Id   Login,
                             Password  Password)
        {

            this.Login     = Login;
            this.Password  = Password;

        }

        #endregion


        #region VerifyPassword(Password)

        /// <summary>
        /// Verify the given password.
        /// </summary>
        /// <param name="Password">A password to verify.</param>
        public Boolean VerifyPassword(Password Password)

            => this.Password.Equals(Password);


        /// <summary>
        /// Verify the given password.
        /// </summary>
        /// <param name="Password">A password to verify.</param>
        public Boolean VerifyPassword(Password? Password)

            => Password.HasValue
                   ? this.Password.Equals(Password.Value)
                   : false;


        /// <summary>
        /// Verify the given password.
        /// </summary>
        /// <param name="Password">A password to verify.</param>
        public Boolean VerifyPassword(String Password)

            => Password.IsNotNullOrEmpty()
                   ? this.Password.Verify(Password)
                   : false;

        #endregion


        #region Operator overloading

        #region Operator == (LoginPassword1, LoginPassword2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="LoginPassword1">A login & password.</param>
        /// <param name="LoginPassword2">Another login & password.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (LoginPassword LoginPassword1, LoginPassword LoginPassword2)

            => LoginPassword1.Equals(LoginPassword2);

        #endregion

        #region Operator != (LoginPassword1, LoginPassword2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="LoginPassword1">A login & password.</param>
        /// <param name="LoginPassword2">Another login & password.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (LoginPassword LoginPassword1, LoginPassword LoginPassword2)

            => !LoginPassword1.Equals(LoginPassword2);

        #endregion

        #endregion

        #region IComparable<LoginPassword> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is LoginPassword loginPassword
                   ? CompareTo(loginPassword)
                   : throw new ArgumentException("The given object is not a login/password!", nameof(Object));

        #endregion

        #region CompareTo(LoginPassword)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="LoginPassword">A Login/Password object to compare with.</param>
        public Int32 CompareTo(LoginPassword LoginPassword)

            => Login.CompareTo(LoginPassword.Login);

        #endregion

        #endregion

        #region IEquatable<LoginPassword> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is LoginPassword loginPassword &&
                   Equals(loginPassword);

        #endregion

        #region Equals(LoginPassword)

        /// <summary>
        /// Compares two Login/Password objects for equality.
        /// </summary>
        /// <param name="LoginPassword">A Login/Password object to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(LoginPassword LoginPassword)

            => Login.   Equals(LoginPassword.Login) &&
               Password.Equals(LoginPassword.Password);

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Get the hashcode of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => Login.   GetHashCode() * 3 ^
               Password.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => Login.ToString();

        #endregion

    }

}
