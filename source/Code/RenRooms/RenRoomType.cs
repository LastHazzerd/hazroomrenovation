using hazroomrenovation.source.Code.RenRooms.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;
using static OpenTK.Graphics.OpenGL.GL;

#nullable disable

//TODO- refactor this code into an object that just holds the JSON data so that it can be used to populate lists relavent to the 'FindRoom' search and the 'Room' object.

namespace hazroomrenovation.source.Code.RenRooms {
    /// <summary>
    /// A type of room that can be made when assembling sectioned off cuboids via placing blocks.<br/>
    /// Extends from <see cref="RegistryObjectType"/>.<br/>
    /// The JSON files are used to define what the specific room type requires, and what effects it will have.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class RenRoomType : RegistryObjectType {
        //The following fields are listed properties found in the 'renrooms' folder's JSON files.
        public RenRoomType() {
            Class = "RenRoom";
        }

        /// <summary>
        /// Modifiers that can alter what effects the room will have on mechanics occurring within it.
        /// </summary>
        [JsonProperty]
        public RenRoomBehaviorType[] Behaviors = Array.Empty<RenRoomBehaviorType>();

        /// <summary>
        /// Defining attributes that are required for the room to exist.
        /// </summary>
        [JsonProperty, JsonConverter(typeof(JsonAttributesConverter))]
        public Vintagestory.API.Datastructures.JsonObject Attributes;

        internal RegistryObjectType CreateAndPopulate(ICoreServerAPI api, AssetLocation fullcode, JObject jobject, JsonSerializer deserializer, OrderedDictionary<string, string> variant) {
            RenRoomType resolvedType = CreateResolvedType<RenRoomType>(api, fullcode, jobject, deserializer, variant);
            return resolvedType;
        }

        public void InitRoom(IRoomRegistryAPI instancer, ILogger logger, RenRoom room, OrderedDictionary<string, string> searchReplace) {
            RenRoomBehaviorType[] behaviorTypes = Behaviors;
            if (behaviorTypes != null) {
                List<RenRoomBehavior> roomBehaviors = new List<RenRoomBehavior>();
                foreach (RenRoomBehaviorType behaviorType in behaviorTypes) {
                    RenRoomBehavior behavior;
                    if(instancer.GetRoomBehaviorClass(behaviorType.name) != null) {
                        behavior = instancer.CreateRoomBehavior(room, behaviorType.name);
                    }
                    else {
                        logger.Warning(Lang.Get("Room behavior {0} for room {1} not found", behaviorType.name, room.Code));
                        continue;
                    }

                    if (behaviorType.properties == null) { behaviorType.properties = new JsonObject(new JObject()); }

                    try {
                        behavior.Initialize(behaviorType.properties);
                    }
                    catch (Exception e) {
                        logger.Error("Failed calling Initialize() on room behavior {0} for item {1}, using properties {2}. Will continue anyway.", behaviorType.name, room.Code, behaviorType.properties.ToString());
                        logger.Error(e);
                    }
                    roomBehaviors.Add(behavior);
                }
                room.RoomBehaviors = roomBehaviors.ToArray();
            }
        }

        //TODO, this might be too much, I don't want to create new class files for each RenRoom, I just want the code to use the JSON files to verify what the 'findSearch' is looking for.
    }
}
