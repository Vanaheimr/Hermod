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
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
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
        /// The default elliptic curve to use for service checks.
        /// </summary>
        public const String  DefaultECCurve    = "secp256r1";

        /// <summary>
        /// The default ml-DSA parameters to use for service checks.
        /// </summary>
        public const String  DefaultMLDSAName  = "ml_dsa_65";

        #endregion

        #region Properties

        #region EC

        /// <summary>
        /// The private elliptic curve key of service checks.
        /// </summary>
        public ECPrivateKeyParameters?  PrivateKeyEC     { get; }

        /// <summary>
        /// The private elliptic curve key of service checks as byte array.
        /// </summary>
        public Byte[]?                  PrivateKeyEC_Bytes
            => PrivateKeyEC?.D.ToByteArray();

        /// <summary>
        /// The private elliptic curve key of service checks as hexadecimal string.
        /// </summary>
        public String?                  PrivateKeyEC_HEX
            => PrivateKeyEC_Bytes?.ToHexString();

        /// <summary>
        /// The private elliptic curve key of service checks as DER encoded PKCS#8 PrivateKeyInfo.
        /// </summary>
        public Byte[]?                  PrivateKeyEC_DER
            => PrivateKeyEC is not null
                   ? PrivateKeyInfoFactory.CreatePrivateKeyInfo(PrivateKeyEC).GetDerEncoded()
                   : null;

        /// <summary>
        /// The private elliptic curve key of service checks as hexadecimal DER encoded PKCS#8 PrivateKeyInfo.
        /// </summary>
        public String?                  PrivateKeyEC_DER_HEX
            => PrivateKeyEC_DER?.ToHexString();



        /// <summary>
        /// The public elliptic curve key of service checks.
        /// </summary>
        public ECPublicKeyParameters?   PublicKeyEC      { get; }

        /// <summary>
        /// The public elliptic curve key of service checks as byte array.
        /// </summary>
        public Byte[]?                  PublicKeyEC_Bytes
            => PublicKeyEC?.Q.GetEncoded();

        /// <summary>
        /// The public elliptic curve key of service checks as hexadecimal string.
        /// </summary>
        public String?                  PublicKeyEC_HEX
            => PublicKeyEC_Bytes?.ToHexString();

        /// <summary>
        /// The public elliptic curve key of service checks as DER encoded X.509 SubjectPublicKeyInfo.
        /// </summary>
        public Byte[]?                  PublicKeyEC_DER
            => PublicKeyEC is not null
                   ? SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(PublicKeyEC).GetDerEncoded()
                   : null;

        /// <summary>
        /// The public elliptic curve key of service checks as hexadecimal DER encoded X.509 SubjectPublicKeyInfo.
        /// </summary>
        public String?                  PublicKeyEC_DER_HEX
            => PublicKeyEC_DER?.ToHexString();

        #endregion

        #region ML-DSA

        /// <summary>
        /// The private ML-DSA post-quantum key of service checks.
        /// </summary>
        public MLDsaPrivateKeyParameters?  PrivateKeyMLDSA     { get; }

        /// <summary>
        /// The private ML-DSA post-quantum key of service checks as byte array.
        /// </summary>
        public Byte[]?                     PrivateKeyMLDSA_Bytes
            => PrivateKeyMLDSA?.GetEncoded();

        /// <summary>
        /// The private ML-DSA post-quantum key of service checks as hexadecimal string.
        /// </summary>
        public String?                     PrivateKeyMLDSA_HEX
            => PrivateKeyMLDSA_Bytes?.ToHexString();

        /// <summary>
        /// The private ML-DSA post-quantum key of service checks as DER encoded PKCS#8 PrivateKeyInfo.
        /// </summary>
        public Byte[]?                     PrivateKeyMLDSA_DER
            => PrivateKeyMLDSA is not null
                   ? PrivateKeyInfoFactory.CreatePrivateKeyInfo(PrivateKeyMLDSA).GetDerEncoded()
                   : null;

        /// <summary>
        /// The private ML-DSA post-quantum key of service checks as hexadecimal DER encoded PKCS#8 PrivateKeyInfo.
        /// </summary>
        public String?                     PrivateKeyMLDSA_DER_HEX
            => PrivateKeyMLDSA_DER?.ToHexString();



        /// <summary>
        /// The public ML-DSA post-quantum key of service checks.
        /// </summary>
        public MLDsaPublicKeyParameters?   PublicKeyMLDSA      { get; }

        /// <summary>
        /// The public ML-DSA post-quantum key of service checks as byte array.
        /// </summary>
        public Byte[]?                     PublicKeyMLDSA_Bytes
            => PublicKeyMLDSA?.GetEncoded();

        /// <summary>
        /// The public ML-DSA post-quantum key of service checks as hexadecimal string.
        /// </summary>
        public String?                     PublicKeyMLDSA_HEX
            => PublicKeyMLDSA_Bytes?.ToHexString();

        /// <summary>
        /// The public ML-DSA post-quantum key of service checks as DER encoded X.509 SubjectPublicKeyInfo.
        /// </summary>
        public Byte[]?                     PublicKeyMLDSA_DER
            => PublicKeyMLDSA is not null
                   ? SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(PublicKeyMLDSA).GetDerEncoded()
                   : null;

        /// <summary>
        /// The public ML-DSA post-quantum key of service checks as hexadecimal DER encoded X.509 SubjectPublicKeyInfo.
        /// </summary>
        public String?                     PublicKeyMLDSA_DER_HEX
            => PublicKeyMLDSA_DER?.ToHexString();

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create new service check keys.
        /// </summary>
        /// <param name="PrivateKeyEC">An optional private elliptic curve key.</param>
        /// <param name="PublicKeyEC">An optional public elliptic curve key.</param>
        /// <param name="PrivateKeyMLDSA">An optional private ML-DSA key.</param>
        /// <param name="PublicKeyMLDSA">An optional public ML-DSA key.</param>
        public ServiceCheckKeys(ECPrivateKeyParameters?     PrivateKeyEC      = null,
                                ECPublicKeyParameters?      PublicKeyEC       = null,
                                MLDsaPrivateKeyParameters?  PrivateKeyMLDSA   = null,
                                MLDsaPublicKeyParameters?   PublicKeyMLDSA    = null)
        {

            if ((PrivateKeyEC is null) != (PublicKeyEC is null))
                throw new ArgumentException("The EC private and public keys must either both be set or both be null!");

            if (PrivateKeyMLDSA is null && PublicKeyMLDSA is not null)
                throw new ArgumentException("An ML-DSA public key without the matching private key can not be used as service check keys!");

            PublicKeyMLDSA ??= PrivateKeyMLDSA?.GetPublicKey();

            if (PrivateKeyEC is null && PrivateKeyMLDSA is null)
                throw new ArgumentException("At least one EC or ML-DSA key pair must be set!");

            this.PrivateKeyEC     = PrivateKeyEC;
            this.PublicKeyEC      = PublicKeyEC;
            this.PrivateKeyMLDSA  = PrivateKeyMLDSA;
            this.PublicKeyMLDSA   = PublicKeyMLDSA;

            unchecked
            {

                hashCode = (this.PrivateKeyEC_HEX?.   GetHashCode() ?? 0) * 7 ^
                           (this.PublicKeyEC_HEX?.    GetHashCode() ?? 0) * 5 ^
                           (this.PrivateKeyMLDSA_HEX?.GetHashCode() ?? 0) * 3 ^
                           (this.PublicKeyMLDSA_HEX?. GetHashCode() ?? 0);

            }

        }

        #endregion


        #region GenerateKeys           (ECCurve = "secp256r1", MLDSAName = "ml_dsa_65")

        /// <summary>
        /// Generate new Elliptic Curve (secp256r1) and ML-DSA (ml_dsa_65) service check keys.
        /// </summary>
        /// <param name="ECCurve">An optional elliptic curve to use. The default is secp256r1.</param>
        /// <param name="MLDSAName">The optional ML-DSA parameters to use. The default is ml_dsa_65.</param>
        public static ServiceCheckKeys GenerateKeys(String  ECCurve     = DefaultECCurve,
                                                    String  MLDSAName   = DefaultMLDSAName)
        {

            var ecKeys     = GenerateECKeys   (ECCurve);
            var mlDSAKeys  = GenerateMLDSAKeys(MLDSAName);

            return new ServiceCheckKeys(
                       ecKeys.   PrivateKeyEC,
                       ecKeys.   PublicKeyEC,
                       mlDSAKeys.PrivateKeyMLDSA,
                       mlDSAKeys.PublicKeyMLDSA
                   );

        }

        #endregion


        // Elliptic Curve Cryptography (ECC)

        #region GenerateECKeys         (ECCurve = "secp256r1")

        /// <summary>
        /// Generate new service check keys for the optional given elliptic curve.
        /// </summary>
        /// <param name="ECCurve">An optional elliptic curve to use. The default is secp256r1.</param>
        public static ServiceCheckKeys GenerateECKeys(String ECCurve = DefaultECCurve)
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

        #region GenerateECKeys         (EllipticCurveSpec)

        /// <summary>
        /// Generate new service check keys for the given elliptic curve specification.
        /// </summary>
        /// <param name="EllipticCurveSpec">The elliptic curve specification to use.</param>
        public static ServiceCheckKeys GenerateECKeys(ECDomainParameters EllipticCurveSpec)
        {

            var generator   = GeneratorUtilities.GetKeyPairGenerator("ECDH");
            generator.Init(new ECKeyGenerationParameters(EllipticCurveSpec, new SecureRandom()));
            var keyPair     = generator.GenerateKeyPair();

            return new ServiceCheckKeys(
                       keyPair.Private as ECPrivateKeyParameters,
                       keyPair.Public  as ECPublicKeyParameters
                   );

        }

        #endregion


        #region ParseECKeysHEX         (PrivateKeyEC, PublicKeyEC, ECCurve = null)

        /// <summary>
        /// Parse the given hexadecimal encoded private and public elliptic curve keys for the optional given elliptic curve.
        /// </summary>
        /// <param name="PrivateKeyEC">The hexadecimal encoded private EC key.</param>
        /// <param name="PublicKeyEC">The hexadecimal encoded public EC key.</param>
        /// <param name="ECCurve">An optional elliptic curve to use. The default is secp256r1.</param>
        public static ServiceCheckKeys ParseECKeysHEX(String   PrivateKeyEC,
                                                      String   PublicKeyEC,
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
                       PrivateKeyEC,
                       PublicKeyEC,
                       ecDomainParameters
                   );

        }

        #endregion

        #region ParseECKeysHEX         (PrivateKeyEC, PublicKeyEC, EllipticCurveSpec)

        /// <summary>
        /// Parse the given hexadecimal encoded private and public elliptic curve keys for the given elliptic curve specification.
        /// </summary>
        /// <param name="PrivateKeyEC">The hexadecimal encoded private EC key.</param>
        /// <param name="PublicKeyEC">The hexadecimal encoded public EC key.</param>
        /// <param name="EllipticCurveSpec">The elliptic curve specification to use.</param>
        public static ServiceCheckKeys ParseECKeysHEX(String              PrivateKeyEC,
                                                      String              PublicKeyEC,
                                                      ECDomainParameters  EllipticCurveSpec)
        {

            var privateKeyEC  = new ECPrivateKeyParameters(
                                    new BigInteger(
                                        PrivateKeyEC,
                                        16
                                    ),
                                    EllipticCurveSpec
                                );

            var publicKeyEC   = new ECPublicKeyParameters(
                                    "ECDSA",
                                    EllipticCurveSpec.Curve.DecodePoint(
                                        PublicKeyEC.FromHEX()
                                    ),
                                    EllipticCurveSpec
                                );

            return new ServiceCheckKeys(
                       privateKeyEC,
                       publicKeyEC
                   );

        }

        #endregion


        #region ParseECKeysDER         (PrivateKeyEC_DER,     PublicKeyEC_DER)

        /// <summary>
        /// Parse the given DER encoded PKCS#8 private and X.509 SubjectPublicKeyInfo public elliptic curve keys.
        /// </summary>
        /// <param name="PrivateKeyEC_DER">The DER encoded PKCS#8 private EC key.</param>
        /// <param name="PublicKeyEC_DER">The DER encoded X.509 SubjectPublicKeyInfo public EC key.</param>
        public static ServiceCheckKeys ParseECKeysDER(Byte[]  PrivateKeyEC_DER,
                                                      Byte[]  PublicKeyEC_DER)
        {

            if (PrivateKeyFactory.CreateKey(PrivateKeyEC_DER) is not ECPrivateKeyParameters privateKeyEC)
                throw new ArgumentException("The given DER private key is not an EC key!", nameof(PrivateKeyEC_DER));

            if (PublicKeyFactory. CreateKey(PublicKeyEC_DER)  is not ECPublicKeyParameters  publicKeyEC)
                throw new ArgumentException("The given DER public key is not an EC key!",  nameof(PublicKeyEC_DER));

            return ParseECKeysHEX(
                       privateKeyEC.D.ToByteArray().ToHexString(),
                       publicKeyEC. Q.GetEncoded(). ToHexString(),
                       privateKeyEC.Parameters
                   );

        }

        #endregion

        #region ParseECKeysDER         (PrivateKeyEC_DER_HEX, PublicKeyEC_DER_HEX)

        /// <summary>
        /// Parse the given hexadecimal DER encoded PKCS#8 private and X.509 SubjectPublicKeyInfo public elliptic curve keys.
        /// </summary>
        /// <param name="PrivateKeyEC_DER_HEX">The hexadecimal DER encoded PKCS#8 private EC key.</param>
        /// <param name="PublicKeyEC_DER_HEX">The hexadecimal DER encoded X.509 SubjectPublicKeyInfo public EC key.</param>
        public static ServiceCheckKeys ParseECKeysDER(String  PrivateKeyEC_DER_HEX,
                                                      String  PublicKeyEC_DER_HEX)

            => ParseECKeysDER(
                   PrivateKeyEC_DER_HEX.FromHEX(),
                   PublicKeyEC_DER_HEX. FromHEX()
               );

        #endregion



        // Multi-Level Digital Signature Algorithm (ML-DSA, Post-Quantum Digital Signature Algorithm)

        #region GenerateMLDSAKeys      (MLDSAName = "ml_dsa_65")

        /// <summary>
        /// Generate new service check keys for the optional given ML-DSA parameter set.
        /// </summary>
        /// <param name="MLDSAName">The optional ML-DSA parameters to use. The default is ml_dsa_65.</param>
        public static ServiceCheckKeys GenerateMLDSAKeys(String MLDSAName = DefaultMLDSAName)

            => GenerateMLDSAKeys(
                   ParseMLDSAParameters(MLDSAName)
               );

        #endregion

        #region GenerateMLDSAKeys      (MLDSAParameters)

        /// <summary>
        /// Generate new service check keys for the given ML-DSA parameter set.
        /// </summary>
        /// <param name="MLDSAParameters">The ML-DSA parameters to use.</param>
        public static ServiceCheckKeys GenerateMLDSAKeys(MLDsaParameters MLDSAParameters)
        {

            var generator = new MLDsaKeyPairGenerator();

            generator.Init(
                new MLDsaKeyGenerationParameters(
                    new SecureRandom(),
                    MLDSAParameters
                )
            );

            var keyPair = generator.GenerateKeyPair();

            return new ServiceCheckKeys(
                       PrivateKeyMLDSA: keyPair.Private as MLDsaPrivateKeyParameters,
                       PublicKeyMLDSA:  keyPair.Public  as MLDsaPublicKeyParameters
                   );

        }

        #endregion


        #region ParseMLDSAKeysHEX      (PrivateKeyMLDSA, MLDSAName = "ml_dsa_65")

        /// <summary>
        /// Parse the given hexadecimal encoded private ML-DSA key for the optional given ML-DSA parameter set.
        /// </summary>
        /// <param name="PrivateKeyMLDSA">The hexadecimal encoded private ML-DSA key.</param>
        /// <param name="MLDSAName">The optional ML-DSA parameters to use. The default is ml_dsa_65.</param>
        public static ServiceCheckKeys ParseMLDSAKeysHEX(String  PrivateKeyMLDSA,
                                                         String  MLDSAName = DefaultMLDSAName)

            => ParseMLDSAKeysHEX(
                   PrivateKeyMLDSA,
                   ParseMLDSAParameters(MLDSAName)
               );

        #endregion

        #region ParseMLDSAKeysHEX      (PrivateKeyMLDSA, MLDSAParameters)

        /// <summary>
        /// Parse the given hexadecimal encoded private ML-DSA key for the given ML-DSA parameter set.
        /// </summary>
        /// <param name="PrivateKeyMLDSA">The hexadecimal encoded private ML-DSA key.</param>
        /// <param name="MLDSAParameters">The ML-DSA parameters to use.</param>
        public static ServiceCheckKeys ParseMLDSAKeysHEX(String           PrivateKeyMLDSA,
                                                         MLDsaParameters  MLDSAParameters)
        {

            var privateKeyMLDSA = MLDsaPrivateKeyParameters.FromEncoding(
                                      MLDSAParameters,
                                      PrivateKeyMLDSA.FromHEX()
                                  );

            return new ServiceCheckKeys(
                       PrivateKeyMLDSA: privateKeyMLDSA,
                       PublicKeyMLDSA:  privateKeyMLDSA.GetPublicKey()
                   );

        }

        #endregion


        #region ParseMLDSAKeysDER      (PrivateKeyMLDSA_DER)

        /// <summary>
        /// Parse the given DER encoded PKCS#8 private ML-DSA key.
        /// </summary>
        /// <param name="PrivateKeyMLDSA_DER">The DER encoded PKCS#8 private ML-DSA key.</param>
        public static ServiceCheckKeys ParseMLDSAKeysDER(Byte[] PrivateKeyMLDSA_DER)
        {

            if (PrivateKeyFactory.CreateKey(PrivateKeyMLDSA_DER) is not MLDsaPrivateKeyParameters privateKeyMLDSA)
                throw new ArgumentException("The given ASN.1 private key is not an ML-DSA key!", nameof(PrivateKeyMLDSA_DER));

            return ParseMLDSAKeysHEX(
                       privateKeyMLDSA.GetEncoded().ToHexString(),
                       privateKeyMLDSA.Parameters
                   );

        }

        #endregion

        #region ParseMLDSAKeysDER      (PrivateKeyMLDSA_DER)

        /// <summary>
        /// Parse the given hexadecimal DER encoded PKCS#8 private ML-DSA key.
        /// </summary>
        /// <param name="PrivateKeyMLDSA_DER">The hexadecimal DER encoded PKCS#8 private ML-DSA key.</param>
        public static ServiceCheckKeys ParseMLDSAKeysDER(String PrivateKeyMLDSA_DER)

            => ParseMLDSAKeysDER(
                   PrivateKeyMLDSA_DER.FromHEX()
               );

        #endregion


        #region ParseMLDSAPublicKeyHEX (PublicKeyMLDSA, MLDSAName = "ml_dsa_65")

        /// <summary>
        /// Parse the given hexadecimal encoded public ML-DSA key for the optional given ML-DSA parameter set.
        /// </summary>
        /// <param name="PublicKeyMLDSA">The hexadecimal encoded public ML-DSA key.</param>
        /// <param name="MLDSAName">The optional ML-DSA parameters to use. The default is ml_dsa_65.</param>
        public static MLDsaPublicKeyParameters ParseMLDSAPublicKeyHEX(String  PublicKeyMLDSA,
                                                                      String  MLDSAName = DefaultMLDSAName)

            => MLDsaPublicKeyParameters.FromEncoding(
                   ParseMLDSAParameters(MLDSAName),
                   PublicKeyMLDSA.FromHEX()
               );

        #endregion


        #region ParseMLDSAPublicKeyDER (PublicKeyMLDSA_DER)

        /// <summary>
        /// Parse the given DER encoded X.509 SubjectPublicKeyInfo public ML-DSA key.
        /// </summary>
        /// <param name="PublicKeyMLDSA_DER">The DER encoded X.509 SubjectPublicKeyInfo public ML-DSA key.</param>
        public static MLDsaPublicKeyParameters ParseMLDSAPublicKeyDER(Byte[] PublicKeyMLDSA_DER)
        {

            if (PublicKeyFactory.CreateKey(PublicKeyMLDSA_DER) is not MLDsaPublicKeyParameters publicKeyMLDSA)
                throw new ArgumentException("The given ASN.1 public key is not an ML-DSA key!", nameof(PublicKeyMLDSA_DER));

            return ParseMLDSAPublicKeyHEX(
                       publicKeyMLDSA.GetEncoded().ToHexString(),
                       MLDSAParametersName(publicKeyMLDSA.Parameters)
                   );

        }

        #endregion

        #region ParseMLDSAPublicKeyDER (PublicKeyMLDSA_DER)

        /// <summary>
        /// Parse the given DER encoded X.509 SubjectPublicKeyInfo public ML-DSA key.
        /// </summary>
        /// <param name="PublicKeyMLDSA_DER">The DER encoded X.509 SubjectPublicKeyInfo public ML-DSA key.</param>
        public static MLDsaPublicKeyParameters ParseMLDSAPublicKeyDER(String PublicKeyMLDSA_DER)

            => ParseMLDSAPublicKeyDER(
                   PublicKeyMLDSA_DER.FromHEX()
               );

        #endregion


        #region ParseMLDSAParameters   (MLDSAName)

        /// <summary>
        /// Parse ML-DSA parameters by name.
        /// </summary>
        /// <param name="MLDSAName">The ML-DSA parameter set name.</param>
        public static MLDsaParameters ParseMLDSAParameters(String MLDSAName)

            => MLDSAName switch {
                   "ml_dsa_44"  => MLDsaParameters.ml_dsa_44,  // AES-128-Security (NIST Level 1)
                   "ml_dsa_65"  => MLDsaParameters.ml_dsa_65,  // AES-192-Security (NIST Level 3)
                   "ml_dsa_87"  => MLDsaParameters.ml_dsa_87,  // AES-256-Security (NIST Level 5)
                   _            => throw new ArgumentException("Invalid ML-DSA parameters!", nameof(MLDSAName))
               };

        #endregion

        #region MLDSAParametersName    (MLDSAParameters)

        /// <summary>
        /// Return the canonical ML-DSA parameter set name.
        /// </summary>
        /// <param name="MLDSAParameters">The ML-DSA parameter set.</param>
        public static String MLDSAParametersName(MLDsaParameters MLDSAParameters)

            => MLDSAParameters == MLDsaParameters.ml_dsa_44
                   ? "ml_dsa_44"
                   : MLDSAParameters == MLDsaParameters.ml_dsa_65
                         ? "ml_dsa_65"
                         : MLDSAParameters == MLDsaParameters.ml_dsa_87
                               ? "ml_dsa_87"
                               : throw new ArgumentException("Invalid ML-DSA parameters!", nameof(MLDSAParameters));

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

            var c = String.Compare(PrivateKeyEC_HEX,     ServiceCheckKeys.PrivateKeyEC_HEX,    StringComparison.Ordinal);

            if (c == 0)
                c = String.Compare(PublicKeyEC_HEX,      ServiceCheckKeys.PublicKeyEC_HEX,     StringComparison.Ordinal);

            if (c == 0)
                c = String.Compare(PrivateKeyMLDSA_HEX,  ServiceCheckKeys.PrivateKeyMLDSA_HEX, StringComparison.Ordinal);

            if (c == 0)
                c = String.Compare(PublicKeyMLDSA_HEX,   ServiceCheckKeys.PublicKeyMLDSA_HEX,  StringComparison.Ordinal);

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

               PrivateKeyEC_Bytes.   IsEqualTo(ServiceCheckKeys.PrivateKeyEC_Bytes)    &&
               PublicKeyEC_Bytes.    IsEqualTo(ServiceCheckKeys.PublicKeyEC_Bytes)     &&
               PrivateKeyMLDSA_Bytes.IsEqualTo(ServiceCheckKeys.PrivateKeyMLDSA_Bytes) &&
               PublicKeyMLDSA_Bytes. IsEqualTo(ServiceCheckKeys.PublicKeyMLDSA_Bytes);

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

            => $"EC Keys: {PrivateKeyEC_HEX ?? "-"} / {PublicKeyEC_HEX ?? "-"}, " +
               $"ML-DSA Keys: {PrivateKeyMLDSA_HEX ?? "-"} / {PublicKeyMLDSA_HEX ?? "-"}";

        #endregion

    }

}
