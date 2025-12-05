using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hazroomrenovation.source.Code.RenRooms.Behaviors {
    /// <summary>
    /// Exposed Room Behavior. Reduced effectiveness of Enclosed Rooms. <br/>
    /// - Heat from heatsources in this room can reach a little further.<br/>
    /// - Wind has reduced effect on player body temp.<br/>
    /// - Player body temp will slowly normalize based on room temp.<br/>
    /// 
    /// </summary>
    public class RRBehaviorExposed : RenRoomBehavior {

        public RRBehaviorExposed(RenRoom room) : base(room) {
        }
    }
}
