using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using static OpenTK.Graphics.OpenGL.GL;

#nullable disable

// Have Harmony patch all vanilla methods that call/create instances of `Room` or `RoomRegistry` so that they are using `RenRoom` or `RenRoomRegistry` instead.
// WARNING - The patch night not work correctly if you don't specify the full file path/namespace of the classes/methods you are trying to call since the vanilla file does not have a 'using' declaration identifying the mod.
namespace hazroomrenovation.source.HarmonyPatches {
    [HarmonyPatch]
    public class RoomRegistryPatches {

        /// <summary>
        /// Patch to replace the body of RoomRegistry's 'GetRoomForPosition' method with RenRoomRegistry's 'GetRoomForPosition' method that returns the expanded RenRoom class object.
        /// <param name="pos"></param>
        /// <param name="__result"></param>
        /// <returns></returns>
        //[HarmonyPatch(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition))]
        //[HarmonyPrefix]
        //// There are several private classes in RoomRegistry
        //// Aparently instanced data isn't passed to the method at the time of the prefix, i should use a postfix instead.               
        //public static bool SkipRoomRegGetRoom() {            
        //    //FileLog.Log("GetRoomSkip");
        //    return true;
        //}

        [HarmonyPatch(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition))]
        [HarmonyPostfix]
        public static void PatchRoomRegGetRoom(BlockPos pos, ref Room __result, ICachingBlockAccessor ___blockAccessor, ICoreAPI ___api, int ___chunkMapSizeX,int ___chunkMapSizeZ) {
            hazroomrenovation.source.Systems.RenRoomRegistry renroomreg = new();
            hazroomrenovation.source.Systems.RenRoom renroom = new();
            renroom = (Systems.RenRoom)renroomreg.GetRenRoomForPosition(pos, ___blockAccessor, ___api, ___chunkMapSizeX, ___chunkMapSizeZ);
            //FileLog.Log("GetRoomPatch");
            __result = renroom;            
        }
    }    
}