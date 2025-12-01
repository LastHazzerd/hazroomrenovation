using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

namespace hazroomrenovation.source {
    public class HazRoomRenovationModSystem : ModSystem {

        public static readonly string modid = "hazroomrenovation";
        //public static AssetCategory? roomtypes = null;
        
        /// <summary>
        /// An instance of the harmony patcher.
        /// </summary>
        private Harmony? patcher;

        public override void StartPre(ICoreAPI api) {
            //roomtypes = new AssetCategory(nameof(roomtypes), true, EnumAppSide.Server);
        }

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api) {
            Mod.Logger.Notification("Initializing Start method: " + Lang.Get("hazroomrenovation:hello"));
            base.Start(api);
            //TODO
        }

        public override void StartServerSide(ICoreServerAPI api) {
            Mod.Logger.Notification("Initializing StartServerSide method: " + Lang.Get("hazroomrenovation:hello"));
            Harmony.DEBUG = true; // ACTIVATES HARMONY DEBUG, Turn off for full release builds.
            if (Harmony.DEBUG == true) { 
                Mod.Logger.Notification("HARMONY DEBUG IS ON - Beginning new Log Entry");
            }
            if (!Harmony.HasAnyPatches(Mod.Info.ModID)) {
                patcher = new Harmony(Mod.Info.ModID);
                patcher.PatchAll();
                //Might need more percision as the mod scales up.
            }
        }

        public override void StartClientSide(ICoreClientAPI api) {
            Mod.Logger.Notification("Initializing StartClientSide method: " + Lang.Get("hazroomrenovation:hello"));

        }

        /// <summary>
        /// This function is called when our mod is unloaded - Either when the game closes or a world is exited.
        /// </summary>
        public override void Dispose() {
            patcher?.UnpatchAll(Mod.Info.ModID);
            base.Dispose();
        }

    }
}