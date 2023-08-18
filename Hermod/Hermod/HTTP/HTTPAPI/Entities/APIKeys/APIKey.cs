/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An API key.
    /// </summary>
    public class APIKey : AEntity<APIKey_Id, APIKey>
    {

        #region Data

        /// <summary>
        /// The default JSON-LD context of users.
        /// </summary>
        public readonly static JSONLDContext DefaultJSONLDContext = JSONLDContext.Parse("https://opendata.social/contexts/HTTPExtAPI/APIKey");

        #endregion

        #region Properties

        #region API

        private HTTPExtAPI? _API;

        /// <summary>
        /// The HTTPExtAPI of this API key.
        /// </summary>
        internal HTTPExtAPI API
        {

            get
            {
                return _API;
            }

            set
            {

                if (_API == value)
                    return;

                if (_API != null)
                    throw new ArgumentException("Illegal attempt to change the API of this API key!");

                _API = value ?? throw new ArgumentException("Illegal attempt to delete the API reference of this API key!");

            }

        }

        #endregion

        /// <summary>
        /// The related user.
        /// </summary>
        public User_Id                  UserId                      { get; }

        /// <summary>
        /// An internationalized description of the API key.
        /// </summary>
        public I18NString               Description                 { get; }

        /// <summary>
        /// The access rights of the API key.
        /// </summary>
        public APIKeyRights             AccessRights                { get; }

        /// <summary>
        /// The creation timestamp.
        /// </summary>
        public DateTime                 Created                     { get; }

        /// <summary>
        /// The API key is not valid before this optional timestamp.
        /// </summary>
        public DateTime?                NotBefore                   { get; }

        /// <summary>
        /// The API key is not valid after this optional timestamp.
        /// </summary>
        public DateTime?                NotAfter                    { get; }

        /// <summary>
        /// The API key is only valid when sent from one of the given remote IP addresses.
        /// </summary>
        public IEnumerable<IIPAddress>  ValidRemoteIPAddresses      { get; }

        /// <summary>
        /// The API key is currently disabled.
        /// </summary>
        public Boolean                  IsDisabled                  { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new API key.
        /// </summary>
        /// <param name="Id">The unique identification of the API key.</param>
        /// <param name="UserId">The related user identification.</param>
        /// 
        /// <param name="Description">An optional internationalized description of the API key.</param>
        /// <param name="AccessRights">The access rights of the API key.</param>
        /// <param name="Created">The creation timestamp.</param>
        /// <param name="NotBefore">The API key is not valid before this optional timestamp.</param>
        /// <param name="NotAfter">The API key is not valid after this optional timestamp.</param>
        /// <param name="ValidRemoteIPAddresses">The API key is only valid when sent from one of the given remote IP addresses.</param>
        /// <param name="IsDisabled">The API key is currently disabled.</param>
        /// 
        /// <param name="CustomData">Custom data to be stored with this user.</param>
        /// <param name="JSONLDContext">The JSON-LD context of this user.</param>
        /// <param name="DataSource">The source of all this data, e.g. an automatic importer.</param>
        /// <param name="LastChange">The timestamp of the last changes within this user. Can e.g. be used as a HTTP ETag.</param>
        public APIKey(APIKey_Id                 Id,
                      User_Id                   UserId,

                      I18NString?               Description              = null,
                      APIKeyRights              AccessRights             = APIKeyRights.ReadOnly,
                      DateTime?                 Created                  = null,
                      DateTime?                 NotBefore                = null,
                      DateTime?                 NotAfter                 = null,
                      IEnumerable<IIPAddress>?  ValidRemoteIPAddresses   = null,
                      Boolean?                  IsDisabled               = false,

                      JObject?                  CustomData               = default,
                      JSONLDContext?            JSONLDContext            = default,
                      String?                   DataSource               = default,
                      DateTime?                 LastChange               = default)

            : base(Id,
                   JSONLDContext ?? DefaultJSONLDContext,
                   null,
                   null,
                   null,
                   CustomData,
                   null,
                   LastChange,
                   DataSource)

        {

            this.UserId                  = UserId;
            this.Description             = Description ?? I18NString.Empty;
            this.AccessRights            = AccessRights;
            this.Created                 = Created     ?? Timestamp.Now;
            this.NotBefore               = NotBefore;
            this.NotAfter                = NotAfter;
            this.ValidRemoteIPAddresses  = ValidRemoteIPAddresses is not null && ValidRemoteIPAddresses.Any()
                                               ? ValidRemoteIPAddresses.Distinct().ToArray()
                                               : Array.Empty<IIPAddress>();
            this.IsDisabled              = IsDisabled  ?? false;

        }

        #endregion


        #region (static) TryParse(JSON, ..., out APIKey, out ErrorResponse)

        public static Boolean TryParse(JObject               JSON,
                                       UserProviderDelegate  UserProvider,
                                       out APIKey?           APIKey,
                                       out String?           ErrorResponse,
                                       APIKey_Id?            APIKeyURI = null)
        {

            try
            {

                APIKey = null;

                #region Parse APIKey                    [optional]

                // Verify that a given API key is at least valid.
                if (!JSON.ParseOptional("@id",
                                        "API key",
                                        APIKey_Id.TryParse,
                                        out APIKey_Id? APIKeyBody,
                                        out ErrorResponse))
                {
                    return false;
                }

                if (!APIKeyURI.HasValue && !APIKeyBody.HasValue)
                {
                    ErrorResponse = "The API key is missing!";
                    return false;
                }

                if (APIKeyURI.HasValue && APIKeyBody.HasValue && APIKeyURI.Value != APIKeyBody.Value)
                {
                    ErrorResponse = "The optional API key given within the JSON body does not match the one given in the URI!";
                    return false;
                }

                #endregion

                #region Parse Context                   [mandatory]

                if (!JSON.ParseMandatory("@context",
                                         "JSON-LinkedData context information",
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

                #region Parse UserId                    [mandatory]

                // Verify that a given user identification
                //   is at least valid.
                if (!JSON.ParseMandatory("userId",
                                         "user identification",
                                         User_Id.TryParse,
                                         out User_Id UserId,
                                         out ErrorResponse))
                {
                    return false;
                }

                if (!UserProvider(UserId, out var User))
                {
                    ErrorResponse = "The given user '" + UserId + "' is unknown!";
                    return false;
                }

                #endregion

                #region Parse Description               [optional]

                if (JSON.ParseOptional("description",
                                             "description",
                                             out I18NString Description,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion

                #region Parse AccessRights              [mandatory]

                if (!JSON.ParseMandatoryEnum("accessRights",
                                                   "Access Rights",
                                                  // s => s.ParseMandatory_APIKeyRights(),
                                                   out APIKeyRights AccessRights,
                                                   out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse Created                   [mandatory]

                if (!JSON.ParseMandatory("created",
                                         "creation timestamp",
                                         out DateTime Created,
                                         out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse NotBefore                 [optional]

                if (JSON.ParseOptional("NotBefore",
                                             "'not-valid-before'-timestamp",
                                             out DateTime? NotBefore,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion

                #region Parse NotAfter                  [optional]

                if (JSON.ParseOptional("NotAfter",
                                             "'not-valid-after'-timestamp",
                                             out DateTime? NotAfter,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion

                #region Parse ValidRemoteIPAddresses    [optional]

                if (!JSON.ParseOptionalHashSet("validRemoteIPAddresses",
                                              "valid remote IP addresses",
                                              IPAddress.TryParse,
                                              out HashSet<IIPAddress> ValidRemoteIPAddresses,
                                              out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion

                #region Parse IsDisabled                [optional]

                var IsDisabled = JSON["isDisabled"]?.Value<Boolean>();

                #endregion

                #region Parse CryptoHash                [optional]

                var CryptoHash       = JSON.GetOptional("cryptoHash");

                #endregion


                APIKey = new APIKey(APIKeyBody ?? APIKeyURI.Value,
                                    UserId,
                                    Description,
                                    AccessRights,
                                    Created,
                                    NotBefore,
                                    NotAfter,
                                    ValidRemoteIPAddresses,
                                    IsDisabled);

                ErrorResponse = null;
                return true;

            }
            catch (Exception e)
            {
                ErrorResponse  = e.Message;
                APIKey         = null;
                return false;
            }

        }

        #endregion

        #region ToJSON(Embedded = false)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        public override JObject ToJSON(Boolean Embedded = false)

            => ToJSON(Embedded,
                      true,
                      null);


        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        /// <param name="IncludeLastChange">Whether to include the lastChange timestamp of this object.</param>
        /// <param name="CustomAPIKeySerializer">A delegate to serialize custom API key JSON objects.</param>
        public JObject ToJSON(Boolean                                  Embedded,
                              Boolean                                  IncludeLastChange,
                              CustomJObjectSerializerDelegate<APIKey>  CustomAPIKeySerializer)

        {

            var JSON = base.ToJSON(Embedded,
                                   IncludeLastChange,
                                   null,
                                   new JProperty?[] {

                                       new JProperty("userId",                        UserId.         ToString()),
                                       new JProperty("description",                   Description.    ToJSON()),
                                       new JProperty("accessRights",                  AccessRights.   AsText()),
                                       new JProperty("created",                       Created.        ToIso8601()),

                                       NotBefore.HasValue
                                           ? new JProperty("notBefore",               NotBefore.Value.ToIso8601())
                                           : null,

                                       NotAfter.HasValue
                                           ? new JProperty("notAfter",                NotAfter. Value.ToIso8601())
                                           : null,

                                       ValidRemoteIPAddresses.SafeAny()
                                           ? new JProperty("validRemoteIPAddresses",  new JArray(ValidRemoteIPAddresses.Select(validRemoteIPAddress => validRemoteIPAddress.ToString())))
                                           : null,

                                       IsDisabled
                                           ? new JProperty("isDisabled",              IsDisabled)
                                           : null

                                   });

            return CustomAPIKeySerializer is not null
                       ? CustomAPIKeySerializer(this, JSON)
                       : JSON;

        }

        #endregion

        #region Clone(NewAPIKeyId = null)

        /// <summary>
        /// Clone this object.
        /// </summary>
        /// <param name="NewAPIKeyId">An optional new API key identification.</param>
        public APIKey Clone(APIKey_Id? NewAPIKeyId = null)

            => new APIKey(NewAPIKeyId ?? Id.Clone,
                          UserId,
                          Description?.Clone,
                          AccessRights,
                          Created,
                          NotBefore,
                          NotAfter,
                          ValidRemoteIPAddresses.ToArray(),
                          IsDisabled,

                          CustomData,
                          JSONLDContext,
                          DataSource,
                          LastChangeDate);

        #endregion


        #region CopyAllLinkedDataFrom(OldAPIKey)

        public override void CopyAllLinkedDataFrom(APIKey OldAPIKey)
        {

        }

        #endregion


        #region Operator overloading

        #region Operator == (APIKey1, APIKey2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey1">An API key.</param>
        /// <param name="APIKey2">Another API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (APIKey APIKey1, APIKey APIKey2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(APIKey1, APIKey2))
                return true;

            // If one is null, but not both, return false.
            if (APIKey1 is null || APIKey2 is null)
                return false;

            return APIKey1.Equals(APIKey2);

        }

        #endregion

        #region Operator != (APIKey1, APIKey2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey1">An API key.</param>
        /// <param name="APIKey2">Another API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (APIKey APIKey1, APIKey APIKey2)
            => !(APIKey1 == APIKey2);

        #endregion

        #region Operator <  (APIKey1, APIKey2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey1">An API key.</param>
        /// <param name="APIKey2">Another API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (APIKey APIKey1, APIKey APIKey2)
        {

            if (APIKey1 is null)
                throw new ArgumentNullException(nameof(APIKey1), "The given APIKey1 must not be null!");

            return APIKey1.CompareTo(APIKey2) < 0;

        }

        #endregion

        #region Operator <= (APIKey1, APIKey2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey1">An API key.</param>
        /// <param name="APIKey2">Another API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (APIKey APIKey1, APIKey APIKey2)
            => !(APIKey1 > APIKey2);

        #endregion

        #region Operator >  (APIKey1, APIKey2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey1">An API key.</param>
        /// <param name="APIKey2">Another API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (APIKey APIKey1, APIKey APIKey2)
        {

            if (APIKey1 is null)
                throw new ArgumentNullException(nameof(APIKey1), "The given APIKey1 must not be null!");

            return APIKey1.CompareTo(APIKey2) > 0;

        }

        #endregion

        #region Operator >= (APIKey1, APIKey2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey1">An API key.</param>
        /// <param name="APIKey2">Another API key.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (APIKey APIKey1, APIKey APIKey2)
            => !(APIKey1 < APIKey2);

        #endregion

        #endregion

        #region IComparable<APIKey> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public override Int32 CompareTo(Object Object)

            => Object is APIKey apiKey
                   ? CompareTo(apiKey)
                   : throw new ArgumentException("The given object is not an API key!", nameof(Object));

        #endregion

        #region CompareTo(APIKey)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="APIKey">An object to compare with.</param>
        public override Int32 CompareTo(APIKey APIKey)

            => APIKey is not null
                   ? Id.CompareTo(APIKey.Id)
                   : throw new ArgumentException("The given object is not an API key!", nameof(APIKey));

        #endregion

        #endregion

        #region IEquatable<APIKey> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object? Object)

            => Object is APIKey apiKey &&
                  Equals(apiKey);

        #endregion

        #region Equals(APIKey)

        /// <summary>
        /// Compares two API keys informations for equality.
        /// </summary>
        /// <param name="APIKey">An API key to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public override Boolean Equals(APIKey APIKey)

            => APIKey is not null &&
                   Id.Equals(APIKey.Id);

        #endregion

        #endregion

        #region GetHashCode()

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

            => String.Concat("'", Id, "' for ",
                             UserId.ToString(), ", [",
                             AccessRights.ToString(),
                             NotAfter != null ? ", expires at " + NotAfter.Value.ToIso8601() : "",
                             IsDisabled ? ", disabled]" : "]");

        #endregion


        #region ToBuilder(NewAPIKeyId = null)

        /// <summary>
        /// Return a builder for this API key.
        /// </summary>
        /// <param name="NewAPIKeyId">An optional new API key identification.</param>
        public Builder ToBuilder(APIKey_Id? NewAPIKeyId = null)

            => new Builder(NewAPIKeyId ?? Id.Clone,
                           UserId,
                           Description,
                           AccessRights,
                           Created,
                           NotBefore,
                           NotAfter,
                           ValidRemoteIPAddresses,
                           IsDisabled,

                           CustomData,
                           JSONLDContext,
                           DataSource,
                           LastChangeDate);

        #endregion

        #region (class) Builder

        /// <summary>
        /// An API key builder.
        /// </summary>
        public new class Builder : AEntity<APIKey_Id, APIKey>.Builder
        {

            #region Properties

            /// <summary>
            /// The related user.
            /// </summary>
            public User_Id              UserId                    { get; set; }

            /// <summary>
            /// An internationalized description of the API key.
            /// </summary>
            public I18NString           Description               { get; set; }

            /// <summary>
            /// The access rights of the API key.
            /// </summary>
            public APIKeyRights?        AccessRights              { get; set; }

            /// <summary>
            /// The creation timestamp.
            /// </summary>
            public DateTime?            Created                   { get; set; }

            /// <summary>
            /// The API key is not valid before this optional timestamp.
            /// </summary>
            public DateTime?            NotBefore                 { get; set; }

            /// <summary>
            /// The API key is not valid after this optional timestamp.
            /// </summary>
            public DateTime?            NotAfter                  { get; set; }

            /// <summary>
            /// The API key is only valid when sent from one of the given remote IP addresses.
            /// </summary>
            public HashSet<IIPAddress>  ValidRemoteIPAddresses    { get; }

            /// <summary>
            /// The API key is currently disabled.
            /// </summary>
            public Boolean?             IsDisabled                { get; set; }


            /// <summary>
            /// The hash value of this object.
            /// </summary>
            public String               CurrentCryptoHash         { get; protected set; }

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new API key builder.
            /// </summary>
            /// <param name="Id">The unique identification of the API key.</param>
            /// 
            /// <param name="UserId">The related user.</param>
            /// <param name="Description">An optional internationalized description of the API key.</param>
            /// <param name="AccessRights">The access rights of the API key.</param>
            /// <param name="Created">The creation timestamp.</param>
            /// <param name="NotBefore">The API key is not valid before this optional timestamp.</param>
            /// <param name="NotAfter">The API key is not valid after this optional timestamp.</param>
            /// <param name="ValidRemoteIPAddresses">The API key is only valid when sent from one of the given remote IP addresses.</param>
            /// <param name="IsDisabled">The API key is currently disabled.</param>
            /// 
            /// <param name="CustomData">Custom data to be stored with this API key.</param>
            /// <param name="JSONLDContext">The JSON-LD context of this API key.</param>
            /// <param name="DataSource">The source of all this data, e.g. an automatic importer.</param>
            /// <param name="LastChange">The timestamp of the last changes within this API key. Can e.g. be used as a HTTP ETag.</param>
            public Builder(APIKey_Id                 Id,
                           User_Id                   UserId,
                           I18NString?               Description              = null,
                           APIKeyRights              AccessRights             = APIKeyRights.ReadOnly,
                           DateTime?                 Created                  = null,
                           DateTime?                 NotBefore                = null,
                           DateTime?                 NotAfter                 = null,
                           IEnumerable<IIPAddress>?  ValidRemoteIPAddresses   = null,
                           Boolean?                  IsDisabled               = false,

                           JObject?                  CustomData               = default,
                           JSONLDContext?            JSONLDContext            = default,
                           String?                   DataSource               = default,
                           DateTime?                 LastChange               = default)

                : base(Id,
                       JSONLDContext ?? DefaultJSONLDContext,
                       LastChange,
                       null,
                       CustomData,
                       null,
                       DataSource)

            {

                this.UserId                  = UserId;// ?? throw new ArgumentNullException(nameof(User), "The given API key must not be null!");
                this.Description             = Description ?? I18NString.Empty;
                this.AccessRights            = AccessRights;
                this.Created                 = Created     ?? Timestamp.Now;
                this.NotBefore               = NotBefore;
                this.NotAfter                = NotAfter;
                this.ValidRemoteIPAddresses  = ValidRemoteIPAddresses.SafeAny() ? new HashSet<IIPAddress>(ValidRemoteIPAddresses) : new HashSet<IIPAddress>();
                this.IsDisabled              = IsDisabled  ?? false;

            }

            #endregion


            #region CopyAllLinkedDataFrom(OldAPIKey)

            public override void CopyAllLinkedDataFrom(APIKey OldAPIKey)
            {

            }

            #endregion


            #region ToImmutable

            /// <summary>
            /// Return an immutable version of the API key.
            /// </summary>
            /// <param name="Builder">A API key builder.</param>
            public static implicit operator APIKey(Builder Builder)

                => Builder?.ToImmutable;


            /// <summary>
            /// Return an immutable version of the API key.
            /// </summary>
            public APIKey ToImmutable
            {
                get
                {

                    if (Id.IsNullOrEmpty)
                        throw new ArgumentNullException(nameof(APIKey),  "The given API key identification must not be null or empty!");

                    if (UserId.IsNullOrEmpty)
                        throw new ArgumentNullException(nameof(UserId),  "The given user must not be null!");

                    return new APIKey(Id,
                                      UserId,
                                      Description,
                                      AccessRights ?? APIKeyRights.ReadOnly,
                                      Created      ?? Timestamp.Now,
                                      NotBefore    ?? Timestamp.Now,
                                      NotAfter,
                                      ValidRemoteIPAddresses,
                                      IsDisabled   ?? false,

                                      CustomData,
                                      JSONLDContext,
                                      DataSource,
                                      LastChangeDate);

                }
            }

            #endregion


            #region Operator overloading

            #region Operator == (BuilderId1, BuilderId2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="BuilderId1">A API key builder.</param>
            /// <param name="BuilderId2">Another API key builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator == (Builder BuilderId1, Builder BuilderId2)
            {

                // If both are null, or both are same instance, return true.
                if (Object.ReferenceEquals(BuilderId1, BuilderId2))
                    return true;

                // If one is null, but not both, return false.
                if ((BuilderId1 is null) || (BuilderId2 is null))
                    return false;

                return BuilderId1.Equals(BuilderId2);

            }

            #endregion

            #region Operator != (BuilderId1, BuilderId2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="BuilderId1">A API key builder.</param>
            /// <param name="BuilderId2">Another API key builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator != (Builder BuilderId1, Builder BuilderId2)
                => !(BuilderId1 == BuilderId2);

            #endregion

            #region Operator <  (BuilderId1, BuilderId2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="BuilderId1">A API key builder.</param>
            /// <param name="BuilderId2">Another API key builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator < (Builder BuilderId1, Builder BuilderId2)
            {

                if (BuilderId1 is null)
                    throw new ArgumentNullException(nameof(BuilderId1), "The given BuilderId1 must not be null!");

                return BuilderId1.CompareTo(BuilderId2) < 0;

            }

            #endregion

            #region Operator <= (BuilderId1, BuilderId2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="BuilderId1">A API key builder.</param>
            /// <param name="BuilderId2">Another API key builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator <= (Builder BuilderId1, Builder BuilderId2)
                => !(BuilderId1 > BuilderId2);

            #endregion

            #region Operator >  (BuilderId1, BuilderId2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="BuilderId1">A API key builder.</param>
            /// <param name="BuilderId2">Another API key builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator > (Builder BuilderId1, Builder BuilderId2)
            {

                if (BuilderId1 is null)
                    throw new ArgumentNullException(nameof(BuilderId1), "The given BuilderId1 must not be null!");

                return BuilderId1.CompareTo(BuilderId2) > 0;

            }

            #endregion

            #region Operator >= (BuilderId1, BuilderId2)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="BuilderId1">A API key builder.</param>
            /// <param name="BuilderId2">Another API key builder.</param>
            /// <returns>true|false</returns>
            public static Boolean operator >= (Builder BuilderId1, Builder BuilderId2)
                => !(BuilderId1 < BuilderId2);

            #endregion

            #endregion

            #region IComparable<Builder> Members

            #region CompareTo(Object)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Object">An object to compare with.</param>
            public override Int32 CompareTo(Object? Object)

                => Object is Builder builder
                       ? CompareTo(builder)
                       : throw new ArgumentException("The given object is not an API key!");


            #endregion

            #region CompareTo(APIKey)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="APIKey">An API key object to compare with.</param>
            public override Int32 CompareTo(APIKey? APIKey)

                => APIKey is null
                       ? throw new ArgumentNullException(nameof(APIKey), "The given API key must not be null!")
                       : Id.CompareTo(APIKey.Id);

            #endregion

            #region CompareTo(Builder)

            /// <summary>
            /// Compares two instances of this object.
            /// </summary>
            /// <param name="Builder">An API key object to compare with.</param>
            public Int32 CompareTo(Builder Builder)

                => Builder is null
                       ? throw new ArgumentNullException(nameof(Builder), "The given API key must not be null!")
                       : Id.CompareTo(Builder.Id);

            #endregion

            #endregion

            #region IEquatable<Builder> Members

            #region Equals(Object)

            /// <summary>
            /// Compares two API keys for equality.
            /// </summary>
            /// <param name="Object">An API key to compare with.</param>
            public override Boolean Equals(Object? Object)

                => Object is Builder builder &&
                      Equals(builder);

            #endregion

            #region Equals(APIKey)

            /// <summary>
            /// Compares two API keys for equality.
            /// </summary>
            /// <param name="APIKey">An API key to compare with.</param>
            public override Boolean Equals(APIKey? APIKey)

                => APIKey is not null &&
                       Id.Equals(APIKey.Id);

            #endregion

            #region Equals(Builder)

            /// <summary>
            /// Compares two API keys for equality.
            /// </summary>
            /// <param name="Builder">An API key to compare with.</param>
            public Boolean Equals(Builder Builder)

                => Builder is not null &&
                       Id.Equals(Builder.Id);

            #endregion

            #endregion

            #region GetHashCode()

            /// <summary>
            /// Get the hashcode of this object.
            /// </summary>
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

        #endregion

    }

}
