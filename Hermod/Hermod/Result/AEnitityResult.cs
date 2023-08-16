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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

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
                              CommandResults    Result,
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
                              CommandResults    Result,
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

}
