using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace hazroomrenovation {
    public class HazRoomRenovationModSystem : ModSystem {
        /// <summary>
        /// An instance of the harmony patcher.
        /// </summary>
        private Harmony? patcher;

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api) {
            Mod.Logger.Notification("Hello from template mod: " + api.Side);
            //If the client and server run from the same instance, there's a chance that without this check the patches will exist twice.
            if (!Harmony.HasAnyPatches(Mod.Info.ModID)) {
                //Create our harmony patcher, using our mod ID as a unique ID.
                patcher = new Harmony(Mod.Info.ModID);
                //PatchCategory will look for any [HarmonyPatchCategory("vstutorial")] classes, and patch them. 
                patcher.PatchCategory(Mod.Info.ModID);
            }

        }

        public override void StartServerSide(ICoreServerAPI api) {
            Mod.Logger.Notification("Hello from template mod server side: " + Lang.Get("hazroomrenovation:hello"));
        }

        public override void StartClientSide(ICoreClientAPI api) {
            Mod.Logger.Notification("Hello from template mod client side: " + Lang.Get("hazroomrenovation:hello"));
        }

        /// <summary>
        /// This function is called when our mod is unloaded - Either when the game closes or a world is exited.
        /// </summary>
        public override void Dispose() {
            //It's important to remove our patches when disposed, otherwise any worlds loaded after closing would still contain the patches even if the mod was disabled.
            patcher?.UnpatchAll(Mod.Info.ModID);
        }

    }
}