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

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.PKI
{

    public static class AsymmetricKeyParameterExtensions
    {

        public static String GetAlgorithmName(this AsymmetricKeyParameter key)

            => key switch {
                   RsaKeyParameters            => "RSA",
                   ECPublicKeyParameters       => "ECDSA",
                   Ed25519PublicKeyParameters  => "Ed25519",
                   Ed448PublicKeyParameters    => "Ed448",
                   DsaPublicKeyParameters      => "DSA",
                   MLDsaPublicKeyParameters    => "ML-DSA",
                   SlhDsaPublicKeyParameters   => "SLH-DSA",
                   MLKemPublicKeyParameters    => "ML-KEM",
                   X25519PublicKeyParameters   => "X25519",
                   X448PublicKeyParameters     => "X448",
                   _                           => key.GetType().Name
               };

    }

}
