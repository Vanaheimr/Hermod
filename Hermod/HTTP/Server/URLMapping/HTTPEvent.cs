/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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

using System;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    /// A HTTP event.
    /// </summary>
    public struct HTTPEvent : IEquatable<HTTPEvent>, IComparable<HTTPEvent>
    {

        #region Data

        /// <summary>
        /// The subevent identification of this HTTP event.
        /// </summary>
        public readonly String Subevent;

        /// <summary>
        /// The id of this HTTP event.
        /// </summary>
        public readonly UInt64 Id;

        /// <summary>
        /// The attached data of this HTTP event.
        /// </summary>
        public readonly String Data;

        #endregion

        #region Constructor(s)

        #region HTTPEvent(myId, myData)

        /// <summary>
        /// Create a new HTTP event based on the given parameters.
        /// </summary>
        /// <param name="myId">The id of the event.</param>
        /// <param name="myData">The attached data of the event.</param>
        public HTTPEvent(UInt64 myId, String myData)
        {
            Subevent = "";
            Id = myId;
            Data = myData;
        }

        #endregion

        #region HTTPEvent(mySubevent, myId, myData)

        /// <summary>
        /// Create a new HTTP event based on the given parameters.
        /// </summary>
        /// <param name="mySubevent">The subevent.</param>
        /// <param name="myId">The id of the event.</param>
        /// <param name="myData">The attached data of the event.</param>
        public HTTPEvent(String mySubevent, UInt64 myId, String myData)
        {
            Subevent = mySubevent;
            Id = myId;
            Data = myData;
        }

        #endregion

        #endregion

        #region IEquatable Members

        /// <summary>
        /// Compare two objects.
        /// </summary>
        /// <param name="OtherHTTPEvent">Another HTTP event.</param>
        public Boolean Equals(HTTPEvent OtherHTTPEvent)
        {
            return Id == OtherHTTPEvent.Id;
        }

        #endregion

        #region IComparable Members

        /// <summary>
        /// Compare two objects.
        /// </summary>
        /// <param name="OtherHTTPEvent">Another HTTP event.</param>
        public Int32 CompareTo(HTTPEvent OtherHTTPEvent)
        {
            return Id.CompareTo(OtherHTTPEvent.Id);
        }

        #endregion

        #region ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            if (Subevent != "")
                return String.Format("event:{1}{0}id:{2}{0}data:{3}{0}", Environment.NewLine, Subevent, Id, Data);
            else
                return String.Format("id:{2}{0}data:{3}{0}{0}", Environment.NewLine, Id, Data);
        }

        #endregion

    }

}
