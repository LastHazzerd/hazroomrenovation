using hazroomrenovation.source.Code.RenRooms.Datastructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;

#nullable disable

namespace hazroomrenovation.source.Code.RenRooms {
    [DocumentAsJson]
    public class RenRoomProperties {

        

        /// <summary>
        /// The room code in the code.
        /// </summary>
        public AssetLocation Code;

        /// <summary>
        /// The classification of the room.
        /// </summary>
        public string Class;

        /// <summary>
        /// List of room tags ids
        /// </summary>
        public RoomTagArray Tags = RoomTagArray.Empty;

        /// <summary>
        /// The attributes of the room. Defined by the room type's JSON file.
        /// </summary>
        public JsonObject Attributes;
    }
}
