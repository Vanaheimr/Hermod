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
using System.IO;

using de.ahzf.Hermod.HTTP.Common;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    public class HTTPResponse
    {

        #region Properties

        #region ResponseHeader

        private HTTPResponseHeader  _ResponseHeader;

        public HTTPResponseHeader ResponseHeader
        {
            get
            {
                return _ResponseHeader;
            }
        }

        #endregion

        #region Content

        private Byte[] _Content = null;

        public Byte[] Content
        {
            get
            {
                return _Content;
            }
        }

        #endregion

        #region ContentStream

        private Stream _ContentStream = null;

        public Stream ContentStream
        {
            get
            {
                return _ContentStream;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPResponse(myResponseHeader)

        public HTTPResponse(HTTPResponseHeader myResponseHeader)
        {
            _ResponseHeader = myResponseHeader;
            _Content        = null;
            _ContentStream  = null;
        }

        #endregion

        #region HTTPResponse(myResponseHeader, myByteArray)

        public HTTPResponse(HTTPResponseHeader myResponseHeader, Byte[] myByteArray)
        {

            _ResponseHeader = myResponseHeader;
            _Content        = myByteArray;
            _ContentStream  = null;

            _ResponseHeader.ContentLength = (UInt64) myByteArray.Length;

        }

        #endregion

        #region HTTPResponse(myResponseHeader, myContentStream)

        public HTTPResponse(HTTPResponseHeader myResponseHeader, Stream myContentStream)
        {

            _ResponseHeader = myResponseHeader;
            _Content        = null;
            _ContentStream  = myContentStream;

            if (myContentStream.CanSeek)
                _ResponseHeader.ContentLength = (UInt64) myContentStream.Length;

            else
            {
                //ToDo: Do chunked encoding!
            }

        }

        #endregion

        #endregion

    }

}
