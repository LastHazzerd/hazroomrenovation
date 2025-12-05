using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace hazroomrenovation.source.Code.RenRooms.Interfaces {
    public interface IRoomAPI : ICoreAPI {

        #region Register room content
        void RegisterRoom(string roomName, Type room);

        /// <summary>
        /// Registers a new room behavior class. Must happen before any rooms are loaded. Must be on both client and server side.
        /// </summary>
        /// <param name="className"></param>
        /// <param name="roomBehaviorType"></param>
        void RegisterRoomBehaviorClass(string className, Type roomBehaviorType);
        #endregion

    }
}
