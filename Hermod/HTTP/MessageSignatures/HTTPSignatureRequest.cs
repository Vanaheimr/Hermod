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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// One signature an <c>Accept-Signature</c> field asks for (RFC 9421,
    /// Section 5.1): its label, the exact components to cover, and the metadata
    /// parameters, where <c>created</c> and <c>expires</c> carry no value and ask
    /// the signer to supply one.
    /// </summary>
    /// <param name="Label">The label the signature is to have.</param>
    /// <param name="Components">The components it is to cover, exactly these.</param>
    /// <param name="Parameters">The requested metadata parameters, in their order.</param>
    public sealed record HTTPSignatureRequest(String                                 Label,
                                              IReadOnlyList<HTTPSignatureComponent>  Components,
                                              SFParameters                           Parameters)
    {

        /// <summary>
        /// This request's member of <c>Accept-Signature</c>.
        /// </summary>
        public String AcceptSignatureMember

            => $"{Label}={new SFInnerList(Components.Select(component => component.ToSFItem()), new SFParameters(Parameters.Values))}";

    }

}
