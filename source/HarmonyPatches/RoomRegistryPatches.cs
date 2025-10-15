using Cairo;
using HarmonyLib;
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
using Vintagestory;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using static OpenTK.Graphics.OpenGL.GL;

#nullable disable

namespace hazroomrenovation.source.HarmonyPatches {
    /// <summary>
    /// All Harmony patches directed at the vanilla code's RoomRegistry.cs file.
    /// </summary>
    [HarmonyPatch]
    public class RoomRegistryPatches {

        #region Needed Methods to improve search functionality and optimization     
        
        public static int exposingSearch(int maxSize, int miny, BlockPos seedPos, BlockPos startPos, BlockFacing forwardFace, int iteration, int[] currentVisited, ICachingBlockAccessor blockAccessor, out BlockPos bookmarkPos, out BlockPos exitPos) {
            BlockFacing facing = forwardFace, altfacing = BlockFacing.UP;
            BlockPos npos = startPos, bpos = startPos, returnPos = startPos;
            Block bBlock = blockAccessor.GetBlock(bpos), nBlock = bBlock;
            exitPos = null; //will only be set if we confirm there's a location that needs an exit search.
            bookmarkPos = returnPos; //Used to let the main search know where they need to be start from.
            int posY = seedPos.Y - ((ARRAYSIZE - 1) / 2), 
                posX = seedPos.X - ((ARRAYSIZE - 1) / 2), 
                posZ = seedPos.Z - ((ARRAYSIZE - 1) / 2);
            int dy = startPos.Y - posY,
                dx = startPos.X - posX,
                dz = startPos.Z - posZ;
            int heatRetention, visitedIndex,
                potentialExposed = 1;
            //make sure above block isn't open air.            
            facing = BlockFacing.UP;
            facing.IterateThruFacingOffsets(npos);
            if (blockAccessor.IsValidPos(npos)) {
                nBlock = blockAccessor.GetBlock(npos);
                heatRetention = nBlock.GetRetention(bpos, facing.Opposite, EnumRetentionType.Heat);
                dy = npos.Y - posY;
                visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                if (Math.Abs(currentVisited[visitedIndex]) != iteration) { //if we've haven't visited this pos yet.
                    currentVisited[visitedIndex] = iteration;
                } else if (currentVisited[visitedIndex] < 0) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                    currentVisited[visitedIndex] = -(currentVisited[visitedIndex]);
                }

                if (nBlock.Id == 0) { //above block is open Air; Invalidated exposing wall block search. 
                    potentialExposed = 0;
                    return potentialExposed;
                }
                else if (heatRetention == 0 && ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockDoor))) { 
                    //above block is exposing.
                    potentialExposed++;
                }
                else { //if neither of these, then we assume it is a solid block.
                    npos.Set(bpos);
                    nBlock = bBlock; //if solid, we return nBlock to bBlock's starting position so we can come back if the forward block is invalid.
                }
            }
            else {
                return -potentialExposed; //if we hit an invalid position, return a negative number to let the main search know its invalid and should add to noncoolingblocks and execute a 'continue'.
            }
            if (dy == miny) { //if we're at the lowest Y pos we've been so far, make sure to check the downward block, as it is an unknown.
                facing = BlockFacing.DOWN;
                npos.Set(returnPos);
                facing.IterateThruFacingOffsets(npos);
                if (blockAccessor.IsValidPos(npos)) {
                    nBlock = blockAccessor.GetBlock(npos);
                    heatRetention = bBlock.GetRetention(bpos, facing.Opposite, EnumRetentionType.Heat);
                    dy = npos.Y - posY;

                    visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                    if (Math.Abs(currentVisited[visitedIndex]) != iteration) { //if we've haven't visited this pos yet.
                        currentVisited[visitedIndex] = iteration;
                    } else if (currentVisited[visitedIndex] < 0) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                        currentVisited[visitedIndex] = -(currentVisited[visitedIndex]);
                    }

                    if (nBlock.Id == 0) { //below block is open Air; Invalidated exposing wall block search. 
                        potentialExposed = 0;
                        return potentialExposed;
                    }
                    else if (heatRetention == 0 && ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockDoor))) {
                        //below block is exposing.
                        altfacing = facing;
                        potentialExposed++;
                    }
                    else {//if neither of these, then we assume it is a solid block.
                        npos.Set(bpos);
                        nBlock = bBlock; //if solid, we return nBlock to bBlock's starting position so we can come back if the forward block is invalid.
                    }
                }
                else {
                    return -potentialExposed;
                }
            }
            //make sure forward block isn't open air, and that only one facing has another exposing block, if any.
            facing = forwardFace;
            facing.IterateThruFacingOffsets(bpos);
            if (blockAccessor.IsValidPos(bpos)) {
                bBlock = blockAccessor.GetBlock(bpos);
                heatRetention = bBlock.GetRetention(bpos, facing.Opposite, EnumRetentionType.Heat);
                
                switch (facing.Index) {
                    case 0: dz = bpos.Z - posZ; break; //North -z
                    case 1: dx = bpos.X - posX; break; //East +x
                    case 2: dz = bpos.Z - posZ; break; //South +z
                    case 3: dx = bpos.X - posX; break; //West -x
                    case 4: dy = bpos.Y - posY; break; //Up +y
                    case 5: dy = bpos.Y - posY; break; //Down -y
                }

                visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                if (Math.Abs(currentVisited[visitedIndex]) != iteration) { //if we've haven't visited this pos yet.
                    currentVisited[visitedIndex] = iteration;
                } else if (currentVisited[visitedIndex] < 0) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                    currentVisited[visitedIndex] = -(currentVisited[visitedIndex]);
                }

                if (bBlock.Id == 0) { //forward block is open; Invalidated exposing wall block search. 
                    potentialExposed = 0; bookmarkPos = bpos; //no need to reset bpos, we'd travel here anyway if we end up keeping this position.
                    return potentialExposed;
                }
                else if (heatRetention == 0 && ((bBlock is BlockStairs) || (bBlock is BlockFence) || (bBlock is BlockSlab) || (bBlock is BlockDoor))) {  //infront block is exposing.
                    if (potentialExposed > 1) { //if the above block was already found to be exposing, then this exposing block exceeds the allowed number of exposing blocks to qualify as a viable wall block.
                        potentialExposed = 0; bookmarkPos = bpos;
                        return potentialExposed;
                    }
                    else { //if we made it this far, then the foward block being exposed means we must follow it until we hit an open block or boundry.
                        potentialExposed++; bookmarkPos = bpos;
                    }
                    npos.Set(bpos); //we want to continue forward.
                }
                else {//if neither of these, then we assume the forward block is solid.
                    if (potentialExposed > 1) {
                        facing = altfacing;     //If we found an exposed block above, this will set us to Up, if insted it was below, this will be Down.
                    }
                    bpos.Set(npos); //move the bBlock back to nBlock, which is either at the start position, or in an exposing block above it.
                    bookmarkPos = bpos;
                    bBlock = nBlock;
                }
            }
            else {
                return -potentialExposed;
            }
            //we've now determined which block we intend to start from for this exposing block search, and which facing we should head.
            #region Navigate through the remaining exposed blocks, if any.
            if (potentialExposed > 1 || altfacing.Index == 5) { //Check if we have more iterating to do.
                altfacing = BlockFacing.SOUTH; //need another facing to make sure we don't have any extra exposing blocks.
                if (facing.Index == forwardFace.Index) {
                    altfacing = BlockFacing.UP;
                    returnPos.Set(bpos); bookmarkPos = returnPos; //create a backup Pos in case we need to return.
                }

                altfacing.IterateThruFacingOffsets(bpos); //check for non-solid blocks.
                if (blockAccessor.IsValidPos(bpos)) {
                    bBlock = blockAccessor.GetBlock(bpos);
                    heatRetention = nBlock.GetRetention(bpos, altfacing.Opposite, EnumRetentionType.Heat);

                    switch (facing.Index) {
                        case 0: dz = bpos.Z - posZ; break; //North -z
                        case 1: dx = bpos.X - posX; break; //East +x
                        case 2: dz = bpos.Z - posZ; break; //South +z
                        case 3: dx = bpos.X - posX; break; //West -x
                        case 4: dy = bpos.Y - posY; break; //Up +y
                        case 5: dy = bpos.Y - posY; break; //Down -y
                    }

                    visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                    if (Math.Abs(currentVisited[visitedIndex]) != iteration) { //if we've haven't visited this pos yet.
                        currentVisited[visitedIndex] = iteration;
                    }
                    else if (currentVisited[visitedIndex] < 0) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                        currentVisited[visitedIndex] = -(currentVisited[visitedIndex]);
                    }

                    if (bBlock.Id == 0 || heatRetention == 0) { //adjacent block is not solid. 
                        potentialExposed = 0; bookmarkPos = npos; //invalidated search.
                        return potentialExposed;
                    }
                    bBlock = nBlock; //return to current block.
                }
                else {
                    return -potentialExposed;
                }
                bpos.Set(npos); //return to current pos.

                facing.IterateThruFacingOffsets(npos); //move the facing direction we want.
                heatRetention = nBlock.GetRetention(npos, facing, EnumRetentionType.Heat);
                if (!blockAccessor.IsValidPos(npos)) { return -potentialExposed; }
                while (potentialExposed > 0 && heatRetention == 0 && ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockDoor))) { //while the next block is exposing.
                    potentialExposed++; //we've found another potential exposing block.

                    altfacing.IterateThruFacingOffsets(bpos); //check for non-solid blocks.
                    if (blockAccessor.IsValidPos(bpos)) {
                        bBlock = blockAccessor.GetBlock(bpos);
                        heatRetention = nBlock.GetRetention(bpos, altfacing.Opposite, EnumRetentionType.Heat);

                        switch (facing.Index) {
                            case 0: dz = bpos.Z - posZ; break; //North -z
                            case 1: dx = bpos.X - posX; break; //East +x
                            case 2: dz = bpos.Z - posZ; break; //South +z
                            case 3: dx = bpos.X - posX; break; //West -x
                            case 4: dy = bpos.Y - posY; break; //Up +y
                            case 5: dy = bpos.Y - posY; break; //Down -y
                        }

                        visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                        if (Math.Abs(currentVisited[visitedIndex]) != iteration) { //if we've haven't visited this pos yet.
                            currentVisited[visitedIndex] = iteration;
                        }
                        else if (currentVisited[visitedIndex] < 0) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                            currentVisited[visitedIndex] = -(currentVisited[visitedIndex]);
                        }

                        if (bBlock.Id == 0 || heatRetention == 0) { //adjacent block is not solid. 
                            potentialExposed = 0; //invalidated search.
                            if (facing.Index != forwardFace.Index) { bookmarkPos = returnPos; }
                            else { bookmarkPos = npos; }
                            return potentialExposed;
                        }
                        bBlock = nBlock; //return bBlock to current block.
                    }
                    else {
                        return -potentialExposed;
                    }
                    bpos.Set(npos); //return bpos to current pos.

                    facing.IterateThruFacingOffsets(npos); //move the facing direction we want.
                    if (blockAccessor.IsValidPos(npos)) {
                        nBlock = blockAccessor.GetBlock(npos);
                        heatRetention = nBlock.GetRetention(npos, facing, EnumRetentionType.Heat);

                        switch (facing.Index) {
                            case 0: dz = npos.Z - posZ; break; //North -z
                            case 1: dx = npos.X - posX; break; //East +x
                            case 2: dz = npos.Z - posZ; break; //South +z
                            case 3: dx = npos.X - posX; break; //West -x
                            case 4: dy = npos.Y - posY; break; //Up +y
                            case 5: dy = npos.Y - posY; break; //Down -y
                        }

                        visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                        if (Math.Abs(currentVisited[visitedIndex]) != iteration) { //if we've haven't visited this pos yet.
                            currentVisited[visitedIndex] = iteration;
                        }
                        else if (currentVisited[visitedIndex] < 0) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                            currentVisited[visitedIndex] = -(currentVisited[visitedIndex]);
                        }

                        if (nBlock.Id == 0) { //next block is open air. 
                            potentialExposed = 0; //invalidated search.
                            if (facing.Index != forwardFace.Index) { bookmarkPos = returnPos; }
                            else { bookmarkPos = npos; }
                            return potentialExposed;
                        }
                    }
                    else {
                        return -potentialExposed;
                    } //if the next block isn't valid then we can't continue the search anyway.
                }//leaving this while loop means we found the last position in the series of adjacent exposing blocks.
                 //if we headed downwards, we need to return to the lane we were originally so we can check up.
                if (facing.Index == 5) {
                    facing = BlockFacing.UP;
                    npos.Set(returnPos);
                    bpos.Set(npos);

                    facing.IterateThruFacingOffsets(npos); //move the facing we want.
                    heatRetention = nBlock.GetRetention(npos, facing, EnumRetentionType.Heat);
                    if (!blockAccessor.IsValidPos(npos)) { return -potentialExposed; }
                    while (potentialExposed > 0 && heatRetention == 0 && ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockDoor))) { //while the next block is exposing.
                        potentialExposed++; //we've found another potential exposing block.

                        altfacing.IterateThruFacingOffsets(bpos); //check for non-solid blocks.
                        if (blockAccessor.IsValidPos(bpos)) {
                            bBlock = blockAccessor.GetBlock(bpos);
                            heatRetention = nBlock.GetRetention(bpos, altfacing.Opposite, EnumRetentionType.Heat);

                            switch (facing.Index) {
                                case 0: dz = bpos.Z - posZ; break; //North -z
                                case 1: dx = bpos.X - posX; break; //East +x
                                case 2: dz = bpos.Z - posZ; break; //South +z
                                case 3: dx = bpos.X - posX; break; //West -x
                                case 4: dy = bpos.Y - posY; break; //Up +y
                                case 5: dy = bpos.Y - posY; break; //Down -y
                            }

                            visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                            if (Math.Abs(currentVisited[visitedIndex]) != iteration) { //if we've haven't visited this pos yet.
                                currentVisited[visitedIndex] = iteration;
                            }
                            else if (currentVisited[visitedIndex] < 0) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                                currentVisited[visitedIndex] = -(currentVisited[visitedIndex]);
                            }

                            if (bBlock.Id == 0 || heatRetention == 0) { //adjacent block is not solid. 
                                potentialExposed = 0; //invalidated search.
                                bookmarkPos = returnPos;
                                return potentialExposed;
                            }
                            bBlock = nBlock; //return bBlock to current block.
                        }
                        else {
                            return -potentialExposed;
                        }
                        bpos.Set(npos); //return bpos to current pos.

                        facing.IterateThruFacingOffsets(npos); //move the facing we want.
                        if (blockAccessor.IsValidPos(npos)) {
                            nBlock = blockAccessor.GetBlock(npos);
                            heatRetention = nBlock.GetRetention(npos, facing, EnumRetentionType.Heat);

                            switch (facing.Index) {
                                case 0: dz = npos.Z - posZ; break; //North -z
                                case 1: dx = npos.X - posX; break; //East +x
                                case 2: dz = npos.Z - posZ; break; //South +z
                                case 3: dx = npos.X - posX; break; //West -x
                                case 4: dy = npos.Y - posY; break; //Up +y
                                case 5: dy = npos.Y - posY; break; //Down -y
                            }

                            visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                            if (Math.Abs(currentVisited[visitedIndex]) != iteration) { //if we've haven't visited this pos yet.
                                currentVisited[visitedIndex] = iteration;
                            }
                            else if (currentVisited[visitedIndex] < 0) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                                currentVisited[visitedIndex] = -(currentVisited[visitedIndex]);
                            }

                            if (nBlock.Id == 0) { //next block is open air. 
                                potentialExposed = 0; //invalidated search.
                                bookmarkPos = returnPos;
                                return potentialExposed;
                            }
                        }
                        else {
                            return -potentialExposed;
                        } //if the next block isn't valid then we can't continue the search anyway.
                    }
                }
            }
            //If we make it here, then we have successfully found a wall with a complete set of exposing blocks inside it. 
            bookmarkPos = npos;
            exitPos = startPos;
            return potentialExposed;
        }

        public static int exitSearch(int maxSize, BlockPos seedPos, BlockPos startPos, BlockFacing facing, ICachingBlockAccessor blockAccessor) {
            return 1;
        }


        #endregion
        
        /// <summary>
        /// PreFix to skip the body of RoomRegistry's 'FindRoomForPosition' method a postfix will implement the modded 'FindRoomForPosition' method.
        /// </summary>
        /// <returns></returns>            
        [HarmonyPatch(typeof(RoomRegistry), "FindRoomForPosition")]
        [HarmonyPrefix]
        // Argument data isn't passed to the method at the time of the prefix, i should use a postfix if i want to take advantage of the actual data getting sent to the method.
        public static bool SkipRoomRegFindRoom() {
            return true;
        }

        #region constants for FindRoom
        // These variables are direct copies of the original RoomRegistry, which has them as private, but i don't believe they require the original RoomRegistry's instance.
        const int ARRAYSIZE = 29;  // Original Dev Note: if this constant is increased beyond 32, the bitshifts for compressedPos in the bfsQueue.Enqueue() and .Dequeue() calls may need updating
        //readonly int[] currentVisited = new int[ARRAYSIZE * ARRAYSIZE * ARRAYSIZE]; // The array's index count represents the compressed XYZ Pos coordinates of blocks that make up the room.
        //readonly int[] skyLightXZChecked = new int[ARRAYSIZE * ARRAYSIZE]; // An array who's index count represents the XZ Pos coordinates of skylight blocks that have been identified.
        const int MAXROOMSIZE = 14; // Max size of what a room can be. (Doubling it and adding 1 will give you the ARRAYSIZE value.)
        const int MAXCELLARSIZE = 7; // Max size of what a standard cellar can be
        const int ALTMAXCELLARSIZE = 9; // If any wall|floor is larger than 7 then 9 is what only one axis of the room could possibly be
        const int ALTMAXCELLARVOLUME = 150; // If having to use ALTMAX size, then the number of interior blocks (volume) can't be more than 150
                                            //int iteration = 0; // How many times the search loop has iterated.

        // MODDED CONSTANTS
        readonly int[] recursivePos; //For when the search needs to check 'recursively' beyond a specific XYZ position, once the search returns to this position it will pop the stack and iterate the search.


        #endregion

        /// <summary>
        /// Harmony Postfix patch that rewrites the 'FindRoomForPosition' method in the original RoomRegistry. It is intended to utilize the new 'RenRoom' class that's derivative of the base Room class.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="__result"></param>
        /// <param name="___iteration"></param>
        /// <param name="___currentVisited"></param>
        /// <param name="___skyLightXZChecked"></param>
        /// <param name="___blockAccessor"></param>
        /// <param name="___api"></param>
        [HarmonyPatch(typeof(RoomRegistry), "FindRoomForPosition")]
        [HarmonyPostfix]
        // The private instanced data that the method is dependant on does not get passed to my own code unless I actively send it from here, so I must include the private field data as extra arguments.
        public static void PatchRoomRegFindRoom(BlockPos pos, ref Room __result, int ___iteration, int[] ___currentVisited, int[] ___skyLightXZChecked, ICachingBlockAccessor ___blockAccessor, ICoreAPI ___api) {
            if (Harmony.DEBUG == true) FileLog.Log("FindRoom Patch");
            #region Use one of VintageStory's datastructures to quickly and performantly store block position data as a compressed integer value.
            QueueOfInt bfsQueue = new(); // Enqueue a single value with four separate components, assumed to be signed int, in the range of -128 to +127
            #endregion

            #region Use the 'MAXROOMSIZE' value to Enqueue the theoretical position of the block that'd be in the exact center of the room.
            int halfSize = (ARRAYSIZE - 1) / 2;
            int maxSize = halfSize + halfSize;
            bfsQueue.Enqueue(halfSize << 10 | halfSize << 5 | halfSize);
            #endregion

            #region Add the theoretical center block to the start of the 'vistedIndex' array and add '1' to the search 'iteration' value. Then set that iteration value to the currentVisited value at index [visitedIndex].
            int visitedIndex = (halfSize * ARRAYSIZE + halfSize) * ARRAYSIZE + halfSize; // Center node
            int iteration = ++___iteration;
            ___currentVisited[visitedIndex] = iteration; //NOTE: the positional value of the block is the Array's [INDEX], while the iteration count is the VALUE at said index.
            //The array's size is [29*29*29], meaning it has [24,389] unique indexes (Over twice the size of the MAXROOMSIZE allowance).
            //This is because each index represents a potential position in the room's cuboid; Using twice the size to account for the fact that the starting position might be at any boundry of the room.
            //Only the indexes that we visit will get an 'iteration value' assigned to them.
            #endregion

            #region [Vanilla] Establish the int variables that represent notable block/entity/condition types found during the search.            
            int coolingWallCount = 0; // [Vanilla] Represents blocks that qualify as temperature controlled for cellars.
            int nonCoolingWallCount = 0;

            int skyLightCount = 0; // [Vanilla] Represents ceiling blocks that count as skylights for greenhouses.
            int nonSkyLightCount = 0;

            int exitCount = 0; // [Vanilla] Reprsents the number times the search has gone beyond the MAXROOMSIZE value in any direction.
            #endregion

            #region Start up the blockAccessor for this method. (Accessed from the original RoomRegistry, which has a thread static attribute, so it will end once the code returns back to the original class.
            ___blockAccessor.Begin();
            #endregion

            #region A Boolean check to verify that all relevant chunks are loaded, default setting is true.
            bool allChunksLoaded = true;
            #endregion

            #region Establish the minimum x|y|z values and the current center block x|y|z values as individual int variables. Then use them to create the BlockPos objects 'npos' and 'bpos'.
            int minx = halfSize, miny = halfSize, minz = halfSize, maxx = halfSize, maxy = halfSize, maxz = halfSize;
            int posX = pos.X - halfSize;
            int posY = pos.Y - halfSize;
            int posZ = pos.Z - halfSize;
            //Original Code: BlockPos npos = new BlockPos();
            //Original Code: BlockPos bpos = new BlockPos();
            BlockPos npos = new(Dimensions.NormalWorld); //new dimension aware code
            BlockPos bpos = new(Dimensions.NormalWorld); //new dimension aware code
            BlockPos returnPos = npos; //create a backup Pos in case we need to return.
            int dx, dy, dz; // Additionally creates 3 int variables representing the 'decompressed' x|y|z integer values
            int sx, sy, sz; // 'starting' positions for each axis value.

            //MODDED VARIABLES
            //Search specific
            bool recursiveCall = false; //flag for when the current search is recursive
            bool externalCheck = false; //flag for when the current search is potentially outside the room's external walls.
            bool needSmallest = true; //flag for when the search needs to nagivage to the smallest possible XYZ pos it can reach.
            bool exitSearch = false; //flag for when the search needs to try and find a way direct way outside the bounding box from the current POS.
            int lastLaneDepth = 0; //the depth the scanline search has reached traversing the Z axis.
            int lastRowWidth = 0; //the width the search reached while traversing the X axis.
            BlockPos exitSearchPOS; //block Pos of the starting position for an exit search

            //room defining
            int enclosingBlocks = 0; //blocks that are heat retaining.
            int exposingBlocks = 0; //blocks that aren't heat retaining, but still physically make a wall. (Fences, Stairs, slabs, crude doors, etc.)
            int ventilatedBlocks = 0; //blocks that would be considered exposing, but aren't exposed to an exit point for the room. (Without mods, these are just exposing blocks that don't count for certain checks.)

            #endregion

            #region [THE SEARCH] Use an optimized Scanline sequence to identify the boundries and contents of the room. This while-loop is modified from the original code to accomodate new room types.
            //Modded Flood-Fill search - a modified scanline search designed by Adam Milazzo, that has been reworked for 3D spaces.
            while (bfsQueue.Count > 0) {
                #region Dequeue the current xyz block POS data from 'bfsQueue' and use it to set BlockPos objects 'npos' and 'bpos'
                int compressedPos = bfsQueue.Dequeue();
                dx = compressedPos >> 10; //the relative x position value, initialized at the theoretical center of the room. (relative meaning it's not the global position, but instead a local position based on the min/max allowed room size.
                dy = (compressedPos >> 5) & 0x1f; //the relative y position value, initialized at the theoretical center of the room.
                dz = compressedPos & 0x1f; //the relative z position value, initialized at the theoretical center of the room.
                npos.Set(posX + dx, posY + dy, posZ + dz);
                bpos.Set(npos);
                //modded code
                bool outsideCube = false; //flag for when we are confirmed outside the max limit 
                int potentialExposed = 0; //blocks found during this iteration that could be exposing if they are found to be inside an external wall.
                int laneDepth = 0; //new starting position in the Z-lane, we're back at 0 depth.
                int rowWidth = 0; //new starting position in the X-row, we're back at 0 width.
                sx = dx; sy = dy; sz = dz; //record the starting positions for this new begining search.
                #endregion

                #region Update the current min|max x|y|z value if the current dx|dy|dz value are less|greater than the recorded min|max x|y|z value
                if (dx < minx) minx = dx;
                else if (dx > maxx) maxx = dx;

                if (dy < miny) miny = dy;
                else if (dy > maxy) maxy = dy;

                if (dz < minz) minz = dz;
                else if (dz > maxz) maxz = dz;
                #endregion

                #region Uses the newly set 'bpos' value to tell the blockAccessor the location of the next block we'd like to assign to the 'bBlock' object.
                Block bBlock = ___blockAccessor.GetBlock(bpos);
                Block nBlock = ___blockAccessor.GetBlock(npos);
                #endregion                

                //here's where the modded search differs, the original code is a BFS, while this is closer to a DFS, but this is a scanline floodfill algorithm optimized for 3D space.
                //Design philosophy: Avoid re-scanning already visited blocks. Avoid scanning blocks you know you're going to scan in the future. Do not use recursion.

                BlockFacing direction;
                #region the 'needSmallest' search loop - navigate to the smallest XYZ value possible.
                while (needSmallest == true) { //this will be a bit more tricky than Adam's original algo, since the bounds of the room are not already known.
                    int ox = dx, oy = dy, oz = dz; //remember the original xyz at the start of each navigation iteration.

                    direction = BlockFacing.DOWN;//navigate to the smallest Y value possible. Priority axis, we really need to aim for the room's floor at the bottom of Y's Column.
                    int heatBlock = nBlock.GetRetention(npos, direction, EnumRetentionType.Heat);
                    while (___blockAccessor.IsValidPos(npos) && !(dy < 0 || (maxy - miny + 1 >= MAXROOMSIZE)) && (nBlock.Id == 0 || heatBlock == 0 || (nBlock is not BlockStairs) || (nBlock is not BlockFence) || (nBlock is not BlockSlab) || (nBlock is not BlockDoor))) {
                        direction.IterateThruFacingOffsets(npos); //navigate downwards from the current position.
                        nBlock = ___blockAccessor.GetBlock(npos); //assign the new position to the 'nBlock' element so we can verify info about it.
                        dy--; //update dy to their new position.
                        if (dy < 0 || maxy - miny + 1 >= MAXROOMSIZE) { //if we've moved outside the bounds of the max room size
                            outsideCube = true;
                            dy++; //increment the y value back within the bounds of the room.
                            break;
                        }
                        if (dy < miny) miny = dy;
                        else if (dy > maxy) maxy = dy;
                    }// we can conclude that we've gone as far 'DOWN' as we can in the current Y Column.

                    direction = BlockFacing.WEST; //navigate to the smallest X value. Second Priority, the 'left' (Western) wall should be where we start in the X Row.
                    while (___blockAccessor.IsValidPos(npos) && !(dx < 0 || (maxx - minx + 1 >= MAXROOMSIZE)) && (nBlock.Id == 0 || heatBlock == 0 || (nBlock is not BlockStairs) || (nBlock is not BlockFence) || (nBlock is not BlockSlab) || (nBlock is not BlockDoor))) {
                        direction.IterateThruFacingOffsets(npos); //navigate Westwards from the current position.
                        nBlock = ___blockAccessor.GetBlock(npos); //assign the new position to the 'nBlock' element so we can verify info about it.
                        dx--; //update dx to their new position.
                        if (dx < 0 || maxx - minx + 1 >= MAXROOMSIZE) { //if we've moved outside the bounds of the max room size
                            outsideCube = true;
                            dx++; //increment the x value back within the bounds of the room.
                            break;
                        }
                        if (dx < minx) minx = dx;
                        else if (dx > maxx) maxx = dx;
                    }// we can conclude that we've gone as far 'WEST' as we can in the current X Row.

                    direction = BlockFacing.NORTH; //navigate to the smallest Z value. The Z-axis' Lane is our primary 'scanline', and we'll be navigating North(-) to South(+) for each iteration.
                    while (___blockAccessor.IsValidPos(npos) && !(dz < 0 || (maxz - minz + 1 >= MAXROOMSIZE)) && (nBlock.Id == 0 || heatBlock == 0 || (nBlock is not BlockStairs) || (nBlock is not BlockFence) || (nBlock is not BlockSlab) || (nBlock is not BlockDoor))) {
                        direction.IterateThruFacingOffsets(npos); //navigate Northwards from the current position.
                        nBlock = ___blockAccessor.GetBlock(npos); //assign the new position to the 'nBlock' element so we can verify info about it.
                        dz--; //update dz to their new position.
                        if (dz < 0 || maxz - minz + 1 >= MAXROOMSIZE) { //if we've moved outside the bounds of the max room size
                            outsideCube = true;
                            dz++; //increment the z value back within the bounds of the room.
                            break;
                        }
                        if (dz < minz) minz = dz;
                        else if (dz > maxz) maxz = dz;
                    }// we can conclude that we've gone as far 'NORTH' as we can in the current Z Lane.

                    //IF the navigation has failed to move, or left the bounds, we can assume we're at the smallest POS we can reach.
                    if (outsideCube || (ox == dx && oy == dy && oz == dz)) {
                        bpos.Set(posX + dx, posY + dy, posZ + dz); //set the npos and npos values to the currently found X Y Z values.
                        sx = dx; sy = dy; sz = dz;
                        npos.Set(bpos);
                        needSmallest = false;
                    }  //then we've found the smallest XYZ we can currently reach. From this point forward we will assume we're in the North Western Bottom corner of the room.
                }
                if (outsideCube) {
                    exitCount++;
                    outsideCube = false;
                    continue; //force iteration of the scanline search to the next stack'd position since the needSmallest search found a way out of bounds.
                }
                bBlock = ___blockAccessor.GetBlock(bpos);
                #endregion

                # region Begin the scanline search
                direction = BlockFacing.SOUTH; //The scanline will be going from North to South. (-Z to +Z)
                int heatRetention = bBlock.GetRetention(bpos, direction, EnumRetentionType.Heat); //check our current block's heat retention value.

                #region If the starting block of the lane is a solid block. Navigate to the first non-solid block in the lane. [WIP]
                //Ref: if(lastRowLength != 0 && array[y, x]) // if this is not the first row and the leftmost cell is filled...
                if (lastLaneDepth != 0 && bBlock.Id != 0 && heatRetention != 0) { //if this is not the first check and we're inside a solid block for our current starting position.
                    //We need to progress down the Z lane to find the first non-solid block.
                    //We'll be using the nBlock as the new position we're looking at, and we'll navigate until that block is in a non-solid block.
                    npos.Set(bpos);
                    nBlock = ___blockAccessor.GetBlock(npos);
                    allChunksLoaded &= ___blockAccessor.LastChunkLoaded;
                    
                    while (nBlock.Id != 0 && heatRetention != 0) {//if the new block is inside a solid block
                        if (heatRetention < 0) coolingWallCount -= heatRetention; //make sure to increment the relavent count values based on the heat value of the solid block. 
                        else nonCoolingWallCount += heatRetention;

                        direction.IterateThruFacingOffsets(npos); //move the npos position forward so we can check if the next block is solid or not.
                        if (!___blockAccessor.IsValidPos(npos)) { nonCoolingWallCount++; break; } //if we hit an invalid position, break the navigation.
                        
                        nBlock = ___blockAccessor.GetBlock(npos); //confirm the new block at the npos position.                        
                        heatRetention = nBlock.GetRetention(npos, direction, EnumRetentionType.Heat); //confirm the new block's heat retention.
                    }//leaving this loop means we found a non-solid block and assigned it to npos. (Either an open block, or a block that counts as exposing.
                    dz = npos.Z - posZ; //update dz to their new position.
                    sz = dz; //update starting position.
                    bpos = npos; 
                    bBlock = nBlock; //we need to move the current position to the newly found non-solid block so we can start the search.
                }
                #endregion
                #region If we're in a block that ISN'T solid, and this ISN'T the first Z-lane search of the row, we run our checks to confirm there IS a solid block behind us. [WIP]
                else if (lastLaneDepth != 0) {
                    direction = BlockFacing.NORTH; //face behind us. (-z)
                    direction.IterateThruFacingOffsets(npos); //iterate the new block position (npos) by -z.
                    if (___blockAccessor.IsValidPos(npos)) {//ensure the new npos is a valid world position.
                        nBlock = ___blockAccessor.GetBlock(npos); //get the Block at the supposed position.
                        allChunksLoaded &= ___blockAccessor.LastChunkLoaded;
                        heatRetention = nBlock.GetRetention(npos, direction.Opposite, EnumRetentionType.Heat);
                        if (nBlock.Id == 0 || heatRetention == 0) {
                            //ref: we begin scanning a [lane] and then find on the next [lane] that it has [an open block behind the starting position]. 
                            if (((bBlock is BlockStairs) || (bBlock is BlockFence) || (bBlock is BlockSlab) || (bBlock is BlockDoor)) && heatRetention == 0) {
                                returnPos.Set(npos);
                                externalCheck = true;
                            } //If we pass through an exposing block, we might've fallen through an exterior wall, so remember this position if we hit an exit point.
                            while (nBlock.Id == 0 || heatRetention == 0) { //the current nBlock is confirmed a non-solid block.
                                bpos = npos;
                                bBlock = nBlock; //we need to move the current position to the newly found non-solid block.
                                dz = npos.Z - posZ;

                                visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                                if (Math.Abs(___currentVisited[visitedIndex]) != iteration) { //if we've haven't visited this pos yet.
                                    if (externalCheck == true) ___currentVisited[visitedIndex] = iteration;
                                    else ___currentVisited[visitedIndex] = -iteration;
                                }
                                else if (___currentVisited[visitedIndex] < 0 && externalCheck == false) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                                    ___currentVisited[visitedIndex] = -(___currentVisited[visitedIndex]);
                                }

                                direction.IterateThruFacingOffsets(npos); //move the npos position backwards again to check if we're infront of a solid block.
                                if (!___blockAccessor.IsValidPos(npos)) { nonCoolingWallCount++; continue; } //if we hit an invalid position, force iteration.
                                if (dz < 0 && externalCheck) { 
                                    npos.Set(returnPos);
                                    bpos.Set(returnPos);
                                    break; //If we exceeded the room size limit after passing through an exposing block, then we must've passed through an external wall.
                                }
                                nBlock = ___blockAccessor.GetBlock(npos); //confirm the new block at the npos position.                        
                                heatRetention = nBlock.GetRetention(npos, direction.Opposite, EnumRetentionType.Heat); //confirm the new block's heat retention.
                            }//leaving this loop means we found a npos position that's a solid block, meaning we don't need to move bpos to that position.
                            dz = bpos.Z - posZ; //update dz to their new position.
                            sz = dz; //update starting position.
                        }
                    }
                    else { 
                        nonCoolingWallCount++;
                        continue;
                    }//leaving this if statment means we've confirmed we are starting this lane at a non solid block.
                    direction = BlockFacing.SOUTH; //face infront of us. (+z)
                    npos.Set(bpos); //return new pos to bpos.
                    nBlock = bBlock; //return npos and nblock to the starting position.
                }
                #endregion
                                
                #region If the starting block of the lane is an exposing block, check for any gaps or if it's truely a potential exposing wall. [WIP]
                if (((bBlock is BlockStairs) || (bBlock is BlockFence) || (bBlock is BlockSlab) || (bBlock is BlockDoor)) && heatRetention == 0) { 
                     
                    potentialExposed++; //starting block adds to potential exposed count.
                    returnPos.Set(npos);
                    //make sure above block isn't open air.
                    direction = BlockFacing.UP;
                    direction.IterateThruFacingOffsets(npos);
                    if (___blockAccessor.IsValidPos(npos)) {
                        nBlock = ___blockAccessor.GetBlock(npos);
                        heatRetention = nBlock.GetRetention(npos, direction.Opposite, EnumRetentionType.Heat);
                        dy = npos.Y - posY;

                        visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                        if (Math.Abs(___currentVisited[visitedIndex]) != iteration) { //if we've haven't visited this pos yet.
                            if (externalCheck == true) ___currentVisited[visitedIndex] = iteration;
                            else ___currentVisited[visitedIndex] = -iteration;
                        }
                        else if (___currentVisited[visitedIndex] < 0 && externalCheck == false) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                            ___currentVisited[visitedIndex] = -(___currentVisited[visitedIndex]);
                        }

                        if (nBlock.Id == 0) { //above block is open Air; Invalidated exposing wall block search. 
                            potentialExposed = 0;
                            npos.Set(bpos);
                            nBlock = bBlock; //if Air, we return nBlock to bBlock's starting position so we can come back if the forward block is invalid.
                        }
                        else if (heatRetention == 0 && ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockDoor))) { //above block is exposing.
                            potentialExposed++;
                        }
                        else {//if neither of these, then we assume it is a solid block.
                            npos.Set(bpos);
                            nBlock = bBlock; //if solid, we return nBlock to bBlock's starting position so we can come back if the forward block is invalid.
                        }
                    }
                    else {
                        nonCoolingWallCount++;
                        continue;
                    }
                    BlockFacing altDirection = BlockFacing.UP; //need another direction to make sure we don't mix things up later.
                    if (dy == miny) { //if we're at the lowest Y pos we've been so far.
                        //make sure above block isn't open air.
                        direction = BlockFacing.DOWN;
                        npos.Set(returnPos);
                        direction.IterateThruFacingOffsets(npos);
                        if (___blockAccessor.IsValidPos(npos)) {
                            nBlock = ___blockAccessor.GetBlock(npos);
                            heatRetention = bBlock.GetRetention(bpos, direction.Opposite, EnumRetentionType.Heat);
                            dy = npos.Y - posY;

                            visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                            if (Math.Abs(___currentVisited[visitedIndex]) != iteration) { //if we've haven't visited this pos yet.
                                if (externalCheck == true) ___currentVisited[visitedIndex] = iteration;
                                else ___currentVisited[visitedIndex] = -iteration;
                            }
                            else if (___currentVisited[visitedIndex] < 0 && externalCheck == false) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                                ___currentVisited[visitedIndex] = -(___currentVisited[visitedIndex]);
                            }

                            if (nBlock.Id == 0) { //below block is open Air; Invalidated exposing wall block search. 
                                potentialExposed = 0;
                                npos.Set(bpos);
                                nBlock = bBlock; //if Air, we return nBlock to bBlock's starting position so we can come back if the forward block is invalid.
                            }
                            else if (heatRetention == 0 && ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockDoor))) { //below block is exposing.
                                potentialExposed++;
                            }
                            else {//if neither of these, then we assume it is a solid block.
                                npos.Set(bpos);
                                nBlock = bBlock; //if solid, we return nBlock to bBlock's starting position so we can come back if the forward block is invalid.
                            }
                        }
                        else {
                            nonCoolingWallCount++;
                            continue;
                        }
                    }

                    //If to account for when we are inside a North/South wall on the North side of the room (-Z)
                    //TODO

                    //make sure forward block isn't open air, and that only one direction has another exposing block, if any.
                    direction = BlockFacing.SOUTH;
                    direction.IterateThruFacingOffsets(bpos);
                    if (___blockAccessor.IsValidPos(bpos)) {
                        bBlock = ___blockAccessor.GetBlock(bpos);
                        heatRetention = bBlock.GetRetention(bpos, direction.Opposite, EnumRetentionType.Heat);
                        dz = npos.Z - posZ;

                        visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                        if (Math.Abs(___currentVisited[visitedIndex]) != iteration) { //if we've haven't visited this pos yet.
                            if (externalCheck == true) ___currentVisited[visitedIndex] = iteration;
                            else ___currentVisited[visitedIndex] = -iteration;
                        }
                        else if (___currentVisited[visitedIndex] < 0 && externalCheck == false) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                            ___currentVisited[visitedIndex] = -(___currentVisited[visitedIndex]);
                        }

                        if (bBlock.Id == 0) { //forward block is open; Invalidated exposing wall block search. 
                            potentialExposed = 0; //no need to reset bpos, we'd travel here anyway if we end up keeping this position.
                        }
                        else if (heatRetention == 0 && ((bBlock is BlockStairs) || (bBlock is BlockFence) || (bBlock is BlockSlab) || (bBlock is BlockDoor))) {  //infront block is exposing.
                            if (potentialExposed > 1) { //if the above block was already found to be exposing, then this exposing block exceeds the allowed number of exposing blocks to qualify as a viable wall block.
                                potentialExposed = 0;
                            }
                            else if (potentialExposed > 0) { //if the above block was found to be open, and the count was reset, then this block being exposed doesn't matter.
                                potentialExposed++;
                            }
                            npos.Set(bpos); //we want to continue forward if the block in front of us is open/exposing, regardless of if the above block was or not.
                        }
                        else {//if neither of these, then we assume the forward block is solid.
                            if (potentialExposed > 1) {       //if the forward block is solid but an upper/lower block is exposing,
                                direction = altDirection;     //If we found an exposed block above, this will set us to Up, if insted it was below, this will be Down.
                            }
                            bpos.Set(npos); //move the bBlock back to nBlock, which is either at the start position, or in an exposing block above it.
                            bBlock = nBlock;
                        }
                    }
                    else {
                        nonCoolingWallCount++;
                        continue;
                    }
                    //we've now determined which block we intend to start from for this exposing block search, and which direction we should head.
                    #region Navigate through the remaining exposed blocks, if any.
                    if (potentialExposed > 1 || direction == BlockFacing.DOWN) { //if we found more than just the one exposing block (with solid blocks above and infront of them.
                        altDirection = BlockFacing.SOUTH; //need another direction to make sure we don't have any extra exposing blocks.
                        if (direction.Index == 2) {
                            altDirection = BlockFacing.UP;
                            returnPos.Set(bpos); //create a backup Pos in case we need to return.
                        }

                        altDirection.IterateThruFacingOffsets(bpos); //check for non-solid blocks.
                        if (___blockAccessor.IsValidPos(bpos)) {
                            bBlock = ___blockAccessor.GetBlock(bpos);
                            heatRetention = nBlock.GetRetention(bpos, altDirection.Opposite, EnumRetentionType.Heat);
                            if (bBlock.Id == 0 || heatRetention == 0) { //adjacent block is not solid. 
                                potentialExposed = 0; //invalidated search.
                            }
                            bBlock = nBlock; //return to current block.
                        }
                        else {
                            nonCoolingWallCount++;
                            continue;
                        }
                        bpos.Set(npos); //return to current pos.

                        direction.IterateThruFacingOffsets(npos); //move the direction we want.
                        heatRetention = nBlock.GetRetention(npos, direction, EnumRetentionType.Heat);
                        while (potentialExposed > 0 && heatRetention == 0 && ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockDoor))) { //while the next block is exposing.
                            potentialExposed++; //we've found another potential exposing block.

                            altDirection.IterateThruFacingOffsets(bpos); //check for non-solid blocks.
                            if (___blockAccessor.IsValidPos(bpos)) {
                                bBlock = ___blockAccessor.GetBlock(bpos);
                                heatRetention = nBlock.GetRetention(bpos, altDirection.Opposite, EnumRetentionType.Heat);
                                if (bBlock.Id == 0 || heatRetention == 0) { //adjacent block is not solid. 
                                    potentialExposed = 0; //invalidated search.
                                }
                                bBlock = nBlock; //return bBlock to current block.
                            }
                            else {
                                nonCoolingWallCount++;
                                continue;
                            }
                            bpos.Set(npos); //return bpos to current pos.

                            direction.IterateThruFacingOffsets(npos); //move the direction we want.
                            if (___blockAccessor.IsValidPos(npos)) {
                                nBlock = ___blockAccessor.GetBlock(npos);
                                heatRetention = nBlock.GetRetention(npos, direction, EnumRetentionType.Heat);
                                if (nBlock.Id == 0) { //next block is open air. 
                                    potentialExposed = 0; //invalidated search.
                                }
                            }
                            else { 
                                potentialExposed = 0;
                                nonCoolingWallCount++;
                                continue;
                            } //if the next block isn't valid then we can't continue the search anyway.
                        }//leaving this while loop means we found the last position in the series of adjacent exposing blocks.
                        //if we headed downwards, we need to return to the lane we were originally so we can check up.
                        if (direction.Index == 5) {
                            direction = BlockFacing.UP;
                            npos.Set(returnPos);
                            bpos.Set(npos);

                            direction.IterateThruFacingOffsets(npos); //move the direction we want.
                            heatRetention = nBlock.GetRetention(npos, direction, EnumRetentionType.Heat);
                            while (potentialExposed > 0 && heatRetention == 0 && ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockDoor))) { //while the next block is exposing.
                                potentialExposed++; //we've found another potential exposing block.

                                altDirection.IterateThruFacingOffsets(bpos); //check for non-solid blocks.
                                if (___blockAccessor.IsValidPos(bpos)) {
                                    bBlock = ___blockAccessor.GetBlock(bpos);
                                    heatRetention = nBlock.GetRetention(bpos, altDirection.Opposite, EnumRetentionType.Heat);
                                    if (bBlock.Id == 0 || heatRetention == 0) { //adjacent block is not solid. 
                                        potentialExposed = 0; //invalidated search.
                                    }
                                    bBlock = nBlock; //return bBlock to current block.
                                }
                                else {
                                    nonCoolingWallCount++;
                                    continue;
                                }
                                bpos.Set(npos); //return bpos to current pos.

                                direction.IterateThruFacingOffsets(npos); //move the direction we want.
                                if (___blockAccessor.IsValidPos(npos)) {
                                    nBlock = ___blockAccessor.GetBlock(npos);
                                    heatRetention = nBlock.GetRetention(npos, direction, EnumRetentionType.Heat);
                                    if (nBlock.Id == 0) { //next block is open air. 
                                        potentialExposed = 0; //invalidated search.
                                    }
                                }
                                else {
                                    potentialExposed = 0;
                                    nonCoolingWallCount++;
                                    continue;
                                } //if the next block isn't valid then we can't continue the search anyway.
                            }                            
                        }

                        if (direction.Index == 4) { //if we headed upwards, we need to return to the lane we were originally searching.
                            direction = BlockFacing.SOUTH;
                            bpos.Set(returnPos);
                            bBlock = ___blockAccessor.GetBlock(bpos);
                        }
                        nBlock = bBlock; //we are now at the current position we need to be.

                        if (potentialExposed > 0) { //If we found potential exposing walls we will want to perform an exitSearch.
                            exitSearch = true; //We'll perform an exit search from the right position later.
                            if (dx == sx) { //if the X value of our POS is the starting POS, that means we've come into this wall going east to west, so the outside of the wall would be on the western side.
                                returnPos.Set(posX + (dx - 1), posY + dy, posZ + dz); //we'll want to start the exitSearch one block to the West(-x) so we're outside the room.
                            } else { //if we're not at the starting X pos, then we've come into this wall from west to east, so the outside of the wall would be on the eastern side.
                                returnPos.Set(posX + (dx + 1), posY + dy, posZ + dz); //we'll want to start the exitSearch one block to the East(+x) so we're outside the room.
                            }
                        }//If we haven't we don't need to do anything and continuing the search won't cause issue.
                    }
                    #endregion
                }//We've confirmed whether or not the exposing blocks we found make up a potential wall or not. We'll continue with the regular Scanline search until calling for an exit check.
                #endregion

                #region Standard new block iteration down the z-axis
                direction.IterateThruFacingOffsets(npos); //move the new block position (npos) towards the direction we're headed.

                if (!___blockAccessor.IsValidPos(npos)) {
                    nonCoolingWallCount++;
                    continue;
                }

                nBlock = ___blockAccessor.GetBlock(npos);
                allChunksLoaded &= ___blockAccessor.LastChunkLoaded;
                heatRetention = nBlock.GetRetention(npos, direction.Opposite, EnumRetentionType.Heat); //'.opposite' to ensure the face pointed at bBlock is checked, not the face on the other side of nBlock.
                #endregion

                #region Standard scanline block inspection. [WIP]

                //so long as the next z-axis block is open air, keep iterating and adding visted blocks to the index.
                while (nBlock.Id == 0 || heatRetention == 0) {
                    if (nBlock.Id == 0) { //if the block is air
                        visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;

                        if (Math.Abs(___currentVisited[visitedIndex]) != iteration) { //if we've haven't visited this pos yet.
                            if (externalCheck == false) ___currentVisited[visitedIndex] = iteration;
                            else ___currentVisited[visitedIndex] = -iteration;
                        } else if (___currentVisited[visitedIndex] < 0 && externalCheck == false) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                            ___currentVisited[visitedIndex] = -(___currentVisited[visitedIndex]);
                        }

                        laneDepth++; //increase the lane depth for each successful internal block traversed.
                    } 
                    else if ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockDoor)) { //if the block is one of these exposing block types.
                        //Here we will need to do something similar to when we encounter an exposing block on the first position. But now for a North/South facing wall.
                        //TODO - Horizontal Exposing Wall Check.
                        
                        laneDepth++; //exposing blocks will still count as part of the depth.
                    }
                    // We hit a wall, check if we ended early.
                    if (heatRetention != 0) {
                        if (heatRetention < 0) coolingWallCount -= heatRetention;
                        else nonCoolingWallCount += heatRetention;

                        if (laneDepth < lastLaneDepth) { //if current lane is shorter than previous lane
                            //progress until we hit previous lane depth to make sure we aren't missing anything beyond the wall.
                            int end = lastLaneDepth - laneDepth;
                            while (end > 0) { //End is the distance between last lane's Z value depth and this lane's. Progress until the difference is 0.
                                direction.IterateThruFacingOffsets(npos);
                                if (___blockAccessor.IsValidPos(npos)) { //valid position
                                    nBlock = ___blockAccessor.GetBlock(npos);
                                    allChunksLoaded &= ___blockAccessor.LastChunkLoaded;
                                    heatRetention = nBlock.GetRetention(npos, direction.Opposite, EnumRetentionType.Heat);
                                    
                                    if (nBlock.Id == 0 || heatRetention == 0) { //block is not solid.
                                        if (nBlock.Id == 0) { //if the block is air
                                            visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                                            if (Math.Abs(___currentVisited[visitedIndex]) != iteration) { //if we haven't visited this pos yet.
                                                if (externalCheck == false) ___currentVisited[visitedIndex] = iteration;
                                                else ___currentVisited[visitedIndex] = -iteration;
                                            }
                                            else if (___currentVisited[visitedIndex] < 0 && externalCheck == false) {//if we have visited, but it was believed to be external, and we're now believed to be internal.
                                                ___currentVisited[visitedIndex] = -(___currentVisited[visitedIndex]);
                                            }
                                        }
                                        else if ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockDoor)) { //if the block is one of these exposing block types.
                                            

                                        }
                                    }
                                }
                                else { //invalid position.
                                    nonCoolingWallCount++;
                                    continue;
                                }
                                end--; //reduce the distance between the current position and the end of the last lane.
                            }
                        }

                        else if (laneDepth > lastLaneDepth) { //if current lane is longer than previous lane
                            //Traverse backwards while looking towards the previous row to ensure we haven't missed anything beyond the last row's ending point.

                        }

                        //confirm lastLaneDepth
                        lastLaneDepth = laneDepth;
                    }
                }

                #endregion

                #region Encountering Exposing Blocks during Z-traversal [WIP]

                #endregion

                #region Exit Check [WIP]
                #endregion

                #region Iteration position preperation [WIP] - TODO NEED TO ASSIGN ITERATION VALUES TO THE VISITEDINDEX ARRAY
                // Compute the new dz offset for npos
                dz = npos.Z - posZ;

                outsideCube = false;
                if (dz > maxz) {
                    outsideCube = dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE;
                }
                if (outsideCube) {
                    exitCount++;
                    continue;
                }

                visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                if (___currentVisited[visitedIndex] == iteration) continue;
                ___currentVisited[visitedIndex] = iteration;

                int skyLightIndex = dx * ARRAYSIZE + dz;
                if (___skyLightXZChecked[skyLightIndex] < iteration) {
                    ___skyLightXZChecked[skyLightIndex] = iteration;
                    int light = ___blockAccessor.GetLightLevel(npos, EnumLightLevelType.OnlySunLight);

                    if (light >= ___api.World.SunBrightness - 1) {
                        skyLightCount++;
                    }
                    else {
                        nonSkyLightCount++;
                    }
                }

                bfsQueue.Enqueue(dx << 10 | dy << 5 | dz);//if we make it to here, that means we've succesfully traversed
                #endregion

                #endregion
            }


            //Vanilla style search
            while (bfsQueue.Count > 0) {
                
                #region Dequeue the current xyz block POS data from 'bfsQueue' and use it to set BlockPos objects 'npos' and 'bpos'
                int compressedPos = bfsQueue.Dequeue();
                dx = compressedPos >> 10;
                dy = (compressedPos >> 5) & 0x1f;
                dz = compressedPos & 0x1f;
                npos.Set(posX + dx, posY + dy, posZ + dz);
                bpos.Set(npos);
                #endregion

                #region Update the current min|max x|y|z value if the current dx|dy|dz value are less|greater than the recorded min|max x|y|z value
                if (dx < minx) minx = dx;
                else if (dx > maxx) maxx = dx;

                if (dy < miny) miny = dy;
                else if (dy > maxy) maxy = dy;

                if (dz < minz) minz = dz;
                else if (dz > maxz) maxz = dz;
                #endregion

                #region Uses the newly set 'bpos' value to tell the blockAccessor the location of the next block we'd like to assign to the 'bBlock' object.
                Block bBlock = ___blockAccessor.GetBlock(bpos);
                #endregion
                
                //bool exposingFlag = false;
                #region Use a 'foreach' loop that accounts for each face of the block in question to see what is in the given face's direction.
                foreach (BlockFacing facing in BlockFacing.ALLFACES) {
                    facing.IterateThruFacingOffsets(npos);  // This must be the first command in the loop, to ensure all facings will be properly looped through regardless of any 'continue;' statements
                    int heatRetention = bBlock.GetRetention(bpos, facing, EnumRetentionType.Heat);

                    #region Check if the current or next blocks are solid, if yes then record relavent data and restart loop, else prepare to move to the next block.

                    #region If the current block isn't 'air' and has a heatRetention value other than 0; Check if it's +/-, then add to the noncooling/cooling wall counts respectively.
                    // We cannot exit current block, if the facing is heat retaining (e.g. chiselled block with solid side)
                    if (bBlock.Id != 0 && heatRetention != 0) {
                        if (heatRetention < 0) coolingWallCount -= heatRetention;
                        else nonCoolingWallCount += heatRetention;

                        continue;
                    }
                    #endregion

                    #region If the block IS air, but doesn't exist within the map's bounding box, then add to the nonCoolingWallCount.
                    if (!___blockAccessor.IsValidPos(npos)) {
                        nonCoolingWallCount++;
                        continue;
                    }
                    #endregion

                    #region If the checks above don't restart the loop, then create a new Block object called 'nBlock' and set its position to the 'npos' value for a few checks.
                    Block nBlock = ___blockAccessor.GetBlock(npos);
                    allChunksLoaded &= ___blockAccessor.LastChunkLoaded;
                    heatRetention = nBlock.GetRetention(npos, facing.Opposite, EnumRetentionType.Heat);
                    #endregion

                    #region Check the block opposite of nBlock; If it counts as a wall then we need to add to the relavent counters and jump back to the start of the for loop.
                    // We hit a wall, no need to scan further
                    if (heatRetention != 0) {
                        if (heatRetention < 0) { coolingWallCount -= heatRetention; enclosingBlocks++; }
                        else { nonCoolingWallCount += heatRetention; enclosingBlocks++; }

                        continue; //jump back to the top of the for loop.
                    } // Since this is a wall, the block types 'stairs'/'fence'/'chisel'/'slab' are important because the wall might not be heatRetaining, but it is still a 'wall' of sorts.
                    else if (((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab)) & heatRetention == 0) {
                        nonCoolingWallCount++; exposingBlocks++; //exposingFlag = true;
                        //continue; //jump back to the top of the for loop.
                        //UPDATE: I realized that players can exploit blue cheese making by placing an exposing block and then a block behind it.
                        //I need to find a way to counter this. I should re-evaluate how exposing blocks are counted. Worth noting, I am also counting enclosing blocks.
                        //In theory, I could use the number of enclosing blocks and exposing blocks counted to identify if there's any open holes in the walls or if all the walls are accaptable blocks of some sort.
                        //a 2x2 'window' of fences inside a 4x4 wall of stone would be the equivalent of 4 exposing faces and 8 enclosing faces.
                        //
                    }
                    #endregion

                    #endregion

                    #region Compute the new dx, dy, dz offsets for npos
                    dx = npos.X - posX; // new dx is the curren X position of npos subtracted by the center block's X position
                    dy = npos.Y - posY; // new dy is the curren Y position of npos subtracted by the center block's Y position
                    dz = npos.Z - posZ; // new dz is the curren Z position of npos subtracted by the center block's Z position
                    #endregion

                    #region THE TRAVERSAL SECTION, the part that moves from the currently observed block to the next block to be observed. If it moves outside maxSize, it adds to exitCount and jumps to loop start.
                    // Only traverse within maxSize range, and overall room size must not exceed MAXROOMSIZE
                    //   If outside that, count as an exit and don't continue searching in this direction
                    //   Note: for performance, this switch statement ensures only one conditional check in each case on the dimension which has actually changed, instead of 6 conditionals or more
                    bool outsideCube = false;
                    switch (facing.Index) {
                        case 0: // North
                            if (dz < minz) outsideCube = dz < 0 || maxz - minz + 1 >= MAXROOMSIZE; //if the z value is less than the minimum seen z value, check if the z value is outside of the cube's min or max roomsize allowance.
                            break;
                        case 1: // East
                            if (dx > maxx) outsideCube = dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE;
                            break;
                        case 2: // South
                            if (dz > maxz) outsideCube = dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE; //if the z value is greater than the minimum seen z value, check if the z value is outside of the cube's min or max roomsize allowance.
                            break;
                        case 3: // West
                            if (dx < minx) outsideCube = dx < 0 || maxx - minx + 1 >= MAXROOMSIZE;
                            break;
                        case 4: // Up
                            if (dy > maxy) outsideCube = dy > maxSize || maxy - miny + 1 >= MAXROOMSIZE;
                            break;
                        case 5: // Down
                            if (dy < miny) outsideCube = dy < 0 || maxy - miny + 1 >= MAXROOMSIZE;
                            break;
                    }
                    if (outsideCube) { //if the above switch determins that the block is outside the allowed roomsize, add to the exitcount and jump back up to the top of the search loop.
                        exitCount++;
                        continue;
                    }
                    #endregion

                    #region add the current XYZ pos data into the currentVisited array. If the current POS has already been visited, then move back to the top of the for loop.
                    visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                    if (___currentVisited[visitedIndex] == iteration) continue;   // continue if block position was already visited
                    ___currentVisited[visitedIndex] = iteration; // If the block position has not been visited, add it to the currentVisited array, using the current iteration as the index value for the array.
                    #endregion

                    #region If the block is a new block, check that it qualifies for any relavent qualities. [Potentially modded checks entered here]
                    // We only need to check the skylight if it's a block position not already visited ...
                    int skyLightIndex = dx * ARRAYSIZE + dz;
                    if (___skyLightXZChecked[skyLightIndex] < iteration) {
                        ___skyLightXZChecked[skyLightIndex] = iteration;
                        int light = ___blockAccessor.GetLightLevel(npos, EnumLightLevelType.OnlySunLight);

                        if (light >= ___api.World.SunBrightness - 1) {
                            skyLightCount++;
                        }
                        else {
                            nonSkyLightCount++;
                        }
                    }
                    #endregion

                    bfsQueue.Enqueue(dx << 10 | dy << 5 | dz);
                }
                #endregion
            }
            #endregion

            #region Use the search results to establish the rough size of the room's X|Y|Z axes, and make a byte array to track x|y|z positions in the room, with the center as the first index.
            int sizex = maxx - minx + 1;
            int sizey = maxy - miny + 1;
            int sizez = maxz - minz + 1;
            byte[] posInRoom = new byte[(sizex * sizey * sizez + 7) / 8];
            #endregion

            #region Establish the volume of the room by counting up every internal block that was recorded by the search.
            int volumeCount = 0;
            for (dx = 0; dx < sizex; dx++) {
                for (dy = 0; dy < sizey; dy++) {
                    visitedIndex = ((dx + minx) * ARRAYSIZE + (dy + miny)) * ARRAYSIZE + minz;
                    for (dz = 0; dz < sizez; dz++) {
                        if (___currentVisited[visitedIndex + dz] == iteration) {
                            int index = (dy * sizez + dz) * sizex + dx;

                            posInRoom[index / 8] = (byte)(posInRoom[index / 8] | (1 << (index % 8)));
                            volumeCount++;
                        }
                    }
                }
            }
            #endregion

            #region Verify if the room is under the cellar's size limit.
            bool isCellar = sizex <= MAXCELLARSIZE && sizey <= MAXCELLARSIZE && sizez <= MAXCELLARSIZE;
            if (!isCellar && volumeCount <= ALTMAXCELLARVOLUME) {
                isCellar = sizex <= ALTMAXCELLARSIZE && sizey <= MAXCELLARSIZE && sizez <= MAXCELLARSIZE
                    || sizex <= MAXCELLARSIZE && sizey <= ALTMAXCELLARSIZE && sizez <= MAXCELLARSIZE
                    || sizex <= MAXCELLARSIZE && sizey <= MAXCELLARSIZE && sizez <= ALTMAXCELLARSIZE;
            }
            #endregion

            if (Harmony.DEBUG == true) Console.WriteLine("enclosing blocks = "+enclosingBlocks+" | exposingBlockCount = "+exposingBlocks);

            #region Return a RenRoom object with all the related data found by the method.
            RenRoom toReturn = new() {
                //obj counts
                CoolingWallCount = coolingWallCount,
                NonCoolingWallCount = nonCoolingWallCount,
                SkylightCount = skyLightCount,
                NonSkylightCount = nonSkyLightCount,
                EnclosingBlockCount = enclosingBlocks,
                ExposingBlockCount = exposingBlocks,
                ExitCount = exitCount,
                RoomTemp = 5, // Default is 5°C until I come up with a good way to calc internal temp. Will be using sources of heat and cold to modify it.
                //chunk and pos checks
                AnyChunkUnloaded = allChunksLoaded ? 0 : 1,
                Location = new Cuboidi(posX + minx, posY + miny, posZ + minz, posX + maxx, posY + maxy, posZ + maxz),
                PosInRoom = posInRoom,
                //bool checks
                IsEnclosedRenRoom = (exitCount == 0 & exposingBlocks == 0) || (skyLightCount < nonSkyLightCount), // if there's no exits and exposing blocks, OR if there's less skylights than regular ceiling blocks. (caves)
                IsGreenHouseRenRoom = (exitCount == 0 & exposingBlocks == 0 & (skyLightCount > nonSkyLightCount)), // if there's no exits and exposing blocks, AND if there's more skylights than regular ceiling blocks.
                IsSmallRoom = isCellar && (exitCount == 0 & exposingBlocks == 0), //if it meets the cellar size requirements, AND there's no exits and exposing blocks.
                //calculated values || room effects
                SkyLightProportion = skyLightCount / Math.Max(1, skyLightCount + nonSkyLightCount), //proportion value of the skylight's effects.
                CellarProportion = GameMath.Clamp(nonCoolingWallCount / Math.Max(1, coolingWallCount), 0f, 1f) //proportion value of the cellar's effects.
            };
            //calculated values || room effects - that can't be in the above declaration.
            if (toReturn.IsGreenHouseRenRoom) toReturn.Roomness = 5; // if it's a greenhouse then roomness has a temperature bonus.
            else toReturn.Roomness = 0; //If not, then roomness is 0.

            if (toReturn.IsEnclosedRenRoom || toReturn.IsGreenHouseRenRoom) { //if the room functions as a greenhouse or standard enclosed rooms.
                toReturn.tempSourceModifier = (toReturn.RoomTemp / 5); //heat/cold sources affected to by this value.
            }
            else { toReturn.tempSourceModifier = 1; } //TODO - find a way to balance this value based on the room's sources of temperature, as well as insulation or exposed blocks.            
            __result = toReturn;
            #endregion
        }
    }
}