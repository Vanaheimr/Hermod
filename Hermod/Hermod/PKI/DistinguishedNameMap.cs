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

        /// <summary>
        /// Common Name - StringType(SIZE(1..64)) - OID 2.5.4.3
        /// </summary>
        public String? CN
            => TryGet("CN");

        public String? CommonName
            => TryGet("CN");

        #endregion

        #region Organization       (O)

        /// <summary>
        /// Organization - StringType(SIZE(1..64)) - OID 2.5.4.10
        /// </summary>
        public String? O
            => TryGet("O");

        /// <summary>
        /// Organization - StringType(SIZE(1..64)) - OID 2.5.4.10
        /// </summary>
        public String? Organization
            => TryGet("O");

        #endregion

        #region OrganizationalUnit (OU)

        /// <summary>
        /// organizational unit name - StringType(SIZE(1..64)) - 2.5.4.11
        /// </summary>
        public String? OU
            => TryGet("OU");

        public String? OrganizationalUnit
            => TryGet("OU");

        #endregion

        #region Country            (C)

        /// <summary>
        /// Country Code - StringType(SIZE(2)) - OID OID 2.5.4.6
        /// </summary>
        public String? C
            => TryGet("C");

        /// <summary>
        /// Country Code - StringType(SIZE(2)) - OID OID 2.5.4.6
        /// </summary>
        public String? CountryCode
            => TryGet("C");

        #endregion

        #region State              (ST/S)

        /// <summary>
        /// State, or Province - StringType(SIZE(1..64)) - 2.5.4.8
        /// </summary>
        public String? ST
            => TryGet("ST");

        /// <summary>
        /// State, or Province - StringType(SIZE(1..64)) - 2.5.4.8
        /// </summary>
        public String? State
            => TryGet("ST");

        #endregion

        #region Locality           (L)

        /// <summary>
        /// Locality - StringType(SIZE(1..64)) - OID 2.5.4.7
        /// </summary>
        public String? L
            => TryGet("L");

        /// <summary>
        /// Locality - StringType(SIZE(1..64)) - OID 2.5.4.7
        /// </summary>
        public String? Locality
            => TryGet("L");

        #endregion

        #region EmailAddress       (E)

        /// <summary>
        /// Email Address - IA5String -  OID 1.2.840.113549.1.9.1
        /// </summary>
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

        #region Title              (T)

        /// <summary>
        /// Title - ? - OID 2.5.4.12
        /// </summary>
        public String? T
            => TryGet("T");

        /// <summary>
        /// Title - ? - OID 2.5.4.12
        /// </summary>
        public String? Title
            => TryGet("T");

        #endregion

        #region Street             (Street)

        /// <summary>
        /// Street - StringType(SIZE(1..64)) - 2.5.4.9
        /// </summary>
        public String? Street
            => TryGet("Street");

        #endregion

        #region SerialNumber       (SerialNumber)

        /// <summary>
        /// Serial Number - StringType(SIZE(1..64)) - 2.5.4.5
        /// </summary>
        public String? SerialNumber
            => TryGet("SerialNumber");

        #endregion

        #region Role               (Role)

        /// <summary>
        /// Role - DirectoryString(SIZE(1..64) - OID 2.5.4.72
        /// </summary>
        public String? Role
            => TryGet("Role");

        #endregion

        #region Pseudonym          (Pseudonym)

        /// <summary>
        /// Pseudonym - DirectoryString(SIZE(1..64) - OID 2.5.4.65
        /// </summary>
        public String? Pseudonym
            => TryGet("Pseudonym");

        #endregion

        #region DateOfBirth        (DateOfBirth)

        /// <summary>
        /// DateOfBirth - GeneralizedTime - YYYYMMDD000000Z - OID 1.3.6.1.5.5.7 .9 .1
        /// </summary>
        public String? DateOfBirth
            => TryGet("DateOfBirth");

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
