using hazroomrenovation.source.Code.RenRooms.Datastructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace hazroomrenovation.source.Code.RenRooms.Interfaces {
    /// <summary>
    /// A derived interface from 'ITagRegistry' that includes tags associated with RenRoom JSON files.<br/>
    /// On server side: blocks, items, entities tags and tags from preloaded-tags.json are registered after 'AssetsLoaded' and before 'AssetsFinalize' stages.<br/>
    /// On client side: all tags are received from server along side blocks, times, and entities, an available in 'AssetsFinalize' stage.<br/>
    /// Tags can be converted to tag array or tag id as soon as it is registered.<br/>
    /// Tags can be registered only on server side no later than 'AssetsFinalize' stage.
    /// </summary>
    public interface IRoomTagRegistry : ITagRegistry {
        /// <summary>
        /// API derived from TagRegistry, used for converting between registry room tags and tag ids, and for registering new room tags.
        /// </summary>
        IRoomTagRegistry RoomTagRegistry { get; }

        /// <summary>
        /// Registers new room tags. Should be called only on server side. Should be called no later than 'AssetsFinalize' stage.
        /// </summary>
        /// <param name="tags"></param>
        void RegisterRoomTags(params string[] tags);

        /// <summary>
        /// Converts a list of room tags to their corresponding tag IDs.<br/>
        /// If removeUnknownTags is true, any unknown tags will be removed from the list.<br/>
        /// Result is sorted in ascending order.
        /// </summary>
        /// <param name="tags"></param>
        /// <param name="removeUnknonwTags"></param>
        /// <returns></returns>
        ushort[] RoomTagsToTagIds(string[] tags, bool removeUnknonwTags = false);

        /// <summary>
        /// Converts list of room tags to tags array. Unknown tags are ignored<br/>
        /// Blocks, items, entities tags and tags from preloaded-tags.json are registered after 'AssetsLoaded' and before 'AssetsFinalize' stages.<br/>
        /// This goes for room tags as well.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public RoomTagArray RoomTagsToTagArray(params string[] tags);

        /// <summary>
        /// Returns tag id of the room tag. If the tag is not registered, it will return 0.<br/>
        /// Blocks, items, entities tags and tags from preloaded-tags.json are registered after 'AssetsLoaded' and before 'AssetsFinalize' stages.<br/>
        /// This goes for room tags as well.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        ushort RoomTagToTagId(string tag);

        /// <summary>
        /// Returns tag by room tag id. If the tag id is not registered, it will return an empty string.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        string RoomTagIdToTag(ushort id);

        // Modder's Note: The 'void LoadTagsFromAssets(ICoreServerAPI api);' method is excluded as it should be inherited from the ITagRegistry base interface.
    }
}
