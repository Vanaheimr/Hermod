/*
 * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
    /// An attached file.
    /// </summary>
    public class AttachedFile : IHasId<AttachedFile_Id>
    {

        #region Data

        /// <summary>
        /// The default JSON-LD context of attached files.
        /// </summary>
        private readonly static JSONLDContext DefaultJSONLDContext = JSONLDContext.Parse("https://opendata.social/contexts/HTTPExtAPI+json/attachedFile");

        #endregion

        #region Properties

        public AttachedFile_Id        Id               { get; }

        public I18NString             Description      { get; }

        public IEnumerable<HTTPPath>  Locations        { get; }

        public HTTPContentType        ContentType      { get; }

        public UInt64?                Size             { get; }

        public HTTPPath?              Icon             { get; }

        public DateTime?              Created          { get; }

        public DateTime?              LastModified     { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new attached file.
        /// </summary>
        private AttachedFile(AttachedFile_Id        Id,
                             I18NString             Description,
                             IEnumerable<HTTPPath>  Locations,
                             HTTPContentType        ContentType   = null,
                             UInt64?                Size          = null,
                             HTTPPath?              Icon          = null,
                             DateTime?              Created       = null,
                             DateTime?              LastModifed   = null,
                             String                 DataSource    = null)
        {

            this.Id            = Id;
            this.Description   = Description;
            this.Locations     = Locations;
            this.ContentType   = ContentType;
            this.Size          = Size;
            this.Icon          = Icon;
            this.Created       = Created     ?? Timestamp.Now;
            this.LastModified  = LastModifed ?? Timestamp.Now;
            //this.CryptoHashes  = 
            //this.Signatures    = 

        }

        #endregion


        #region ToJSON(...)

        ///// <summary>
        ///// Return a JSON representation of this object.
        ///// </summary>
        ///// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        ///// <param name="IncludeCryptoHash">Include the crypto hash value of this object.</param>
        //public JObject ToJSON(Boolean Embedded           = false,
        //                      Boolean IncludeCryptoHash  = false)

        //    => ToJSON(Embedded:           false,
        //              IncludeSignatures:  InfoStatus.Hidden,
        //              IncludeCryptoHash:  true);



        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data is embedded into another data structure, e.g. into a AttachedFile.</param>
        /// <param name="IncludeCryptoHash">Whether to include the cryptograhical hash value of this object.</param>
        public JObject ToJSON(Boolean                                       Embedded                       = false,
                              InfoStatus                                    IncludeSignatures              = InfoStatus.Hidden,
                              Boolean                                       IncludeCryptoHash              = true,
                              CustomJObjectSerializerDelegate<AttachedFile> CustomAttachedFileSerializer   = null)
        {

            var JSON = JSONObject.Create(

                           new JProperty("@id", Id.ToString()),

                           Embedded
                               ? null
                               : new JProperty("@context",            DefaultJSONLDContext.ToString()),

                           Description.IsNotNullOrEmpty()
                               ? new JProperty("description",         Description.ToJSON())
                               : null,

                           Icon.HasValue
                               ? new JProperty("icon",                Icon.              ToString())
                               : null,

                           ContentType != null
                               ? new JProperty("contentType",         ContentType.       ToString())
                               : null,

                           Created.HasValue
                               ? new JProperty("created",             Created.     Value.ToIso8601())
                               : null,

                           LastModified.HasValue
                               ? new JProperty("lastModified",        LastModified.Value.ToIso8601())
                               : null,

                           Size.HasValue
                               ? new JProperty("size",                Size.Value)
                               : null,

                           Locations.SafeAny()
                               ? new JProperty("locations",           new JArray(Locations.SafeSelect(location => location.ToString())))
                               : null

                           //IncludeCryptoHash
                           //    ? new JProperty("cryptoHash", CurrentCryptoHash)
                           //    : null


            );

            return CustomAttachedFileSerializer is not null
                       ? CustomAttachedFileSerializer(this, JSON)
                       : JSON;

        }

        #endregion

        #region (static) TryParseJSON(JSONObject, ..., out AttachedFile, out ErrorResponse)

        /// <summary>
        /// Try to parse the given communicator group JSON.
        /// </summary>
        /// <param name="JSONObject">A JSON object.</param>
        /// <param name="AttachedFile">The parsed attached file.</param>
        /// <param name="ErrorResponse">An error message.</param>
        public static Boolean TryParseJSON(JObject           JSONObject,
                                           out AttachedFile  AttachedFile,
                                           out String        ErrorResponse)

            => TryParseJSON(JSONObject,
                            out AttachedFile,
                            out ErrorResponse,
                            null);


        /// <summary>
        /// Try to parse the given communicator group JSON.
        /// </summary>
        /// <param name="JSONObject">A JSON object.</param>
        /// <param name="AttachedFile">The parsed attached file.</param>
        /// <param name="ErrorResponse">An error message.</param>
        public static Boolean TryParseJSON(JObject            JSONObject,
                                           out AttachedFile?  AttachedFile,
                                           out String?        ErrorResponse,
                                           AttachedFile_Id?   AttachedFileIdURL)
        {

            try
            {

                AttachedFile = null;

                if (JSONObject?.HasValues != true)
                {
                    ErrorResponse = "The given JSON object must not be null or empty!";
                    return false;
                }

                #region Parse AttachedFileId   [optional]

                // Verify that a given AttachedFile identification
                //   is at least valid.
                if (JSONObject.ParseOptional("@id",
                                             "attached file identification",
                                             AttachedFile_Id.TryParse,
                                             out AttachedFile_Id? AttachedFileIdBody,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                if (!AttachedFileIdURL.HasValue && !AttachedFileIdBody.HasValue)
                {
                    ErrorResponse = "The AttachedFile identification is missing!";
                    return false;
                }

                if (AttachedFileIdURL.HasValue && AttachedFileIdBody.HasValue && AttachedFileIdURL.Value != AttachedFileIdBody.Value)
                {
                    ErrorResponse = "The optional AttachedFile identification given within the JSON body does not match the one given in the URI!";
                    return false;
                }

                #endregion

                #region Parse Context          [mandatory]

                if (!JSONObject.ParseMandatory("@context",
                                               "JSON-LD context",
                                               JSONLDContext.TryParse,
                                               out JSONLDContext Context,
                                               out ErrorResponse))
                {
                    ErrorResponse = @"The JSON-LD ""@context"" information is missing!";
                    return false;
                }

                if (Context != DefaultJSONLDContext)
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

                var Locations     = new List<HTTPPath>();

                var ContentType   = HTTPContentType.Application.JSONLD_UTF8;

                var Size          = 0UL;

                var Icon          = new HTTPPath?();

                var Created       = Timestamp.Now;

                var LastModified  = Timestamp.Now;

 
                #region Get   DataSource       [optional]

                var DataSource = JSONObject.GetOptional("dataSource");

                #endregion


                #region Parse CryptoHash       [optional]

                var CryptoHash    = JSONObject.GetOptional("cryptoHash");

                #endregion


                AttachedFile = new AttachedFile(Id:               AttachedFileIdBody ?? AttachedFileIdURL.Value,
                                                Description:      Description,
                                                Locations:        Locations,
                                                ContentType:      ContentType,
                                                Size:             Size,
                                                Icon:             Icon,
                                                Created:          Created,
                                                LastModifed:      LastModified,
                                                DataSource:       DataSource);

                ErrorResponse = null;
                return true;

            }
            catch (Exception e)
            {
                ErrorResponse = e.Message;
                AttachedFile  = null;
                return false;
            }

        }

        #endregion


        #region Operator overloading

        #region Operator == (AttachedFileId1, AttachedFileId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFileId1">An attached file.</param>
        /// <param name="AttachedFileId2">Another attached file.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (AttachedFile AttachedFileId1,
                                           AttachedFile AttachedFileId2)
        {

            if (Object.ReferenceEquals(AttachedFileId1, AttachedFileId2))
                return true;

            if ((AttachedFileId1 is null) || (AttachedFileId2 is null))
                return false;

            return AttachedFileId1.Equals(AttachedFileId2);

        }

        #endregion

        #region Operator != (AttachedFileId1, AttachedFileId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFileId1">An attached file.</param>
        /// <param name="AttachedFileId2">Another attached file.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (AttachedFile AttachedFileId1,
                                           AttachedFile AttachedFileId2)

            => !(AttachedFileId1 == AttachedFileId2);

        #endregion

        #region Operator <  (AttachedFileId1, AttachedFileId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFileId1">An attached file.</param>
        /// <param name="AttachedFileId2">Another attached file.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (AttachedFile AttachedFileId1,
                                          AttachedFile AttachedFileId2)

            => AttachedFileId1 is null
                   ? throw new ArgumentNullException(nameof(AttachedFileId1), "The given attached file must not be null!")
                   : AttachedFileId1.CompareTo(AttachedFileId2) < 0;

        #endregion

        #region Operator <= (AttachedFileId1, AttachedFileId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFileId1">An attached file.</param>
        /// <param name="AttachedFileId2">Another attached file.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (AttachedFile AttachedFileId1,
                                           AttachedFile AttachedFileId2)

            => !(AttachedFileId1 > AttachedFileId2);

        #endregion

        #region Operator >  (AttachedFileId1, AttachedFileId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFileId1">An attached file.</param>
        /// <param name="AttachedFileId2">Another attached file.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (AttachedFile AttachedFileId1, AttachedFile AttachedFileId2)

            => AttachedFileId1 is null
                   ? throw new ArgumentNullException(nameof(AttachedFileId1), "The given attached file must not be null!")
                   : AttachedFileId1.CompareTo(AttachedFileId2) > 0;

        #endregion

        #region Operator >= (AttachedFileId1, AttachedFileId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFileId1">An attached file.</param>
        /// <param name="AttachedFileId2">Another attached file.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (AttachedFile AttachedFileId1,
                                           AttachedFile AttachedFileId2)

            => !(AttachedFileId1 < AttachedFileId2);

        #endregion

        #endregion

        #region IComparable<AttachedFile> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)

            => Object is AttachedFile attachedFile
                   ? CompareTo(attachedFile)
                   : throw new ArgumentException("The given object is not an attached file!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(AttachedFile)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="AttachedFile">An object to compare with.</param>
        public Int32 CompareTo(AttachedFile AttachedFile)

            => AttachedFile is null
                   ? throw new ArgumentNullException(nameof(AttachedFile),  "The given attached file must not be null!")
                   : Id.CompareTo(AttachedFile.Id);

        #endregion

        #endregion

        #region IEquatable<AttachedFileId> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)

            => Object is AttachedFile attachedFile &&
                   Equals(attachedFile);

        #endregion

        #region Equals(AttachedFile)

        /// <summary>
        /// Compares two defibrillator identifications for equality.
        /// </summary>
        /// <param name="AttachedFile">A defibrillator identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(AttachedFile AttachedFile)

            => !(AttachedFile is null) &&

                 Id.          Equals(AttachedFile.Id)          &&
                 Description. Equals(AttachedFile.Description) &&
                 Locations.   Equals(AttachedFile.Locations)   &&
                 ContentType. Equals(AttachedFile.ContentType) &&
                 Size.        Equals(AttachedFile.Size)        &&
                 Icon.        Equals(AttachedFile.Icon)        &&
                 Created.     Equals(AttachedFile.Created)     &&
                 LastModified.Equals(AttachedFile.LastModified);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => Id.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => Id.ToString();

        #endregion

    }

}
