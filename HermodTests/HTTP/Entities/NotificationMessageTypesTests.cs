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

#region Usings

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Hermod.HTTP.Notifications;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.Entities
{

    /// <summary>
    /// The message types a notification is sent for: added, removed, and what
    /// is said of it - and when two notifications are equal.
    /// </summary>
    /// <remarks>
    /// Both Remove methods of ANotification added the message types they were
    /// to remove - the one of a list only those it did not have yet - and said
    /// so as a removal.
    /// </remarks>
    [TestFixture]
    public class NotificationMessageTypesTests
    {

        private static readonly NotificationMessageType  UserAdded    = NotificationMessageType.Parse("userAdded");
        private static readonly NotificationMessageType  UserRemoved  = NotificationMessageType.Parse("userRemoved");
        private static readonly NotificationMessageType  KeyChanged   = NotificationMessageType.Parse("keyChanged");


        #region RemovingATypeTakesItAway()

        [Test]
        public void RemovingATypeTakesItAway()
        {

            var notification = new TestNotification("a", UserAdded, UserRemoved);
            var said         = 0;

            notification.Remove(UserAdded, () => said++);

            Assert.That(notification.NotificationMessageTypes, Is.EquivalentTo(new[] { UserRemoved }));
            Assert.That(said, Is.EqualTo(1));

            notification.Remove(UserAdded, () => said++);

            Assert.That(notification.NotificationMessageTypes, Is.EquivalentTo(new[] { UserRemoved }));
            Assert.That(said, Is.EqualTo(1), "a type it did not have was said to be removed");

        }

        #endregion

        #region RemovingTypesTakesAwayThoseItHas()

        [Test]
        public void RemovingTypesTakesAwayThoseItHas()
        {

            var notification = new TestNotification("a", UserAdded, UserRemoved);
            var said         = 0;

            notification.Remove([ UserAdded, KeyChanged ], () => said++);

            Assert.That(notification.NotificationMessageTypes, Is.EquivalentTo(new[] { UserRemoved }));
            Assert.That(said, Is.EqualTo(1));

            notification.Remove([ KeyChanged ], () => said++);

            Assert.That(notification.NotificationMessageTypes, Is.EquivalentTo(new[] { UserRemoved }));
            Assert.That(said, Is.EqualTo(1), "types it did not have were said to be removed");

        }

        #endregion

        #region EqualsOfAnObjectSaysWhatEqualityOperatorSays()

        /// <summary>
        /// == went by Equals(ANotification), object.Equals by reference: a
        /// collection of objects found a notification == said was there
        /// missing.
        /// </summary>
        [Test]
        public void EqualsOfAnObjectSaysWhatEqualityOperatorSays()
        {

            var one     = new TestNotification("a", UserAdded);
            var same    = new TestNotification("a", KeyChanged);
            var another = new TestNotification("b", UserAdded);

            Assert.Multiple(() => {
                Assert.That(one == same,                    Is.True);
                Assert.That(one.Equals((Object) same),      Is.True);
                Assert.That(one.GetHashCode(),              Is.EqualTo(same.GetHashCode()));
                Assert.That(one == another,                 Is.False);
                Assert.That(one.Equals((Object) another),   Is.False);
                Assert.That(one.Equals((Object) "a"),       Is.False);
                Assert.That(one.Equals((Object?) null),     Is.False);
            });

        }

        #endregion


        #region (private) TestNotification

        /// <summary>
        /// A notification of nothing in particular, equal to another by its sort key,
        /// as every notification of Hermod is.
        /// </summary>
        private sealed class TestNotification(String                            SortKey,
                                              params NotificationMessageType[]  Types)

            : ANotification(Types, null, SortKey)

        {

            public override JObject  ToJSON(Boolean Embedded = false)
                => new ();

            public override Boolean  OptionalEquals(ANotification other)
                => Equals(other);

            public override Int32    CompareTo(ANotification? other)
                => String.Compare(SortKey, other?.SortKey, StringComparison.Ordinal);

            public override Int32    CompareTo(Object? obj)
                => CompareTo(obj as ANotification);

            public override Boolean  Equals(ANotification? other)
                => other is not null && SortKey == other.SortKey;

            public override Int32    GetHashCode()
                => base.GetHashCode();

        }

        #endregion

    }

}
