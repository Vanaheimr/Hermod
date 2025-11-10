/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.PKI
{

    public static class DistinguishedNameMapExtensions
    {

        public static DistinguishedNameMap ToMap(this X500DistinguishedName DistinguishedName)
            => DistinguishedNameMap.Parse(DistinguishedName);

    }

    public class DistinguishedNameMap
    {

        #region Data

        private readonly Dictionary<String, String> attributes = new (StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Properties

        #region CommonName         (CN)

        public String? CN
            => TryGet("CN");

        public String? CommonName
            => TryGet("CN");

        #endregion

        #region Organization       (O)

        public String? O
            => TryGet("O");

        public String? Organization
            => TryGet("O");

        #endregion

        #region OrganizationalUnit (OU)

        public String? OU
            => TryGet("OU");
        public String? OrganizationalUnit
            => TryGet("OU");

        #endregion

        #region Country            (C)
        public String? C
            => TryGet("C");
        public String? Country
            => TryGet("C");

        #endregion

        #region State              (ST/S)

        public String? ST
            => TryGet("ST");

        public String? S
            => TryGet("S");

        public String? State
            => TryGet("ST");

        #endregion

        #region LocalityName       (L)

        public String? L
            => TryGet("L");

        public String? Locality
            => TryGet("L");

        #endregion

        #region EmailAddress       (E)

        public String? E
            => TryGet("E");

        public String? EmailAddress
            => TryGet("E");

        #endregion

        #region DomainComponent    (DC)

        public String? DC
            => TryGet("DC");

        public String? DomainComponent
            => TryGet("DC");

        #endregion

        #region UserId             (UID)

        public String? UID
            => TryGet("UID");

        public String? UserId
            => TryGet("UID");

        #endregion

        #region GivenName          (GN)

        public String? GN
            => TryGet("GN");

        public String? GivenName
            => TryGet("GN");

        #endregion

        #region Surname            (SN)

        public String? SN
            => TryGet("SN");

        public String? Surname
            => TryGet("SN");

        #endregion

        #endregion

        #region Constructor(s)

        private DistinguishedNameMap(Dictionary<String, String> Attributes)
        {

            foreach (var attribute in Attributes)
            {
                attributes.Add(
                    attribute.Key,
                    attribute.Value
                );
            }

        }

        #endregion


        public static DistinguishedNameMap Parse(X500DistinguishedName DistinguishedName)
        {

            var attributes = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            foreach (var rdn in DistinguishedName.EnumerateRelativeDistinguishedNames())
            {
                if (!rdn.HasMultipleElements)
                {

                    var key = rdn.GetSingleElementType().FriendlyName ??
                              rdn.GetSingleElementType().Value;

                    if (key.IsNotNullOrEmpty())
                        attributes[key] = rdn.GetSingleElementValue() ?? "";

                }
            }

            return new DistinguishedNameMap(attributes);

        }


        public String? TryGet(String Name)
        {

            if (attributes.TryGetValue(Name, out var value))
                return value;

            return null;

        }



    }

}
