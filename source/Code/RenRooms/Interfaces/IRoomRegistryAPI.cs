using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace hazroomrenovation.source.Code.RenRooms.Interfaces {

    /// <summary>
    /// Interface for creating instances of rooms
    /// </summary>
    public interface IRoomRegistryAPI : IClassRegistryAPI {

        Dictionary<string, Type> RoomClassToTypeMapping { get; }

        string GetRoomBehaviorClassName(Type roomBehaviorType);

        /// <summary>
        /// Creates a room instance from given room class
        /// </summary>
        /// <param name="roomClass"></param>
        /// <returns></returns>
        RenRoom CreateRoom(string roomClass);

        /// <summary>
        /// Creates a room behavior instance from given behavior code.
        /// </summary>
        /// <param name="forRoom"></param>
        /// <param name="code"></param>
        /// <returns></returns>
        RenRoomBehavior CreateRoomBehavior(RenRoom forRoom, string code);

        /// <summary>
        /// Returns the type of the registered room class or null otherwise
        /// </summary>
        /// <param name="roomClass"></param>
        /// <returns></returns>
        Type GetRoomClass(string roomClass);

        /// <summary>
        /// Returns the room behavior type registered for given name or null
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        Type GetRoomBehaviorClass(string code);

        RenRoomConfig RenRoomConfig { get; }
    }
}
