/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// SecurePassword: PBKDF2 hashes that carry their parameters and
    /// survive the round trip through their PHC text representation.
    /// </summary>
    [TestFixture]
    public class SecurePasswordTests
    {

        #region Data

        // 16 and 32 zero bytes as Base64 without padding.
        private static readonly String  validSalt  = new ('A', 22);
        private static readonly String  validHash  = new ('A', 43);

        #endregion


        #region Create_And_Verify()

        [Test]
        public void Create_And_Verify()
        {

            var securePassword = SecurePassword.Create("correct horse battery staple");

            Assert.That(securePassword.Algorithm,                               Is.EqualTo(SecurePassword.PBKDF2SHA256));
            Assert.That(securePassword.Iterations,                              Is.EqualTo(SecurePassword.DefaultIterations));
            Assert.That(securePassword.Salt.Length,                             Is.EqualTo(SecurePassword.DefaultSaltSize));
            Assert.That(securePassword.Hash.Length,                             Is.EqualTo(SecurePassword.DefaultHashSize));
            Assert.That(securePassword.IsNotNullOrEmpty,                        Is.True);
            Assert.That(securePassword.Verify("correct horse battery staple"),  Is.True);
            Assert.That(securePassword.Verify("correct horse battery stapl"),   Is.False);
            Assert.That(securePassword.Verify(""),                              Is.False);
            Assert.That(securePassword.NeedsRehash(),                           Is.False);

        }

        #endregion

        #region Create_Rejects_Empty_Passwords()

        [Test]
        public void Create_Rejects_Empty_Passwords()
        {
            Assert.That(() => SecurePassword.Create(""),                       Throws.ArgumentException);
            Assert.That(() => SecurePassword.Create("secret", Iterations: 0),  Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        #endregion

        #region Text_Representation_Round_Trip()

        [Test]
        public void Text_Representation_Round_Trip()
        {

            var original  = SecurePassword.Create("secret", Iterations: 1000);
            var text      = original.ToString();
            var parts     = text.Split('$');

            Assert.That(text,          Does.StartWith("$pbkdf2-sha256$i=1000$"));
            Assert.That(parts.Length,  Is.EqualTo(5));
            Assert.That(parts[3],      Does.Not.Contain("="),  "PHC strings carry salt and hash as Base64 without padding!");
            Assert.That(parts[4],      Does.Not.Contain("="));

            var reparsed = SecurePassword.Parse(text);

            Assert.That(reparsed,                    Is.EqualTo(original));
            Assert.That(reparsed == original,        Is.True);
            Assert.That(reparsed.GetHashCode(),      Is.EqualTo(original.GetHashCode()));
            Assert.That(reparsed.Iterations,         Is.EqualTo(1000));
            Assert.That(reparsed.Verify("secret"),   Is.True);
            Assert.That(reparsed.Verify("Secret"),   Is.False);
            Assert.That(reparsed.ToString(),         Is.EqualTo(text));

        }

        #endregion

        #region Parse_Accepts_Padded_Base64()

        [Test]
        public void Parse_Accepts_Padded_Base64()
        {

            var original  = SecurePassword.Create("secret", Iterations: 1000);
            var parts     = original.ToString().Split('$');
            var padded    = $"${parts[1]}${parts[2]}${Pad(parts[3])}${Pad(parts[4])}";

            Assert.That(padded,                          Does.Contain("="));
            Assert.That(SecurePassword.Parse(padded),    Is.EqualTo(original));

        }

        private static String Pad(String Base64)
            => Base64 + new String('=', (4 - Base64.Length % 4) % 4);

        #endregion

        #region Older_Parameters_Ask_For_A_Rehash()

        [Test]
        public void Older_Parameters_Ask_For_A_Rehash()
        {

            var weak = SecurePassword.Create("secret", Iterations: 1000);

            Assert.That(weak.Verify("secret"),                          Is.True,  "verification uses the stored iteration count");
            Assert.That(weak.NeedsRehash(),                             Is.True);
            Assert.That(weak.NeedsRehash(MinIterations: 1000),          Is.False);
            Assert.That(SecurePassword.Create("secret").NeedsRehash(),  Is.False);

        }

        #endregion

        #region Parts_Constructor_Matches_Create()

        [Test]
        public void Parts_Constructor_Matches_Create()
        {

            var created  = SecurePassword.Create("secret", Iterations: 1000);
            var rebuilt  = new SecurePassword(created.Algorithm, created.Iterations, created.Salt.ToArray(), created.Hash.ToArray());

            Assert.That(rebuilt,                   Is.EqualTo(created));
            Assert.That(rebuilt.Verify("secret"),  Is.True);

            Assert.That(() => new SecurePassword("md5",                        1000, created.Salt.ToArray(), created.Hash.ToArray()),  Throws.ArgumentException);
            Assert.That(() => new SecurePassword(SecurePassword.PBKDF2SHA256,  1000, new Byte[4],           created.Hash.ToArray()),  Throws.ArgumentException);

        }

        #endregion

        #region Malformed_Text_Is_Rejected()

        [Test]
        public void Malformed_Text_Is_Rejected()
        {

            var cases = new Dictionary<String, String> {
                { "",                                                     "empty" },
                { "secret",                                               "a plain password" },
                { $"$pbkdf2-sha256$i=1000${validSalt}",                   "a missing hash" },
                { $"$argon2id$i=1000${validSalt}${validHash}",            "an unknown algorithm" },
                { $"$pbkdf2-sha256$i=0${validSalt}${validHash}",          "zero iterations" },
                { $"$pbkdf2-sha256$i=abc${validSalt}${validHash}",        "a non-numeric iteration count" },
                { $"$pbkdf2-sha256$rounds=1000${validSalt}${validHash}",  "an unknown parameter" },
                { $"$pbkdf2-sha256$i=1000$not*base64${validHash}",        "an invalid salt" },
                { $"$pbkdf2-sha256$i=1000$AAAA${validHash}",              "a salt that is too short" },
                { $"$pbkdf2-sha256$i=1000${validSalt}$AAAAAAAAAAAAAAAA",  "a hash that is too short" }
            };

            foreach (var (text, description) in cases)
            {
                Assert.That(SecurePassword.TryParse(text, out _, out var errorResponse),  Is.False,                   description);
                Assert.That(errorResponse,                                                Is.Not.Null.And.Not.Empty,  description);
                Assert.That(() => SecurePassword.Parse(text),                             Throws.ArgumentException,   description);
                Assert.That(SecurePassword.TryParse(text),                                Is.Null,                    description);
            }

            Assert.That(SecurePassword.TryParse($"$pbkdf2-sha256$i=1000${validSalt}${validHash}", out _, out _),  Is.True,  "the well-formed reference string parses");

        }

        #endregion

        #region A_Plain_Password_Points_To_Create()

        [Test]
        public void A_Plain_Password_Points_To_Create()
        {

            // Before the redesign Parse(text) hashed a plain password; now it
            // says what to do instead.
            Assert.That(SecurePassword.TryParse("secret", out _, out var errorResponse),  Is.False);
            Assert.That(errorResponse,                                                    Does.Contain("Create(Password)"));

        }

        #endregion

        #region Equality_Is_By_Content()

        [Test]
        public void Equality_Is_By_Content()
        {

            var a = SecurePassword.Create("secret", Iterations: 1000);
            var b = SecurePassword.Create("secret", Iterations: 1000);

            Assert.That(a,                                         Is.Not.EqualTo(b),  "a fresh salt each time");
            Assert.That(a != b,                                    Is.True);
            Assert.That(a == SecurePassword.Parse(a.ToString()),   Is.True);

        }

        #endregion

        #region Default_Value_Is_Empty_And_Verifies_Nothing()

        [Test]
        public void Default_Value_Is_Empty_And_Verifies_Nothing()
        {

            SecurePassword empty = default;

            Assert.That(empty.IsNullOrEmpty,     Is.True);
            Assert.That(empty.Algorithm,         Is.Empty);
            Assert.That(empty.Length,            Is.EqualTo(0));
            Assert.That(empty.Verify("secret"),  Is.False);
            Assert.That(empty.NeedsRehash(),     Is.True);
            Assert.That(empty.ToString(),        Is.Empty);
            Assert.That(empty,                   Is.EqualTo(default(SecurePassword)));

            SecurePassword? nothing = null;

            Assert.That(nothing.IsNullOrEmpty(),  Is.True);

        }

        #endregion

        #region Legacy_Equals_Forwards_To_Verify()

        [Test]
        public void Legacy_Equals_Forwards_To_Verify()
        {

            var securePassword = SecurePassword.Create("secret", Iterations: 1000);

#pragma warning disable CS0618 // kept for source compatibility with Equals(password)
            Assert.That(securePassword.Equals("secret"),  Is.True);
            Assert.That(securePassword.Equals("other"),   Is.False);
#pragma warning restore CS0618

        }

        #endregion

    }

}
