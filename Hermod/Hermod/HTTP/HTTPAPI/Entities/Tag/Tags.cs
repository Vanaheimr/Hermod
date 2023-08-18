/*
 * Copyright (c) 2014-2023 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of HTTPExtAPI <https://www.github.com/Vanaheimr/HTTPExtAPI>
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

using System.Collections;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public enum TagEdgeLabel
    {

        IsSameAs,
        IsOpposite,
        IsPartOf,
        IsLinked

    }

    public struct TagEdge
    {

        public TagEdgeLabel  Label   { get; }
        public Single        Value   { get; }

        public TagEdge(TagEdgeLabel Label,
                       Single       Value)
        {
            this.Label  = Label;
            this.Value  = Value;
        }

        public static TagEdge Create(TagEdgeLabel  Edge,
                                     Single        Value)

            => new TagEdge(Edge, Value);


    }

    public struct TagTriple : IEquatable<TagTriple>
    {

        public Tag           Tag1    { get; }
        public TagEdgeLabel  Label   { get; }
        public Single        Value   { get; }
        public Tag           Tag2    { get; }

        public TagTriple(Tag           Tag1,
                         TagEdgeLabel  Label,
                         Single        Value,
                         Tag           Tag2)
        {

            this.Tag1   = Tag1;
            this.Label  = Label;
            this.Value  = Value;
            this.Tag2   = Tag2;

        }

        public JToken ToJSON(InfoStatus ExpandTags = InfoStatus.ShowIdOnly)

            => ExpandTags.Switch(this,

                   info => new JObject(new JProperty("tag1",   info.Tag1. ToJSON(true, ExpandDescription: InfoStatus.Expanded)), 
                                       new JProperty("label",  info.Label.ToString()),
                                       new JProperty("value",  info.Value),
                                       new JProperty("tag2",   info.Tag2. ToJSON(true, ExpandDescription: InfoStatus.Expanded))),

                   info => new JArray(info.Tag1.Id.ToString(),
                                      info.Label.  ToString(),
                                      info.Value,
                                      info.Tag2.Id.ToString()));


        #region IComparable<TagTriple> Members

        //#region CompareTo(Object)

        ///// <summary>
        ///// Compares two instances of this object.
        ///// </summary>
        ///// <param name="Object">An object to compare with.</param>
        //public override Int32 CompareTo(Object Object)
        //{

        //    if (Object == null)
        //        throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

        //    var EVSE_Operator = Object as TagTriple;
        //    if ((Object) EVSE_Operator == null)
        //        throw new ArgumentException("The given object is not a data source!");

        //    return CompareTo(EVSE_Operator);

        //}

        //#endregion

        //#region CompareTo(TagTriple)

        ///// <summary>
        ///// Compares two instances of this object.
        ///// </summary>
        ///// <param name="TagTriple">A data source object to compare with.</param>
        //public Int32 CompareTo(TagTriple TagTriple)
        //{

        //    if ((Object) TagTriple == null)
        //        throw new ArgumentNullException(nameof(TagTriple), "The given data source must not be null!");

        //    return Id.CompareTo(TagTriple.Id);

        //}

        //#endregion

        #endregion

        #region IEquatable<TagTriple> Members

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

            if (!(Object is TagTriple _TagTriple))
                return false;

            return Equals(_TagTriple);

        }

        #endregion

        #region Equals(TagTriple)

        /// <summary>
        /// Compares two data sources for equality.
        /// </summary>
        /// <param name="TagTriple">A data source to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(TagTriple TagTriple)
        {

            if ((Object) TagTriple == null)
                return false;

            return Tag1. Equals(TagTriple.Tag1)  &&
                   Label.Equals(TagTriple.Label) &&
                   Value.Equals(TagTriple.Value) &&
                   Tag2. Equals(TagTriple.Tag2);

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Get the hashcode of this object.
        /// </summary>
        public override Int32 GetHashCode()
        {
            unchecked
            {

                return Tag1. GetHashCode() * 7 ^
                       Label.GetHashCode() * 5 ^
                       Value.GetHashCode() * 3 ^
                       Tag2. GetHashCode();

            }
        }

        #endregion

        #region ToString()

        /// <summary>
        /// Get a string representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(Tag1, " --", Label, "(", Value, ")--> ", Tag2);

        #endregion

    }

    public struct TagRelevance
    {

        public Tag           Tag     { get; }
        public Single        Value   { get; }

        public TagRelevance(Tag     Tag,
                            Single  Value)
        {
            this.Tag    = Tag;
            this.Value  = Value;
        }


        public JToken ToJSON(InfoStatus ExpandTags = InfoStatus.ShowIdOnly)

            => ExpandTags.Switch(this,

                   info => new JObject(new JProperty("tag",       info.Tag.ToJSON(true, ExpandDescription: InfoStatus.Expanded)),
                                       new JProperty("relevance", info.Value)),

                   info => new JArray(info.Tag.Id.ToString(),
                                      info.Value));

    }

    public struct TagInfo
    {

        public TagEdgeLabel  Label   { get; }
        public Tag           Tag     { get; }
        public Single        Value   { get; }

        public TagInfo(TagEdgeLabel  Label,
                       Tag           Tag,
                       Single        Value)
        {
            this.Label  = Label;
            this.Tag    = Tag;
            this.Value  = Value;
        }


        public JToken ToJSON(InfoStatus ExpandTags = InfoStatus.ShowIdOnly)

            => ExpandTags.Switch(this,

                   info => new JObject(new JProperty("edge",  info.Label.  ToString()),
                                       new JProperty("tag",   info.Tag.ToJSON(true, ExpandDescription: InfoStatus.Expanded)),
                                       new JProperty("value", info.Value)),

                   info => new JArray(info.Label.  ToString(),
                                      info.Tag.Id.ToString(),
                                      info.Value));

    }

    public static class TagsExtensions
    {

        public static JArray ToJSON(this IEnumerable<TagInfo> TagInfos,
                                    InfoStatus                ExpandTags = InfoStatus.ShowIdOnly)

            => TagInfos == null
                   ? new JArray()
                   : new JArray(TagInfos.Select(taginfo => taginfo.ToJSON(ExpandTags)));

    }


    /// <summary>
    /// A collection of Open Data tags.
    /// </summary>
    public class Tags : IEnumerable<TagInfo>
    {

        #region Data

        private readonly Dictionary<Tag, TagEdge> _Tags;

        #endregion

        #region Properties

        public UInt32 Count
            => (UInt32) _Tags.Count;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new collection of tags.
        /// </summary>
        /// <param name="Tags">An enumeration of tags.</param>
        public Tags(IEnumerable<KeyValuePair<Tag, TagEdge>> Tags = null)
        {

            _Tags = new Dictionary<Tag, TagEdge>();

            if (Tags != null)
                foreach (var tag in Tags)
                    _Tags.Add(tag.Key, tag.Value);

        }

        #endregion


        #region ToJSON(...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        public JArray ToJSON()
            => ToJSON(ExpandTags:  InfoStatus.Hidden);


        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        public JArray ToJSON(InfoStatus  ExpandTags   = InfoStatus.Hidden)

            => _Tags.Count > 0
                   ? _Tags.Select(tag => new TagInfo(tag.Value.Label, tag.Key, tag.Value.Value)).ToJSON(ExpandTags)
                   : new JArray();

        #endregion

        #region (static) TryParseJSON(JSONArray, ..., out Tags, out ErrorResponse)

        /// <summary>
        /// Try to parse the given tag JSON.
        /// </summary>
        /// <param name="JSONArray">A JSON array.</param>
        /// <param name="Tags">The parsed tags.</param>
        /// <param name="ErrorResponse">An error message.</param>
        public static Boolean TryParseJSON(JArray      JSONArray,
                                           out Tags    Tags,
                                           out String  ErrorResponse)
        {

            try
            {

                ErrorResponse = null;

                if (JSONArray == null)
                {
                    ErrorResponse  = "The given JSON object must not be null!";
                    Tags           = null;
                    return false;
                }

                if (JSONArray.HasValues)
                {

                    var _tags = new Dictionary<Tag, TagEdge>();

                    foreach (var item in JSONArray)
                    {

                        try
                        {

                            if (item is JArray array && array.Children().Count() == 2)
                            {

                                var TagId     = item[0].Value<String>();
                                var TagObject = item[0] as JObject;

                                if (TagObject != null && Tag.TryParseJSON(item[0] as JObject,
                                                                          out Tag Tag2,
                                                                          out ErrorResponse))
                                {
                                    _tags.Add(Tag2,
                                              new TagEdge(TagEdgeLabel.IsSameAs, item[1].Value<Single>()));
                                }

                                else
                                {
                                    _tags.Add(new Tag(Tag_Id.Parse(TagId)),
                                              new TagEdge(TagEdgeLabel.IsSameAs, item[1].Value<Single>()));
                                }

                            }

                        }
                        catch (Exception e)
                        {
                            DebugX.Log("Invalid tag found: " + item);
                        }

                    }

                    Tags = new Tags(_tags);
                    return true;

                }

            }
            catch (Exception e)
            {
                DebugX.Log("Invalid tags found: " + JSONArray);
                ErrorResponse = e.Message;
            }

            Tags = null;
            return false;

        }

        #endregion


        public IEnumerator<TagInfo> GetEnumerator()
            => _Tags.Select(tag => new TagInfo(tag.Value.Label, tag.Key, tag.Value.Value)).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => _Tags.Select(tag => new TagInfo(tag.Value.Label, tag.Key, tag.Value.Value)).GetEnumerator();



        #region ToBuilder()

        /// <summary>
        /// Return a builder for this tag.
        /// </summary>
        public Builder ToBuilder()

            => new Builder(_Tags);

        #endregion

        #region (class) Builder

        /// <summary>
        /// A tag builder.
        /// </summary>
        public class Builder : IEnumerable<TagInfo>
        {

            #region Data

            private readonly Dictionary<Tag, TagEdge> _Tags;

            #endregion

            #region Properties

            public UInt32 Count
            => (UInt32) _Tags.Count;

            #endregion

            #region Constructor(s)

            /// <summary>
            /// Create a new tag builder.
            /// </summary>
            public Builder(IEnumerable<KeyValuePair<Tag, TagEdge>> Tags = null)
            {

                _Tags = new Dictionary<Tag, TagEdge>();

                if (Tags != null)
                    foreach (var tag in Tags)
                        _Tags.Add(tag.Key, tag.Value);

            }

            #endregion


            //public Builder Add(Tag Tag, TagRelation Value)
            //{
            //    _Tags.Add(Tag, Value);
            //    return this;
            //}

            public Builder Add(TagEdgeLabel Edge, Tag Tag, Single Value)
            {
                _Tags.Add(Tag, new TagEdge(Edge, Value));
                return this;
            }


            public IEnumerator<TagInfo> GetEnumerator()
            => _Tags.Select(tag => new TagInfo(tag.Value.Label, tag.Key, tag.Value.Value)).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator()
                => _Tags.Select(tag => new TagInfo(tag.Value.Label, tag.Key, tag.Value.Value)).GetEnumerator();


            #region ToImmutable

            /// <summary>
            /// Return an immutable version of the tag.
            /// </summary>
            public static implicit operator Tags(Builder Builder)

                => Builder?.ToImmutable;


            /// <summary>
            /// Return an immutable version of the tag.
            /// </summary>
            public Tags ToImmutable

                => new Tags(_Tags);

            #endregion

        }

        #endregion

    }

}
