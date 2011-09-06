/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
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
using System.Linq;
using System.Text;
using System.Collections.Generic;

#endregion

namespace de.ahzf.Hermod
{

    public static class Extensions
    {

        #region IEnumerable<...>

        #region ForEach<T>(this myIEnumerable, myAction)

        public static void ForEach<T>(this IEnumerable<T> myIEnumerable, Action<T> myAction)
        {

            if (myIEnumerable == null)
                throw new ArgumentNullException("myIEnumerable must not be null!");

            if (myAction == null)
                throw new ArgumentNullException("myAction must not be null!");

            foreach (var _item in myIEnumerable)
                myAction(_item);

        }

        #endregion

        #region ForEach<S, T>(this myIEnumerable, mySeed, myAction)

        public static S ForEach<S, T>(this IEnumerable<T> myIEnumerable, S mySeed, Action<S, T> myAction)
        {

            if (myIEnumerable == null)
                throw new ArgumentNullException("myIEnumerable must not be null!");

            if (myAction == null)
                throw new ArgumentNullException("myAction must not be null!");

            S _R = mySeed;

            foreach (var _item in myIEnumerable)
                myAction(_R, _item);

            return _R;

        }

        #endregion

        #region Skip<T>(this myIEnumerable, myCount)

        public static IEnumerable<T> Skip<T>(this IEnumerable<T> myIEnumerable, UInt32 myCount)
        {
            return myIEnumerable.Skip((Int32)myCount);
        }

        #endregion

        #region Take<T>(this myIEnumerable, myCount)

        public static IEnumerable<T> Take<T>(this IEnumerable<T> myIEnumerable, UInt32 myCount)
        {
            return myIEnumerable.Take((Int32) myCount);
        }

        #endregion

        #region IsNullOrEmpty<T>(this myEnumerable)

        public static Boolean IsNullOrEmpty<T>(this IEnumerable<T> myEnumerable)
        {

            if (myEnumerable == null || !myEnumerable.Any())
                return true;

            return false;

        }

        #endregion

        #region IsNeitherNullNorEmpty<T>(this myEnumerable)

        public static Boolean IsNeitherNullNorEmpty<T>(this IEnumerable<T> myEnumerable)
        {

            if (myEnumerable == null || !myEnumerable.Any())
                return false;

            return true;

        }

        #endregion

        #region CountIsAtLeast<T>(this myIEnumerable, myNumberOfElements)

        public static Boolean CountIsAtLeast<T>(this IEnumerable<T> myIEnumerable, UInt64 myNumberOfElements)
        {

            if (myIEnumerable == null)
                return false;

            var _Enumerator = myIEnumerable.GetEnumerator();

            while (myNumberOfElements > 0 && _Enumerator.MoveNext())
                myNumberOfElements--;

            return (myNumberOfElements == 0 && !_Enumerator.MoveNext());

        }

        #endregion

        #region CountIsGreater<T>(this myIEnumerable, myNumberOfElements)

        public static Boolean CountIsGreater<T>(this IEnumerable<T> myIEnumerable, UInt64 myNumberOfElements)
        {

            if (myIEnumerable == null)
                return false;

            var _Enumerator = myIEnumerable.GetEnumerator();

            while (myNumberOfElements > 0 && _Enumerator.MoveNext())
                myNumberOfElements--;

            return (myNumberOfElements == 0 && _Enumerator.MoveNext());

        }

        #endregion

        #region CountIsGreaterOrEquals<T>(this myIEnumerable, myNumberOfElements)

        public static Boolean CountIsGreaterOrEquals<T>(this IEnumerable<T> myIEnumerable, UInt64 myNumberOfElements)
        {

            if (myIEnumerable == null)
                return false;

            var _Enumerator = myIEnumerable.GetEnumerator();

            while (myNumberOfElements > 0 && _Enumerator.MoveNext())
                myNumberOfElements--;

            return (myNumberOfElements == 0);

        }

        #endregion

        #endregion


        #region String...

        #region IsNullOrEmpty

        public static Boolean IsNullOrEmpty(this String myString)
        {
            return String.IsNullOrEmpty(myString);
        }

        #endregion

        #region ToBase64(myString)

        public static String ToBase64(this String myString)
        {

            try
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(myString));
            }

            catch (Exception e)
            {
                throw new Exception("Error in base64Encode" + e.Message);
            }

        }

        #endregion

        #region FromBase64(myBase64String)

        public static String FromBase64(this String myBase64String)
        {

            try
            {

                var _UTF8Decoder  = new UTF8Encoding().GetDecoder();
                var _Bytes        = Convert.FromBase64String(myBase64String);
                var _DecodedChars = new Char[_UTF8Decoder.GetCharCount(_Bytes, 0, _Bytes.Length)];
                _UTF8Decoder.GetChars(_Bytes, 0, _Bytes.Length, _DecodedChars, 0);

                return new String(_DecodedChars);

            }

            catch (Exception e)
            {
                throw new Exception("Error in base64Decode" + e.Message);
            }

        }

        #endregion

        #region EscapeForXMLandHTML(myString)

        public static String EscapeForXMLandHTML(this String myString)
        {

            if (myString == null)
                throw new ArgumentNullException("myString must not be null!");

            myString = myString.Replace("<", "&lt;");
            myString = myString.Replace(">", "&gt;");
            myString = myString.Replace("&", "&amp;");

            return myString;

        }

        #endregion

        #region ToUTF8String(this myByteArray, NumberOfBytes = 0)

        public static String ToUTF8String(this Byte[] myByteArray, Int32 NumberOfBytes = 0)
        {

            if (myByteArray == null)
                throw new ArgumentNullException("myString must not be null!");

            if (myByteArray.Length == 0)
                return String.Empty;

            if (NumberOfBytes == 0)
                return Encoding.UTF8.GetString(myByteArray);
            else
                return Encoding.UTF8.GetString(myByteArray, 0, NumberOfBytes);

        }

        #endregion

        #region ToUTF8Bytes(this myString)

        public static Byte[] ToUTF8Bytes(this String myString)
        {

            if (myString == null)
                throw new ArgumentNullException("myString must not be null!");

            return Encoding.UTF8.GetBytes(myString);

        }

        #endregion

        #endregion


        #region UNIXTime conversion

        private static DateTime _UNIXEpoch = new DateTime(1970, 1, 1, 0, 0, 0);

        public static Int64 ToUnixTimeStamp(this DateTime myDateTime)
        {
            return myDateTime.Subtract(_UNIXEpoch).Ticks;
        }

        public static DateTime FromUnixTimeStamp(this Int64 myTimestamp)
        {
            return _UNIXEpoch.AddTicks(myTimestamp);
        }

        #endregion


    }

}

