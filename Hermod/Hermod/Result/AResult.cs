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
using System.Collections;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public enum AddedOrIgnored
    {
        Ignored,
        Added
    }

    public enum AddedOrUpdated
    {
        NoOperation,
        Add,
        Update,
        Enqueued,
        Failed
    }


    /// <summary>
    /// An abstract result.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    public abstract class AResult<T>
    {

        #region Properties

        /// <summary>
        /// The unqiue identification of the authenticator.
        /// </summary>
        public    IId?                  AuthId              { get; }

        public    Object                Sender              { get; }

        /// <summary>
        /// The object of the operation.
        /// </summary>
        protected T?                    Object              { get; }

        /// <summary>
        /// The unique event tracking identification for correlating this request with other events.
        /// </summary>
        public    EventTracking_Id      EventTrackingId     { get; }

        /// <summary>
        /// Whether the operation was successful, or not.
        /// </summary>
        public    Boolean               IsSuccess           { get; }

        /// <summary>
        /// Whether the operation failed, or not.
        /// </summary>
        public    Boolean               IsFailed
            => !IsSuccess;

        public    String?               Argument            { get; }

        public    I18NString            Description         { get; }

        /// <summary>
        /// Warnings or additional information.
        /// </summary>
        public    IEnumerable<Warning>  Warnings            { get; }

        /// <summary>
        /// The runtime of the request.
        /// </summary>
        public    TimeSpan              Runtime             { get;  }








        public PushDataResultTypes  Result         { get; }
        public Object?              SendPOIData    { get; }



        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract result.
        /// </summary>
        /// <param name="Object">The object of the operation.</param>
        /// <param name="EventTrackingId">The unique event tracking identification for correlating this request with other events.</param>
        /// <param name="IsSuccess">Whether the operation was successful, or not.</param>
        /// <param name="Argument"></param>
        /// <param name="Description"></param>
        public AResult(T?                Object,
                       EventTracking_Id  EventTrackingId,
                       Boolean           IsSuccess,
                       String?           Argument      = null,
                       I18NString?       Description   = null)
        {

            this.Object            = Object;
            this.EventTrackingId   = EventTrackingId;
            this.IsSuccess         = IsSuccess;
            this.Argument          = Argument;
            this.Description       = Description ?? I18NString.Empty;

        }

        /// <summary>
        /// Create a new abstract result.
        /// </summary>
        /// <param name="Object">The object of the operation.</param>
        /// <param name="EventTrackingId">The unique event tracking identification for correlating this request with other events.</param>
        /// <param name="IsSuccess">Whether the operation was successful, or not.</param>
        /// <param name="Argument"></param>
        /// <param name="Description"></param>
        public AResult(T                      Entity,
                       PushDataResultTypes    Result,
                       EventTracking_Id?      EventTrackingId   = null,
                       IId?                   AuthId            = null,
                       Object?                SendPOIData       = null,
                       I18NString?            Description       = null,
                       IEnumerable<Warning>?  Warnings          = null,
                       TimeSpan?              Runtime           = null)
        {

            this.Object            = Entity;
            this.Result            = Result;
            this.EventTrackingId   = EventTrackingId ?? EventTracking_Id.New;
            this.AuthId            = AuthId;
            this.SendPOIData       = SendPOIData;
            this.Description       = Description     ?? I18NString.Empty;
            this.Warnings          = Warnings        ?? Array.Empty<Warning>();
            this.Runtime           = Runtime         ?? TimeSpan.Zero;

            //this.IsSuccess         = IsSuccess;
            //this.Argument          = Argument;

        }

        #endregion



        public JObject ToJSON()

            => JSONObject.Create(
                   Description is not null
                       ? Description.Count == 1
                             ? new JProperty("description",  Description.FirstText())
                             : new JProperty("description",  Description.ToJSON())
                       : null
               );


        public override String ToString()

            => IsSuccess
                    ? "Success"
                    : "Failed" + (Description is not null && Description.IsNullOrEmpty()
                                      ? ": " + Description.FirstText()
                                      : "!");

    }

    /// <summary>
    /// An abstract result.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TId">The type of the entity identification.</typeparam>
    public abstract class AEnitityResult<TEntity, TId> : AResult<TEntity>

        where TEntity : class, IHasId<TId>
        where TId     : IId

    {

        #region Properties

        /// <summary>
        /// The unique identification of the entity.
        /// </summary>
        public TId?  Identification    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract result.
        /// </summary>
        /// <param name="Entity">The entity of the operation.</param>
        /// <param name="EventTrackingId">The unique event tracking identification for correlating this request with other events.</param>
        /// <param name="IsSuccess">Whether the operation was successful, or not.</param>
        /// <param name="Argument"></param>
        /// <param name="ErrorDescription"></param>
        public AEnitityResult(TEntity?          Entity,
                              EventTracking_Id  EventTrackingId,
                              Boolean           IsSuccess,
                              String?           Argument           = null,
                              I18NString?       ErrorDescription   = null)

            : base(Entity,
                   EventTrackingId,
                   IsSuccess,
                   Argument,
                   ErrorDescription)

        {

            this.Identification = Entity is not null
                                      ? Entity.Id
                                      : default;

        }




        /// <summary>
        /// Create a new abstract result.
        /// </summary>
        /// <param name="Entity">The entity of the operation.</param>
        /// <param name="EventTrackingId">The unique event tracking identification for correlating this request with other events.</param>
        /// <param name="IsSuccess">Whether the operation was successful, or not.</param>
        /// <param name="Argument"></param>
        /// <param name="ErrorDescription"></param>
        public AEnitityResult(TEntity                Entity,
                              PushDataResultTypes    Result,
                              EventTracking_Id?      EventTrackingId   = null,
                              IId?                   AuthId            = null,
                              Object?                SendPOIData       = null,
                              I18NString?            Description       = null,
                              IEnumerable<Warning>?  Warnings          = null,
                              TimeSpan?              Runtime           = null)

            : base(Entity,
                   Result,
                   EventTrackingId,
                   AuthId,
                   SendPOIData,
                   Description,
                   Warnings,
                   Runtime)

        {

            this.Identification = Entity is not null
                                      ? Entity.Id
                                      : default;

        }

        /// <summary>
        /// Create a new abstract result.
        /// </summary>
        /// <param name="Entity">The entity of the operation.</param>
        /// <param name="EventTrackingId">The unique event tracking identification for correlating this request with other events.</param>
        /// <param name="IsSuccess">Whether the operation was successful, or not.</param>
        /// <param name="Argument"></param>
        /// <param name="ErrorDescription"></param>
        public AEnitityResult(TId                    EntityId,
                              PushDataResultTypes    Result,
                              EventTracking_Id?      EventTrackingId   = null,
                              IId?                   AuthId            = null,
                              Object?                SendPOIData       = null,
                              I18NString?            Description       = null,
                              IEnumerable<Warning>?  Warnings          = null,
                              TimeSpan?              Runtime           = null)

            : base(default,
                   Result,
                   EventTrackingId,
                   AuthId,
                   SendPOIData,
                   Description,
                   Warnings,
                   Runtime)

        {

            this.Identification = EntityId;

        }


        /// <summary>
        /// Create a new abstract result.
        /// </summary>
        /// <param name="EntityId">The object of the operation.</param>
        /// <param name="EventTrackingId">The unique event tracking identification for correlating this request with other events.</param>
        /// <param name="IsSuccess">Whether the operation was successful, or not.</param>
        /// <param name="Argument"></param>
        /// <param name="ErrorDescription"></param>
        public AEnitityResult(TId               EntityId,
                              EventTracking_Id  EventTrackingId,
                              Boolean           IsSuccess,
                              String?           Argument           = null,
                              I18NString?       ErrorDescription   = null)

            : base(default,
                   EventTrackingId,
                   IsSuccess,
                   Argument,
                   ErrorDescription)

        {

            this.Identification = EntityId;

        }

        #endregion

    }

    public enum PushDataResultTypes
    {

        /// <summary>
        /// The result is unknown and/or should be ignored.
        /// </summary>
        Unspecified,

        /// <summary>
        /// The service was disabled by the administrator.
        /// </summary>
        AdminDown,

        /// <summary>
        /// No operation e.g. because no EVSE data passed the EVSE filter.
        /// </summary>
        NoOperation,

        /// <summary>
        /// The data has been enqueued for later transmission.
        /// </summary>
        Enqueued,

        /// <summary>
        /// Success.
        /// </summary>
        Success,

        /// <summary>
        /// Exists already.
        /// </summary>
        Exists,

        /// <summary>
        /// Out-Of-Service.
        /// </summary>
        OutOfService,

        /// <summary>
        /// A lock timeout occured.
        /// </summary>
        LockTimeout,

        /// <summary>
        /// A timeout occured.
        /// </summary>
        Timeout,

        /// <summary>
        /// The operation led to an error.
        /// </summary>
        Error,

        ArgumentError,

        CanNotBeRemoved

    }


    /// <summary>
    /// An abstract result.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TId">The type of the entity identification.</typeparam>
    public abstract class AEnititiesResult<TResult, TEntity, TId>

        where TResult : AEnitityResult<TEntity, TId>
        where TEntity : class, IHasId<TId>
        where TId     : IId

    {

        #region Properties

        public PushDataResultTypes   Result             { get; }
        public IEnumerable<TResult>  SuccessfulItems    { get; }
        public IEnumerable<TResult>  RejectedItems      { get; }

        public IId?                  AuthId             { get; }
        public Object?               SendPOIData        { get; }
        public EventTracking_Id      EventTrackingId    { get; }
        public I18NString            Description        { get; }
        public IEnumerable<Warning>  Warnings           { get; }
        public TimeSpan              Runtime            { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract result.
        /// </summary>
        /// <param name="Entity">The entity of the operation.</param>
        /// <param name="EventTrackingId">The unique event tracking identification for correlating this request with other events.</param>
        /// <param name="IsSuccess">Whether the operation was successful, or not.</param>
        /// <param name="Argument"></param>
        /// <param name="ErrorDescription"></param>
        public AEnititiesResult(PushDataResultTypes    Result,
                                IEnumerable<TResult>?  SuccessfulEVSEs   = null,
                                IEnumerable<TResult>?  RejectedEVSEs     = null,
                                IId?                   AuthId            = null,
                                Object?                SendPOIData       = null,
                                EventTracking_Id?      EventTrackingId   = null,
                                I18NString?            Description       = null,
                                IEnumerable<Warning>?  Warnings          = null,
                                TimeSpan?              Runtime           = null)
        {

            this.Result           = Result;
            this.SuccessfulItems  = SuccessfulEVSEs ?? Array.Empty<TResult>();
            this.RejectedItems    = RejectedEVSEs   ?? Array.Empty<TResult>();
            this.AuthId           = AuthId;
            this.SendPOIData      = SendPOIData;
            this.EventTrackingId  = EventTrackingId ?? EventTracking_Id.New;
            this.Description      = Description     ?? I18NString.Empty;
            this.Warnings         = Warnings        ?? Array.Empty<Warning>();
            this.Runtime          = Runtime         ?? TimeSpan.Zero;

        }

        #endregion


    }



    /// <summary>
    /// An abstract result.
    /// </summary>
    /// <typeparam name="T1">The type of the result.</typeparam>
    /// <typeparam name="T2">The type of the result.</typeparam>
    public abstract class AResult<T1, T2>
    {

        #region Properties

        /// <summary>
        /// The object of the operation.
        /// </summary>
        protected T1                Object1             { get; }

        /// <summary>
        /// The object of the operation.
        /// </summary>
        protected T2                Object2             { get; }

        /// <summary>
        /// The unique event tracking identification for correlating this request with other events.
        /// </summary>
        public    EventTracking_Id  EventTrackingId     { get; }

        /// <summary>
        /// Whether the operation was successful, or not.
        /// </summary>
        public    Boolean           IsSuccess           { get; }

        public    String?           Argument            { get; }

        public    I18NString?       ErrorDescription    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new abstract result.
        /// </summary>
        /// <param name="Object1">The object of the operation.</param>
        /// <param name="Object2">The object of the operation.</param>
        /// <param name="EventTrackingId">The unique event tracking identification for correlating this request with other events.</param>
        /// <param name="IsSuccess">Whether the operation was successful, or not.</param>
        /// <param name="Argument"></param>
        /// <param name="ErrorDescription"></param>
        public AResult(T1                Object1,
                       T2                Object2,
                       EventTracking_Id  EventTrackingId,
                       Boolean           IsSuccess,
                       String?           Argument           = null,
                       I18NString?       ErrorDescription   = null)
        {

            this.Object1           = Object1;
            this.Object2           = Object2;
            this.EventTrackingId   = EventTrackingId;
            this.IsSuccess         = IsSuccess;
            this.Argument          = Argument;
            this.ErrorDescription  = ErrorDescription;

        }

        #endregion



        public JObject ToJSON()

            => JSONObject.Create(
                   ErrorDescription is not null
                       ? ErrorDescription.Count == 1
                             ? new JProperty("description",  ErrorDescription.FirstText())
                             : new JProperty("description",  ErrorDescription.ToJSON())
                       : null
               );


        public override String ToString()

            => IsSuccess
                    ? "Success"
                    : "Failed" + (ErrorDescription is not null && ErrorDescription.IsNullOrEmpty()
                                      ? ": " + ErrorDescription.FirstText()
                                      : "!");

    }

}
