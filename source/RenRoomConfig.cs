using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace hazroomrenovation.source {
    public class RenRoomConfig {
        public static RenRoomConfig? Instance = null;

        public static void Load(ICoreAPI api) {
            try {
                Instance = api.LoadModConfig<RenRoomConfig>("renroomcon.json");
            } catch (Exception e){
                api.Logger.Error("Failed to load config file for RenRooms: " + e);
            }
            if (Instance == null) {
                Instance = new RenRoomConfig();
            }
            api.StoreModConfig(Instance, "renroomcon.json");
        }
    }
}
