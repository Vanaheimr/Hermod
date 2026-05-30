/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using Org.BouncyCastle.Math;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Parameters;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Service check keys for HTTP APIs.
    /// </summary>
    public class ServiceCheckKeys : IEquatable<ServiceCheckKeys>,
                                    IComparable<ServiceCheckKeys>,
                                    IComparable
    {

        #region Data

        /// <summary>
        /// The default elliptic curve to use for the service check.
        /// </summary>
        public const String DefaultECCurve = "secp256r1";

        #endregion

        #region Properties

        #region PrivateKey

        /// <summary>
        /// The private elliptic curve key of the service check.
        /// </summary>
        public ECPrivateKeyParameters  PrivateKey    { get; }

        /// <summary>
        /// The private elliptic curve key of the service check as byte array.
        /// </summary>
        public Byte[]                  PrivateKeyBytes
            => PrivateKey.D.ToByteArray();

        /// <summary>
        /// The private elliptic curve key of the service check as hexadecimal string.
        /// </summary>
        public String                  PrivateKeyHEX
            => PrivateKeyBytes.ToHexString();

        #endregion

        #region PublicKey

        /// <summary>
        /// The public elliptic curve key of the service check.
        /// </summary>
        public ECPublicKeyParameters   PublicKey     { get; }

        /// <summary>
        /// The public elliptic curve key of the service check as byte array.
        /// </summary>
        public Byte[]                  PublicKeyBytes
            => PublicKey. Q.GetEncoded();

        /// <summary>
        /// The public elliptic curve key of the service check as hexadecimal string.
        /// </summary>
        public String                  PublicKeyHEX
            => PublicKeyBytes.ToHexString();

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create new service check keys.
        /// </summary>
        /// <param name="PrivateKey"></param>
        /// <param name="PublicKey"></param>
        public ServiceCheckKeys(ECPrivateKeyParameters  PrivateKey,
                                ECPublicKeyParameters   PublicKey)
        {

            this.PrivateKey  = PrivateKey;
            this.PublicKey   = PublicKey;

            unchecked
            {

                hashCode = this.PrivateKeyHEX.GetHashCode() * 3 ^
                           this.PublicKeyHEX. GetHashCode();

            }

        }

        #endregion


        #region GenerateECKeys (ECCurve = null)

        /// <summary>
        /// Generate new service check keys for the optional given elliptic curve.
        /// </summary>
        /// <param name="ECCurve">An optional elliptic curve to use. The default is secp256r1.</param>
        public static ServiceCheckKeys GenerateECKeys(String? ECCurve = null)
        {

            var ecParameters        = ECNamedCurveTable.GetByName(
                                          ECCurve ?? DefaultECCurve
                                      );

            var ecDomainParameters  = new ECDomainParameters(
                                          ecParameters.Curve,
                                          ecParameters.G,
                                          ecParameters.N,
                                          ecParameters.H,
                                          ecParameters.GetSeed()
                                      );

            return GenerateECKeys(ecDomainParameters);

        }

        #endregion

        #region GenerateECKeys (EllipticCurveSpec)

        /// <summary>
        /// Generate new service check keys for the given elliptic curve specification.
        /// </summary>
        /// <param name="EllipticCurveSpec">The elliptic curve specification to use.</param>
        public static ServiceCheckKeys GenerateECKeys(ECDomainParameters EllipticCurveSpec)
        {

            var generator   = GeneratorUtilities.GetKeyPairGenerator("ECDH");
            generator.Init(new ECKeyGenerationParameters(EllipticCurveSpec, new SecureRandom()));
            var keyPair     = generator.GenerateKeyPair();

            var privateKey  = keyPair.Private as ECPrivateKeyParameters;
            var publicKey   = keyPair.Public  as ECPublicKeyParameters;

            return new ServiceCheckKeys(
                       privateKey!,
                       publicKey!
                   );

        }

        #endregion


        #region ParseECKeysHEX (PrivateKey, PublicKey, ECCurve = null)

        /// <summary>
        /// Parse the given hexadecimal encoded private and public elliptic curve keys for the optional given elliptic curve.
        /// </summary>
        /// <param name="PrivateKey">The hexadecimal encoded private key.</param>
        /// <param name="PublicKey">The hexadecimal encoded public key.</param>
        /// <param name="ECCurve">An optional elliptic curve to use. The default is secp256r1.</param>
        public static ServiceCheckKeys ParseECKeysHEX(String   PrivateKey,
                                                      String   PublicKey,
                                                      String?  ECCurve   = null)
        {

            var ecParameters        = ECNamedCurveTable.GetByName(
                                          ECCurve ?? DefaultECCurve
                                      );

            var ecDomainParameters  = new ECDomainParameters(
                                          ecParameters.Curve,
                                          ecParameters.G,
                                          ecParameters.N,
                                          ecParameters.H,
                                          ecParameters.GetSeed()
                                      );

            return ParseECKeysHEX(
                       PrivateKey,
                       PublicKey,
                       ecDomainParameters
                   );

        }

        #endregion

        #region ParseECKeysHEX (PrivateKey, PublicKey, EllipticCurveSpec)

        /// <summary>
        /// Parse the given hexadecimal encoded private and public elliptic curve keys for the given elliptic curve specification.
        /// </summary>
        /// <param name="PrivateKey">The hexadecimal encoded private key.</param>
        /// <param name="PublicKey">The hexadecimal encoded public key.</param>
        /// <param name="EllipticCurveSpec">The elliptic curve specification to use.</param>
        public static ServiceCheckKeys ParseECKeysHEX(String              PrivateKey,
                                                      String              PublicKey,
                                                      ECDomainParameters  EllipticCurveSpec)
        {

            var privateKey  = new ECPrivateKeyParameters(
                                  new BigInteger(
                                      PrivateKey,
                                      16
                                  ),
                                  EllipticCurveSpec
                              );

            var publicKey   = new ECPublicKeyParameters(
                                  "ECDSA",
                                  EllipticCurveSpec.Curve.DecodePoint(
                                      PublicKey.FromHEX()
                                  ),
                                  EllipticCurveSpec
                              );

            return new ServiceCheckKeys(
                       privateKey,
                       publicKey
                   );

        }

        #endregion


        #region Operator overloading

        #region Operator == (ServiceCheckKeys1, ServiceCheckKeys2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ServiceCheckKeys1">Service check keys.</param>
        /// <param name="ServiceCheckKeys2">Other service check keys.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (ServiceCheckKeys ServiceCheckKeys1,
                                           ServiceCheckKeys ServiceCheckKeys2)
        {

            if (Object.ReferenceEquals(ServiceCheckKeys1, ServiceCheckKeys2))
                return true;

            if (ServiceCheckKeys1 is null || ServiceCheckKeys2 is null)
                return false;

            return ServiceCheckKeys1.Equals(ServiceCheckKeys2);

        }

        #endregion

        #region Operator != (ServiceCheckKeys1, ServiceCheckKeys2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ServiceCheckKeys1">Service check keys.</param>
        /// <param name="ServiceCheckKeys2">Other service check keys.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (ServiceCheckKeys ServiceCheckKeys1,
                                           ServiceCheckKeys ServiceCheckKeys2)

            => !(ServiceCheckKeys1 == ServiceCheckKeys2);

        #endregion

        #region Operator <  (ServiceCheckKeys1, ServiceCheckKeys2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ServiceCheckKeys1">Service check keys.</param>
        /// <param name="ServiceCheckKeys2">Other service check keys.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (ServiceCheckKeys ServiceCheckKeys1,
                                          ServiceCheckKeys ServiceCheckKeys2)

            => ServiceCheckKeys1 is null
                   ? throw new ArgumentNullException(nameof(ServiceCheckKeys1), "The given service check keys must not be null!")
                   : ServiceCheckKeys1.CompareTo(ServiceCheckKeys2) < 0;

        #endregion

        #region Operator <= (ServiceCheckKeys1, ServiceCheckKeys2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ServiceCheckKeys1">Service check keys.</param>
        /// <param name="ServiceCheckKeys2">Other service check keys.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (ServiceCheckKeys ServiceCheckKeys1,
                                           ServiceCheckKeys ServiceCheckKeys2)

            => !(ServiceCheckKeys1 > ServiceCheckKeys2);

        #endregion

        #region Operator >  (ServiceCheckKeys1, ServiceCheckKeys2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ServiceCheckKeys1">Service check keys.</param>
        /// <param name="ServiceCheckKeys2">Other service check keys.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (ServiceCheckKeys ServiceCheckKeys1,
                                          ServiceCheckKeys ServiceCheckKeys2)

            => ServiceCheckKeys1 is null
                   ? throw new ArgumentNullException(nameof(ServiceCheckKeys1), "The given service check keys must not be null!")
                   : ServiceCheckKeys1.CompareTo(ServiceCheckKeys2) > 0;

        #endregion

        #region Operator >= (ServiceCheckKeys1, ServiceCheckKeys2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ServiceCheckKeys1">Service check keys.</param>
        /// <param name="ServiceCheckKeys2">Other service check keys.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (ServiceCheckKeys ServiceCheckKeys1,
                                           ServiceCheckKeys ServiceCheckKeys2)

            => !(ServiceCheckKeys1 < ServiceCheckKeys2);

        #endregion

        #endregion

        #region IComparable<ServiceCheckKeys> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two service check keys.
        /// </summary>
        /// <param name="Object">Service check keys to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is ServiceCheckKeys serviceCheckKeys
                   ? CompareTo(serviceCheckKeys)
                   : throw new ArgumentException("The given object is not a service check keys!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(ServiceCheckKeys)

        /// <summary>
        /// Compares two service check keys.
        /// </summary>
        /// <param name="ServiceCheckKeys">Service check keys to compare with.</param>
        public Int32 CompareTo(ServiceCheckKeys? ServiceCheckKeys)
        {

            if (ServiceCheckKeys is null)
                throw new ArgumentNullException(nameof(ServiceCheckKeys), "The given service check keys must not be null!");

            var c = PrivateKeyHEX.CompareTo(ServiceCheckKeys.PrivateKeyHEX);

            if (c == 0)
                c = PublicKeyHEX. CompareTo(ServiceCheckKeys.PublicKeyHEX);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<ServiceCheckKeys> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two service check keys for equality.
        /// </summary>
        /// <param name="Object">Service check keys to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is ServiceCheckKeys serviceCheckKeys &&
                   Equals(serviceCheckKeys);

        #endregion

        #region Equals(ServiceCheckKeys)

        /// <summary>
        /// Compares two service check keys for equality.
        /// </summary>
        /// <param name="ServiceCheckKeys">Service check keys to compare with.</param>
        public Boolean Equals(ServiceCheckKeys? ServiceCheckKeys)

            => ServiceCheckKeys is not null &&

               PrivateKeyBytes.SequenceEqual(ServiceCheckKeys.PrivateKeyBytes) &&
               PublicKeyBytes. SequenceEqual(ServiceCheckKeys.PublicKeyBytes);

        #endregion

        #endregion

        #region (override) GetHashCode()

        private readonly Int32 hashCode;

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
            => hashCode;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"PrivateKey: {PrivateKeyHEX}, PublicKey: {PublicKeyHEX}";

        #endregion

    }

}
