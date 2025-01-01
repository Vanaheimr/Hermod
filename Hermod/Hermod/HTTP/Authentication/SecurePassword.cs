/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for secure passwords.
    /// </summary>
    public static class SecurePasswordExtensions
    {

        /// <summary>
        /// Indicates whether this secure password is null or empty.
        /// </summary>
        /// <param name="SecurePassword">A secure password.</param>
        public static Boolean IsNullOrEmpty(this SecurePassword? SecurePassword)
            => !SecurePassword.HasValue || SecurePassword.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this secure password is null or empty.
        /// </summary>
        /// <param name="SecurePassword">A secure password.</param>
        public static Boolean IsNotNullOrEmpty(this SecurePassword? SecurePassword)
            => SecurePassword.HasValue && SecurePassword.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The secure password (PBKDF2)
    /// </summary>
    public readonly struct SecurePassword : IEquatable<SecurePassword>
    {

        #region Data

        private readonly Byte[]  Value;

        /// <summary>
        /// The default size of the salt.
        /// </summary>
        public  const    Byte    DefaultSaltSize     = 16;

        /// <summary>
        /// The default size of the hash.
        /// </summary>
        public  const    Byte    DefaultHashSize     = 32;

        /// <summary>
        /// The default number of iterations.
        /// </summary>
        public  const    UInt32  DefaultIterations   = 100000;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this identification is null or empty.
        /// </summary>
        public readonly Boolean  IsNullOrEmpty
            => Value.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this identification is NOT null or empty.
        /// </summary>
        public readonly Boolean  IsNotNullOrEmpty
            => !Value.IsNullOrEmpty();

        /// <summary>
        /// The length of the secure password.
        /// </summary>
        public readonly UInt64   Length
            => (UInt64) Value.Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new secure password based on the given text.
        /// </summary>
        /// <param name="Text">A text representation of a secure password.</param>
        private SecurePassword(Byte[] Text)
        {
            this.Value = Text;
        }

        #endregion


        #region (static) NewRandom (Length)

        /// <summary>
        /// Create a new random secure password.
        /// </summary>
        /// <param name="Length">The expected length of the random password.</param>
        public static SecurePassword NewRandom(UInt16 Length = 32)

            => Parse(RandomExtensions.RandomString(Length));

        #endregion

        #region (static) Parse     (Password)

        /// <summary>
        /// Parse the given string as a secure password.
        /// </summary>
        /// <param name="Password">A text representation of a secure password.</param>
        public static SecurePassword Parse(String Password)
        {

            if (TryParse(Password, out var securePassword))
                return securePassword;

            throw new ArgumentException($"Invalid text representation of a secure password: '{Password}'!",
                                        nameof(Password));

        }

        #endregion

        #region (static) TryParse  (Password)

        /// <summary>
        /// Try to parse the given text as a secure password.
        /// </summary>
        /// <param name="Password">A text representation of a secure password.</param>
        public static SecurePassword? TryParse(String Password)
        {

            if (TryParse(Password, out var securePassword))
                return securePassword;

            return null;

        }

        #endregion

        #region (static) TryParse  (Password, out SecurePassword, SaltSize = DefaultSaltSize, HashSize = DefaultHashSize, Iterations = DefaultIterations)

        /// <summary>
        /// Try to parse the given text as a secure password.
        /// </summary>
        /// <param name="Password">A text representation of a secure password.</param>
        /// <param name="SecurePassword">The parsed secure password.</param>
        /// <param name="SaltSize">The optional size of the salt.</param>
        /// <param name="HashSize">The optional size of the hash.</param>
        /// <param name="Iterations">The optional number of iterations.</param>
        public static Boolean TryParse(String                                  Password,
                                       [NotNullWhen(true)] out SecurePassword  SecurePassword,
                                       Byte                                    SaltSize     = DefaultSaltSize,
                                       Byte                                    HashSize     = DefaultHashSize,
                                       UInt32                                  Iterations   = DefaultIterations)
        {

            if (Password.IsNotNullOrEmpty())
            {

                var salt = new Byte[SaltSize];
                RandomNumberGenerator.Fill(salt);

                // Hash the password with the salt
                using (var pbkdf2 = new Rfc2898DeriveBytes(
                                        Password,
                                        salt,
                                        (Int32) Iterations,
                                        HashAlgorithmName.SHA256
                                    ))
                {

                    var hash = pbkdf2.GetBytes(HashSize);

                    // Combine salt and hash into a single string for storage
                    var hashBytes = new Byte[SaltSize + HashSize];
                    Array.Copy(salt, 0, hashBytes, 0,        SaltSize);
                    Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

                    SecurePassword = new SecurePassword(hashBytes);
                    return true;

                }

            }

            SecurePassword = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this secure password.
        /// </summary>
        public SecurePassword Clone

            => new (Value);

        #endregion


        #region Operator overloading

        #region Operator == (SecurePassword1, SecurePassword2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SecurePassword1">A secure password.</param>
        /// <param name="SecurePassword2">Another secure password.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (SecurePassword SecurePassword1,
                                           SecurePassword SecurePassword2)

            => SecurePassword1.Equals(SecurePassword2);

        #endregion

        #region Operator != (SecurePassword1, SecurePassword2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SecurePassword1">A secure password.</param>
        /// <param name="SecurePassword2">Another secure password.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (SecurePassword SecurePassword1,
                                           SecurePassword SecurePassword2)

            => !SecurePassword1.Equals(SecurePassword2);

        #endregion

        #endregion

        #region IEquatable<SecurePassword> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two secure passwords for equality.
        /// </summary>
        /// <param name="Object">A secure password to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is SecurePassword securePassword &&
                   Equals(securePassword);

        #endregion

        #region Equals(SecurePassword)

        /// <summary>
        /// Compares two secure passwords for equality.
        /// </summary>
        /// <param name="SecurePassword">A secure password to compare with.</param>
        public Boolean Equals(SecurePassword SecurePassword)

            => Value.SequenceEqual(SecurePassword.Value);

        #endregion

        #region Equals(Password, SaltSize = DefaultSaltSize, HashSize = DefaultHashSize, Iterations = DefaultIterations)

        /// <summary>
        /// Compares a secure password with a password for equality.
        /// </summary>
        /// <param name="Password">A password to compare with.</param>
        /// <param name="SaltSize">The optional size of the salt.</param>
        /// <param name="HashSize">The optional size of the hash.</param>
        /// <param name="Iterations">The optional number of iterations.</param>
        public Boolean Equals(String  Password,
                              Byte    SaltSize     = DefaultSaltSize,
                              Byte    HashSize     = DefaultHashSize,
                              UInt32  Iterations   = DefaultIterations)
        {

            // Extract the salt from the stored hash
            var usedSalt = new Byte[SaltSize];
            Array.Copy(Value, 0,        usedSalt,        0, SaltSize);

            // Extract the original hash from the stored hash
            var storedHashBytes = new Byte[HashSize];
            Array.Copy(Value, SaltSize, storedHashBytes, 0, HashSize);

            // Hash the input password with the same salt
            using (var pbkdf2 = new Rfc2898DeriveBytes(
                                    Password,
                                    usedSalt,
                                    (Int32) Iterations,
                                    HashAlgorithmName.SHA256
                                ))
            {

                var hash = pbkdf2.GetBytes(HashSize);

                return hash.SequenceEqual(storedHashBytes);

            }

        }

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => Value.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => Value.ToString();

        #endregion

    }

}
