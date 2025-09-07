using HarmonyLib;
using hazroomrenovation.source.HarmonyPatches;
using hazroomrenovation.source.Systems;
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
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

// Have Harmony patch all vanilla methods that call/create instances of `Room` or `RoomRegistry` so that they are using `RenRoom` or `RenRoomRegistry` instead.
// WARNING - The patch night not work correctly if you don't specify the full file path/namespace of the classes/methods you are trying to call since the vanilla file does not have a 'using' declaration identifying the mod.
namespace hazroomrenovation.source.HarmonyPatches {
    [HarmonyPatch]
    public class RoomRegistryPatches {

        #region RoomRegistry Field references
        [HarmonyPatch(typeof(InWorldContainer), nameof(InWorldContainer.Init))]
        [HarmonyTranspiler] // Patch to replace the 'RoomRegistry' references in InWorldContainer's Init() method.
        public static IEnumerable<CodeInstruction> PatchInWorldContainerInit(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var codes = new List<CodeInstruction>(instructions);
            bool conFound = false;
            bool fldFound = false;
            var constructorToFind = AccessTools.Constructor(typeof(RoomRegistry));
            var fieldToFind = AccessTools.Field(typeof(RoomRegistry), "roomReg");

            // IL Code to look for:
            // IL_001d: callvirt instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class Vintagestory.GameContent.RoomRegistry>(bool)
            // IL Code to replace with:
            // IL_001d: callvirt instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class Vintagestory.GameContent.RenRoomRegistry>(bool)
            for (int i = 0; i < codes.Count; i++) {
                //find line of IL code calling the RoomRegistry constructor, and replace it.
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is ConstructorInfo constructor1 && constructor1 == constructorToFind) {
                    conFound = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Constructor(typeof(RenRoomRegistry)));
                }
                //find line of IL code calling the RoomRegistry in a field, and replace it.
                if (codes[i].opcode == OpCodes.Stfld && codes[i].operand is FieldInfo Field1 && Field1 == fieldToFind) {
                    fldFound = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Field(typeof(RenRoomRegistry), "roomReg"));
                }
                else if (i + 1 >= codes.Count && conFound == false || fldFound == false) {
                    //Potential Logging spot, if I figure out standard practice for writing logs to server.                    
                    Console.WriteLine("[HazMod WARNING] Could not find RoomRegistry reference in InWorldContainer's Init() method.");
                }
            }
            return codes;
        }

        // -------------------------------------------

        [HarmonyPatch(typeof(BlockFarmland), nameof(BlockFarmland.OnLoaded))]
        [HarmonyTranspiler] // Patch to replace the 'RoomRegistry' variable in BlockFarmland's OnLoaded() method.
        public static IEnumerable<CodeInstruction> PatchBlockFarmlandOnLoaded(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var codes = new List<CodeInstruction>(instructions);
            bool codeFound = false;
            var constructorToFind = AccessTools.Constructor(typeof(RoomRegistry));

            // IL Code to look for:
            // IL_00C2: callvirt  instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class [VSEssentials]Vintagestory.GameContent.RoomRegistry>(bool)
            // IL Code to replace with:
            // IL_00C2: callvirt  instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class [VSEssentials]Vintagestory.GameContent.RenRoomRegistry>(bool)
            for (int i = 0; i < codes.Count; i++) {
                //find line of IL code that needs to be replaced, and replace it.
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is ConstructorInfo constructor1 && constructor1 == constructorToFind) {
                    codeFound = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Constructor(typeof(RenRoomRegistry)));
                }
                else if (i + 1 >= codes.Count && codeFound == false) {
                    //Potential Logging spot, if I figure out standard practice for writing logs to server.                    
                    Console.WriteLine("[HazMod WARNING] Could not find RoomRegistry reference in BlockFarmland's OnLoaded() method.");
                }
            }
            return codes;
        }

        // -------------------------------------------

        [HarmonyPatch(typeof(BlockEntityBeehive), nameof(BlockEntityBeehive.Initialize))]
        [HarmonyTranspiler] // Patch to replace the 'RoomRegistry' variable in BlockEntityBeehive's Initialize() method.
        public static IEnumerable<CodeInstruction> PatchBlockEntityBeehiveInitialize(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var codes = new List<CodeInstruction>(instructions);
            bool conFound = false;
            var constructorToFind = AccessTools.Constructor(typeof(RoomRegistry)); // WARNING - The IL line after the callvirt line also has a 'RoomRegistry' instance, but in theory it should still be returning RenRoomReg

            // IL Code to look for:
            // IL_005C: callvirt  instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class [VSEssentials]Vintagestory.GameContent.RoomRegistry>(bool)
            // IL Code to replace with:
            // IL_005C: callvirt  instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class [VSEssentials]Vintagestory.GameContent.RenRoomRegistry>(bool)
            for (int i = 0; i < codes.Count; i++) {
                //find line of IL code that needs to be replaced, and replace it.
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is ConstructorInfo constructor1 && constructor1 == constructorToFind) {
                    conFound = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Constructor(typeof(RenRoomRegistry)));
                }
                else if (i + 1 >= codes.Count && conFound == false) {
                    //Potential Logging spot, if I figure out standard practice for writing logs to server.                    
                    Console.WriteLine("[HazMod WARNING] Could not find RoomRegistry reference in BlockEntityBeehive's Initialize() method.");
                }
            }
            return codes;
        }

        // -------------------------------------------

        [HarmonyPatch(typeof(BlockEntityBerryBush), nameof(BlockEntityBerryBush.Initialize))]
        [HarmonyTranspiler] // Patch to replace the 'RoomRegistry' variable in BlockEntityBerryBush's Initialize() method.
        public static IEnumerable<CodeInstruction> PatchBlockEntityBerryBushInitialize(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var codes = new List<CodeInstruction>(instructions);
            bool codeFound = false;
            var constructorToFind = AccessTools.Constructor(typeof(RoomRegistry));

            // IL Code to look for:
            // IL_00E5: callvirt  instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class [VSEssentials]Vintagestory.GameContent.RoomRegistry>(bool)
            // IL Code to replace with:
            // IL_00E5: callvirt  instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class [VSEssentials]Vintagestory.GameContent.RenRoomRegistry>(bool)
            for (int i = 0; i < codes.Count; i++) {
                //find line of IL code that needs to be replaced, and replace it.
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is ConstructorInfo constructor1 && constructor1 == constructorToFind) {
                    codeFound = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Constructor(typeof(RenRoomRegistry)));
                }
                else if (i + 1 >= codes.Count && codeFound == false) {
                    //Potential Logging spot, if I figure out standard practice for writing logs to server.                    
                    Console.WriteLine("[HazMod WARNING] Could not find RoomRegistry reference in BlockEntityBerryBush's Initialize() method.");
                }
            }
            return codes;
        }

        // -------------------------------------------

        [HarmonyPatch(typeof(BlockEntityPlantContainer), nameof(BlockEntityPlantContainer.Initialize))]
        [HarmonyTranspiler] // Patch to replace the 'RoomRegistry' variable in BlockEntityPlantContainer's Initialize() method.
        public static IEnumerable<CodeInstruction> PatchBlockEntityPlantContainerInitialize(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var codes = new List<CodeInstruction>(instructions);
            bool codeFound = false;
            var constructorToFind = AccessTools.Constructor(typeof(RoomRegistry));

            // IL Code to look for:
            // IL_003A: callvirt  instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class [VSEssentials]Vintagestory.GameContent.RoomRegistry>(bool)
            // IL Code to replace with:
            // IL_003A: callvirt  instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class [VSEssentials]Vintagestory.GameContent.RenRoomRegistry>(bool)
            for (int i = 0; i < codes.Count; i++) {
                //find line of IL code that needs to be replaced, and replace it.
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is ConstructorInfo constructor1 && constructor1 == constructorToFind) {
                    codeFound = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Constructor(typeof(RenRoomRegistry)));
                }
                else if (i + 1 >= codes.Count && codeFound == false) {
                    //Potential Logging spot, if I figure out standard practice for writing logs to server.                    
                    Console.WriteLine("[HazMod WARNING] Could not find RoomRegistry reference in BlockEntityPlantContainer's Initialize() method.");
                }
            }
            return codes;
        }

        // -------------------------------------------

        [HarmonyPatch(typeof(FruitTreeRootBH), nameof(FruitTreeRootBH.Initialize))]
        [HarmonyTranspiler] // Patch to replace the 'RoomRegistry' variable in FruitTreeRootBH's Initialize() method.
        public static IEnumerable<CodeInstruction> PatchFruitTreeRootBHInitialize(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var codes = new List<CodeInstruction>(instructions);
            bool codeFound = false;
            var constructorToFind = AccessTools.Constructor(typeof(RoomRegistry));

            // IL Code to look for:
            // IL_0068: callvirt  instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class [VSEssentials]Vintagestory.GameContent.RoomRegistry>(bool)
            // IL Code to replace with:
            // IL_0068: callvirt  instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class [VSEssentials]Vintagestory.GameContent.RenRoomRegistry>(bool)
            for (int i = 0; i < codes.Count; i++) {
                //find line of IL code that needs to be replaced, and replace it.
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is ConstructorInfo constructor1 && constructor1 == constructorToFind) {
                    codeFound = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Constructor(typeof(RenRoomRegistry)));
                }
                else if (i + 1 >= codes.Count && codeFound == false) {
                    //Potential Logging spot, if I figure out standard practice for writing logs to server.                    
                    Console.WriteLine("[HazMod WARNING] Could not find RoomRegistry reference in FruitTreeRootBH's Initialize() method.");
                }
            }
            return codes;
        }
        #endregion

        // -------------------------------------------

        #region In Method references
        [HarmonyPatch(typeof(EntityBehaviorHunger), nameof(EntityBehaviorHunger.Initialize))]
        [HarmonyTranspiler] // Patch to replace the 'RoomRegistry' variable in EntityBehaviorHunger's Initialize() method.
        public static IEnumerable<CodeInstruction> PatchEntityBehaviorHungerInitialize(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var codes = new List<CodeInstruction>(instructions);
            bool roomregFound = false;
            bool roomFoundCon = false;
            bool roomFoundFld = false;
            var constructorToFind1 = AccessTools.Constructor(typeof(RoomRegistry));
            var constructorToFind2 = AccessTools.Constructor(typeof(Room)); // WARNING - IL code 'IL_010A' has both 'Room' and 'RoomRegistry' present and I'm not sure if they're both classified as 'Constructor's
            var fieldToFind1 = AccessTools.Field(typeof(Room), "ExitCount");

            // IL Codes to look for:
            // IL_00F5: callvirt  instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class Vintagestory.GameContent.RoomRegistry>(bool)
            // IL_010A: callvirt  instance class Vintagestory.GameContent.Room Vintagestory.GameContent.RoomRegistry::GetRoomForPosition(class [VintagestoryAPI]
            // IL_0128: ldfld     int32 Vintagestory.GameContent.Room::ExitCount
            // IL Code to replace with:
            // IL_0068: callvirt  instance !!0 [VintagestoryAPI]Vintagestory.API.Common.IModLoader::GetModSystem<class [VSEssentials]Vintagestory.GameContent.RenRoomRegistry>(bool)
            // IL_010A: callvirt  instance class Vintagestory.GameContent.Room Vintagestory.GameContent.RenRoomRegistry::GetRoomForPosition(class [VintagestoryAPI]
            // IL_0128: ldfld     int32 Vintagestory.GameContent.RenRoom::ExitCount
            for (int i = 0; i < codes.Count; i++) {
                //find 'RoomReg' constructor line of IL code that needs to be replaced, and replace it.
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is ConstructorInfo constructor1 && constructor1 == constructorToFind1) {
                    roomregFound = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Constructor(typeof(RenRoomRegistry)));
                }
                //find the 'Room' constructor line of IL code that needs to be replaced, and replace it.
                else if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is ConstructorInfo constructor2 && constructor2 == constructorToFind2) {
                    roomFoundCon = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Constructor(typeof(RenRoom)));
                }
                //find 'Room' Field line of IL code that needs to be replaced, and replace it.
                else if (codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo field1 && field1 == fieldToFind1) {
                    roomFoundFld = true;
                    codes[i] = new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom), "ExitCount"));
                }
                else if (i + 1 >= codes.Count && roomregFound == false || roomFoundCon == false || roomFoundFld == false) {
                    //Potential Logging spot, if I figure out standard practice for writing logs to server.
                    Console.WriteLine("[HazMod WARNING] Could not find RoomRegistry and/or Room reference in EntityBehaviorHunger's Initialize() method.");
                    Console.WriteLine("[HazMod WARNING] RoomReg Found: " + roomregFound);
                    Console.WriteLine("[HazMod WARNING] RoomCon Found: " + roomFoundCon);
                    Console.WriteLine("[HazMod WARNING] RoomFld Found: " + roomFoundFld);
                }
            }
            return codes;
        }

        // -------------------------------------------

        [HarmonyPatch(typeof(BlockEntityBeehive), "OnScanForEmptySkep")] //BlockEntityBeehive.OnScanForEmptySkep(dt) is a private method
        [HarmonyTranspiler] // Patch to replace the 'RoomRegistry' variable in BlockEntityBeehive's OnScanForEmptySkep() method.
        /// FIRST will try to replace RoomReg with RenRoomReg, If that doesn't work, then:
        /// IL code that was replaced when changing 'Room' to 'RenRoom'
        ///  [0] class hazroomrenovation.source.Systems.RenRoom room, //Original code: [0] class [VSEssentials]Vintagestory.GameContent.Room room // will need to make a new loc and redirect all [0] locs to the new one.
        ///  IL_0019: castclass hazroomrenovation.source.Systems.RenRoom
        ///  IL_0024: ldfld int32 hazroomrenovation.source.Systems.RenRoom::SkylightCount
        ///  IL_002A: ldfld int32 hazroomrenovation.source.Systems.RenRoom::NonSkylightCount
        ///  IL_0032: ldfld int32 hazroomrenovation.source.Systems.RenRoom::ExitCount
        ///
        public static IEnumerable<CodeInstruction> PatchBlockEntityBeehiveOnScanForEmptySkep(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var codes = new List<CodeInstruction>(instructions);
            bool foundRoomReg = false;
            MethodInfo Original = AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition), new[] { typeof(BlockPos) });
            MethodInfo Replacement = AccessTools.Method(typeof(RenRoomRegistry), nameof(RenRoomRegistry.GetRoomForPosition), new[] { typeof(BlockPos) });
            ///Replace the 'RoomRegistry' type for the 'roomreg' variable with 'RenRoomRegistry' in the call to the instance class. (at the time of writing, it's line IL_0014)
            ///IL_0014: call      instance class [VSEssentials]Vintagestory.GameContent.Room [VSEssentials]Vintagestory.GameContent.RoomRegistry::GetRoomForPosition(class [VintagestoryAPI]
            ///IL_0014: call      instance class [VSEssentials]Vintagestory.GameContent.Room [VSEssentials]Vintagestory.GameContent.RenRoomRegistry::GetRoomForPosition(class [VintagestoryAPI]
            for (int i = 0; i < codes.Count; i++) {
                foundRoomReg = true;
                if (codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo method1 && method1 == Original) {
                    codes[i] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RenRoomRegistry), nameof(RenRoomRegistry.GetRoomForPosition), new[] {typeof(BlockPos)}));
                } else if (i + 1 >= codes.Count && foundRoomReg == false) {
                    Console.WriteLine("[HazMod WARNING] Could not find RoomRegistry reference in BlockEntityBeehive's OnScanForEmptySkep() method.");
                }
            }
            return codes;
        }

        // -------------------------------------------

        [HarmonyPatch(typeof(BlockEntityBerryBush), "CheckGrow")] //BlockEntityBerryBush.CheckGrow() is a protected method
        [HarmonyTranspiler] // Patch to replace the 'RoomRegistry' variable in BlockEntityBerryBush's CheckGrow() method.

        public static IEnumerable<CodeInstruction> PatchBlockEntityBerryBushCheckGrow(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var codes = new List<CodeInstruction>(instructions);
            bool foundRoomReg = false;
            MethodInfo Original = AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition), new[] { typeof(BlockPos) });
            MethodInfo Replacement = AccessTools.Method(typeof(RenRoomRegistry), nameof(RenRoomRegistry.GetRoomForPosition), new[] { typeof(BlockPos) });
            ///Replace the 'RoomRegistry' type for the 'roomreg' variable with 'RenRoomRegistry' in the call to the instance class. (at the time of writing, it's line IL_0014)
            ///IL_0014: call      instance class [VSEssentials]Vintagestory.GameContent.Room [VSEssentials]Vintagestory.GameContent.RoomRegistry::GetRoomForPosition(class [VintagestoryAPI]
            ///IL_0014: call      instance class [VSEssentials]Vintagestory.GameContent.Room [VSEssentials]Vintagestory.GameContent.RenRoomRegistry::GetRoomForPosition(class [VintagestoryAPI]
            for (int i = 0; i < codes.Count; i++) {
                if (codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo method1 && method1 == Original) {
                    foundRoomReg = true;
                    codes[i] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RenRoomRegistry), nameof(RenRoomRegistry.GetRoomForPosition), new[] { typeof(BlockPos) }));
                }
                else if (i + 1 >= codes.Count && foundRoomReg == false) {
                    Console.WriteLine("[HazMod WARNING] Could not find RoomRegistry reference in BlockEntityBerryBush's CheckGrow() method.");
                }
            }
            return codes;
        }

        // -------------------------------------------

        [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), nameof(EntityBehaviorBodyTemperature.OnGameTick))] //EntityBehaviorBodyTemperature.OnGameTick() is a public method
        [HarmonyTranspiler] // Patch to replace the 'Room' and 'RoomRegistry' references in EntityBehaviorBodyTemperature's OnGameTick() method.

        public static IEnumerable<CodeInstruction> PatchEntityBehaviorBodyTemperatureOnGameTick(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var codes = new List<CodeInstruction>(instructions);
            var constructorToFind = AccessTools.Constructor(typeof(RoomRegistry));
            var methodToFind = AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition));
            bool foundRoomReg = false;
            bool foundRoom = false;

            for (int i = 0; i < codes.Count; i++) {
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is ConstructorInfo constructor1 && constructor1 == constructorToFind) {
                    foundRoomReg = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Constructor(typeof(RenRoomRegistry)));
                }
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo method1 && method1 == methodToFind) {
                    foundRoom = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(RenRoomRegistry), nameof(RenRoomRegistry.GetRoomForPosition)));
                }
                else if (i + 1 >= codes.Count && foundRoomReg == false || foundRoom == false) {
                    Console.WriteLine("[HazMod WARNING] Could not find RoomRegistry or Room reference in EntityBehaviorBodyTemperature's OnGameTick() method.");
                }
            }
            return codes;
        }

        // -------------------------------------------

        [HarmonyPatch(typeof(ItemCheese), nameof(ItemCheese.OnTransitionNow))] //ItemCheese.OnTransitionNow() is a public method
        [HarmonyTranspiler] // Patch to replace the 'Room' and 'RoomRegistry' references in ItemCheese's OnTransitionNow() method.

        public static IEnumerable<CodeInstruction> PatchItemCheeseOnTransitionNow(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var codes = new List<CodeInstruction>(instructions);
            var constructorToFind = AccessTools.Constructor(typeof(RoomRegistry));
            var methodToFind = AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition));
            bool foundRoomReg = false;
            bool foundRoom = false;

            for (int i = 0; i < codes.Count; i++) {
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is ConstructorInfo constructor1 && constructor1 == constructorToFind) {
                    foundRoomReg = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Constructor(typeof(RenRoomRegistry)));
                }
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo method1 && method1 == methodToFind) {
                    foundRoom = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(RenRoomRegistry), nameof(RenRoomRegistry.GetRoomForPosition)));
                }
                else if (i + 1 >= codes.Count && foundRoomReg == false || foundRoom == false) {
                    Console.WriteLine("[HazMod WARNING] Could not find RoomRegistry or Room reference in ItemCheese's OnTransitionNow() method.");
                }
            }
            return codes;
        }

        // -------------------------------------------

        [HarmonyPatch(typeof(FruitTreeRootBH), "getGreenhouseTempBonus")] //FruitTreeRootBH.getGreenhouseTempBonus() is a protected method
        [HarmonyTranspiler] // Patch to replace the 'Room' and 'RoomRegistry' references in ItemCheese's OnTransitionNow() method.

        public static IEnumerable<CodeInstruction> PatchFruitTreeRootBHgetGreenhouseTempBonus(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var codes = new List<CodeInstruction>(instructions);
            var methodToFind = AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition));
            bool foundRoom = false;

            for (int i = 0; i < codes.Count; i++) {
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo method1 && method1 == methodToFind) {
                    foundRoom = true;
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(RenRoomRegistry), nameof(RenRoomRegistry.GetRoomForPosition)));
                }
                else if (i + 1 >= codes.Count && foundRoom == false) {
                    Console.WriteLine("[HazMod WARNING] Could not find RoomRegistry or Room reference in ItemCheese's OnTransitionNow() method.");
                }
            }
            return codes;
        }

        // -------------------------------------------
        #endregion
    }
}