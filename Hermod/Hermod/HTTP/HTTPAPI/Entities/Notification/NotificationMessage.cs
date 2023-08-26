/*
 * Copyright (c) 2014-2023 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications
{

    /// <summary>
    /// A notification message.
    /// </summary>
    public class NotificationMessage : AEntity<NotificationMessage_Id,
                                               NotificationMessage>
    {

        #region Data

        /// <summary>
        /// The default JSON-LD context of organizations.
        /// </summary>
        public readonly static JSONLDContext DefaultJSONLDContext = JSONLDContext.Parse("https://opendata.social/contexts/UsersAPI/notificationMessage");

        #endregion

        #region Properties

        #region API

        private Object _API;

        /// <summary>
        /// The API of this News.
        /// </summary>
        internal Object API
        {

            get
            {
                return _API;
            }

            set
            {

                if (_API != null)
                    throw new ArgumentException("Illegal attempt to change the API of this notification message!");

                if (value == null)
                    throw new ArgumentException("Illegal attempt to delete the API reference of this notification message!");

                _API = value;

            }

        }

        #endregion

        /// <summary>
        /// The timestamp of the notification message.
        /// </summary>
        [Mandatory]
        public DateTime                      Timestamp       { get; }

        /// <summary>
        /// The message type of the notification message.
        /// </summary>
        [Mandatory]
        public NotificationMessageType       Type            { get; }

        /// <summary>
        /// The data of the notification message.
        /// </summary>
        [Mandatory]
        public JObject                       Data            { get; }

        /// <summary>
        /// The owners of the notification message.
        /// </summary>
        [Mandatory]
        public IEnumerable<Organization_Id>  Owners          { get; }

        /// <summary>
        /// Optional cryptographic signatures of the notification message.
        /// </summary>
        [Mandatory]
        public IEnumerable<Signature>        Signatures      { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new notification message.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the notification message.</param>
        /// <param name="Type">The message type of the notification message.</param>
        /// <param name="Data">The data of the notification message.</param>
        /// <param name="Owners">The owners of the notification message.</param>
        /// <param name="Signatures">Optional cryptographic signatures of the notification message.</param>
        public NotificationMessage(DateTime                      Timestamp,
                                   NotificationMessageType       Type,
                                   JObject                       Data,
                                   IEnumerable<Organization_Id>  Owners,
                                   IEnumerable<Signature>?      Signatures    = null)

            : this(NotificationMessage_Id.Random(),
                   Timestamp,
                   Type,
                   Data,
                   Owners,
                   Signatures)

        { }


        /// <summary>
        /// Create a new notification message.
        /// </summary>
        /// <param name="Id">The unique identification of the notification message.</param>
        /// <param name="Timestamp">The timestamp of the notification message.</param>
        /// <param name="Type">The message type of the notification message.</param>
        /// <param name="Data">The data of the notification message.</param>
        /// <param name="Owners">The owners of the notification message.</param>
        /// <param name="Signatures">Optional cryptographic signatures of the notification message.</param>
        public NotificationMessage(NotificationMessage_Id        Id,
                                   DateTime                      Timestamp,
                                   NotificationMessageType       Type,
                                   JObject                       Data,
                                   IEnumerable<Organization_Id>  Owners,
                                   IEnumerable<Signature>?       Signatures   = null,

                                   JObject?                      CustomData   = default,
                                   String?                       DataSource   = default,
                                   DateTime?                     LastChange   = default)

            : base(Id,
                   DefaultJSONLDContext,
                   null,
                   null,
                   Signatures,
                   CustomData,
                   null,
                   LastChange,
                   DataSource)

        {

            this.Timestamp     = Timestamp;
            this.Type          = Type;
            this.Data          = Data;
            this.Owners        = Owners == null ? new Organization_Id[0] : Owners;
            this.Signatures    = Signatures    ?? new Signature[0];

        }

        #endregion


        #region ToJSON(IncludeCryptoHash = true)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        public override JObject ToJSON(Boolean Embedded = true)

            => JSONObject.Create(

                   new JProperty("@id",        Id.ToString()),

                   !Embedded
                       ? new JProperty("@context",   JSONLDContext.ToString())
                       : null,

                   new JProperty("timestamp",  Timestamp.ToIso8601()),
                   new JProperty("type",       Type.ToString()),
                   new JProperty("data",       Data),
                   new JProperty("ownerIds",   new JArray(Owners.Select(orgId => orgId.ToString()))),

                   Signatures.SafeAny()
                       ? new JProperty("signatures",  new JArray(Signatures))
                       : null

               );

        #endregion

        #region (static) TryParseJSON(JSONObject, ..., out NotificationMessage, out ErrorResponse)

        public static Boolean TryParseJSON(JObject                              JSONObject,
                                           Func<Organization_Id, Organization>  OrganizationProvider,
                                           out NotificationMessage?             NotificationMessage,
                                           out String?                          ErrorResponse,
                                           NotificationMessage_Id?              NotificationMessageIdURL = null)
        {

            if (OrganizationProvider == null)
            {
                NotificationMessage = null;
                ErrorResponse       = "The given owner/organization provider must not be null!";
                return false;
            }

            try
            {

                NotificationMessage = null;

                #region Parse NotificationMessageId   [optional]

                // Verify that a given NotificationMessage identification
                //   is at least valid.
                if (JSONObject.ParseOptional("@id",
                                             "NotificationMessage identification",
                                             NotificationMessage_Id.TryParse,
                                             out NotificationMessage_Id? NotificationMessageIdBody,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                if (!NotificationMessageIdURL.HasValue && !NotificationMessageIdBody.HasValue)
                {
                    ErrorResponse = "The NotificationMessage identification is missing!";
                    return false;
                }

                if (NotificationMessageIdURL.HasValue && NotificationMessageIdBody.HasValue && NotificationMessageIdURL.Value != NotificationMessageIdBody.Value)
                {
                    ErrorResponse = "The optional NotificationMessage identification given within the JSON body does not match the one given in the URI!";
                    return false;
                }

                #endregion

                #region Parse Context                 [mandatory]

                if (!JSONObject.ParseMandatory("@context",
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

                #region Parse Timestamp               [mandatory]

                if (!JSONObject.ParseMandatory("timestamp",
                                               "timestamp",
                                               out DateTime Timestamp,
                                               out ErrorResponse))
                {
                     return false;
                }

                #endregion

                #region Parse Type                    [mandatory]

                if (!JSONObject.ParseMandatory("type",
                                               "notification message type",
                                               NotificationMessageType.TryParse,
                                               out NotificationMessageType Type,
                                               out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse Data                    [mandatory]

                if (!JSONObject.ParseMandatory("name",
                                               "NotificationMessagename",
                                               out JObject Data,
                                               out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse Owners                  [mandatory]

                if (!JSONObject.ParseMandatory("ownerIds",
                                               "owner identifications",
                                               out JArray OwnersJSON,
                                               out ErrorResponse))
                {
                    return false;
                }

                var OwnerIds = new List<Organization_Id>();

                foreach (var ownerJSON in OwnersJSON)
                {

                    if (!Organization_Id.TryParse(ownerJSON.Value<String>(), out Organization_Id OwnerId))
                    {
                        ErrorResponse = "Invalid owner identification '" + OwnerId + "'!";
                        return false;
                    }

                    OwnerIds.Add(OwnerId);

                }

                if (OwnerIds.Count == 0)
                {
                    ErrorResponse = "Invalid owner identifications!";
                    return false;
                }

                #endregion

                #region Parse Signatures              [optional]

                if (JSONObject.ParseOptional("signatures",
                                             "Signatures",
                                             out JArray Signatures,
                                             out ErrorResponse))
                {

                    if (ErrorResponse is not null)
                        return false;

                }

                #endregion

                #region Parse CryptoHash              [optional]

                var CryptoHash    = JSONObject.GetOptional("cryptoHash");

                #endregion


                NotificationMessage = new NotificationMessage(NotificationMessageIdBody ?? NotificationMessageIdURL.Value,
                                                              Timestamp,
                                                              Type,
                                                              Data,
                                                              OwnerIds,
                                                              null);

                ErrorResponse = null;
                return true;

            }
            catch (Exception e)
            {
                ErrorResponse  = e.Message;
                NotificationMessage  = null;
                return false;
            }

        }

        #endregion


        #region CopyAllLinkedDataFrom(OldNotificationMessage)

        public override void CopyAllLinkedDataFromBase(NotificationMessage OldNotificationMessage)
        {

            //if (_User2Organization_InEdges.Any() && !NewGroup._User2Organization_InEdges.Any())
            //{

            //    NewGroup.Add(_User2Organization_InEdges);

            //    foreach (var edge in NewGroup._User2Organization_InEdges)
            //        edge.Target = NewGroup;

            //}

            //if (_Organization2Organization_InEdges.Any() && !NewGroup._Organization2Organization_InEdges.Any())
            //{

            //    NewGroup.AddInEdges(_Organization2Organization_InEdges);

            //    foreach (var edge in NewGroup._Organization2Organization_InEdges)
            //        edge.Target = NewGroup;

            //}

            //if (_Organization2Organization_OutEdges.Any() && !NewGroup._Organization2Organization_OutEdges.Any())
            //{

            //    NewGroup.AddOutEdges(_Organization2Organization_OutEdges);

            //    foreach (var edge in NewGroup._Organization2Organization_OutEdges)
            //        edge.Source = NewGroup;

            //}

        }

        #endregion


        #region Operator overloading

        #region Operator == (NotificationMessageId1, NotificationMessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageId1">A NotificationMessage identification.</param>
        /// <param name="NotificationMessageId2">Another NotificationMessage identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (NotificationMessage NotificationMessageId1, NotificationMessage NotificationMessageId2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(NotificationMessageId1, NotificationMessageId2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) NotificationMessageId1 == null) || ((Object) NotificationMessageId2 == null))
                return false;

            return NotificationMessageId1.Equals(NotificationMessageId2);

        }

        #endregion

        #region Operator != (NotificationMessageId1, NotificationMessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageId1">A NotificationMessage identification.</param>
        /// <param name="NotificationMessageId2">Another NotificationMessage identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (NotificationMessage NotificationMessageId1, NotificationMessage NotificationMessageId2)
            => !(NotificationMessageId1 == NotificationMessageId2);

        #endregion

        #region Operator <  (NotificationMessageId1, NotificationMessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageId1">A NotificationMessage identification.</param>
        /// <param name="NotificationMessageId2">Another NotificationMessage identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (NotificationMessage NotificationMessageId1, NotificationMessage NotificationMessageId2)
        {

            if ((Object) NotificationMessageId1 == null)
                throw new ArgumentNullException(nameof(NotificationMessageId1), "The given NotificationMessageId1 must not be null!");

            return NotificationMessageId1.CompareTo(NotificationMessageId2) < 0;

        }

        #endregion

        #region Operator <= (NotificationMessageId1, NotificationMessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageId1">A NotificationMessage identification.</param>
        /// <param name="NotificationMessageId2">Another NotificationMessage identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (NotificationMessage NotificationMessageId1, NotificationMessage NotificationMessageId2)
            => !(NotificationMessageId1 > NotificationMessageId2);

        #endregion

        #region Operator >  (NotificationMessageId1, NotificationMessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageId1">A NotificationMessage identification.</param>
        /// <param name="NotificationMessageId2">Another NotificationMessage identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (NotificationMessage NotificationMessageId1, NotificationMessage NotificationMessageId2)
        {

            if ((Object) NotificationMessageId1 == null)
                throw new ArgumentNullException(nameof(NotificationMessageId1), "The given NotificationMessageId1 must not be null!");

            return NotificationMessageId1.CompareTo(NotificationMessageId2) > 0;

        }

        #endregion

        #region Operator >= (NotificationMessageId1, NotificationMessageId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessageId1">A NotificationMessage identification.</param>
        /// <param name="NotificationMessageId2">Another NotificationMessage identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (NotificationMessage NotificationMessageId1, NotificationMessage NotificationMessageId2)
            => !(NotificationMessageId1 < NotificationMessageId2);

        #endregion

        #endregion

        #region IComparable<NotificationMessage> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public override Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

            var NotificationMessage = Object as NotificationMessage;
            if ((Object) NotificationMessage == null)
                throw new ArgumentException("The given object is not an NotificationMessage!");

            return CompareTo(NotificationMessage);

        }

        #endregion

        #region CompareTo(NotificationMessage)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="NotificationMessage">An NotificationMessage object to compare with.</param>
        public override Int32 CompareTo(NotificationMessage NotificationMessage)
        {

            if ((Object) NotificationMessage == null)
                throw new ArgumentNullException("The given NotificationMessage must not be null!");

            return Id.CompareTo(NotificationMessage.Id);

        }

        #endregion

        #endregion

        #region IEquatable<NotificationMessage> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object == null)
                return false;

            if (!(Object is NotificationMessage NotificationMessage))
                return false;

            return Equals(NotificationMessage);

        }

        #endregion

        #region Equals(NotificationMessage)

        /// <summary>
        /// Compares two NotificationMessages for equality.
        /// </summary>
        /// <param name="NotificationMessage">An NotificationMessage to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public override Boolean Equals(NotificationMessage NotificationMessage)
        {

            if ((Object) NotificationMessage == null)
                return false;

            return Id.Equals(NotificationMessage.Id);

        }

        #endregion

        #endregion

        #region (override) GetHashCode()

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


        #region ToBuilder(NewNotificationMessageId = null)

        /// <summary>
        /// Return a builder for this notification message.
        /// </summary>
        /// <param name="NewNotificationMessageId">An optional new notification message identification.</param>
        public Builder ToBuilder(NotificationMessage_Id? NewNotificationMessageId = null)

            => new Builder(NewNotificationMessageId ?? Id,
                           Timestamp,
                           Type,
                           Data,
                           Owners,
                           Signatures,

                           CustomData,
                           DataSource,
                           LastChangeDate);

        #endregion

        #region (class) Builder

        /// <summary>
        /// A notification message builder.
        /// </summary>
        public new class Builder : AEntity<NotificationMessage_Id,
                                           NotificationMessage>.Builder
        {

            #region Properties

            /// <summary>
            /// The timestamp of the notification message.
            /// </summary>
            [Mandatory]
            public DateTime?                     Timestamp       { get; set; }

            /// <summary>
            /// The message type of the notification message.
            /// </summary>
            [Mandatory]
            public NotificationMessageType?      Type            { get; set; }

            /// <summary>
            /// The data of the notification message.
            /// </summary>
            [Mandatory]
            public JObject                       Data            { get; set; }

            /// <summary>
            /// The owners of the notification message.
            /// </summary>
            [Mandatory]
            public HashSet<Organization_Id>      Owners          { get; }

            /// <summary>
            /// Optional cryptographic signatures of the notification message.
            /// </summary>
            [Mandatory]
            public IEnumerable<Signature>        Signatures      { get; }

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new News builder.
            /// </summary>
            public Builder(NotificationMessage_Id?        Id                = null,
                           DateTime?                      Timestamp         = null,
                           NotificationMessageType?       Type              = null,
                           JObject?                       Data              = null,
                           IEnumerable<Organization_Id>?  Owners            = null,
                           IEnumerable<Signature>?        Signatures        = null,

                           JObject?                       CustomData        = default,
                           String?                        DataSource        = default,
                           DateTime?                      LastChange        = default)

                : base(Id ?? NotificationMessage_Id.Random(),
                       DefaultJSONLDContext,
                       LastChange,
                       null,
                       CustomData,
                       null,
                       DataSource)

            {

                this.Timestamp     = Timestamp;
                this.Type          = Type;
                this.Data          = Data;
                this.Owners        = Owners     != null ? new HashSet<Organization_Id>(Owners)     : new HashSet<Organization_Id>();
                this.Signatures    = Signatures != null ? new HashSet<Signature>      (Signatures) : new HashSet<Signature>();

            }

            #endregion


            //public NotificationMessage Sign(ICipherParameters PrivateKey)
            //{

            //    var news        = new NotificationMessage(Id,
            //                                      Headline,
            //                                      Text,
            //                                      Author,
            //                                      PublicationDate,
            //                                      Tags,
            //                                      IsHidden,
            //                                      Signatures);

            //    var ctext       = news.ToJSON(Embedded:           false,
            //                                  ExpandTags:         InfoStatus.ShowIdOnly,
            //                                  IncludeCryptoHash:  false).ToString(Newtonsoft.Json.Formatting.None);

            //    var BlockSize   = 32;

            //    var SHA256      = new SHA256Managed();
            //    var SHA256Hash  = SHA256.ComputeHash(ctext.ToUTF8Bytes());
            //    var signer      = SignerUtilities.GetSigner("NONEwithECDSA");
            //    signer.Init(true, PrivateKey);
            //    signer.BlockUpdate(SHA256Hash, 0, BlockSize);

            //    var signature   = signer.GenerateSignature().ToHexString();
            //    var signatures  = new List<Signature>(Signatures);
            //    signatures.Add(new Signature("json", "secp256k1", "DER+HEX", signature));

            //    return new NotificationMessage(Id,
            //                           Headline,
            //                           Text,
            //                           Author,
            //                           PublicationDate,
            //                           Tags,
            //                           IsHidden,
            //                           signatures);

            //}


            public override void CopyAllLinkedDataFromBase(NotificationMessage OldEnity)
            {
            }

            public override int CompareTo(object obj)
            {
                return 0;
            }

            public override bool Equals(NotificationMessage? other)
            {
                throw new NotImplementedException();
            }

            public override int CompareTo(NotificationMessage? other)
            {
                throw new NotImplementedException();
            }

            #region ToImmutable

            /// <summary>
            /// Return an immutable version of the News.
            /// </summary>
            /// <param name="Builder">A News builder.</param>
            public static implicit operator NotificationMessage(Builder Builder)

                => Builder?.ToImmutable;


            /// <summary>
            /// Return an immutable version of the News.
            /// </summary>
            public NotificationMessage ToImmutable

                => new NotificationMessage(Id,
                                           Timestamp ?? org.GraphDefined.Vanaheimr.Illias.Timestamp.Now,
                                           Type      ?? NotificationMessageType.Parse("default"),
                                           Data,
                                           Owners,
                                           Signatures,

                                           CustomData,
                                           DataSource,
                                           LastChangeDate);

            #endregion

        }

        #endregion

    }

}
