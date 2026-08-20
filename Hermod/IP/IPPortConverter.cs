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

using System.ComponentModel;
using System.Globalization;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Converts an IP port from and to its text representation.
    ///
    /// Configuration values are always text - "ControlPort": 8500 within a JSON
    /// file arrives as the string "8500" - and the configuration binder asks
    /// TypeDescriptor how to turn that into the target type. Without this
    /// converter every IPPort within a configuration object would fail to bind.
    /// </summary>
    public sealed class IPPortConverter : TypeConverter
    {

        /// <summary>
        /// Whether this converter can convert from the given type.
        /// </summary>
        public override Boolean CanConvertFrom(ITypeDescriptorContext?  Context,
                                               Type                     SourceType)

            => SourceType == typeof(String) ||
               SourceType == typeof(UInt16) ||
               SourceType == typeof(Int32)  ||
               base.CanConvertFrom(Context, SourceType);


        /// <summary>
        /// Whether this converter can convert to the given type.
        /// </summary>
        public override Boolean CanConvertTo(ITypeDescriptorContext?  Context,
                                             Type?                    DestinationType)

            => DestinationType == typeof(String) ||
               base.CanConvertTo(Context, DestinationType);


        /// <summary>
        /// Convert the given object into an IP port.
        /// </summary>
        public override Object? ConvertFrom(ITypeDescriptorContext?  Context,
                                            CultureInfo?             Culture,
                                            Object                   Value)
        {

            switch (Value)
            {

                case String text
                    when IPPort.TryParse(text.Trim(), out var ipPort):
                    return ipPort;

                case String text:
                    throw new FormatException($"Invalid IP port: '{text}'!");

                case UInt16 number:
                    return IPPort.Parse(number);

                case Int32 number
                    when number is >= 0 and <= UInt16.MaxValue:
                    return IPPort.Parse(number);

            }

            return base.ConvertFrom(Context, Culture, Value);

        }


        /// <summary>
        /// Convert the given IP port into the given type.
        /// </summary>
        public override Object? ConvertTo(ITypeDescriptorContext?  Context,
                                          CultureInfo?             Culture,
                                          Object?                  Value,
                                          Type                     DestinationType)

            => DestinationType == typeof(String) && Value is IPPort ipPort
                   ? ipPort.ToString()
                   : base.ConvertTo(Context, Culture, Value, DestinationType);

    }

}
