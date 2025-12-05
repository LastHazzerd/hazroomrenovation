using HarmonyLib;
using hazroomrenovation.source.Code.RenRooms;
using hazroomrenovation.source.Code.RenRooms.Behaviors;
using hazroomrenovation.source.Code.RenRooms.Interfaces;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

#nullable disable

namespace hazroomrenovation.source {
    /// <summary>
    /// The core of the Room Renovation Library.
    /// </summary>
    public class HazRoomRenovationLibrary : ModSystem {
        //TODO - Add a list of 'RenRoomType' objects and populate it with every found 'renroom' json file so that the list can be passed on to the 'findroom' search.


        public static readonly string modid = "hazroomrenovation";

        /// <summary>
        /// Returns an instance of the mod's interface for any mods intending to use this library.
        /// </summary>
        /// <param name="api">The Core API</param>
        /// <returns>an instance of the Room Renovation Library mod's interface. [nullable]</returns>
        public static HazRoomRenovationLibrary Instance(ICoreAPI api) => api.ModLoader.GetModSystem("hazroomrenovation.source.HazRoomRenovationLibrary") as HazRoomRenovationLibrary;
       
        /// <summary>
        /// Asset category for RenRoom types.
        /// </summary>
        public AssetCategory RenRoomAssetCategory { get; private set; }
        /// <summary>
        /// Vintage Story API with additional support for Ren Rooms.
        /// </summary>
        public IRoomAPI Api {  get; private set; }

        public IRoomRegistryAPI regApi { get; private set; }

        /// <summary>
        /// Dictionary for loading the different Room types available in the game via mods.
        /// </summary>
        Dictionary<AssetLocation, RenRoom> roomTypes;

        /// <summary>
        /// Register a new room behavior class. Must happen before any rooms are loaded. Must be registered on both client and server side.
        /// </summary>
        public Dictionary<string, Type> RoomBehaviors { get; private set; }
        /// <summary>
        /// List of specific Types (blocks, blockEntities, etc.) that the search needs to keep track of.
        /// </summary>
        public List<Type> SearchList { get; private set; }

        /// <summary>
        /// Intended to be looked for ONLY IF the method calling for the search clarifies it. <br/>
        /// List of specific Types (blocks, blockEntities, etc.) that the 'FindRoom' search will look for.
        /// </summary>
        public List<Type> SpecialSearchList { get; private set; }

        public RenRoomConfig RenRoomConfig { get => regApi.RenRoomConfig; }

        /// <summary>
        /// An instance of the harmony patcher.
        /// </summary>
        private Harmony patcher;

        /// <summary>
        /// Called upon initial mod loading, before any mod calls Start()
        /// </summary>
        /// <param name="api"></param>
        public override void StartPre(ICoreAPI api) {
            base.StartPre(api);
            RenRoomAssetCategory ??= new AssetCategory("renrooms", true, EnumAppSide.Universal);
        }

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public void Start(ICoreAPI api, IRoomAPI roomApi) {
            base.Start(api);

            RegisterDefaultRoomBehaviors();
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

        private void RegisterDefaultRoomBehaviors() {
            Api.RegisterRoomBehaviorClass("Enclosed", typeof(RRBehaviorEnclosed));
            Api.RegisterRoomBehaviorClass("Exposed", typeof(RRBehaviorExposed));
            Api.RegisterRoomBehaviorClass("ControlledTemp", typeof(RRBehaviorControlledTemp));
            Api.RegisterRoomBehaviorClass("ControlledClimate", typeof(RRBehaviorControlledClimate));
            Api.RegisterRoomBehaviorClass("ControlledSpoil", typeof(RRBehaviorControlledSpoil));
        }




        /// <summary>
        /// Retrieves the provided Room JSON files. Used to define what the 'findroom' search needs to be looking for.
        /// </summary>
        private void LoadRoomAssets() {
            Dictionary<AssetLocation, JToken> tokens = Api.Assets.GetMany<JToken>(this.Mod.Logger, "renrooms");
            if (tokens.Count == 0) {
                Api.Assets.Reload(RenRoomAssetCategory);
                tokens = Api.Assets.GetMany<JToken>(this.Mod.Logger, "renrooms");
            }
            Dictionary<AssetLocation,JToken>.Enumerator enumerator = tokens.GetEnumerator();

            while (enumerator.MoveNext()) {
                enumerator.Current.Key.RemoveEnding();
                
            }

        }

        /// <summary>
        /// This function is called when our mod is unloaded - Either when the game closes or a world is exited.
        /// </summary>
        public override void Dispose() {
            patcher?.UnpatchAll(Mod.Info.ModID);
            base.Dispose();
            patcher = null;
        }

    }
}