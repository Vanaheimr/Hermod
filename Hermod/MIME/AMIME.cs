/*
 * Copyright (c) 2010-2021, Achim Friedland <achim.friedland@graphdefined.com>
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

using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;

using Newtonsoft.Json.Linq;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Illias.ConsoleLog;
using System.Text;

#endregion


namespace org.GraphDefined.Vanaheimr.Hermod.MIME
{

    public static class Ext
    {


        /// <summary>Looks for the next occurrence of a sequence in a byte array</summary>
        /// <param name="array">Array that will be scanned</param>
        /// <param name="start">Index in the array at which scanning will begin</param>
        /// <param name="sequence">Sequence the array will be scanned for</param>
        /// <returns>
        ///   The index of the next occurrence of the sequence of -1 if not found
        /// </returns>
        public static int findSequence(this byte[] array, int start, byte[] sequence)
        {
            int end = array.Length - sequence.Length; // past here no match is possible
            byte firstByte = sequence[0]; // cached to tell compiler there's no aliasing

            while (start < end)
            {
                // scan for first byte only. compiler-friendly.
                if (array[start] == firstByte)
                {
                    // scan for rest of sequence
                    for (int offset = 1; offset < sequence.Length; ++offset)
                    {
                        if (array[start + offset] != sequence[offset])
                        {
                            break; // mismatch? continue scanning with next byte
                        }
                        else if (offset == sequence.Length - 1)
                        {
                            return start; // all bytes matched!
                        }
                    }
                }
                ++start;
            }

            // end of array reached without match
            return -1;
        }

    }


    public class MIME
    {

        private readonly static String[] Splitter1 = new String[] { "\r\n" };
        private readonly static Char[]   Splitter2 = new Char[]   { ';' };

        public Dictionary<String, Object>  Headers       { get; }
        public Byte[]                      Content       { get; }
        public HTTPContentType             ContentType   { get; }
        public String                      Name          { get; }
        public String                      Filename      { get; }

        public MIME(String           Part,
                    HTTPContentType  ContentType)
        {
        }

        public MIME(Dictionary<String, Object>  Headers,
                    Byte[]                      Content)
        {

            this.Headers = Headers ?? throw new ArgumentNullException(nameof(Headers), "The given headers must not be null or empty!");
            this.Content = Content ?? throw new ArgumentNullException(nameof(Headers), "The given content must not be null or empty!");


            if (Headers.TryGetValue("Content-Type", out Object Value) &&
                HTTPContentType.TryParseString(Value as String, out HTTPContentType ContentType))
            {
                Headers["Content-Type"] = ContentType;
                this.ContentType = ContentType;
            }

            if (Headers.TryGetValue("Content-Disposition", out Value) &&
                Value is String)
            {

                // Content-Disposition: form-data; name="file"; filename="OffenesJena.svg"

                var Parts = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
                var index = 0;

                foreach (var part in (Value as String).Split(Splitter2, StringSplitOptions.RemoveEmptyEntries))
                {

                    index = part.IndexOf("=");

                    if (index > 0)
                        Parts.Add(part.Substring(0, part.IndexOf("=")).Trim(),
                                  part.Substring(part.IndexOf("=") + 1).Trim());

                    else
                        Parts.Add(part, "");

                }

                if (Parts.TryGetValue("name",     out String Text))
                    this.Name     = Text.Substring(1, Text.Length - 2);

                if (Parts.TryGetValue("filename", out Text))
                    this.Filename = Text.Substring(1, Text.Length - 2);

            }

        }


        public static MIME Parse(Byte[] Bytes)
        {

            var Sequence  = "\r\n\r\n".ToUTF8Bytes();
            var Found     = Bytes.findSequence(0, Sequence);
            var Headers   = Encoding.UTF8.GetString(Bytes, 0, Found).
                                          Split(Splitter1, StringSplitOptions.RemoveEmptyEntries).
                                          ToDictionary(line => line.Substring(0, line.IndexOf(":")    ).Trim(),
                                                       line => line.Substring(   line.IndexOf(":") + 1).Trim() as Object,
                                                       StringComparer.OrdinalIgnoreCase);

            var Content   = new Byte[Bytes.Length - 4 - Found];
            Array.Copy(Bytes, Found + 4, Content, 0, Content.Length);

            return new MIME(Headers, Content);

        }


    }

    public class Multipart
    {

        private readonly Dictionary<String, MIME> _Parts;

        public Multipart(IEnumerable<MIME> MIMEParts)
        {

            _Parts = new Dictionary<String, MIME>(StringComparer.OrdinalIgnoreCase);

            foreach (var MIMEPart in MIMEParts)
                _Parts.Add(MIMEPart.Name, MIMEPart);

        }

        public MIME this[String Name]
            => _Parts[Name];

        public Boolean TryGet(String Name, out MIME MIMEPart)
            => _Parts.TryGetValue(Name, out MIMEPart);


        public static Multipart Parse(Byte[] Bytes,
                                      String MIMEBoundary)
        {

            var Sequence    = ("--" + MIMEBoundary + '\r' + '\n').ToUTF8Bytes();
            var EndSequence = ("--" + MIMEBoundary + "--").ToUTF8Bytes();

            var Found  = 1;
            var Parts  = new List<MIME>();
            var Start  = Sequence.Length;

            do
            {

                Found = Bytes.findSequence(Start, Sequence);

                if (Found < 0)
                    Found = Bytes.findSequence(Start, EndSequence);

                if (Found > 0)
                {
                    var ArrayPart = new Byte[Found - Start];
                    Array.Copy(Bytes, Start, ArrayPart, 0, Found - Start);
                    Parts.Add(MIME.Parse(ArrayPart));
                    Start = Found + Sequence.Length;
                }

            } while (Found > 0);

            return new Multipart(Parts);

        }


    }

}
