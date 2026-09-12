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

using System.Globalization;
using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

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
    /// A salted password hash (PBKDF2 with HMAC-SHA256) that carries its own
    /// parameters, so that the iteration count can be raised later without
    /// invalidating the hashes already stored.
    ///
    /// Create(Password) hashes a password with a fresh salt, Verify(Password)
    /// checks a password against the hash in constant time, and the text
    /// representation follows the PHC string format, e.g.
    /// "$pbkdf2-sha256$i=600000$&lt;salt&gt;$&lt;hash&gt;" with salt and hash
    /// as Base64 without padding, so that Parse(Text) reads a stored hash back.
    /// </summary>
    public readonly struct SecurePassword : IEquatable<SecurePassword>
    {

        #region Data

        /// <summary>
        /// The identifier of PBKDF2 with HMAC-SHA256, so far the only algorithm.
        /// </summary>
        public  const    String   PBKDF2SHA256        = "pbkdf2-sha256";

        /// <summary>
        /// The default size of the salt in bytes.
        /// </summary>
        public  const    Byte     DefaultSaltSize     = 16;

        /// <summary>
        /// The default size of the hash in bytes.
        /// </summary>
        public  const    Byte     DefaultHashSize     = 32;

        /// <summary>
        /// The default number of iterations, the OWASP recommendation
        /// for PBKDF2-HMAC-SHA256 since 2023.
        /// </summary>
        public  const    UInt32   DefaultIterations   = 600_000;

        private readonly String?  algorithm;
        private readonly Byte[]?  salt;
        private readonly Byte[]?  hash;

        #endregion

        #region Properties

        /// <summary>
        /// The algorithm identifier, e.g. "pbkdf2-sha256".
        /// </summary>
        public String                Algorithm
            => algorithm ?? "";

        /// <summary>
        /// The number of iterations this hash was created with.
        /// </summary>
        public UInt32                Iterations         { get; }

        /// <summary>
        /// The salt.
        /// </summary>
        public ReadOnlyMemory<Byte>  Salt
            => salt;

        /// <summary>
        /// The hash.
        /// </summary>
        public ReadOnlyMemory<Byte>  Hash
            => hash;

        /// <summary>
        /// Indicates whether this secure password is null or empty.
        /// </summary>
        public Boolean               IsNullOrEmpty
            => hash is null || hash.Length == 0;

        /// <summary>
        /// Indicates whether this secure password is NOT null or empty.
        /// </summary>
        public Boolean               IsNotNullOrEmpty
            => !IsNullOrEmpty;

        /// <summary>
        /// The length of salt and hash in bytes.
        /// </summary>
        public UInt64                Length
            => (UInt64) ((salt?.Length ?? 0) + (hash?.Length ?? 0));

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a secure password from its parts, e.g. when converting
        /// a hash from another storage format.
        /// </summary>
        /// <param name="Algorithm">The algorithm identifier, so far "pbkdf2-sha256".</param>
        /// <param name="Iterations">The number of iterations the hash was created with.</param>
        /// <param name="Salt">The salt, at least 8 bytes.</param>
        /// <param name="Hash">The hash, at least 16 bytes.</param>
        public SecurePassword(String  Algorithm,
                              UInt32  Iterations,
                              Byte[]  Salt,
                              Byte[]  Hash)
        {

            if (Algorithm != PBKDF2SHA256)
                throw new ArgumentException($"Unknown password hash algorithm '{Algorithm}'!", nameof(Algorithm));

            if (Iterations == 0 || Iterations > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(Iterations), "The number of iterations must be between 1 and 2^31-1!");

            if (Salt is null || Salt.Length < 8)
                throw new ArgumentException("The salt must have at least 8 bytes!", nameof(Salt));

            if (Hash is null || Hash.Length < 16)
                throw new ArgumentException("The hash must have at least 16 bytes!", nameof(Hash));

            this.algorithm   = Algorithm;
            this.Iterations  = Iterations;
            this.salt        = (Byte[]) Salt.Clone();
            this.hash        = (Byte[]) Hash.Clone();

        }

        #endregion


        #region (static) Create   (Password, Iterations = DefaultIterations, SaltSize = DefaultSaltSize, HashSize = DefaultHashSize)

        /// <summary>
        /// Hash the given password with a fresh random salt.
        /// </summary>
        /// <param name="Password">The password to hash.</param>
        /// <param name="Iterations">The optional number of iterations.</param>
        /// <param name="SaltSize">The optional size of the salt in bytes.</param>
        /// <param name="HashSize">The optional size of the hash in bytes.</param>
        public static SecurePassword Create(String  Password,
                                            UInt32  Iterations   = DefaultIterations,
                                            Byte    SaltSize     = DefaultSaltSize,
                                            Byte    HashSize     = DefaultHashSize)
        {

            if (Password.IsNullOrEmpty())
                throw new ArgumentException("The password must not be null or empty!", nameof(Password));

            if (Iterations == 0 || Iterations > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(Iterations), "The number of iterations must be between 1 and 2^31-1!");

            if (SaltSize < 8)
                throw new ArgumentOutOfRangeException(nameof(SaltSize), "The salt must have at least 8 bytes!");

            if (HashSize < 16)
                throw new ArgumentOutOfRangeException(nameof(HashSize), "The hash must have at least 16 bytes!");

            var salt = RandomNumberGenerator.GetBytes(SaltSize);

            return new SecurePassword(
                       PBKDF2SHA256,
                       Iterations,
                       salt,
                       Derive(Password, salt, Iterations, HashSize)
                   );

        }

        #endregion

        #region (static) NewRandom(Length = 32)

        /// <summary>
        /// Hash a new random password. The password itself is discarded, so
        /// the result only serves as a placeholder that no password verifies
        /// against, e.g. for accounts without a password.
        /// </summary>
        /// <param name="Length">The length of the random password.</param>
        public static SecurePassword NewRandom(UInt16 Length = 32)
            => Create(RandomExtensions.RandomString(Length));

        #endregion

        #region (static) Parse    (Text)

        /// <summary>
        /// Parse the stored text representation of a secure password, e.g.
        /// "$pbkdf2-sha256$i=600000$...$...". To hash a new password use Create(Password)!
        /// </summary>
        /// <param name="Text">A text representation of a secure password.</param>
        public static SecurePassword Parse(String Text)
        {

            if (TryParse(Text, out var securePassword, out var errorResponse))
                return securePassword;

            throw new ArgumentException(errorResponse, nameof(Text));

        }

        #endregion

        #region (static) TryParse (Text)

        /// <summary>
        /// Try to parse the stored text representation of a secure password.
        /// </summary>
        /// <param name="Text">A text representation of a secure password.</param>
        public static SecurePassword? TryParse(String Text)

            => TryParse(Text, out var securePassword, out _)
                   ? securePassword
                   : null;

        #endregion

        #region (static) TryParse (Text, out SecurePassword)

        /// <summary>
        /// Try to parse the stored text representation of a secure password.
        /// </summary>
        /// <param name="Text">A text representation of a secure password.</param>
        /// <param name="SecurePassword">The parsed secure password.</param>
        public static Boolean TryParse(String              Text,
                                       out SecurePassword  SecurePassword)

            => TryParse(Text, out SecurePassword, out _);

        #endregion

        #region (static) TryParse (Text, out SecurePassword, out ErrorResponse)

        /// <summary>
        /// Try to parse the stored text representation of a secure password.
        /// </summary>
        /// <param name="Text">A text representation of a secure password.</param>
        /// <param name="SecurePassword">The parsed secure password.</param>
        /// <param name="ErrorResponse">Why the text could not be parsed.</param>
        public static Boolean TryParse(String                            Text,
                                       out SecurePassword                SecurePassword,
                                       [NotNullWhen(false)] out String?  ErrorResponse)
        {

            SecurePassword  = default;
            ErrorResponse   = null;

            if (Text is null || Text.Length == 0)
            {
                ErrorResponse = "The secure password must not be null or empty!";
                return false;
            }

            // The most likely mistake: a plain password instead of a stored hash.
            if (Text[0] != '$')
            {
                ErrorResponse = "The text is not a stored secure password of the form '$pbkdf2-sha256$i=<iterations>$<salt>$<hash>'; to hash a new password use Create(Password)!";
                return false;
            }

            var parts = Text.Split('$');

            if (parts.Length != 5 || parts[0].Length != 0)
            {
                ErrorResponse = "A secure password has the form '$pbkdf2-sha256$i=<iterations>$<salt>$<hash>'!";
                return false;
            }

            if (parts[1] != PBKDF2SHA256)
            {
                ErrorResponse = $"Unknown password hash algorithm '{parts[1]}'!";
                return false;
            }

            if (!parts[2].StartsWith("i=", StringComparison.Ordinal) ||
                !UInt32.TryParse(parts[2].AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) ||
                iterations == 0 ||
                iterations  > Int32.MaxValue)
            {
                ErrorResponse = $"Invalid iteration count '{parts[2]}'!";
                return false;
            }

            if (!TryFromBase64(parts[3], out var salt) || salt.Length < 8)
            {
                ErrorResponse = "Invalid salt!";
                return false;
            }

            if (!TryFromBase64(parts[4], out var hash) || hash.Length < 16)
            {
                ErrorResponse = "Invalid hash!";
                return false;
            }

            SecurePassword = new SecurePassword(PBKDF2SHA256, iterations, salt, hash);
            return true;

        }

        #endregion


        #region Verify(Password)

        /// <summary>
        /// Whether the given password produces this hash. The comparison
        /// takes the same time whether it succeeds or fails.
        /// </summary>
        /// <param name="Password">The password to verify.</param>
        public Boolean Verify(String Password)
        {

            if (Password is null || salt is null || hash is null || algorithm != PBKDF2SHA256)
                return false;

            return CryptographicOperations.FixedTimeEquals(
                       Derive(Password, salt, Iterations, hash.Length),
                       hash
                   );

        }

        #endregion

        #region NeedsRehash(MinIterations = DefaultIterations)

        /// <summary>
        /// Whether this hash was created with fewer iterations than wanted
        /// now, so that it should be replaced by Create(Password) at the next
        /// opportunity, i.e. right after a successful sign-in, when the
        /// password is at hand.
        /// </summary>
        /// <param name="MinIterations">The number of iterations wanted now.</param>
        public Boolean NeedsRehash(UInt32 MinIterations = DefaultIterations)
            => IsNullOrEmpty || algorithm != PBKDF2SHA256 || Iterations < MinIterations;

        #endregion

        #region Clone()

        /// <summary>
        /// A secure password is immutable, so a clone is the value itself.
        /// </summary>
        public SecurePassword Clone()
            => this;

        #endregion


        #region (private static) Derive(Password, Salt, Iterations, HashSize)

        private static Byte[] Derive(String  Password,
                                     Byte[]  Salt,
                                     UInt32  Iterations,
                                     Int32   HashSize)

            => Rfc2898DeriveBytes.Pbkdf2(
                   Password,
                   Salt,
                   (Int32) Iterations,
                   HashAlgorithmName.SHA256,
                   HashSize
               );

        #endregion

        #region (private static) Base64 without padding

        private static String ToBase64(Byte[] Bytes)
            => Convert.ToBase64String(Bytes).TrimEnd('=');

        private static Boolean TryFromBase64(String                           Text,
                                             [NotNullWhen(true)] out Byte[]?  Bytes)
        {

            Bytes = null;

            var unpadded = Text.TrimEnd('=');

            if (unpadded.Length == 0 || unpadded.Length % 4 == 1)
                return false;

            var padded = unpadded + new String('=', (4 - unpadded.Length % 4) % 4);

            try
            {
                Bytes = Convert.FromBase64String(padded);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }

        }

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
        /// Compares two secure passwords for equality: same algorithm,
        /// iterations, salt and hash.
        /// </summary>
        /// <param name="SecurePassword">A secure password to compare with.</param>
        public Boolean Equals(SecurePassword SecurePassword)

            => algorithm  == SecurePassword.algorithm  &&
               Iterations == SecurePassword.Iterations &&
               (salt ?? []).AsSpan().SequenceEqual(SecurePassword.salt ?? []) &&
               (hash ?? []).AsSpan().SequenceEqual(SecurePassword.hash ?? []);

        #endregion

        #region Equals(Password)

        /// <summary>
        /// Verify the given password. Kept for source compatibility, use Verify(Password)!
        /// </summary>
        /// <param name="Password">A password to verify.</param>
        [Obsolete("Use Verify(Password) instead!")]
        public Boolean Equals(String Password)
            => Verify(Password);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()
        {

            var hashCode = new HashCode();

            hashCode.Add     (algorithm);
            hashCode.Add     (Iterations);
            hashCode.AddBytes(salt ?? []);
            hashCode.AddBytes(hash ?? []);

            return hashCode.ToHashCode();

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// The PHC string representation, e.g. "$pbkdf2-sha256$i=600000$&lt;salt&gt;$&lt;hash&gt;",
        /// or an empty string for an empty secure password.
        /// </summary>
        public override String ToString()

            => salt is null || hash is null
                   ? ""
                   : $"${Algorithm}$i={Iterations.ToString(CultureInfo.InvariantCulture)}${ToBase64(salt)}${ToBase64(hash)}";

        #endregion

    }

}
