using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

#nullable disable

namespace hazroomrenovation.source.Code.RenRooms {
    /// <summary>
    /// Defines a basic room behavior that can be attached to rooms
    /// </summary>
    
    public abstract class RenRoomBehaviorType {

        /// <summary>
        /// The code of the reoom behavior to add.
        /// </summary>
        [JsonProperty]
        public string name;

        /// <summary>
        /// A list of properties for the specific behavior. If any exist.
        /// </summary>
        [JsonProperty, JsonConverter(typeof(JsonAttributesConverter))]
        public JsonObject properties;
    }
}
