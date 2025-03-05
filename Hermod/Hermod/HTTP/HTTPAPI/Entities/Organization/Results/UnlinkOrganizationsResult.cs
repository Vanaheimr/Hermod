/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of OrganizationOutsAPI <https://www.github.com/Vanaheimr/OrganizationOutsAPI>
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
using org.GraphDefined.Vanaheimr.Hermod;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public class UnlinkOrganizationsResult : AResult<IOrganization, IOrganization>
    {

        public IOrganization                        OrganizationOut
            => Object1;

        public Organization2OrganizationEdgeLabel  EdgeLabel       { get; }

        public IOrganization                        OrganizationIn
            => Object2;


        public UnlinkOrganizationsResult(IOrganization                        OrganizationOut,
                                         Organization2OrganizationEdgeLabel  EdgeLabel,
                                         IOrganization                        OrganizationIn,
                                         EventTracking_Id                    EventTrackingId,
                                         Boolean                             IsSuccess,
                                         String                              Argument           = null,
                                         I18NString                          ErrorDescription   = null)

            : base(OrganizationOut,
                   OrganizationIn,
                   EventTrackingId,
                   IsSuccess,
                   Argument,
                   ErrorDescription)

        {

            this.EdgeLabel = EdgeLabel;

        }


        public static UnlinkOrganizationsResult Success(IOrganization                        OrganizationOut,
                                                        Organization2OrganizationEdgeLabel  EdgeLabel,
                                                        IOrganization                        OrganizationIn,
                                                        EventTracking_Id                    EventTrackingId)

            => new UnlinkOrganizationsResult(OrganizationOut,
                                             EdgeLabel,
                                             OrganizationIn,
                                             EventTrackingId,
                                             true);


        public static UnlinkOrganizationsResult ArgumentError(IOrganization                        OrganizationOut,
                                                              Organization2OrganizationEdgeLabel  EdgeLabel,
                                                              IOrganization                        OrganizationIn,
                                                              EventTracking_Id                    EventTrackingId,
                                                              String                              Argument,
                                                              String                              Description)

            => new UnlinkOrganizationsResult(OrganizationOut,
                                             EdgeLabel,
                                             OrganizationIn,
                                             EventTrackingId,
                                             false,
                                             Argument,
                                             I18NString.Create(
                                                               Description));

        public static UnlinkOrganizationsResult ArgumentError(IOrganization                        OrganizationOut,
                                                              Organization2OrganizationEdgeLabel  EdgeLabel,
                                                              IOrganization                        OrganizationIn,
                                                              EventTracking_Id                    EventTrackingId,
                                                              String                              Argument,
                                                              I18NString                          Description)

            => new UnlinkOrganizationsResult(OrganizationOut,
                                             EdgeLabel,
                                             OrganizationIn,
                                             EventTrackingId,
                                             false,
                                             Argument,
                                             Description);


        public static UnlinkOrganizationsResult Failed(IOrganization                        OrganizationOut,
                                                       Organization2OrganizationEdgeLabel  EdgeLabel,
                                                       IOrganization                        OrganizationIn,
                                                       EventTracking_Id                    EventTrackingId,
                                                       String                              Description)

            => new UnlinkOrganizationsResult(OrganizationOut,
                                             EdgeLabel,
                                             OrganizationIn,
                                             EventTrackingId,
                                             false,
                                             null,
                                             I18NString.Create(
                                                               Description));

        public static UnlinkOrganizationsResult Failed(IOrganization                        OrganizationOut,
                                                       Organization2OrganizationEdgeLabel  EdgeLabel,
                                                       IOrganization                        OrganizationIn,
                                                       EventTracking_Id                    EventTrackingId,
                                                       I18NString                          Description)

            => new UnlinkOrganizationsResult(OrganizationOut,
                                             EdgeLabel,
                                             OrganizationIn,
                                             EventTrackingId,
                                             false,
                                             null,
                                             Description);

        public static UnlinkOrganizationsResult Failed(IOrganization                        OrganizationOut,
                                                       Organization2OrganizationEdgeLabel  EdgeLabel,
                                                       IOrganization                        OrganizationIn,
                                                       EventTracking_Id                    EventTrackingId,
                                                       Exception                           Exception)

            => new UnlinkOrganizationsResult(OrganizationOut,
                                             EdgeLabel,
                                             OrganizationIn,
                                             EventTrackingId,
                                             false,
                                             null,
                                             I18NString.Create(
                                                               Exception.Message));

    }

}
