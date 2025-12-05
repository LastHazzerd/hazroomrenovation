using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace hazroomrenovation.source.Code.RenRooms.Behaviors {
    /// <summary>
    /// Enclosed Room Behavior <br/>
    /// - Heat from heatsources in this room can reach further.<br/>
    /// - Wind will no longer reduce player body temp.<br/>
    /// - Player body temp will normalize based on room temp.<br/>
    /// 
    /// </summary>
    public class RRBehaviorEnclosed : RenRoomBehavior {



        public RRBehaviorEnclosed(RenRoom room) : base(room) {
        }

        public override void Initialize(JsonObject properties) {
            base.Initialize(properties);
        }

        public bool GetEnclosedRoom() { return true; }

    }
}
