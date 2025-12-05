using hazroomrenovation.source.Code.RenRooms.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace hazroomrenovation.source.Code.RenRooms.Datastructures {
    /// <summary>
    /// Modder's Note: All code in this is based on the vanilla tag system, adapted for the addition of Rooms as a taggable JSON type.<br/>
    /// List of room tags meant to be used for fast comparisons. Restricts number of registered room tags to 128
    /// </summary>
    public readonly struct RoomTagArray {
        public readonly ulong BitMask1;
        public readonly ulong BitMask2;

        public const byte MasksNumber = 2;

        public RoomTagArray(IEnumerable<ushort> tags) {
            foreach (ushort tag in tags) {
                WriteTagToBitMasks(tag, ref BitMask1, ref BitMask2);
            }
        }

        public RoomTagArray(ushort tag) {
            WriteTagToBitMasks(tag, ref BitMask1, ref BitMask2);
        }

        public RoomTagArray() {

        }

        public RoomTagArray(ulong bitMask1, ulong bitMask2) {
            BitMask1 = bitMask1;
            BitMask2 = bitMask2;
        }

        public static readonly RoomTagArray Empty = new();

        /// <summary>
        /// Converts tag array into list of tag ids sorted in ascending order.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ushort> ToArray() {
            for (ushort index = 0; index < 64; index++) {
                if ((BitMask1 & (1UL << index)) != 0) {
                    yield return (ushort)(index + 1);
                }
            }
            for (ushort index = 0; index < 64; index++) {
                if ((BitMask2 & (1UL << index)) != 0) {
                    yield return (ushort)(index + 64 + 1);
                }
            }
        }

        /// <summary>
        /// Converts tag array into list of tags.
        /// </summary>
        /// <param name="api"></param>
        /// <returns></returns>
        public IEnumerable<string> ToArray(IRoomTagRegistry api) {
            return ToArray().Select(api.RoomTagRegistry.RoomTagIdToTag);
        }
        /// <summary>
        /// Checks if this tag array contains all tags from <paramref name="other"/>
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool ContainsAll(RoomTagArray other) {
            return (BitMask1 & other.BitMask1) == other.BitMask1 &&
                   (BitMask2 & other.BitMask2) == other.BitMask2;
        }

        /// <summary>
        /// Checks if this tag array contains at least on tag from each element of <paramref name="tags"/>
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public bool IntersectsWithEach(RoomTagArray[] tags) {
            foreach (RoomTagArray tagArray in tags) {
                if (!Intersect(this, tagArray)) return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if this tag array contains all tags from at least one element of <paramref name="tags"/>
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public bool ContainsAllFromAtLeastOne(RoomTagArray[] tags) {
            foreach (RoomTagArray tagArray in tags) {
                if (ContainsAll(tagArray)) return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if two tag arrays have at least one common tag
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static bool Intersect(RoomTagArray first, RoomTagArray second) {
            ulong intersect1 = first.BitMask1 & second.BitMask1;
            ulong intersect2 = first.BitMask2 & second.BitMask2;

            return (intersect1 | intersect2) != 0;
        }

        /// <summary>
        /// Checks if this tag array has at least one common tag with <paramref name="other"/>
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Intersect(RoomTagArray other) {
            return Intersect(this, other);
        }

        public RoomTagArray Remove(RoomTagArray other) {
            return new(BitMask1 & ~other.BitMask1, BitMask2 & ~other.BitMask2);
        }

        public static RoomTagArray And(RoomTagArray first, RoomTagArray second) {
            return new RoomTagArray
            (
                first.BitMask1 & second.BitMask1,
                first.BitMask2 & second.BitMask2
            );
        }

        public static RoomTagArray Or(RoomTagArray first, RoomTagArray second) {
            return new RoomTagArray
            (
                first.BitMask1 | second.BitMask1,
                first.BitMask2 | second.BitMask2
            );
        }

        public static RoomTagArray Not(RoomTagArray value) {
            return new RoomTagArray
            (
                ~value.BitMask1,
                ~value.BitMask2
            );
        }

        public static RoomTagArray operator &(RoomTagArray first, RoomTagArray second) => And(first, second);

        public static RoomTagArray operator |(RoomTagArray first, RoomTagArray second) => Or(first, second);

        public static RoomTagArray operator ~(RoomTagArray value) => Not(value);

        public static bool operator ==(RoomTagArray first, RoomTagArray second) {
            return first.BitMask1 == second.BitMask1 &&
                   first.BitMask2 == second.BitMask2;
        }

        public static bool operator !=(RoomTagArray first, RoomTagArray second) => !(first == second);

        public override bool Equals(object? obj) {
            if (obj is RoomTagArray other) {
                return this == other;
            }
            return false;
        }

        public override int GetHashCode() {
            return (int)(BitMask1 ^ BitMask2);
        }

        public override string ToString() => PrintBitMask(BitMask2) + ":" + PrintBitMask(BitMask1);

        public void ToBytes(BinaryWriter writer) {
            writer.Write(BitMask1);
            writer.Write(BitMask2);
        }

        public static RoomTagArray FromBytes(BinaryReader reader) {
            ulong bitMask1 = reader.ReadUInt64();
            ulong bitMask2 = reader.ReadUInt64();
            return new(bitMask1, bitMask2);
        }

        private static void WriteTagToBitMasks(ushort tag, ref ulong bitMask1, ref ulong bitMask2) {
            if (tag == 0) return;

            int tagIndex = (tag - 1) % 64;
            int bitMaskIndex = (tag - 1) / 64;

            switch (bitMaskIndex) {
                case 0:
                    bitMask1 |= 1UL << tagIndex;
                    break;
                case 1:
                    bitMask2 |= 1UL << tagIndex;
                    break;
                default:
                    break;
            }
        }
        private static string PrintBitMask(ulong bitMask) => $"{bitMask:X16}".Chunk(4).Select(chunk => new string(chunk)).Aggregate((first, second) => $"{first}.{second}");
    }

    /// <summary>
    /// Modder's Note: All code in this is based on the vanilla tag system, adapted for the addition of Rooms as a taggable JSON type.<br/>
    /// Pair of tag arrays that is used for implementation of tag inversion
    /// </summary>
    public readonly struct RoomTagRule {
        public readonly RoomTagArray TagsThatShouldBePresent;
        public readonly RoomTagArray TagsThatShouldBeAbsent;

        public const string NotPrefix = "not-";
        public readonly static RoomTagRule Empty = new(RoomTagArray.Empty, RoomTagArray.Empty);

        public RoomTagRule(RoomTagArray tagsThatShouldBePresent, RoomTagArray tagsThatShouldBeAbsent) {
            TagsThatShouldBePresent = tagsThatShouldBePresent;
            TagsThatShouldBeAbsent = tagsThatShouldBeAbsent;
        }

        public RoomTagRule(IRoomTagRegistry api, IEnumerable<string> tags) {
            List<string> straightTags = [];
            List<string> inverseTags = [];
            foreach (string tag in tags) {
                if (tag.StartsWith(NotPrefix)) {
                    inverseTags.Add(tag[NotPrefix.Length..]);
                }
                else {
                    straightTags.Add(tag);
                }
            }
            
            TagsThatShouldBePresent = api.RoomTagRegistry.RoomTagsToTagArray([.. straightTags]);
            TagsThatShouldBeAbsent = api.RoomTagRegistry.RoomTagsToTagArray([.. inverseTags]);
        }

        public bool Intersects(RoomTagArray tags) {
            if (TagsThatShouldBePresent != RoomTagArray.Empty && !tags.Intersect(TagsThatShouldBePresent)) return false;
            if (TagsThatShouldBeAbsent != RoomTagArray.Empty && tags.ContainsAll(TagsThatShouldBeAbsent)) return false;

            return true;
        }

        /// <summary>
        /// Checks if <paramref name="roomTag"/> contains at least on tag from each rule from <paramref name="rules"/>.
        /// </summary>
        /// <param name="roomTag"></param>
        /// /// <param name="rules"></param>
        /// <returns></returns>
        public static bool IntersectsWithEach(RoomTagArray roomTag, RoomTagRule[] rules) {
            foreach (RoomTagRule rule in rules) {
                if (rule.TagsThatShouldBePresent != RoomTagArray.Empty && !roomTag.Intersect(rule.TagsThatShouldBePresent) ||
                    rule.TagsThatShouldBeAbsent != RoomTagArray.Empty && roomTag.ContainsAll(rule.TagsThatShouldBeAbsent)) return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if <paramref name="roomTag"/> contains all tags from at least one rule from <paramref name="rules"/>.
        /// </summary>
        /// <param name="roomTag"></param>
        /// /// <param name="rules"></param>
        /// <returns></returns>
        public static bool ContainsAllFromAtLeastOne(RoomTagArray roomTag, RoomTagRule[] rules) {
            foreach (RoomTagRule rule in rules) {
                if (roomTag.ContainsAll(rule.TagsThatShouldBePresent) && !(rule.TagsThatShouldBeAbsent != RoomTagArray.Empty && roomTag.Intersect(rule.TagsThatShouldBeAbsent))) return true;
            }
            return false;
        }

        public static bool operator ==(RoomTagRule first, RoomTagRule second) {
            return first.TagsThatShouldBePresent == second.TagsThatShouldBePresent &&
                   first.TagsThatShouldBeAbsent == second.TagsThatShouldBeAbsent;
        }
        public static bool operator !=(RoomTagRule first, RoomTagRule second) {
            return !(first == second);
        }
        public override bool Equals(object? obj) {
            if (obj is RoomTagRule other) {
                return this == other;
            }
            return false;
        }
        public override int GetHashCode() {
            return TagsThatShouldBePresent.GetHashCode() ^ TagsThatShouldBeAbsent.GetHashCode();
        }
        public override string ToString() => $"+{TagsThatShouldBePresent}\n-{TagsThatShouldBeAbsent}";
    }
}
