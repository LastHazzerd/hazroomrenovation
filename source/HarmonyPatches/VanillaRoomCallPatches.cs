using HarmonyLib;
using hazroomrenovation.source.Code.RenRooms;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace hazroomrenovation.source.HarmonyPatches {
    public delegate BlockPos PositionProviderDelegate();
    /// <summary>
    /// All patches that are meant to adjust the rest of the vanilla game's interactions with the 'RoomRegistry' file, to accomodate the new info contained in the RenRoom class object.
    /// Good lord this is the hardest part of regarding the library mod itself.
    /// </summary>
    [HarmonyPatch]
    internal class VanillaRoomCallPatches {

        [HarmonyPatch(typeof(EntityBehaviorHunger), "SlowTick")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PatchHungerSlowTick(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {            
            if (Harmony.DEBUG == true) FileLog.Log("HungerPatch Start");
            var codeMatcher = new CodeMatcher(instructions, generator);

            LocalBuilder roomLocal;
            codeMatcher.DeclareLocal(typeof(RenRoom), out LocalBuilder renRoomLocal);
            Label jumpLabel;

            #region codeMatcher Patching
            /// Replace every instance of the local for 'room', with a created local 'renRoomLocal'
            codeMatcher.Start();
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Room), nameof(Room.ExitCount))))
                .MatchStartBackwards(new CodeMatch(OpCodes.Stloc_S));//this SHOULD be the 'room' local.
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check the instruction: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            roomLocal = (LocalBuilder)codeMatcher.Operand;
            codeMatcher.Set(OpCodes.Stloc_S, renRoomLocal);
            codeMatcher.Start();
            while (codeMatcher.IsValid) {
                codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldloc_S, roomLocal));
                if (codeMatcher.IsValid) codeMatcher.Set(OpCodes.Ldloc_S, renRoomLocal);
            }

            /// C# Code to [find] __ [Replace]
            codeMatcher.Start();
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition))));
            codeMatcher.InsertAfter(new CodeInstruction(OpCodes.Castclass, typeof(RenRoom)));
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Room), nameof(Room.ExitCount))));
            codeMatcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom), nameof(RenRoom.IsEnclosedRenRoom))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check if the replacement worked: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            jumpLabel = (Label)codeMatcher.Operand;
            codeMatcher.Set(OpCodes.Brtrue_S, jumpLabel);
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check if the branch worked: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.End();
            #endregion

            if (Harmony.DEBUG == true) FileLog.Log("HungerPatch End");
            return codeMatcher.Instructions();
        }

        [HarmonyPatch(typeof(InWorldContainer), "OnTick")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PatchInWorldContainerOnTick (IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            if (Harmony.DEBUG == true) FileLog.Log("ContainerTickPatch Start");
            var codeMatcher = new CodeMatcher(instructions);

            #region codeMatcher Patching
            /// C# Code to [find] __ [Replace]
            codeMatcher.Start();
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition))));
            codeMatcher.InsertAfter(new CodeInstruction(OpCodes.Castclass, typeof(RenRoom)));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check if the cast worked: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.End();
            #endregion

            if (Harmony.DEBUG == true) FileLog.Log("ContainerTickPatch End");
            return codeMatcher.Instructions();
        }

        [HarmonyPatch(typeof(InWorldContainer), nameof(InWorldContainer.GetPerishRate))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PatchInWorldContainerPerish(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            if (Harmony.DEBUG == true) FileLog.Log("ContainerPerishPatch Start");
            var codeMatcher = new CodeMatcher(instructions, generator);
                        
            codeMatcher.DeclareLocal(typeof(RenRoom), out LocalBuilder renRoomLocal);

            int branchStart;
            int branchEnd;

            #region codeMatcher Patching
            codeMatcher.Start();
            //      if (this.room == null)
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(InWorldContainer), "room")));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check start of changes: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Brtrue_S)); //THIS is the start of the first branch we need to change.
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG am at branch start: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            branchStart = codeMatcher.Pos;
            //      this.room = (RenRoom)this.roomReg.GetRoomForPosition(this.positionProvider());
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition))));
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Castclass, typeof(RenRoom)))
                .Advance(1);
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldarg_0)); //THIS should be the branch end for the Brtrue_S instruction found above.
            codeMatcher.CreateLabel(out Label branchTarget);
            branchEnd = codeMatcher.Pos;
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG am at branch end: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.Advance(branchStart - branchEnd);
            codeMatcher.RemoveInstruction()
                .Insert(new CodeInstruction(OpCodes.Brtrue_S, branchTarget));
            codeMatcher.Advance(branchEnd - branchStart); // Should be ldarg.0
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG am I back at branch end: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            //     RenRoom renRoom = (RenRoom)this.room;
            codeMatcher.InsertAfterAndAdvance(
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(InWorldContainer), "room")),
                new CodeInstruction(OpCodes.Castclass, typeof(RenRoom)),
                new CodeInstruction(OpCodes.Stloc_S, renRoomLocal)
                );
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldc_R4));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG am I at ldc.r4 0.0: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldarg_0));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check before deletion: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, renRoomLocal))
                .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom), nameof(RenRoom.SkyLightProportion))));
            while (codeMatcher.Opcode != OpCodes.Div) //should remove instructions after Ldfld RenRoom SkyLightProportion until the given instruction.OpCode.
            {
                codeMatcher.RemoveInstruction();
            }
            codeMatcher.RemoveInstruction(); //removes Div
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check after deletion: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos); //should be stloc.3 (skyLightProportion)
            // if (this.room.IsSmallRoom) [first one]
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldarg_0)); //should be at ldarg.0, the start of if (this.room.IsSmallRoom)
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check for arg.0 at small room check: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.Advance(1); // have to move on to the next one because we don't want to change this small room check.
            //         soilTempWeight = 1f;
            //         soilTempWeight -= 0.4f * skyLightProportion;
            //         soilTempWeight -= 0.5f * renRoom.CellarProportion; || soilTempWeight -= 0.5f * GameMath.Clamp((float)this.room.NonCoolingWallCount / (float)Math.Max(1, this.room.CoolingWallCount), 0f, 1f); 
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldarg_0)); //should be at ldarg.0 for soilTempWeight -= 0.5f * GameMath.Clamp...
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check for arg.0 at soilTemp CellarProportion start: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, renRoomLocal))
                .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom), nameof(RenRoom.CellarProportion))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check before deletion: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos); //should be ldfld int32 VintageStory.GameContent.Room::NonCoolingWallCount
            while (codeMatcher.Opcode != OpCodes.Mul) //should remove instructions until the given instruction.
            {
                codeMatcher.RemoveInstruction();
            }
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check after deletion: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos); //should be mul NULL

            //     float airTemp = temperature + (float)GameMath.Clamp(lightlevel - 11, 0, 10) * lightImportance;
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)11)).ThrowIfInvalid("couldn't find ldc.i4.s 11, might have coded it wrong");
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check if ldc.i4.s 11: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);

            //     float cellarTemp = (float)renRoom.RoomTemp; // make the cellar temp also adjustable by future mods.
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldc_R4)).ThrowIfInvalid("couldn't find ldc.r4 5, might have coded it wrong");
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check if cellarTemp 5: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldloc_S, renRoomLocal)); //load renRoom local
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom),nameof(RenRoom.RoomTemp))));
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Conv_R4));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check that i loaded renroom: " + codeMatcher.InstructionAt(-2) + " | Then field RoomTemp: " + codeMatcher.InstructionAt(-1) + " | Then converted to float (conv.r4): " + codeMatcher.Instruction);
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG ensure next is cellarTemp: " + codeMatcher.InstructionAt(1));

            #endregion

            if (Harmony.DEBUG == true) FileLog.Log("ContainerPerishPatch End");
            return codeMatcher.Instructions();
        }

        [HarmonyPatch(typeof(InWorldContainer), nameof(InWorldContainer.ReloadRoom))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PatchInWorldContainerReloadRoom(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            if (Harmony.DEBUG == true) FileLog.Log("ContainerReloadRoomPatch Start");
            var codeMatcher = new CodeMatcher(instructions);

            #region codeMatcher Patching
            /// C# Code to [find] __ [Replace]
            codeMatcher.Start();
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition))));
            codeMatcher.InsertAfter(new CodeInstruction(OpCodes.Castclass, typeof(RenRoom)));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check if the cast worked: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.End();
            #endregion

            if (Harmony.DEBUG == true) FileLog.Log("ContainerReloadRoomPatch End");
            return codeMatcher.Instructions();
        }

        [HarmonyPatch(typeof(EntityParticleInsect), "playsound")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PatchEntityParticleInsectPlaysound(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            if (Harmony.DEBUG == true) FileLog.Log("InsectPlaysoundPatch Start");
            var codeMatcher = new CodeMatcher(instructions);

            #region codeMatcher Patching
            /// C# Code to [find] __ [Replace]
            codeMatcher.Start();
            //     float attnRoom = (((RenRoom)this.capi.ModLoader.GetModSystem<RoomRegistry>(true).GetRoomForPosition(plrPos.AsBlockPos)).IsEnclosedRenRoom ? 0.1f : 1f);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Room), nameof(Room.ExitCount))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG is this ExitCount: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Castclass, typeof(RenRoom)));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check the cast: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.Advance(1).SetInstruction(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom), nameof(RenRoom.IsEnclosedRenRoom))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check new field: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.Advance(1).SetOpcodeAndAdvance(OpCodes.Brtrue_S); //change branch code from blt.s to brtrue.s
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check new branch: " + codeMatcher.InstructionAt(-1) + " At position: " + (codeMatcher.Pos-1));

            codeMatcher.End();
            #endregion

            if (Harmony.DEBUG == true) FileLog.Log("InsectPlaysoundPatch End");
            return codeMatcher.Instructions();
        }

        [HarmonyPatch(typeof(BlockEntityBeehive), "OnScanForEmptySkep")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PatchBlockEntityBeehiveOnScanForEmptySkep(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            if (Harmony.DEBUG == true) FileLog.Log("OnScanForEmptySkepPatch Start");
            var codeMatcher = new CodeMatcher(instructions, generator);

            codeMatcher.DeclareLocal(typeof(RenRoom), out LocalBuilder renRoomLocal);

            List<Label> labelList = codeMatcher.DistinctLabels(instructions);

            //replace every instance of 'room' with the new local 'renRoomLocal', make sure to accomodate labels
            codeMatcher.Start();
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Stloc_0));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG is it Stloc.0: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Stloc_S, renRoomLocal));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG is it now Stloc.S: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            while (codeMatcher.Remaining >= 0) {
                codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldloc_0));
                if (codeMatcher.IsInvalid) break;
                if (Harmony.DEBUG == true) FileLog.Log("DEBUG is it Ldloc.0: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
                List<Label> tmpLabel = codeMatcher.Instruction.ExtractLabels();
                codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldloc_S, renRoomLocal).WithLabels(tmpLabel));
                if (Harmony.DEBUG == true) FileLog.Log("DEBUG is it now Ldloc.S: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            }

            #region patch
            codeMatcher.Start();

            //     RenRoom room = (RenRoom)((roomRegistry != null) ? roomRegistry.GetRoomForPosition(this.Pos) : null);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Call));
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Castclass, typeof(RenRoom)).WithLabels(labelList.ElementAt(1)));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check if the cast worked: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            //codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Stloc_0));
            //if (Harmony.DEBUG == true) FileLog.Log("DEBUG check labels: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            //codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Stloc_S, renRoomLocal));
            //if (Harmony.DEBUG == true) FileLog.Log("DEBUG check label removal: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);

            //     this.roomness = (float)((room != null && room.IsGreenHouseRenRoom) ? 1 : 0);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld)); // should be 'SkylightCount'
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check before replacement: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom), nameof(RenRoom.IsGreenHouseRenRoom))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check after replacement: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.Advance(1);
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check before deletion: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            while (codeMatcher.Opcode != OpCodes.Brfalse_S) {
                if (Harmony.DEBUG == true) FileLog.Log("Deleting: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
                codeMatcher.RemoveInstruction();
            }
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check after deletion: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetOpcodeAndAdvance(OpCodes.Brtrue_S); //just change false to true, hopefully it works without issue.
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check branch with label4: " + codeMatcher.InstructionAt(-1) + " At position: " + (codeMatcher.Pos-1));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check where we are: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);

            #endregion

            if (Harmony.DEBUG == true) FileLog.Log("OnScanForEmptySkepPatch End");
            return codeMatcher.Instructions();
        }

        [HarmonyPatch(typeof(BlockEntityBerryBush), "CheckGrow")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PatchBlockEntityBerryBushCheckGrow(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            if (Harmony.DEBUG == true) FileLog.Log("PatchBerryBushCheckGrow Start");
            var codeMatcher = new CodeMatcher(instructions, generator);

            codeMatcher.DeclareLocal(typeof(RoomRegistry), out LocalBuilder roomreg);
            codeMatcher.DeclareLocal(typeof(RenRoom), out LocalBuilder renRoomLocal);


            List<Label> labelList = codeMatcher.DistinctLabels(instructions);
            LocalBuilder oldRoomLocal;

            #region pre-patch Local changes
            //change all 'room' locals to 'renroom'.
            codeMatcher.Start();
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG pre-patch Local changes START");
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG precheck at call getroom: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.Advance(1); //should be stloc.s room
            oldRoomLocal = (LocalBuilder)codeMatcher.Operand; //get the room local for reference so i can search for them all.
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Stloc_S, renRoomLocal)); //should NOT have label 5
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG precheck room is now renroom no label: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.Start().Advance(1); //just make sure I don't miss any Stloc.S 
            while (codeMatcher.IsValid == true) { //loop until codeMatcher runs out of valid positions.
                codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldloc_S, oldRoomLocal));
                if (codeMatcher.IsValid == true) {
                    //if (Harmony.DEBUG == true) FileLog.Log("DEBUG precheck replacing: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
                    List<Label> tempLabel = codeMatcher.Labels;
                    codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldloc_S, renRoomLocal).WithLabels(tempLabel));
                    if (Harmony.DEBUG == true) FileLog.Log("DEBUG precheck replaced: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
                }
            }
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG pre-patch Local changes END");
            #endregion

            #region patch
            codeMatcher.Start();

            //      RenRoom room = (RenRoom)((roomreg != null) ? roomreg.GetRoomForPosition(this.upPos) : null);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition)))); //finally gets to the actual call.
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG at call getroom: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Castclass, typeof(RenRoom)).WithLabels(labelList.ElementAt(4)));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG confirm the cast with label 5: " + codeMatcher.Instruction + " At: " + codeMatcher.Pos);

            //         this.roomness = ((room != null && room.IsGreenHouseRenRoom) ? room.Roomness : 0);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Room), nameof(Room.SkylightCount))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG am I at ldfld skylightcount: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom),nameof(RenRoom.IsGreenHouseRenRoom))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG ensure IsGreenHouseRenRoom: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.Advance(1); //make sure I don't delete the greenhouse check
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG before deletion: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            while (codeMatcher.Opcode != OpCodes.Brfalse_S) {
                codeMatcher.RemoveInstruction();
            }
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG after deletion: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetOpcodeAndAdvance(OpCodes.Brtrue_S); //should replace brfalse [label8] with brtrue [label8]
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check if Brtrue: " + codeMatcher.InstructionAt(-1) + " At position: " + (codeMatcher.Pos-1));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check current ldc labels: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldc_I4_0).WithLabels(labelList.ElementAt(5))); //should only be label 6, label 7 was deleted.
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check new ldc labels, should be label 6: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldc_I4_1)); //roomness = 1
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldloc_S, renRoomLocal).WithLabels(labelList.ElementAt(7))); //should replace ldc.i4.1, and have label 8
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check that renroom has label 8: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom),nameof(RenRoom.Roomness))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check that roomness is the field: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);

            //move down to where the greenhouse temp bonus gets applied
            codeMatcher.Advance(1).MatchStartForward(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(BlockEntityBerryBush),nameof(BlockEntityBerryBush.roomness))));

            //		temperature += (float)this.roomness; //Patch to make this much more flexible as a bonus
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldc_R4));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG are we at ldc.r4: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldarg_0));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check ldarg.0: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(BlockEntityBerryBush),nameof(BlockEntityBerryBush.roomness))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check that roomness is the field: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Conv_R4)); //temp bonus should now be variable dependant on roomness' value.
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check conv.r4: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);

            codeMatcher.End();
            #endregion

            if (Harmony.DEBUG == true) FileLog.Log("PatchBerryBushCheckGrow End");
            return codeMatcher.Instructions();
        }

        [HarmonyPatch(typeof(BlockEntityFarmland), "Update")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PatchBlockEntityFarmlandUpdate(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            if (Harmony.DEBUG == true) FileLog.Log("PatchBlockEntityFarmlandUpdate Start");
            var codeMatcher = new CodeMatcher(instructions, generator);

            codeMatcher.DeclareLocal(typeof(RoomRegistry), out LocalBuilder roomreg);
            codeMatcher.DeclareLocal(typeof(RenRoom), out LocalBuilder renRoomLocal);


            List<Label> labelList = codeMatcher.DistinctLabels(instructions);
            LocalBuilder oldRoomLocal;

            #region pre-patch Local changes
            //change all 'room' locals to 'renroom', also cast the getroomforposition method as RenRoom.
            codeMatcher.Start();
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG pre-patch Local changes START");
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(RoomRegistry),nameof(RoomRegistry.GetRoomForPosition))));
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition))));
            codeMatcher.InsertAfterAndAdvance(new CodeMatch(OpCodes.Castclass, typeof(RenRoom)));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG precheck class cast RenRoom: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.Advance(1); //should be stloc.s room
            oldRoomLocal = (LocalBuilder)codeMatcher.Operand; //get the room local for reference so i can search for them all.
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Stloc_S, renRoomLocal).WithLabels(labelList.ElementAt(10)));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG precheck stloc for room is now renroom: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);            
            codeMatcher.Start().Advance(1); //just make sure I don't miss any Stloc.S 
            while (codeMatcher.IsValid == true) { //loop until codeMatcher runs out of valid positions.
                codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldloc_S, oldRoomLocal));
                if (codeMatcher.IsValid == true) {
                    //if (Harmony.DEBUG == true) FileLog.Log("DEBUG precheck replacing: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
                    List<Label> tempLabel = codeMatcher.Labels;
                    codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldloc_S, renRoomLocal).WithLabels(tempLabel));
                    //if (Harmony.DEBUG == true) FileLog.Log("DEBUG precheck replaced: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
                }
            }
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG pre-patch Local changes END");
            #endregion

            #region patch
            codeMatcher.Start();

            //      RoomRegistry roomreg = this.blockFarmland.roomreg;
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(BlockFarmland), nameof(BlockFarmland.roomreg))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check patch START: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(new CodeInstruction(OpCodes.Stloc_S, roomreg)));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check new set loc: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);

            //      RenRoom room = (RenRoom)((roomreg != null) ? roomreg.GetRoomForPosition(this.upPos) : null);
            codeMatcher.Advance(1); //should be Dup
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check GetRoom start: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldloc_S, roomreg));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check loaded loc: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Pop));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG am I at Pop: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.RemoveInstruction(); //MIGHT PREFER JUST TO noOP THIS?
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Br_S)); //has label 11
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, roomreg).WithLabels(labelList.ElementAt(9)));
            codeMatcher.Advance(1).SetInstruction(new CodeInstruction(OpCodes.Ldarg_0));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG make sure current ldarg has no label: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);

            //      this.roomness = ((room != null && room.IsGreenHouseRenRoom) ? room.Roomness : 0);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Room), nameof(Room.SkylightCount))));
            codeMatcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom), nameof(RenRoom.IsGreenHouseRenRoom))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check before deletion: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            while (codeMatcher.Opcode != OpCodes.Brfalse_S) {
                codeMatcher.RemoveInstruction();
            }
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check after deletion: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetOpcodeAndAdvance(OpCodes.Brtrue_S);
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check new True branch label: " + codeMatcher.InstructionAt(-1) + " At position: " + (codeMatcher.Pos - 1));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check old ldc.i4 labels: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldc_I4_0).WithLabels(labelList.ElementAt(11)));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check new ldc.i4 labels: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldc_I4_1));
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldloc_S, renRoomLocal).WithLabels(labelList.ElementAt(13)));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check new ldloc labels: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom), nameof(RenRoom.Roomness))));
            
            //      Make your way down to the next needed changes.
            codeMatcher.Advance(1).MatchStartForward(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(BlockEntityFarmland), nameof(BlockEntityFarmland.roomness)))); // if (this.roomness > 0)
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ble_S));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG Am I at Ble.S [label 24]: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);

            //      conds.Temperature += (float)this.roomness;
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldc_R4));
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldarg_0))
                .InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(BlockEntityFarmland), nameof(BlockEntityFarmland.roomness))))
                .InsertAfterAndAdvance(new CodeInstruction(OpCodes.Conv_R4));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG Check am I at end of conds.Temp (conv.r4): " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            
            codeMatcher.End();
            #endregion

            if (Harmony.DEBUG == true) FileLog.Log("PatchBlockEntityFarmlandUpdate End");
            return codeMatcher.Instructions();
        }

        [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), nameof(EntityBehaviorBodyTemperature.OnGameTick))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PatchBodyTempOnGameTick(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            if (Harmony.DEBUG == true) FileLog.Log("BodyTempPatch Start");
            var codeMatcher = new CodeMatcher(instructions, generator);

            codeMatcher.DeclareLocal(typeof(RenRoom), out LocalBuilder renRoomLocal);
            object targetBranchLabel;

            //TODO, might need to figure out a variable here to affect how quickly/effectively body temp will be able to change depending on the room temp.
            //This will be in relation to rooms with temp control, or rooms where the point is being hot/cold, affecting the player. Staying in a freezer or sauna too long should have adverse effects.
            //Least intrusive way would be affecting the 'near heat source strength' to be affected by insulation, humidity, etc.

            #region codeMatcher Patching
            /// Replace every instance of the local found at index 2, with a created local 'renRoomLocal'
            codeMatcher.Start()
                .MatchStartForward(new CodeMatch(OpCodes.Stloc_2))
                .Set(OpCodes.Stloc_S, renRoomLocal);
            codeMatcher.Start();
            while (codeMatcher.IsValid) {
                codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldloc_2));
                if (codeMatcher.IsValid) codeMatcher.Set(OpCodes.Ldloc_S, renRoomLocal);
            }

            /// Patch method to use RenRoom features.
            codeMatcher.Start();

            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Bne_Un_S));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check if at bne.un => label1: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            //         if (this.api.Side == EnumAppSide.Server)
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Bne_Un));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check if at bne.un => label9: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            targetBranchLabel = codeMatcher.Operand;

            //             RenRoom room = (RenRoom)this.api.ModLoader.GetModSystem<RoomRegistry>(true).GetRoomForPosition(this.plrpos);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check GetRoom: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Castclass, typeof(RenRoom)));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check cast class: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);

            //             this.inEnclosedRoom = room.IsEnclosedRenRoom;
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Room), nameof(Room.ExitCount))));
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom), nameof(RenRoom.IsEnclosedRenRoom))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check before deletion = ldfld 'IsEnclosedRenRoom': " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.Advance(1); //make sure not to delete the IsEnclosedRenRoom check
            while (codeMatcher.Opcode != OpCodes.Stfld) {
                codeMatcher.RemoveInstruction();
            }
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check after deletion = stfld 'isEnclosedRoom': " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "inEnclosedRoom")));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG no labels: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);

            //             this.blockAccess.WalkBlocks(min, max, delegate(Block block, int x, int y, int z)
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(IBlockAccessor),nameof(IBlockAccessor.WalkBlocks)))).ThrowIfInvalid("could not find WalkBlocks method");

            //             if (this.inEnclosedRoom)
            codeMatcher.InsertAfterAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "inEnclosedRoom")));
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Brfalse_S, targetBranchLabel));//branch to label 9
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG confirm brfalse.s label9: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);
            codeMatcher.InsertAfterAndAdvance(
            //                 this.nearHeatSourceStrength *= room.tempSourceModifier;
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EntityBehaviorBodyTemperature),"get_nearHeatSourceStrength")), //not sure if i need to include the "get_"
                new CodeInstruction(OpCodes.Ldloc_S, renRoomLocal),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom), nameof(RenRoom.tempSourceModifier))),
                new CodeInstruction(OpCodes.Mul),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EntityBehaviorBodyTemperature), "set_nearHeatSourceStrength")) //not sure if i need to include the "set_"
                );
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG ensure call method set_nearHeatSourceStrength: " + codeMatcher.Instruction + " At position: " + codeMatcher.Pos);            
            #endregion

            if (Harmony.DEBUG == true) FileLog.Log("BodyTempPatch End");
            return codeMatcher.Instructions();
        }

        [HarmonyPatch(typeof(ItemCheese), nameof(ItemCheese.OnTransitionNow))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PatchItemCheeseOnTransitionNow(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            if (Harmony.DEBUG == true) FileLog.Log("PatchItemCheeseOnTransitionNow Start");
            var codeMatcher = new CodeMatcher(instructions, generator);

            codeMatcher.DeclareLocal(typeof(RenRoom), out LocalBuilder renRoomLocal);

            codeMatcher.DefineLabel(out Label targetLabel);
            List<Label> labelList = [targetLabel];
            
            #region patch
            codeMatcher.Start();

            //             RenRoom roomForPosition = (RenRoom)this.api.ModLoader.GetModSystem<RoomRegistry>(true).GetRoomForPosition(pos);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition))));
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Castclass, typeof(RenRoom)));
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Stloc_S, renRoomLocal));

            //             if ((roomForPosition.ExitCount > 0 || roomForPosition.ExposingBlockCount > 0) && lightlevel < 2)
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Room), nameof(Room.ExitCount))));
            codeMatcher.Advance(-1).InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, renRoomLocal));
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldc_I4_0));
            codeMatcher.InsertAfterAndAdvance(
                new CodeInstruction(OpCodes.Bgt_S, targetLabel),
                new CodeInstruction(OpCodes.Ldloc_S, renRoomLocal),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom), nameof(RenRoom.ExposingBlockCount))),
                new CodeInstruction(OpCodes.Ldc_I4_0)
                );
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldc_I4_2));
            codeMatcher.Advance(-1).AddLabels(labelList);
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check labels: " + codeMatcher.Instruction);

            codeMatcher.End();
            #endregion

            if (Harmony.DEBUG == true) FileLog.Log("PatchItemCheeseOnTransitionNow End");
            return codeMatcher.Instructions();
        }

        [HarmonyPatch(typeof(FruitTreeRootBH), "getGreenhouseTempBonus")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PatchFruitTreeRootBHgetGreenhouse(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            if (Harmony.DEBUG == true) FileLog.Log("PatchFruitTreeRootBHgetGreenhouse Start");
            var codeMatcher = new CodeMatcher(instructions, generator);

            codeMatcher.DeclareLocal(typeof(RenRoom), out LocalBuilder renRoomLocal);

            List<Label> labelList = codeMatcher.DistinctLabels(instructions);

            #region patch
            codeMatcher.Start();

            //         RenRoom room = ((roomRegistry != null) ? ((RenRoom)roomRegistry.GetRoomForPosition(this.be.Pos)) : null);
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition))));
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(RoomRegistry), nameof(RoomRegistry.GetRoomForPosition))));
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Castclass, typeof(RenRoom)));
            codeMatcher.Advance(1).SetInstruction(new CodeInstruction(OpCodes.Stloc_S, renRoomLocal).WithLabels(labelList.ElementAt(1))); //replace stloc.0 with stloc.s RenRoom
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check label on stloc: " + codeMatcher.Instruction + " at: " + codeMatcher.Pos);

            //         if (room.IsGreenHouseRenRoom)
            codeMatcher.Advance(1).SetInstruction(new CodeInstruction(OpCodes.Ldloc_S, renRoomLocal)); //replace ldloc.0 with ldloc.s renroom at the start of if statment.
            codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom), nameof(RenRoom.IsGreenHouseRenRoom))));
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check before removal: " + codeMatcher.Instruction + " at: " + codeMatcher.Pos);
            codeMatcher.Advance(1);
            while (codeMatcher.Opcode != OpCodes.Ldc_I4_1) {
                codeMatcher.RemoveInstruction();
            }
            codeMatcher.RemoveInstruction();
            codeMatcher.RemoveInstruction();
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check if at ble: "+codeMatcher.Instruction+" at: "+codeMatcher.Pos);
            codeMatcher.SetOpcodeAndAdvance(OpCodes.Brfalse_S);
            if (Harmony.DEBUG == true) FileLog.Log("DEBUG check if now brfalse.s: " + codeMatcher.InstructionAt(-1) + " at: " + (codeMatcher.Pos-1));
            //             return (float)room.Roomness;
            codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldloc_S, renRoomLocal));
            codeMatcher.InsertAfterAndAdvance(
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RenRoom),nameof(RenRoom.Roomness))),
                new CodeInstruction(OpCodes.Conv_R4)
                );

            codeMatcher.End();
            #endregion

            if (Harmony.DEBUG == true) FileLog.Log("PatchFruitTreeRootBHgetGreenhouse End");
            return codeMatcher.Instructions();
        }

        #region attempted fruit tree blockinfo transpiler, currently not working
        //[HarmonyPatch(typeof(BlockEntityFruitTreePart), nameof(BlockEntityFruitTreePart.GetBlockInfo))]
        //[HarmonyTranspiler]
        //// This patch is just to make the greenhouse temp bonus appear in the 
        //public static IEnumerable<CodeInstruction> PatchFruitTreePartGetBlockInfo(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        //    if (Harmony.DEBUG == true) FileLog.Log("PatchFruitTreePartGetBlockInfo Start");
        //    var codeMatcher = new CodeMatcher(instructions, generator);

        //    codeMatcher.DeclareLocal(typeof(float), out LocalBuilder roomness);

        //    codeMatcher.DefineLabel(out Label targetLabel);
        //    List<Label> labelList = [targetLabel];

        //    CodeInstruction instCopy;

        //    #region patch
        //    codeMatcher.Start();

        //    //         dsc.AppendLine(Lang.Get("treestate", new object[] { Lang.Get("treestate-" + props.State.ToString().ToLowerInvariant(), Array.Empty<object>()) }));
        //    codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Call));
        //    instCopy = codeMatcher.Instruction;

        //    //         dsc.AppendLine(Lang.Get("treestate", new object[] { Lang.Get("treestate-" + props.State.ToString().ToLowerInvariant(), Array.Empty<object>()) }));
        //    codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(String),nameof(String.ToLowerInvariant))));
        //    codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Pop));

        //    //     float roomness = this.rootBh.applyGreenhouseTempBonus(0f);
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldarg_0));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(BlockEntityFruitTreePart),"rootBh")));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldc_R4, (float)0.0));//might be an issue, ensure the numeral value is read correctly.
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(FruitTreeRootBH), nameof(FruitTreeRootBH.applyGreenhouseTempBonus))));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Stloc_S, roomness));

        //    //     if (roomness > 0f)
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, roomness));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldc_R4, (float)0.0));//might be an issue, ensure the numeral value is read correctly.
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ble_Un_S, targetLabel));
        //    if (Harmony.DEBUG == true) FileLog.Log("DEBUG confirm label: "+codeMatcher.Instruction+" At: "+codeMatcher.Pos);

        //    //         dsc.AppendLine("+" + roomness.ToString() + "°C " + Lang.Get("greenhousetempbonus", Array.Empty<object>()));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldarg_2));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldstr, "+"));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldloca_S, roomness));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(String), nameof(String.ToString))));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldstr, "°C "));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Ldstr, "greenhousetempbonus"));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(instCopy)); //unsure how to write 'call static System.Object[] System.Array::Empty()' in harmony. The same instruction exists earlier in the stack so i copied it.
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Lang), nameof(Lang.Get))));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(String), "Concat", new Type[] { typeof(string), typeof(string), typeof(string), typeof(string) })));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(StringBuilder), nameof(StringBuilder.AppendLine), new Type[] { typeof(string) })));
        //    codeMatcher.InsertAfterAndAdvance(new CodeInstruction(OpCodes.Pop));

        //    //     base.GetBlockInfo(forPlayer, dsc);
        //    codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldarg_0));
        //    codeMatcher.AddLabels(labelList);
        //    if (Harmony.DEBUG == true) FileLog.Log("DEBUG confirm label: " + codeMatcher.Instruction + " At: " + codeMatcher.Pos);


        //    codeMatcher.End();
        //    #endregion

        //    if (Harmony.DEBUG == true) FileLog.Log("PatchFruitTreePartGetBlockInfo End");
        //    return codeMatcher.Instructions();
        //}
        #endregion
    }
}
