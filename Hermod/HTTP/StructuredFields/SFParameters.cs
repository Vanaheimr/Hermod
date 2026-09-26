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
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The parameters that may follow any item or inner list (RFC 9651,
    /// Section 3.1.2):
    ///
    ///    parameters = *( ";" *SP parameter )
    ///    parameter  = param-key [ "=" bare-item ]
    ///
    /// Order is kept, because serializing has to reproduce the field byte for
    /// byte; a parameter given twice keeps the position of the first and the
    /// value of the last, which is what the specification's ordered map does.
    ///
    /// A parameter written without a value is true. That is the grammar, not a
    /// convenience: "a;b" and "a;b=?1" are the same field.
    /// </summary>
    /// <seealso cref="https://www.rfc-editor.org/rfc/rfc9651.html#section-3.1.2"/>
    public sealed class SFParameters
    {

        #region Data

        private readonly List<KeyValuePair<String, SFBareItem>> parameters;

        #endregion

        #region Properties

        /// <summary>
        /// No parameters at all.
        /// </summary>
        public static SFParameters Empty
            => new ();

        /// <summary>
        /// The parameters, in the order they were written.
        /// </summary>
        public IReadOnlyList<KeyValuePair<String, SFBareItem>>  Values
            => parameters;

        /// <summary>
        /// The number of parameters.
        /// </summary>
        public Int32  Count
            => parameters.Count;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new set of parameters.
        /// </summary>
        public SFParameters(IEnumerable<KeyValuePair<String, SFBareItem>>? Parameters = null)
        {

            this.parameters = [];

            foreach (var parameter in Parameters ?? [])
                Set(parameter.Key, parameter.Value);

        }

        #endregion


        #region Set(Key, Value)

        /// <summary>
        /// Add a parameter, or replace the value of one that is already there
        /// without moving it.
        /// </summary>
        public SFParameters Set(String Key, SFBareItem Value)
        {

            if (!StructuredFields.IsKey(Key))
                throw new ArgumentException($"'{Key}' is not a valid structured field key!", nameof(Key));

            var existing = parameters.FindIndex(parameter => parameter.Key == Key);

            if (existing >= 0)
                parameters[existing] = new (Key, Value);
            else
                parameters.Add(new (Key, Value));

            return this;

        }

        #endregion

        #region TryGet(Key, out Value)

        /// <summary>
        /// Look up one parameter.
        /// </summary>
        public Boolean TryGet(String Key, out SFBareItem Value)
        {

            foreach (var parameter in parameters)
            {
                if (parameter.Key == Key)
                {
                    Value = parameter.Value;
                    return true;
                }
            }

            Value = default;
            return false;

        }

        #endregion

        #region ContainsKey(Key)

        /// <summary>
        /// Whether the given parameter is present.
        /// </summary>
        public Boolean ContainsKey(String Key)

            => TryGet(Key, out _);

        #endregion


        #region ToString()

        /// <summary>
        /// Serialize these parameters (RFC 9651, Section 4.1.1.2), leaving out
        /// the value of every parameter that is true - which is how the grammar
        /// writes it, and what a recipient will read back.
        /// </summary>
        public override String ToString()
        {

            if (parameters.Count == 0)
                return "";

            var serialized = new StringBuilder();

            foreach (var parameter in parameters)
            {

                serialized.Append(';').Append(parameter.Key);

                if (parameter.Value.Type != SFType.Boolean ||
                   !parameter.Value.AsBoolean)
                    serialized.Append('=').Append(parameter.Value.ToString());

            }

            return serialized.ToString();

        }

        #endregion

    }

}
