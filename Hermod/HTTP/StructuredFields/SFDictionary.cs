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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    using System.Text;

    /// <summary>
    /// An ordered map of keys to members (RFC 9651, Section 3.2):
    ///
    ///    sf-dictionary = dict-member *( OWS "," OWS dict-member )
    ///    dict-member   = member-key ( parameters / ( "=" member-value ))
    ///    member-value  = sf-item / inner-list
    ///
    /// Ordered, because serializing has to reproduce the field; and a map,
    /// because a key given twice keeps the position of the first and the value
    /// of the last.
    ///
    /// A key written without a value is true, which is why "a, b=?0, c" is
    /// three entries and not one entry and two strays.
    /// </summary>
    /// <seealso cref="https://www.rfc-editor.org/rfc/rfc9651.html#section-3.2"/>
    public sealed class SFDictionary
    {

        #region Data

        private readonly List<KeyValuePair<String, SFMember>> members;

        #endregion

        #region Properties

        /// <summary>
        /// The entries, in the order they were written.
        /// </summary>
        public IReadOnlyList<KeyValuePair<String, SFMember>>  Values
            => members;

        /// <summary>
        /// The keys, in the order they were written.
        /// </summary>
        public IEnumerable<String>  Keys
            => members.Select(member => member.Key);

        /// <summary>
        /// The number of entries.
        /// </summary>
        public Int32  Count
            => members.Count;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new dictionary.
        /// </summary>
        public SFDictionary(IEnumerable<KeyValuePair<String, SFMember>>? Members = null)
        {

            this.members = [];

            foreach (var member in Members ?? [])
                Set(member.Key, member.Value);

        }

        #endregion


        #region Set(Key, Value)

        /// <summary>
        /// Add an entry, or replace the value of one that is already there
        /// without moving it.
        /// </summary>
        public SFDictionary Set(String Key, SFMember Value)
        {

            if (!StructuredFields.IsKey(Key))
                throw new ArgumentException($"'{Key}' is not a valid structured field key!", nameof(Key));

            var existing = members.FindIndex(member => member.Key == Key);

            if (existing >= 0)
                members[existing] = new (Key, Value);
            else
                members.Add(new (Key, Value));

            return this;

        }

        /// <summary>
        /// Add a bare item under a key, or replace it.
        /// </summary>
        public SFDictionary Set(String Key, SFBareItem Value)

            => Set(Key, new SFItem(Value));

        #endregion

        #region TryGet     (Key, out Value)

        /// <summary>
        /// Look up one entry.
        /// </summary>
        public Boolean TryGet(String Key, out SFMember? Value)
        {

            foreach (var member in members)
            {
                if (member.Key == Key)
                {
                    Value = member.Value;
                    return true;
                }
            }

            Value = null;
            return false;

        }

        #endregion

        #region TryGetItem (Key, out Value)

        /// <summary>
        /// Look up one entry, when it is expected to be a bare item rather than
        /// an inner list.
        /// </summary>
        public Boolean TryGetItem(String Key, out SFBareItem Value)
        {

            if (TryGet(Key, out var member) && member is SFItem item)
            {
                Value = item.Value;
                return true;
            }

            Value = default;
            return false;

        }

        #endregion

        #region ContainsKey(Key)

        /// <summary>
        /// Whether the given key is present.
        /// </summary>
        public Boolean ContainsKey(String Key)

            => TryGet(Key, out _);

        #endregion


        #region ToString()

        /// <summary>
        /// Serialize this dictionary (RFC 9651, Section 4.1.2), leaving out the
        /// value of every entry that is true - which is how the grammar writes
        /// it, and what a recipient will read back.
        /// </summary>
        public override String ToString()
        {

            var serialized = new StringBuilder();

            foreach (var member in members)
            {

                if (serialized.Length > 0)
                    serialized.Append(", ");

                serialized.Append(member.Key);

                if (member.Value is SFItem item &&
                    item.Value.Type == SFType.Boolean &&
                    item.Value.AsBoolean)
                    serialized.Append(item.Parameters.ToString());

                else
                    serialized.Append('=').Append(member.Value.ToString());

            }

            return serialized.ToString();

        }

        #endregion

    }

}
