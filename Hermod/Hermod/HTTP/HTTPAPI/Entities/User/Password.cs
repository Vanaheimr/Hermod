/*
 * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
 * This file is part of HTTPExtAPI <https://www.github.com/Vanaheimr/HTTPExtAPI>
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

using System.Text;
using System.Security;
using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for passwords.
    /// </summary>
    public static class PasswordExtensions
    {

        /// <summary>
        /// Indicates whether this password is null or empty.
        /// </summary>
        /// <param name="Password">A password.</param>
        public static Boolean IsNullOrEmpty(this Password? Password)
            => !Password.HasValue || Password.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this password is null or empty.
        /// </summary>
        /// <param name="Password">A password.</param>
        public static Boolean IsNotNullOrEmpty(this Password? Password)
            => Password.HasValue && Password.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A password.
    /// </summary>
    public readonly struct Password : IEquatable<Password>
    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly SecureString InternalPassword;

        /// <summary>
        /// The default length of the password hash.
        /// </summary>
        public const Byte DefaultLengthOfSalt = 16;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this password is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalPassword.UnsecureString().IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this password is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => InternalPassword.UnsecureString().IsNotNullOrEmpty();

        /// <summary>
        /// The salt of the password.
        /// </summary>
        /// <remarks>It is a SecureString just because we can ;)</remarks>
        public SecureString  Salt   { get; }

        /// <summary>
        /// Return the internal password as an unsecured string,
        /// e.g. in order to store it on disc.
        /// </summary>
        internal String UnsecureString
            => InternalPassword.UnsecureString();

        /// <summary>
        /// The length of the password.
        /// </summary>
        public UInt16 Length
            => (UInt16) InternalPassword.UnsecureString().Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new password based on the given string.
        /// </summary>
        /// <param name="Salt">The salt of the password.</param>
        /// <param name="Password">The string representation of the password.</param>
        private Password(SecureString  Salt,
                         SecureString  Password)
        {

            this.Salt              = Salt;
            this.InternalPassword  = Password;

        }

        #endregion


        #region (static) Random  (PasswordLength = 16, LengthOfSalt = 16)

        /// <summary>
        /// Create a new random password.
        /// </summary>
        /// <param name="PasswordLength">The expected length of the password.</param>
        /// <param name="LengthOfSalt">The optional length of the random salt.</param>
        public static Password Random(Byte PasswordLength  = 16,
                                      Byte LengthOfSalt    = DefaultLengthOfSalt)

            => Parse(RandomExtensions.RandomString(PasswordLength),
                     null,
                     LengthOfSalt);

        #endregion

        #region (static) Parse   (Text,               PasswordQualityCheck = null, LengthOfSalt = 16)

        /// <summary>
        /// Parse the given string as a password.
        /// </summary>
        /// <param name="Text">A text representation of a password.</param>
        /// <param name="LengthOfSalt">The optional length of the random salt.</param>
        public static Password Parse(String                         Text,
                                     PasswordQualityCheckDelegate?  PasswordQualityCheck   = null,
                                     Byte                           LengthOfSalt           = DefaultLengthOfSalt)
        {

            if (TryParse(Text,
                         out var password,
                         PasswordQualityCheck,
                         LengthOfSalt))
            {
                return password;
            }

            throw new ArgumentException($"Invalid text representation of a password: '" + Text + "'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text,               PasswordQualityCheck = null, LengthOfSalt = 16)

        /// <summary>
        /// Try to parse the given string as a password.
        /// </summary>
        /// <param name="Text">A text representation of a password.</param>
        /// <param name="LengthOfSalt">The optional length of the random salt.</param>
        public static Password? TryParse(String                         Text,
                                         PasswordQualityCheckDelegate?  PasswordQualityCheck   = null,
                                         Byte                           LengthOfSalt           = DefaultLengthOfSalt)
        {

            if (TryParse(Text,
                         out var password,
                         PasswordQualityCheck,
                         LengthOfSalt))
            {
                return password;
            }

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out Password, PasswordQualityCheck = null, LengthOfSalt = 16)

        /// <summary>
        /// Try to parse the given string as a password.
        /// </summary>
        /// <param name="Text">A text representation of a password.</param>
        /// <param name="Password">The parsed password.</param>
        public static Boolean TryParse(String        Text,
                                       out Password  Password)

            => TryParse(Text,
                        out Password,
                        null,
                        DefaultLengthOfSalt);


        /// <summary>
        /// Try to parse the given string as a password.
        /// </summary>
        /// <param name="Text">A text representation of a password.</param>
        /// <param name="Password">The parsed password.</param>
        /// <param name="LengthOfSalt">The optional length of the random salt.</param>
        public static Boolean TryParse(String                         Text,
                                       out Password                   Password,
                                       PasswordQualityCheckDelegate?  PasswordQualityCheck   = null,
                                       Byte                           LengthOfSalt           = DefaultLengthOfSalt)
        {

            #region Initial checks

            if (Text is not null)
                Text = Text.Trim();

            if (Text is null || Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of a password must not be null or empty!");

            if (PasswordQualityCheck is not null && PasswordQualityCheck(Text) < 1.0)
                throw new ArgumentException("The given password '" + Text + "' does not match the password quality criteria!", nameof(Text));

            #endregion

            try
            {

                // Salt...
                var salt            = RandomExtensions.RandomString(LengthOfSalt);
                var secureSalt      = new SecureString();

                foreach (var character in salt)
                    secureSalt.AppendChar(character);

                // Password...
                var securePassword  = new SecureString();
                var hashedPassword  = SHA256.HashData(Encoding.UTF8.GetBytes(salt + ":" + Text));

                foreach (var character in hashedPassword.ToHexString(ToLower: true))
                    securePassword.AppendChar(character);

                Password            = new Password(secureSalt,
                                                   securePassword);

                return true;

            }
            catch
            {
                Password = default;
                return false;
            }

        }

        #endregion


        #region (static) ParseHash   (Salt, HashedPassword)

        /// <summary>
        /// Parse the given strings as a salted and SHA256 hashed password.
        /// </summary>
        /// <param name="Salt">The salt of the password.</param>
        /// <param name="HashedPassword">A text representation of a SHA256 hashed password.</param>
        public static Password ParseHash(String  Salt,
                                         String  HashedPassword)
        {

            if (TryParseHash(Salt,
                             HashedPassword,
                             out var password))
            {
                return password;
            }

            throw new ArgumentException("Invalid salted password hash: '" + Salt + ", " + HashedPassword + "'!",
                                        nameof(HashedPassword));

        }

        #endregion

        #region (static) TryParseHash(Salt, HashedPassword)

        /// <summary>
        /// Try to parse the given strings as a salted and SHA256 hashed password.
        /// </summary>
        /// <param name="Salt">The salt of the SHA256 hashed password.</param>
        /// <param name="HashedPassword">A text representation of a SHA256 hashed password.</param>
        public static Password? TryParseHash(String  Salt,
                                             String  HashedPassword)
        {

            if (TryParseHash(Salt,
                             HashedPassword,
                             out var password))
            {
                return password;
            }

            return new Password?();

        }

        #endregion

        #region (static) TryParseHash(Salt, HashedPassword, out Password)

        /// <summary>
        /// Try to parse the given strings as a salted and SHA256 hashed password.
        /// </summary>
        /// <param name="Salt">The salt of the SHA256 hashed password.</param>
        /// <param name="HashedPassword">A text representation of a SHA256 hashed password.</param>
        /// <param name="Password">The parsed password.</param>
        public static Boolean TryParseHash(String        Salt,
                                           String        HashedPassword,
                                           out Password  Password)
        {

            try
            {

                // Salt...
                var secureSalt      = new SecureString();

                foreach (var character in Salt)
                    secureSalt.AppendChar(character);


                // Password...
                var securePassword  = new SecureString();

                foreach (var character in HashedPassword.Trim().ToLower())
                    securePassword.AppendChar(character);


                Password = new Password(secureSalt,
                                        securePassword);

                return true;

            }
            catch
            {
                Password = default(Password);
                return false;
            }

        }

        #endregion


        #region Clone

        /// <summary>
        /// Clone a password.
        /// </summary>
        public Password Clone

            => new Password(Salt,
                            InternalPassword.Copy());

        #endregion


        #region Verify(PlainPassword)

        /// <summary>
        /// Verify the given password.
        /// </summary>
        /// <param name="PlainPassword">A password.</param>
        public Boolean Verify(String PlainPassword)

            => InternalPassword.UnsecureString().
                   Equals(
                       SHA256.HashData(
                           Encoding.UTF8.GetBytes(Salt.UnsecureString() + ":" + PlainPassword)
                       ).ToHexString(ToLower: true)
                   );

        #endregion


        #region Operator overloading

        #region Operator == (Password1, Password2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Password1">A password.</param>
        /// <param name="Password2">Another password.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (Password Password1, Password Password2)

            => Password1.Equals(Password2);

        #endregion

        #region Operator != (Password1, Password2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Password1">A password.</param>
        /// <param name="Password2">Another password.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (Password Password1, Password Password2)

            => !Password1.Equals(Password2);

        #endregion

        #endregion

        #region IEquatable<Password> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object? Object)

            => Object is Password password &&
                   Equals(password);

        #endregion

        #region Equals(Password)

        /// <summary>
        /// Compares two passwords for equality.
        /// </summary>
        /// <param name="Password">An password to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(Password Password)
        {

            return Salt.            UnsecureString().Equals(Password.Salt.            UnsecureString()) &&
                   InternalPassword.UnsecureString().Equals(Password.InternalPassword.UnsecureString());

            //var bstr1 = IntPtr.Zero;
            //var bstr2 = IntPtr.Zero;

            //try
            //{

            //    bstr1 = Marshal.SecureStringToBSTR(SecurePassword);
            //    bstr2 = Marshal.SecureStringToBSTR(Password.SecurePassword);

            //    var length1 = Marshal.ReadInt32(bstr1, -4);
            //    var length2 = Marshal.ReadInt32(bstr2, -4);

            //    if (length1 != length2)
            //        return false;

            //    byte b1;
            //    byte b2;

            //    for (var x = 0; x < length1; ++x)
            //    {

            //        b1 = Marshal.ReadByte(bstr1, x);
            //        b2 = Marshal.ReadByte(bstr2, x);

            //        if (b1 != b2)
            //            return false;

            //    }

            //    return true;

            //}
            //finally
            //{
            //    if (bstr2 != IntPtr.Zero) Marshal.ZeroFreeBSTR(bstr2);
            //    if (bstr1 != IntPtr.Zero) Marshal.ZeroFreeBSTR(bstr1);
            //}

        }

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => Salt.            GetHashCode() ^
               InternalPassword.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => nameof(Password); // We do not know anything else!

        #endregion

    }

}
