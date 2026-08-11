/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.Entities
{

    /// <summary>
    /// Organization &lt;-&gt; Organization edge removal tests.
    ///
    /// Organization.Builder.RemoveInEdges(...) once filtered the OUT edges but removed
    /// the matches from the IN edges, so it never removed anything. These tests pin the
    /// expected behaviour of all four Remove*Edges variants on both the immutable
    /// organization and its builder.
    /// </summary>
    [TestFixture]
    public class OrganizationEdgeTests
    {

        #region Builder_RemoveInEdges_RemovesMatchingInEdges()

        /// <summary>
        /// Removing in-edges via the builder must remove the matching in-edges.
        /// </summary>
        [Test]
        public void Builder_RemoveInEdges_RemovesMatchingInEdges()
        {

            var sourceOrganization  = new Organization(Organization_Id.Parse("sourceOrg"));
            var builder             = new Organization.Builder(Organization_Id.Parse("mainOrg"));

            builder.AddInEdge(Organization2OrganizationEdgeLabel.IsChildOf, sourceOrganization);

            Assert.That(builder.Organization2OrganizationInEdges.Count(), Is.EqualTo(1));

            builder.RemoveInEdges(Organization2OrganizationEdgeLabel.IsChildOf, sourceOrganization);

            Assert.That(builder.Organization2OrganizationInEdges, Is.Empty);

        }

        #endregion

        #region Builder_RemoveInEdges_KeepsNonMatchingInEdges()

        /// <summary>
        /// Removing in-edges via the builder must only remove edges matching
        /// both the edge label and the source organization.
        /// </summary>
        [Test]
        public void Builder_RemoveInEdges_KeepsNonMatchingInEdges()
        {

            var sourceOrganization1  = new Organization(Organization_Id.Parse("sourceOrg1"));
            var sourceOrganization2  = new Organization(Organization_Id.Parse("sourceOrg2"));
            var builder              = new Organization.Builder(Organization_Id.Parse("mainOrg"));

            builder.AddInEdge(Organization2OrganizationEdgeLabel.IsChildOf,   sourceOrganization1);
            builder.AddInEdge(Organization2OrganizationEdgeLabel.IsSubsidary, sourceOrganization1);
            builder.AddInEdge(Organization2OrganizationEdgeLabel.IsChildOf,   sourceOrganization2);

            builder.RemoveInEdges(Organization2OrganizationEdgeLabel.IsChildOf, sourceOrganization1);

            Assert.That(builder.Organization2OrganizationInEdges.Count(), Is.EqualTo(2));
            Assert.That(builder.Organization2OrganizationInEdges.Any(edge => edge.EdgeLabel == Organization2OrganizationEdgeLabel.IsSubsidary &&
                                                                             ReferenceEquals(edge.Source, sourceOrganization1)),
                        Is.True);
            Assert.That(builder.Organization2OrganizationInEdges.Any(edge => edge.EdgeLabel == Organization2OrganizationEdgeLabel.IsChildOf &&
                                                                             ReferenceEquals(edge.Source, sourceOrganization2)),
                        Is.True);

        }

        #endregion

        #region Builder_RemoveInEdges_DoesNotTouchOutEdges()

        /// <summary>
        /// Removing in-edges via the builder must not remove any out-edges,
        /// even when they carry the same edge label and organization.
        /// </summary>
        [Test]
        public void Builder_RemoveInEdges_DoesNotTouchOutEdges()
        {

            var otherOrganization  = new Organization(Organization_Id.Parse("otherOrg"));
            var builder            = new Organization.Builder(Organization_Id.Parse("mainOrg"));

            builder.AddInEdge (Organization2OrganizationEdgeLabel.IsChildOf, otherOrganization);
            builder.AddOutEdge(Organization2OrganizationEdgeLabel.IsChildOf, otherOrganization);

            builder.RemoveInEdges(Organization2OrganizationEdgeLabel.IsChildOf, otherOrganization);

            Assert.That(builder.Organization2OrganizationInEdges,          Is.Empty);
            Assert.That(builder.Organization2OrganizationOutEdges.Count(), Is.EqualTo(1));

        }

        #endregion

        #region Builder_RemoveOutEdges_RemovesMatchingOutEdges()

        /// <summary>
        /// Removing out-edges via the builder must remove the matching out-edges.
        /// </summary>
        [Test]
        public void Builder_RemoveOutEdges_RemovesMatchingOutEdges()
        {

            var targetOrganization  = new Organization(Organization_Id.Parse("targetOrg"));
            var builder             = new Organization.Builder(Organization_Id.Parse("mainOrg"));

            builder.AddOutEdge(Organization2OrganizationEdgeLabel.IsChildOf, targetOrganization);

            Assert.That(builder.Organization2OrganizationOutEdges.Count(), Is.EqualTo(1));

            builder.RemoveOutEdges(Organization2OrganizationEdgeLabel.IsChildOf, targetOrganization);

            Assert.That(builder.Organization2OrganizationOutEdges, Is.Empty);

        }

        #endregion


        #region Organization_RemoveInEdges_RemovesMatchingInEdges()

        /// <summary>
        /// Removing in-edges on the immutable organization must remove the matching in-edges.
        /// </summary>
        [Test]
        public void Organization_RemoveInEdges_RemovesMatchingInEdges()
        {

            var sourceOrganization  = new Organization(Organization_Id.Parse("sourceOrg"));
            var organization        = new Organization(Organization_Id.Parse("mainOrg"));

            organization.AddInEdge(Organization2OrganizationEdgeLabel.IsChildOf, sourceOrganization);

            Assert.That(organization.Organization2OrganizationInEdges.Count(), Is.EqualTo(1));

            organization.RemoveInEdges(Organization2OrganizationEdgeLabel.IsChildOf, sourceOrganization);

            Assert.That(organization.Organization2OrganizationInEdges, Is.Empty);

        }

        #endregion

        #region Organization_RemoveOutEdges_RemovesMatchingOutEdges()

        /// <summary>
        /// Removing out-edges on the immutable organization must remove the matching out-edges.
        /// </summary>
        [Test]
        public void Organization_RemoveOutEdges_RemovesMatchingOutEdges()
        {

            var targetOrganization  = new Organization(Organization_Id.Parse("targetOrg"));
            var organization        = new Organization(Organization_Id.Parse("mainOrg"));

            organization.AddOutEdge(Organization2OrganizationEdgeLabel.IsChildOf, targetOrganization);

            Assert.That(organization.Organization2OrganizationOutEdges.Count(), Is.EqualTo(1));

            organization.RemoveOutEdges(Organization2OrganizationEdgeLabel.IsChildOf, targetOrganization);

            Assert.That(organization.Organization2OrganizationOutEdges, Is.Empty);

        }

        #endregion

    }

}
