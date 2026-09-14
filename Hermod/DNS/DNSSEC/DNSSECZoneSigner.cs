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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// What a zone is hashed with when its authenticated denial is NSEC3
    /// (RFC 5155 §4).
    /// </summary>
    /// <param name="Salt">The salt; empty for none, which is what RFC 9276 §3.1 now recommends.</param>
    /// <param name="Iterations">Extra hash rounds; RFC 9276 §3.1 says zero.</param>
    /// <param name="OptOut">
    /// Whether insecure delegations are left out of the chain (RFC 5155 §6). It
    /// makes a zone of mostly-unsigned delegations far smaller and buys nothing
    /// for a zone that has none.
    /// </param>
    public sealed record NSEC3Parameters(Byte[]   Salt,
                                         UInt16   Iterations   = 0,
                                         Boolean  OptOut       = false)
    {

        /// <summary>No salt and no extra iterations — RFC 9276 §3.1's recommendation.</summary>
        public static NSEC3Parameters Recommended
            => new ([]);

    }


    /// <summary>
    /// Signing a zone: RRSIGs over its RRsets and a chain of authenticated denial
    /// over its names (RFC 4035 §2, RFC 5155 §7).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other direction from <see cref="DNSSECValidator"/>, and the half the
    /// authoritative server never had — it could serve a zone somebody else had
    /// signed and nothing more.
    /// </para>
    /// <para>
    /// What it does not do is replace BIND as the source of the conformance
    /// suite's fixtures, and that is deliberate rather than unfinished. A
    /// validator measured against signatures made by the same code is measuring
    /// itself; the fixtures stay BIND's so that they keep being somebody else's
    /// numbers. The judgement runs the other way instead: BIND's
    /// <c>dnssec-verify</c> is given zones this signs.
    /// </para>
    /// </remarks>
    public static class DNSSECZoneSigner
    {

        #region (static) SignRRSet(RRSet, Key, Inception, Expiration)

        /// <summary>
        /// One RRSIG over one RRset (RFC 4034 §3).
        /// </summary>
        /// <param name="RRSet">Records that share an owner name, class and type.</param>
        /// <param name="Key">The key to sign with.</param>
        /// <param name="Inception">When the signature becomes valid.</param>
        /// <param name="Expiration">When it stops being valid.</param>
        public static RRSIG SignRRSet(IEnumerable<IDNSResourceRecord>  RRSet,
                                      DNSSECSigningKey                 Key,
                                      DateTime                         Inception,
                                      DateTime                         Expiration)
        {

            var records = RRSet.ToArray();

            if (records.Length == 0)
                throw new ArgumentException("There is nothing to sign.", nameof(RRSet));

            var owner = records[0].DomainName.FullName;
            var type  = records[0].Type;

            if (records.Any(record => record.Type != type))
                throw new ArgumentException("An RRset holds one type; these do not.", nameof(RRSet));

            if (records.Any(record => !String.Equals(record.DomainName.FullName, owner, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("An RRset holds one owner name; these do not.", nameof(RRSet));

            // RFC 2181 §5.2: every record of an RRset has the same TTL, and it is
            // that TTL the signature is taken over. Taking the smallest is what a
            // signer does with a set that disagrees with itself.
            var originalTTL = (UInt32) records.Min(record => record.TimeToLive).TotalSeconds;

            // The signature covers the RRSIG's own RDATA, so the record has to
            // exist before it can be signed. Everything but the signature field is
            // already known.
            var template    = new RRSIG(
                                  DomainName.ParseLenient(owner),
                                  DNSQueryClasses.IN,
                                  TimeSpan.FromSeconds(originalTTL),
                                  type,
                                  Key.Algorithm,
                                  DNSSECCanonical.LabelCount(owner),
                                  originalTTL,
                                  (UInt32) new DateTimeOffset(Expiration, TimeSpan.Zero).ToUnixTimeSeconds(),
                                  (UInt32) new DateTimeOffset(Inception,  TimeSpan.Zero).ToUnixTimeSeconds(),
                                  Key.KeyTag,
                                  DomainName.ParseLenient(Key.DNSKEY.DomainName.FullName),
                                  []
                              );

            return new RRSIG(
                       DomainName.ParseLenient(owner),
                       DNSQueryClasses.IN,
                       TimeSpan.FromSeconds(originalTTL),
                       template.TypeCovered,
                       template.Algorithm,
                       template.Labels,
                       template.OriginalTTL,
                       template.SignatureExpiration,
                       template.SignatureInception,
                       template.KeyTag,
                       template.SignerName,
                       Key.Sign(DNSSECCanonical.SignedData(records, template))
                   );

        }

        #endregion

        #region (static) Sign(Records, Origin, Keys, Inception = null, Expiration = null, NSEC3 = null)

        /// <summary>
        /// Sign a whole zone: every authoritative RRset gets an RRSIG and every
        /// name gets its place in the chain of denial, with the DNSKEY RRset
        /// signed by the key signing keys and everything else by the zone signing
        /// keys (RFC 4035 §2).
        /// </summary>
        /// <param name="Records">The unsigned zone.</param>
        /// <param name="Origin">The zone apex.</param>
        /// <param name="Keys">One or more keys; those with the Secure Entry Point flag sign the DNSKEY RRset.</param>
        /// <param name="Inception">When the signatures become valid; an hour ago by default, for clock skew.</param>
        /// <param name="Expiration">When they stop; thirty days by default, which is what dnssec-signzone uses.</param>
        /// <param name="NSEC3">Hash the names instead of listing them (RFC 5155); NSEC by default.</param>
        public static List<IDNSResourceRecord> Sign(IEnumerable<IDNSResourceRecord>  Records,
                                                    DomainName                       Origin,
                                                    IEnumerable<DNSSECSigningKey>    Keys,
                                                    DateTime?                        Inception    = null,
                                                    DateTime?                        Expiration   = null,
                                                    NSEC3Parameters?                 NSEC3        = null)
        {

            var keys = Keys.ToArray();

            if (keys.Length == 0)
                throw new ArgumentException("A zone cannot be signed with no keys.", nameof(Keys));

            var inception   = Inception  ?? DateTime.UtcNow.AddHours(-1);
            var expiration  = Expiration ?? DateTime.UtcNow.AddDays(30);

            var zoneKeys    = keys.Where(key => !key.IsKeySigningKey).ToArray();
            var keyKeys     = keys.Where(key =>  key.IsKeySigningKey).ToArray();

            // A zone keyed with only one of the two is unusual but legal, and its
            // RRsets still have to be signed by something.
            if (zoneKeys.Length == 0) zoneKeys = keys;
            if (keyKeys. Length == 0) keyKeys  = keys;

            var apex      = Normalize(Origin.FullName);
            var signed    = new List<IDNSResourceRecord>();
            var unsigned  = Records.ToList();

            // The DNSKEY RRset is part of the zone and is signed like the rest of
            // it, so it has to be in place before anything is grouped.
            unsigned.AddRange(keys.Select(key => key.DNSKEY));

            #region Which names are the zone's own

            // RFC 4035 §2.2: a delegation's NS RRset and the glue beneath it are
            // not this zone's data and are not signed. The DS that authorises the
            // delegation is, because it belongs to the parent.
            var delegations = unsigned.
                                  Where (record => record.Type == DNSResourceRecordTypes.NS &&
                                                  !Normalize(record.DomainName.FullName).Equals(apex, StringComparison.OrdinalIgnoreCase)).
                                  Select(record => Normalize(record.DomainName.FullName)).
                                  ToHashSet(StringComparer.OrdinalIgnoreCase);

            Boolean IsBelowADelegation(String Name)
                => delegations.Any(delegation => !Name.Equals(delegation, StringComparison.OrdinalIgnoreCase) &&
                                                  Name.EndsWith("." + delegation, StringComparison.OrdinalIgnoreCase));

            Boolean IsAuthoritative(IDNSResourceRecord Record)
            {

                var name = Normalize(Record.DomainName.FullName);

                if (IsBelowADelegation(name))
                    return false;

                if (delegations.Contains(name))
                    return Record.Type == DNSResourceRecordTypes.DS ||
                           Record.Type == DNSResourceRecordTypes.NSEC;

                return true;

            }

            #endregion

            #region The chain of authenticated denial

            var minimumTTL = MinimumTTL(unsigned);

            if (NSEC3 is not null)
                AddNSEC3Chain(unsigned, apex, NSEC3, minimumTTL, delegations, IsBelowADelegation, IsAuthoritative);

            else
                AddNSECChain (unsigned, apex, minimumTTL, IsBelowADelegation, IsAuthoritative);

            #endregion

            #region Sign every authoritative RRset

            signed.AddRange(unsigned);

            foreach (var rrset in unsigned.
                                      Where  (IsAuthoritative).
                                      GroupBy(record => (Name: Normalize(record.DomainName.FullName).ToLowerInvariant(),
                                                         record.Type)))
            {

                var signers = rrset.Key.Type == DNSResourceRecordTypes.DNSKEY
                                  ? keyKeys
                                  : zoneKeys;

                foreach (var key in signers)
                    signed.Add(SignRRSet(rrset, key, inception, expiration));

            }

            #endregion

            return signed;

        }

        #endregion

        #region (private static) AddNSECChain(Records, Apex, MinimumTTL, IsBelowADelegation, IsAuthoritative)

        /// <summary>
        /// Every name the zone owns, in the canonical order of RFC 4034 §6.1, each
        /// pointing at the next and the last wrapping back to the apex
        /// (RFC 4035 §2.3).
        /// </summary>
        private static void AddNSECChain(List<IDNSResourceRecord>               Records,
                                         String                                 Apex,
                                         UInt32                                 MinimumTTL,
                                         Func<String, Boolean>                  IsBelowADelegation,
                                         Func<IDNSResourceRecord, Boolean>      IsAuthoritative)
        {

            // Glue names get no NSEC: they are not this zone's data.
            var names = Records.
                            Select(record => Normalize(record.DomainName.FullName)).
                            Where (name   => !IsBelowADelegation(name)).
                            Distinct(StringComparer.OrdinalIgnoreCase).
                            ToList();

            names.Sort(DenialOfExistenceValidator.CompareCanonical);

            for (var i = 0; i < names.Count; i++)
            {

                var name  = names[i];
                var next  = names[(i + 1) % names.Count];

                var types = Records.
                                Where (record => Normalize(record.DomainName.FullName).Equals(name, StringComparison.OrdinalIgnoreCase)).
                                Select(record => record.Type).
                                Distinct().
                                ToList();

                // Unlike NSEC3, an NSEC does announce itself (RFC 4034 §4.1.2).
                types.Add(DNSResourceRecordTypes.NSEC);

                var placeholder = new NSEC(DomainName.ParseLenient(name), DNSQueryClasses.IN, TimeSpan.Zero,
                                           DomainName.ParseLenient(next), []);

                if (IsAuthoritative(placeholder))
                    types.Add(DNSResourceRecordTypes.RRSIG);

                Records.Add(
                    new NSEC(
                        DomainName.ParseLenient(name),
                        DNSQueryClasses.IN,
                        TimeSpan.FromSeconds(MinimumTTL),
                        DomainName.ParseLenient(next),
                        ADNSResourceRecord.EncodeTypeBitMaps(types.Order().Select(ADNSResourceRecord.TypeName))
                    )
                );

            }

        }

        #endregion

        #region (private static) AddNSEC3Chain(Records, Apex, Parameters, MinimumTTL, Delegations, IsBelowADelegation)

        /// <summary>
        /// The hashed chain of RFC 5155 §7.1, and the NSEC3PARAM of §4 that tells
        /// a server how to walk it.
        /// </summary>
        private static void AddNSEC3Chain(List<IDNSResourceRecord>  Records,
                                          String                    Apex,
                                          NSEC3Parameters           Parameters,
                                          UInt32                    MinimumTTL,
                                          HashSet<String>           Delegations,
                                          Func<String, Boolean>     IsBelowADelegation,
                                          Func<IDNSResourceRecord, Boolean> IsAuthoritative)
        {

            var ttl   = TimeSpan.FromSeconds(MinimumTTL);
            var flags = (Byte) (Parameters.OptOut ? 1 : 0);

            // §4: the parameters live at the apex, and §7.1 wants them in the
            // apex's own type bitmap — so the record goes in before anything is
            // counted. §4.1.2 gives an NSEC3PARAM flags of zero even when the
            // chain it describes is opt-out.
            Records.Add(
                new NSEC3PARAM(
                    DomainName.ParseLenient(Apex),
                    DNSQueryClasses.IN,
                    ttl,
                    NSEC3.HashAlgorithmSHA1,
                    0,
                    Parameters.Iterations,
                    Parameters.Salt
                )
            );

            var owners = Records.
                             Select(record => Normalize(record.DomainName.FullName)).
                             Where (name   => !IsBelowADelegation(name)).
                             ToHashSet(StringComparer.OrdinalIgnoreCase);

            #region Empty non-terminals

            // §7.1: every empty non-terminal gets an NSEC3 of its own. A name with
            // no records still exists if something lives beneath it, and a chain
            // that skips it proves that name absent — which it is not.
            //
            // NSEC never needed this: its owner names are the zone's names, so an
            // empty non-terminal has no NSEC to place. Hashing destroys the
            // hierarchy, so the names have to be enumerated before they are hashed
            // or the information is gone.
            foreach (var owner in owners.ToArray())
            {

                // The apex has no empty non-terminals above it: walking up from
                // there leaves the zone, and the name it would reach first is the
                // parent zone's. An NSEC3 for that is a claim about somebody
                // else's data, and dnssec-verify says so.
                if (owner.Equals(Apex, StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = owner;

                while (true)
                {

                    var dot = name.IndexOf('.');

                    if (dot < 0 || dot + 1 >= name.Length)
                        break;

                    name = name[(dot + 1)..];

                    if (name.Equals(Apex, StringComparison.OrdinalIgnoreCase))
                        break;

                    // Belt as well as braces: every empty non-terminal is inside
                    // the zone by construction, and anything else is a bug.
                    if (!name.EndsWith("." + Apex, StringComparison.OrdinalIgnoreCase))
                        break;

                    owners.Add(name);

                }

            }

            #endregion

            #region Opt-out

            // §6: a delegation with no DS beside it is insecure, and opt-out lets
            // the signer leave it out of the chain rather than prove it exists.
            if (Parameters.OptOut)
                foreach (var delegation in Delegations)
                    if (!Records.Any(record => record.Type == DNSResourceRecordTypes.DS &&
                                               Normalize(record.DomainName.FullName).Equals(delegation, StringComparison.OrdinalIgnoreCase)))
                    {
                        owners.Remove(delegation);
                    }

            #endregion

            // §7.1: the chain is ordered by hash rather than by name, which is the
            // whole point — somebody walking it learns hashes instead of names.
            var chain = owners.
                            Select(name => (Name: name,
                                            Hash: NSEC3.ComputeHash(DomainName.ParseLenient(name),
                                                                    Parameters.Iterations,
                                                                    Parameters.Salt))).
                            OrderBy(entry => entry.Hash, Comparer<Byte[]>.Create(DNSSECCanonical.Compare)).
                            ToArray();

            for (var i = 0; i < chain.Length; i++)
            {

                var (name, hash) = chain[i];
                var next         = chain[(i + 1) % chain.Length].Hash;

                var types = Records.
                                Where (record => Normalize(record.DomainName.FullName).Equals(name, StringComparison.OrdinalIgnoreCase)).
                                Select(record => record.Type).
                                Distinct().
                                ToList();

                // §7.1: "NSEC3 RRs are not present in the bitmap" — the record does
                // not announce itself, which is the opposite of NSEC. An empty
                // non-terminal has nothing at all and keeps an empty bitmap.
                //
                // RRSIG belongs in the bitmap only where something is actually
                // signed. At a delegation nothing is — the NS RRset is the
                // child's — so claiming an RRSIG there is a bitmap that promises
                // a signature the zone does not contain.
                if (Records.Any(record => Normalize(record.DomainName.FullName).Equals(name, StringComparison.OrdinalIgnoreCase) &&
                                          IsAuthoritative(record)))
                {
                    types.Add(DNSResourceRecordTypes.RRSIG);
                }

                Records.Add(
                    new NSEC3(
                        DomainName.ParseLenient($"{NSEC3.Base32HexEncode(hash)}.{Apex}"),
                        DNSQueryClasses.IN,
                        ttl,
                        NSEC3.HashAlgorithmSHA1,
                        flags,
                        Parameters.Iterations,
                        Parameters.Salt,
                        next,
                        ADNSResourceRecord.EncodeTypeBitMaps(types.Order().Select(ADNSResourceRecord.TypeName))
                    )
                );

            }

        }

        #endregion

        #region (private static) MinimumTTL(Records), Normalize(Name)

        private static UInt32 MinimumTTL(IEnumerable<IDNSResourceRecord> Records)

            => (UInt32) Records.
                            Where (record => record.Type == DNSResourceRecordTypes.SOA).
                            Select(record => ((SOA) record).Minimum.TotalSeconds).
                            DefaultIfEmpty(3600).
                            Min();

        private static String Normalize(String Name)
            => Name.EndsWith('.') ? Name : Name + ".";

        #endregion

    }

}
