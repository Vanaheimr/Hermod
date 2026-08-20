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

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// What a rendezvous control client got back: either a response of the
    /// service, or the reason why there is none.
    ///
    /// A transport failure and a rejected command are told apart on purpose -
    /// "the service said no" and "the service never answered" are very
    /// different things for whoever has to react to them.
    /// </summary>
    public sealed class ControlClientResponse
    {

        #region Properties

        /// <summary>
        /// The request this is the answer to.
        /// </summary>
        public ControlRequest                 Request        { get; }

        /// <summary>
        /// The response of the service, or null when it did not answer.
        /// </summary>
        public ControlResponse?               Response       { get; }

        /// <summary>
        /// The keys the response was signed with, when the client was configured
        /// to verify them.
        /// </summary>
        public IReadOnlyList<ControlKey>      SignedBy       { get; }

        /// <summary>
        /// Why there is no response, or null.
        /// </summary>
        public String?                        Error          { get; }

        /// <summary>
        /// Whether the service answered at all.
        /// </summary>
        public Boolean                        HasResponse
            => Response is not null;

        /// <summary>
        /// Whether the command was executed successfully.
        /// </summary>
        public Boolean                        IsSuccess
            => Response?.IsSuccess == true;

        /// <summary>
        /// The response code of the service, or null.
        /// </summary>
        public ResponseCode?                  Code
            => Response?.Code;

        /// <summary>
        /// The TCP ports of the rendezvous.
        /// </summary>
        public IReadOnlyList<IPPort>          Ports
            => Response?.Ports ?? [];

        /// <summary>
        /// The effective transfer profile of the rendezvous, or null.
        /// </summary>
        public TransferProfile?               Profile
            => Response?.Profile;

        /// <summary>
        /// The description of the rendezvous, or null.
        /// </summary>
        public String?                        Description
            => Response?.Description;

        /// <summary>
        /// When the rendezvous was opened, or null.
        /// </summary>
        public DateTimeOffset?                Created
            => Response?.Created;

        /// <summary>
        /// The identifications of the keys that own the rendezvous.
        /// </summary>
        public IReadOnlyList<String>          CreatedBy
            => Response?.CreatedBy ?? [];

        /// <summary>
        /// Whether the rendezvous also sends everything back to its sender.
        /// </summary>
        public Boolean                        EchoToSender
            => Response?.EchoToSender == true;

        /// <summary>
        /// A human readable description of what happened.
        /// </summary>
        public String                         Message
            => Error ?? Response?.Message ?? "No response!";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new control client response.
        /// </summary>
        /// <param name="Request">The request this is the answer to.</param>
        /// <param name="Response">The response of the service, or null.</param>
        /// <param name="SignedBy">The keys the response was signed with.</param>
        /// <param name="Error">Why there is no response, or null.</param>
        public ControlClientResponse(ControlRequest               Request,
                                     ControlResponse?             Response,
                                     IEnumerable<ControlKey>?     SignedBy   = null,
                                     String?                      Error      = null)
        {

            this.Request   = Request;
            this.Response  = Response;
            this.SignedBy  = SignedBy is null ? [] : [.. SignedBy];
            this.Error     = Error;

        }

        #endregion


        #region (static) Failed(Request, Error)

        /// <summary>
        /// The service did not answer.
        /// </summary>
        /// <param name="Request">The request that was sent.</param>
        /// <param name="Error">Why there is no response.</param>
        public static ControlClientResponse Failed(ControlRequest  Request,
                                                   String          Error)

            => new (Request, null, null, Error);

        #endregion


        #region ToString()

        /// <summary>
        /// Return a text representation of this response.
        /// </summary>
        public override String ToString()

            => Response is not null
                   ? $"{Response}{(SignedBy.Count > 0 ? $" (signed by {String.Join(", ", SignedBy.Select(key => key.Id))})" : "")}"
                   : $"failed: {Error}";

        #endregion

    }

}
