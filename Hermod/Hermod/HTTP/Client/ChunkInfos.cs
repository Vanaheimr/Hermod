/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{
    public class ChunkInfos
    {

        #region Properties

        public UInt32                             Length        { get; }

        public Dictionary<String, List<String>>?  Extentions    { get; }

        #endregion

        #region Constructor(s)

        private ChunkInfos(UInt32                             Length,
                           Dictionary<String, List<String>>?  Extentions)
        {
            this.Length     = Length;
            this.Extentions = Extentions;
        }

        #endregion


        #region (static) Parse(TESourceBlock, Position, TELength)

        public static ChunkInfos Parse(Byte[] TESourceBlock, UInt32 Position, UInt32 TELength)
        {

            if (TELength < 1)
            {
                DebugX.Log("TE Block Length(" + Position + "," + TELength + ")");
                return new ChunkInfos(0, null);
            }

            var TEBlock = new Byte[TELength];
            Array.Copy(TESourceBlock, Position, TEBlock, 0, TELength);

            var len = 0U;
            Dictionary<String, List<String>>? extentions = null;

            try
            {

                var chunkHeader = TEBlock.ToUTF8String()?.Split(';');

                if (chunkHeader != null)
                {

                    if (chunkHeader[0].IsNotNullOrEmpty())
                    {
                        // Hex-String
                        len = Convert.ToUInt32(chunkHeader[0], 16);
                    }

                    if (chunkHeader.Length > 1)
                    {

                        extentions = new Dictionary<String, List<String>>();

                        for (var i = 1; i < chunkHeader.Length; i++)
                        {

                            var tuple = chunkHeader[i]?.Split('=');

                            if (tuple != null && tuple.Length > 0 && tuple[0].IsNotNullOrEmpty())
                            {

                                if (!extentions.ContainsKey(tuple[0]))
                                    extentions.Add(tuple[0], new List<String>());

                                extentions[tuple[0]]!.Add(tuple[1] ?? "");

                            }

                        }

                    }

                }

                //ToDo: Process Chunk Extensions!

            }
            catch (Exception ex)
            {
                DebugX.Log("TE Block Length exception (" + TEBlock.ToUTF8String() + "): " + ex.Message);
            }

            return new ChunkInfos(len,
                                  extentions);

        }

        #endregion


    }

}
