/*
 * Copyright (c) 2010-2013, Achim 'ahzf' Friedland <achim@graph-database.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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
using System.Linq;
using System.Collections.Generic;

#endregion

namespace de.ahzf.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A single HTTP event.
    /// </summary>
    public class HTTPEvent : IEquatable<HTTPEvent>,
                             IComparable<HTTPEvent>,
                             IComparable
    {

        #region Properties

        #region Subevent

        /// <summary>
        /// The subevent identification of this HTTP event.
        /// </summary>
        public String Subevent { get; private set; }

        #endregion

        #region Id

        /// <summary>
        /// The identification of this HTTP event.
        /// </summary>
        public UInt64 Id { get; private set; }

        #endregion

        #region Data

        private readonly String[] _Data;

        /// <summary>
        /// The attached data of this HTTP event.
        /// </summary>
        public IEnumerable<String> Data
        {
            get
            {
                return _Data;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPEvent(Id, Data)

        /// <summary>
        /// Create a new HTTP event based on the given parameters.
        /// </summary>
        /// <param name="Id">The id of the event.</param>
        /// <param name="Data">The attached data of the event.</param>
        public HTTPEvent(UInt64 Id, params String[] Data)
        {
            this.Subevent = "";
            this.Id       = Id;
            this._Data    = Data;
        }

        #endregion

        #region HTTPEvent(Subevent, Id, Data)

        /// <summary>
        /// Create a new HTTP event based on the given parameters.
        /// </summary>
        /// <param name="Subevent">The subevent.</param>
        /// <param name="Id">The id of the event.</param>
        /// <param name="Data">The attached data of the event.</param>
        public HTTPEvent(String Subevent, UInt64 Id, params String[] Data)
        {
            this.Subevent = Subevent;
            this.Id       = Id;
            this._Data    = Data;
        }

        #endregion

        #endregion

        #region IEquatable<HTTPEvent> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object == null)
                return false;

            // Check if the given object is a HTTPEvent.
            var HTTPEvent = Object as HTTPEvent;
            if ((Object) HTTPEvent == null)
                return false;

            return this.Equals(HTTPEvent);

        }

        #endregion

        #region Equals(VertexId)

        /// <summary>
        /// Compares two HTTPEvents for equality.
        /// </summary>
        /// <param name="HTTPEvent">A HTTPEvent to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPEvent OtherHTTPEvent)
        {

            if ((Object) OtherHTTPEvent == null)
                return false;

            return Id.Equals(OtherHTTPEvent.Id);

        }

        #endregion

        #endregion

        #region IComparable<HTTPEvent> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            // Check if the given object is a HTTPEvent.
            var HTTPEvent = Object as HTTPEvent;
            if ((Object) HTTPEvent == null)
                throw new ArgumentException("The given object is not a HTTPEvent!");

            return CompareTo(HTTPEvent);

        }

        #endregion

        #region CompareTo(OtherHTTPEvent)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OtherHTTPEvent">Another HTTP event.</param>
        public Int32 CompareTo(HTTPEvent OtherHTTPEvent)
        {

            if ((Object) OtherHTTPEvent == null)
                throw new ArgumentNullException("HTTPEvent", "The given HTTPEvent must not be null!");

            return Id.CompareTo(OtherHTTPEvent.Id);

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            return Subevent.GetHashCode() ^ Id.GetHashCode();
        }

        #endregion

        #region ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override String ToString()
        {

            if (Subevent == null || Subevent.Trim() == "")
                return String.Format("id:{1}{0}data:{2}{0}{0}",
                                       Environment.NewLine,
                                       Id,
                                       _Data.Aggregate((a, b) => { return a + Environment.NewLine + "data: __" + b; }));

            else
                return String.Concat("id:",    Id,       Environment.NewLine,
                                     "event:", Subevent, Environment.NewLine,
                                     "data: ", _Data.Aggregate((a, b) => { return a + Environment.NewLine + "data:" + b; }), Environment.NewLine,
                                     Environment.NewLine);

        }

        #endregion

    }

}
