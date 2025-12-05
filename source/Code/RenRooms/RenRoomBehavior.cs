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
    
    public abstract class RenRoomBehavior {
        /// <summary>
        /// The 'Room' object for this behavior instance.
        /// </summary>
        public RenRoom room;

        /// <summary>
        /// The properties of this room behavior.
        /// </summary>
        public string propertiesAtString;

        public RenRoomBehavior(RenRoom room) {
            this.room = room;
        }

        /// <summary>
        /// Initializes the room Behavior.
        /// </summary>
        /// <param name="properties"></param>
        public virtual void Initialize(JsonObject properties) {
            propertiesAtString = properties.ToString();
        }

        /// <summary>
        /// Disposes information as needed.
        /// </summary>
        public virtual void Dispose() {
            //TODO determine if this method is needed.
        }
    }
}
