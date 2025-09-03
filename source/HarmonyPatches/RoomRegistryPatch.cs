//using HarmonyLib;
//using hazroomrenovation.source.HarmonyPatches;
//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.Linq;
//using System.Reflection;
//using System.Reflection.Emit;
//using System.Runtime.CompilerServices;
//using System.Security.Cryptography.X509Certificates;
//using System.Text;
//using System.Threading.Tasks;
//using Vintagestory.API.Common;
//using Vintagestory.API.Config;
//using Vintagestory.API.MathTools;
//using Vintagestory.GameContent;

//// Have Harmony redirect functions in the room registry class to my own code so I can overhaul the room reg process for the game.
//namespace hazroomrenovation.source.HarmonyPatches {

//    [HarmonyDebug]
//    public static class RoomRegistryPatch {
//        static RoomRegistryPatch() {
//            var harmony = new Harmony("HazRoomRenovation.RoomRegistryPatch");
            
//            harmony.Patch(
//                original: AccessTools.Method(typeof(RoomRegistry), "GetRoomForPosition"),
//                transpiler: new HarmonyMethod(typeof(RoomRegistryPatch), nameof(RoomRegistry_Transpiler))
//                );
//        }

//        public static IEnumerable<CodeInstruction> RoomRegistry_Transpiler(IEnumerable<CodeInstruction> instructions) {
            
//            var codes = new List<CodeInstruction>(instructions);

//            //go through all the existing instructions, rewriting the first lines to do a 
            
//        }
//    }
//}

///*
// * Room room;
// * room = GetRoomForPosition(pos);
// * return room; 
// */