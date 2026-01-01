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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{

    public class ServiceCheckKeys
    {

        #region Data

        public const String DefaultECCurve = "secp256r1";

        #endregion

        #region Properties

        #region PrivateKey

        public ECPrivateKeyParameters  PrivateKey    { get; }

        public Byte[] PrivateKeyBytes
            => PrivateKey.D.ToByteArray();

        public String PrivateKeyHEX
            => PrivateKeyBytes.ToHexString();

        #endregion

        #region PublicKey

        public ECPublicKeyParameters   PublicKey     { get; }

        public Byte[] PublicKeyBytes
            => PublicKey. Q.GetEncoded();

        public String PublicKeyHEX
            => PublicKeyBytes.ToHexString();

        #endregion

        #endregion


        public ServiceCheckKeys(ECPrivateKeyParameters PrivateKey,
                                ECPublicKeyParameters  PublicKey)
        {

            this.PrivateKey  = PrivateKey;
            this.PublicKey   = PublicKey;

        }




        public static ServiceCheckKeys GenerateKeys(String? ECCurve = null)
        {

            var ecp                = ECNamedCurveTable.GetByName(ECCurve ?? DefaultECCurve);
            var ellipticCurveSpec  = new ECDomainParameters(ecp.Curve, ecp.G, ecp.N, ecp.H, ecp.GetSeed());

            return GenerateKeys(ellipticCurveSpec);

        }

        public static ServiceCheckKeys GenerateKeys(ECDomainParameters EllipticCurveSpec)
        {

            var g           = GeneratorUtilities.GetKeyPairGenerator("ECDH");
            g.Init(new ECKeyGenerationParameters(EllipticCurveSpec, new SecureRandom()));
            var keyPair     = g.GenerateKeyPair();

            var privateKey  = keyPair.Private as ECPrivateKeyParameters;
            var publicKey   = keyPair.Public  as ECPublicKeyParameters;

            return new ServiceCheckKeys(
                       privateKey!,
                       publicKey!
                   );

        }


        public static ServiceCheckKeys ParseKeysHEX(String   PrivateKey,
                                                    String   PublicKey,
                                                    String?  ECCurve   = null)
        {

            var ecp                = ECNamedCurveTable.GetByName(ECCurve ?? DefaultECCurve);
            var ellipticCurveSpec  = new ECDomainParameters(ecp.Curve, ecp.G, ecp.N, ecp.H, ecp.GetSeed());

            return ParseKeysHEX(
                       PrivateKey,
                       PublicKey,
                       ellipticCurveSpec
                   );

        }

        public static ServiceCheckKeys ParseKeysHEX(String              PrivateKey,
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


    }

}
