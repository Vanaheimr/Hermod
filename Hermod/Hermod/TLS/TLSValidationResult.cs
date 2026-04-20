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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A TLS validation result.
    /// </summary>
    public readonly struct TLSValidationResult
    {

        /// <summary>
        /// Whether the TLS validation was successful or not.
        /// </summary>
        public Boolean               IsValid     { get; }

        /// <summary>
        /// Optional errors during TLS validation.
        /// </summary>
        public IEnumerable<Error>    Errors      { get; }

        /// <summary>
        /// Optional warnings during  TLS validation.
        /// </summary>
        public IEnumerable<Warning>  Warnings    { get; }


        /// <summary>
        /// Create a new 
        /// </summary>
        /// <param name="IsValid">Whether the TLS validation was successful or not.</param>
        /// <param name="Errors">Optional errors during TLS validation.</param>
        /// <param name="Warnings">Optional warnings during  TLS validation.</param>
        public TLSValidationResult(Boolean                IsValid,
                                   IEnumerable<Error>?    Errors,
                                   IEnumerable<Warning>?  Warnings   = null)
        {

            this.IsValid   = IsValid;
            this.Errors    = Errors   ?? [];
            this.Warnings  = Warnings ?? [];

        }



        public static TLSValidationResult Success(IEnumerable<Warning>? Warnings = null)

            => new (
                   true,
                   null,
                   Warnings
               );


        public static TLSValidationResult Success(String   WarningMessage,
                                                  Object?  Context  = null)

            => new (
                   true,
                   null,
                   Illias.Warnings.Create(
                       WarningMessage,
                       Context
                   )
               );


        public static TLSValidationResult Failed(IEnumerable<Error>     Errors,
                                                 IEnumerable<Warning>?  Warnings = null)

            => new (
                   false,
                   Errors,
                   Warnings
               );


        public static TLSValidationResult Failed(String   ErrorMessage,
                                                 Object?  Context  = null)

            => new (
                   false,
                   Illias.Errors.Create(
                       ErrorMessage,
                       Context
                   )
               );


        public static TLSValidationResult GeneralError()

            => new (
                   false,
                   Illias.Errors.Create(
                       "General Error"
                   )
               );





        public static TLSValidationResult From(Boolean               IsValid,
                                               IEnumerable<String>?  Texts = null)

            => new (
                   IsValid,
                  !IsValid ?      Illias.Errors.  From(Texts ?? []) : [],
                   IsValid ? [] : Illias.Warnings.From(Texts ?? [])
               );




    }

}
