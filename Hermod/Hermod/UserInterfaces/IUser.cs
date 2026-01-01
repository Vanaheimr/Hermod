///*
// * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
// * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
// *
// * Licensed under the Apache License, Version 2.0 (the "License");
// * you may not use this file except in compliance with the License.
// * You may obtain a copy of the License at
// *
// *     http://www.apache.org/licenses/LICENSE-2.0
// *
// * Unless required by applicable law or agreed to in writing, software
// * distributed under the License is distributed on an "AS IS" BASIS,
// * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// * See the License for the specific language governing permissions and
// * limitations under the License.
// */

//#region Usings

//using System;

//using org.GraphDefined.Vanaheimr.Illias;
//using org.GraphDefined.Vanaheimr.Hermod.Mail;

//#endregion

//namespace org.GraphDefined.Vanaheimr.Hermod
//{

//    /// <summary>
//    /// The user interface.
//    /// </summary>
//    public interface IUser
//    {

//        /// <summary>
//        /// The primary E-Mail address of the user.
//        /// </summary>
//        [Mandatory]
//        public EMailAddress               EMail                { get; }

//        ///// <summary>
//        ///// The official public name of the user.
//        ///// </summary>
//        //[Optional]
//        //public String                     Name                 { get; }

//        /// <summary>
//        /// The language setting of the user.
//        /// </summary>
//        [Mandatory]
//        public Languages                  UserLanguage         { get; }

//        ///// <summary>
//        ///// An optional (multi-language) description of the user.
//        ///// </summary>
//        //[Optional]
//        //public I18NString                 Description          { get; }

//        /// <summary>
//        /// Timestamp when the user accepted the End-User-License-Agreement.
//        /// </summary>
//        [Mandatory]
//        public DateTime?                  AcceptedEULA         { get; }

//        /// <summary>
//        /// The user will not be shown in user listings, as its
//        /// primary e-mail address is not yet authenticated.
//        /// </summary>
//        [Mandatory]
//        public Boolean                    IsAuthenticated      { get; }

//        /// <summary>
//        /// The user is disabled.
//        /// </summary>
//        [Mandatory]
//        public Boolean                    IsDisabled           { get; }


//    }

//}
