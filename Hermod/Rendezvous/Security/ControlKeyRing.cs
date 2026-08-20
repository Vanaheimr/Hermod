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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using Org.BouncyCastle.Crypto.Parameters;

#endregion

// SYSLIB5006: ML-DSA (FIPS 204) is still "experimental" within .NET 10 - suppressed
// for this file, as the whole feature is built on it.
#pragma warning disable SYSLIB5006

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// The public keys that may authorize rendezvous control commands.
    ///
    /// Keys can be added and removed at any time, also while the control
    /// endpoint is serving requests - a compromised key has to be revocable
    /// without a restart.
    /// </summary>
    public sealed class ControlKeyRing
    {

        #region Data

        private readonly ConcurrentDictionary<String, ControlKey> keys = [];

        #endregion

        #region Properties

        /// <summary>
        /// All known keys, valid or not.
        /// </summary>
        public IEnumerable<ControlKey>  Keys
            => keys.Values;

        /// <summary>
        /// The number of known keys.
        /// </summary>
        public Int32                    Count
            => keys.Count;

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever a key was added.
        /// </summary>
        public event Action<ControlKey>? OnKeyAdded;

        /// <summary>
        /// An event fired whenever a key was removed.
        /// </summary>
        public event Action<ControlKey>? OnKeyRemoved;

        #endregion


        #region Add(Key)

        /// <summary>
        /// Add the given key, replacing an existing key of the same identification.
        /// </summary>
        /// <param name="Key">A control key.</param>
        public ControlKey Add(ControlKey Key)
        {

            ArgumentNullException.ThrowIfNull(Key);

            keys[Key.Id] = Key;

            OnKeyAdded?.Invoke(Key);

            return Key;

        }

        #endregion

        #region Add(Id, Algorithm, PublicKey, NotBefore = null, NotAfter = null, ...)

        /// <summary>
        /// Add a key from a raw public key.
        /// </summary>
        /// <param name="Id">The unique identification of the key.</param>
        /// <param name="KeyType">The type of the key.</param>
        /// <param name="PublicKey">The raw public key.</param>
        /// <param name="NotBefore">An optional timestamp before which the key is not valid.</param>
        /// <param name="NotAfter">An optional timestamp after which the key is not valid.</param>
        /// <param name="Description">An optional description.</param>
        /// <param name="IsAdministrator">Whether this key may also close the rendezvous of somebody else.</param>
        /// <param name="Created">When this key was configured, the current time is used otherwise.</param>
        /// <param name="CreatedBy">Who configured this key.</param>
        public ControlKey Add(String              Id,
                              SignatureKeyType    KeyType,
                              Byte[]              PublicKey,
                              DateTimeOffset?     NotBefore         = null,
                              DateTimeOffset?     NotAfter          = null,
                              String?             Description       = null,
                              Boolean             IsAdministrator   = false,
                              DateTimeOffset?     Created           = null,
                              String?             CreatedBy         = null)

            => Add(new ControlKey(Id, KeyType, PublicKey, NotBefore, NotAfter, Description, IsAdministrator, Created, CreatedBy));

        #endregion

        #region AddEd25519(Id, PublicKey, NotBefore = null, NotAfter = null, ...)

        /// <summary>
        /// Add an Ed25519 key.
        /// </summary>
        /// <param name="Id">The unique identification of the key.</param>
        /// <param name="PublicKey">The Ed25519 public key parameters.</param>
        /// <param name="NotBefore">An optional timestamp before which the key is not valid.</param>
        /// <param name="NotAfter">An optional timestamp after which the key is not valid.</param>
        /// <param name="Description">An optional description.</param>
        /// <param name="IsAdministrator">Whether this key may also close the rendezvous of somebody else.</param>
        /// <param name="Created">When this key was configured, the current time is used otherwise.</param>
        /// <param name="CreatedBy">Who configured this key.</param>
        public ControlKey AddEd25519(String                        Id,
                                     Ed25519PublicKeyParameters    PublicKey,
                                     DateTimeOffset?               NotBefore         = null,
                                     DateTimeOffset?               NotAfter          = null,
                                     String?                       Description       = null,
                                     Boolean                       IsAdministrator   = false,
                                     DateTimeOffset?               Created           = null,
                                     String?                       CreatedBy         = null)
        {

            ArgumentNullException.ThrowIfNull(PublicKey);

            return Add(
                       new ControlKey(
                           Id,
                           SignatureKeyType.Ed25519,
                           PublicKey.GetEncoded(),
                           NotBefore,
                           NotAfter,
                           Description,
                           IsAdministrator,
                           Created,
                           CreatedBy
                       )
                   );

        }

        #endregion

        #region AddEd448  (Id, PublicKey, NotBefore = null, NotAfter = null, ...)

        /// <summary>
        /// Add an Ed448 key.
        /// </summary>
        /// <param name="Id">The unique identification of the key.</param>
        /// <param name="PublicKey">The Ed448 public key parameters.</param>
        /// <param name="NotBefore">An optional timestamp before which the key is not valid.</param>
        /// <param name="NotAfter">An optional timestamp after which the key is not valid.</param>
        /// <param name="Description">An optional description.</param>
        /// <param name="IsAdministrator">Whether this key may also close the rendezvous of somebody else.</param>
        /// <param name="Created">When this key was configured, the current time is used otherwise.</param>
        /// <param name="CreatedBy">Who configured this key.</param>
        public ControlKey AddEd448(String                      Id,
                                   Ed448PublicKeyParameters    PublicKey,
                                   DateTimeOffset?             NotBefore         = null,
                                   DateTimeOffset?             NotAfter          = null,
                                   String?                     Description       = null,
                                   Boolean                     IsAdministrator   = false,
                                   DateTimeOffset?             Created           = null,
                                   String?                     CreatedBy         = null)
        {

            ArgumentNullException.ThrowIfNull(PublicKey);

            return Add(
                       new ControlKey(
                           Id,
                           SignatureKeyType.Ed448,
                           PublicKey.GetEncoded(),
                           NotBefore,
                           NotAfter,
                           Description,
                           IsAdministrator,
                           Created,
                           CreatedBy
                       )
                   );

        }

        #endregion

        #region AddMLDsa  (Id, PublicKey, NotBefore = null, NotAfter = null, ...)

        /// <summary>
        /// Add an ML-DSA key.
        /// </summary>
        /// <param name="Id">The unique identification of the key.</param>
        /// <param name="PublicKey">An ML-DSA key, only its public key is used.</param>
        /// <param name="NotBefore">An optional timestamp before which the key is not valid.</param>
        /// <param name="NotAfter">An optional timestamp after which the key is not valid.</param>
        /// <param name="Description">An optional description.</param>
        /// <param name="IsAdministrator">Whether this key may also close the rendezvous of somebody else.</param>
        /// <param name="Created">When this key was configured, the current time is used otherwise.</param>
        /// <param name="CreatedBy">Who configured this key.</param>
        public ControlKey AddMLDsa(String            Id,
                                   MLDsa             PublicKey,
                                   DateTimeOffset?   NotBefore         = null,
                                   DateTimeOffset?   NotAfter          = null,
                                   String?           Description       = null,
                                   Boolean           IsAdministrator   = false,
                                   DateTimeOffset?   Created           = null,
                                   String?           CreatedBy         = null)

            => Add(ControlKey.FromMLDsa(Id, PublicKey, NotBefore, NotAfter, Description, IsAdministrator, Created, CreatedBy));

        #endregion


        #region Remove(Id)

        /// <summary>
        /// Remove the key of the given identification.
        /// </summary>
        /// <param name="Id">The unique identification of a key.</param>
        public Boolean Remove(String Id)
        {

            if (keys.TryRemove(Id, out var removedKey))
            {
                OnKeyRemoved?.Invoke(removedKey);
                return true;
            }

            return false;

        }

        #endregion

        #region RemoveExpired(Timestamp)

        /// <summary>
        /// Remove all keys that are no longer valid at the given timestamp,
        /// and return how many were removed.
        /// </summary>
        /// <param name="Timestamp">A timestamp.</param>
        public Int32 RemoveExpired(DateTimeOffset Timestamp)
        {

            var removed = 0;

            foreach (var key in keys.Values)
            {
                if (key.NotAfter.HasValue && Timestamp > key.NotAfter.Value && Remove(key.Id))
                    removed++;
            }

            return removed;

        }

        #endregion

        #region Clear()

        /// <summary>
        /// Remove all keys. The control endpoint will then reject everything,
        /// as at least one valid signature is mandatory.
        /// </summary>
        public void Clear()
        {

            foreach (var key in keys.Values)
                Remove(key.Id);

        }

        #endregion


        #region TryGet(Id, out Key)

        /// <summary>
        /// Try to get the key of the given identification.
        /// </summary>
        /// <param name="Id">The unique identification of a key.</param>
        /// <param name="Key">The key.</param>
        public Boolean TryGet(String                                Id,
                              [NotNullWhen(true)] out ControlKey?   Key)

            => keys.TryGetValue(Id, out Key);

        #endregion

        #region ValidKeysAt(Timestamp)

        /// <summary>
        /// Return all keys that are valid at the given timestamp.
        /// </summary>
        /// <param name="Timestamp">A timestamp.</param>
        public IEnumerable<ControlKey> ValidKeysAt(DateTimeOffset Timestamp)

            => keys.Values.Where(key => key.IsValidAt(Timestamp));

        #endregion

    }

}

#pragma warning restore SYSLIB5006
