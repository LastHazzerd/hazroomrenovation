using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

#nullable disable

namespace hazroomrenovation.source.Code.RenRooms.Behaviors {
    /// <summary>
    /// Defines a basic room behavior that can be attached to rooms
    /// </summary>
    public abstract class RenRoomBehavior {
        /// <summary>
        /// The 'Room' object for this behavior instance.
        /// </summary>
        public Room room;

        /// <summary>
        /// The properties of this room behavior.
        /// </summary>
        public string propertiesAtString;

        public RenRoomBehavior(Room room) {
            this.room = room;
        }

        /// <summary>
        /// Called right after the room behavior was created, must call base method
        /// </summary>
        /// <param name="properties"></param>
        public virtual void Initialize(JsonObject properties) {
            this.propertiesAtString = properties.ToString();
            //TODO create properties for room definitions.
        }

        /// <summary>
        /// Server Side: Called once the room has been registered
        /// Client Side: Called once the room has been loaded from server packet
        /// </summary>
        /// <param name="api"></param>
        public virtual void OnLoaded(ICoreAPI api) {
            //TODO determine if this method is needed.
        }

        public virtual void OnUnloaded(ICoreAPI api) {
            //TODO determine if this method is needed.
        }
    }
}
