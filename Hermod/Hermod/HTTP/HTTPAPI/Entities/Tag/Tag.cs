/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An Open Data tag.
    /// </summary>
    public class Tag : IEquatable<Tag>,
                       IComparable<Tag>,
                       IComparable
    {

        #region Data

        /// <summary>
        /// The JSON-LD context of this object.
        /// </summary>
        public const String JSONLDContext = "https://opendata.social/contexts/HTTPExtAPI+json/tag";

        #endregion

        #region Properties

        /// <summary>
        /// The unique identification of the tag.
        /// </summary>
        [Mandatory]
        public Tag_Id      Id             { get; }

        /// <summary>
        /// An optional (multi-language) description of the tag.
        /// </summary>
        [Optional]
        public I18NString  Description    { get; }


        public Tags.Builder Tags { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new tag.
        /// </summary>
        /// <param name="Id">The unique identification of the user tag.</param>
        /// <param name="Description">An optional (multi-language) description of the user tag.</param>
        public Tag(Tag_Id      Id,
                   I18NString  Description   = null)
        {

            this.Id           = Id;
            this.Description  = Description  ?? new I18NString();
            this.Tags         = new Tags.Builder();

        }

        #endregion


        public static Tag Create(Tag_Id     Id,
                                 Languages  Language,
                                 String     Text)

            => new Tag(Id,
                       I18NString.Create(Language,
                                         Text));


        #region ToJSON(...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        public JObject ToJSON()

            => ToJSON(Embedded:           false,
                      ExpandDescription:  InfoStatus.Hidden);


        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data is embedded into another data structure, e.g. into a remote party.</param>
        public JObject ToJSON(Boolean     Embedded           = false,
                              InfoStatus  ExpandDescription  = InfoStatus.Hidden)

            => JSONObject.Create(

                   new JProperty("@id", Id.ToString()),

                   Embedded
                       ? null
                       : new JProperty("@context",  JSONLDContext.ToString()),

                   Description.IsNotNullOrEmpty()
                       ? new JProperty("description", Description.ToJSON())
                       : null

               );

        #endregion

        #region (static) TryParseJSON(JSONObject, ..., out Tag, out ErrorResponse)

        /// <summary>
        /// Try to parse the given tag JSON.
        /// </summary>
        /// <param name="JSONObject">A JSON object.</param>
        /// <param name="Tag">The parsed tag.</param>
        /// <param name="ErrorResponse">An error message.</param>
        /// <param name="TagIdURL">An optional tag identification, e.g. from the HTTP URL.</param>
        public static Boolean TryParseJSON(JObject      JSONObject,
                                           out Tag?     Tag,
                                           out String?  ErrorResponse,
                                           Tag_Id?      TagIdURL = null)
        {

            try
            {

                Tag = null;

                if (JSONObject is null)
                {
                    ErrorResponse = "The given JSON object must not be null!";
                    return false;
                }

                #region Parse Id

                // Verify that a given tag identification
                //   is at least valid.
                if (JSONObject.ParseOptional("@id",
                                             "tag identification",
                                             Tag_Id.TryParse,
                                             out Tag_Id? TagIdBody,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                if (!TagIdURL.HasValue && !TagIdBody.HasValue)
                {
                    ErrorResponse = "The tag identification is missing!";
                    return false;
                }

                if (TagIdURL.HasValue && TagIdBody.HasValue && TagIdURL.Value != TagIdBody.Value)
                {
                    ErrorResponse = "The optional tag identification given within the JSON body does not match the one given in the URI!";
                    return false;
                }

                #endregion

                #region Parse Context          [mandatory]

                if (!JSONObject.ParseMandatoryText("@context",
                                                   "JSON-LD context",
                                                   out String Context,
                                                   out ErrorResponse))
                {
                    ErrorResponse = @"The JSON-LD ""@context"" information is missing!";
                    return false;
                }

                if (Context != JSONLDContext)
                {
                    ErrorResponse = @"The given JSON-LD ""@context"" information '" + Context + "' is not supported!";
                    return false;
                }

                #endregion

                #region Parse Description      [optional]

                if (JSONObject.ParseOptional("description",
                                             "description",
                                             out I18NString Description,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion


                Tag = new Tag(TagIdBody ?? TagIdURL.Value,
                              Description);

                ErrorResponse = null;
                return true;

            }
            catch (Exception e)
            {
                ErrorResponse  = e.Message;
                Tag            = null;
                return false;
            }

        }

        #endregion


        #region IComparable<Tag> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object is null)
                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

            var EVSE_Operator = Object as Tag;
            if ((Object) EVSE_Operator is null)
                throw new ArgumentException("The given object is not a tag!");

            return CompareTo(EVSE_Operator);

        }

        #endregion

        #region CompareTo(Tag)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Tag">A tag object to compare with.</param>
        public Int32 CompareTo(Tag Tag)
        {

            if ((Object) Tag is null)
                throw new ArgumentNullException(nameof(Tag), "The given tag must not be null!");

            return Id.CompareTo(Tag.Id);

        }

        #endregion

        #endregion

        #region IEquatable<Tag> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object is null)
                return false;

            var Tag = Object as Tag;
            if ((Object) Tag is null)
                return false;

            return Equals(Tag);

        }

        #endregion

        #region Equals(Tag)

        /// <summary>
        /// Compares two tags for equality.
        /// </summary>
        /// <param name="Tag">A tag to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(Tag Tag)
        {

            if ((Object) Tag is null)
                return false;

            return Id.Equals(Tag.Id);

        }

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Get the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
            => Id.GetHashCode();

        #endregion

        #region ToString()

        /// <summary>
        /// Get a string representation of this object.
        /// </summary>
        public override String ToString()
            => Id.ToString();

        #endregion


        #region ToBuilder(NewTagId = null)

        /// <summary>
        /// Return a builder for this tag.
        /// </summary>
        /// <param name="NewTagId">An optional new tag identification.</param>
        public Builder ToBuilder(Tag_Id? NewTagId = null)

            => new Builder(NewTagId ?? Id,
                           Description);

        #endregion

        #region (class) Builder

        /// <summary>
        /// A tag builder.
        /// </summary>
        public class Builder
        {

            #region Properties

            /// <summary>
            /// The unique identification of the tag.
            /// </summary>
            [Mandatory]
            public Tag_Id      Id             { get; }

            /// <summary>
            /// An optional (multi-language) description of the tag.
            /// </summary>
            [Optional]
            public I18NString  Description    { get; set; }

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new tag builder.
            /// </summary>
            /// <param name="Id">The unique identification of the user tag.</param>
            /// <param name="Description">An optional (multi-language) description of the user tag.</param>
            public Builder(Tag_Id      Id,
                           I18NString  Description  = null)
            {

                this.Id           = Id;
                this.Description  = Description  ?? new I18NString();

            }

            #endregion


            public static Builder Create(Tag_Id     Id,
                                         Languages  Language,
                                         String     Text)

            => new Builder(Id,
                           I18NString.Create(Language,
                                             Text));


            #region ToImmutable

            /// <summary>
            /// Return an immutable version of the tag.
            /// </summary>
            public static implicit operator Tag(Builder Builder)

                => Builder?.ToImmutable;


            /// <summary>
            /// Return an immutable version of the tag.
            /// </summary>
            public Tag ToImmutable

                => new Tag(Id,
                           Description);

            #endregion

        }

        #endregion

    }

}
