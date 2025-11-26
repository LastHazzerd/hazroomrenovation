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

        #region Methods needed for exposing wall searches.
        /// <summary>
        /// Inspired by VS' own 'IterateThruFacingOffsets' method. Is a simple modification to the provided POS that is dependant on the direction the search is facing.
        /// The key difference between this and the vanilla code is that it doesn't assume you are iterating through all sides, and only changes a single POS axis value based on the BlockFacing value.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public static BlockPos PosTraversal(BlockFacing direction, BlockPos pos) {
            switch (direction.Index) {
                case 0: //North -Z
                    pos.Z--;
                    break;
                case 1: //East +X
                    pos.X++;
                    break;
                case 2: //South +Z
                    pos.Z++;
                    break;
                case 3: //West -X
                    pos.X--;
                    break;
                case 4: //Up +Y
                    pos.Y++;
                    break;
                case 5: //Down -Y
                    pos.Y--;
                    break;
            }
            return pos;
        }

        /// <summary>
        /// <para> A simple search casting in linear directions to see if there's any immediatly obvoius exit beyond a specific point, checking the directions Forward/Up/Down/Left/Right from the provided 'startPos'. </para>
        /// <para> 0 = no exit found | 1 = an exit found | -1 = A previously visited internal air block found. </para> 
        /// </summary>
        /// <param name="maxSize"></param>
        /// <param name="miny"></param>
        /// <param name="seedPos"></param>
        /// <param name="startPos"></param>
        /// <param name="forwardFace"></param>
        /// <param name="iteration"></param>
        /// <param name="currentVisited"></param>
        /// <param name="blockAccessor"></param>
        /// <returns></returns>
        public static int ExitSearch(int minx, int miny, int minz, int maxx, int maxy, int maxz, int exposedBlocks, BlockPos seedPos, BlockPos startPos, BlockFacing forwardFace, int iteration, int[] currentVisited, ICachingBlockAccessor blockAccessor) {
            int halfSize = (ARRAYSIZE - 1) / 2;
            int maxSize = halfSize + halfSize;
            BlockPos npos = new(Dimensions.NormalWorld),
                bpos = new(Dimensions.NormalWorld);
            Block bBlock = blockAccessor.GetBlock(startPos), nBlock = bBlock;
            int posY = seedPos.Y - halfSize,
                posX = seedPos.X - halfSize,
                posZ = seedPos.Z - halfSize;
            int dy, dx, dz;
            int heatRetention;
            int visitedIndex;
            bool foundExit = false;
            
            if (Harmony.DEBUG == true) Console.WriteLine("++Beginning Exit Search++");

            #region Linear searches extending from the 'returnPos' in all 5 directions not pointed towards the room.
            BlockFacing searchDirection = forwardFace;
            foreach (BlockFacing facing in BlockFacing.ALLFACES) {
                //will cycle through all faces of the returnPos block (will skip the face opposite of 'forwardFace').
                searchDirection = facing;
                if (searchDirection == forwardFace.Opposite) continue;
                
                npos.Set(startPos);
                bpos.Set(startPos);
                int cycle = 0;               
                while (cycle < 3) { 
                    //When hitting a wall, try to 'snake' around in the other directions, incase there's not an immediately obvious hole.
                    //npos will be moved in the 'searchDirection' relative to the current 'bpos' value.
                    npos.Set(PosTraversal(searchDirection, npos));
                    if (blockAccessor.IsValidPos(npos)) {
                        dy = npos.Y - posY;
                        dx = npos.X - posX;
                        dz = npos.Z - posZ;

                        //check if we've found an exit point after traversing.
                        switch (searchDirection.Index) {
                            case 0: { foundExit = dz < 0 || maxz - minz + 1 >= MAXROOMSIZE; break; } //North -z
                            case 1: { foundExit = dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE; break; }//East +x
                            case 2: { foundExit = dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE; break; }//South +z
                            case 3: { foundExit = dx < 0 || maxx - minx + 1 >= MAXROOMSIZE; break; } //West -x
                            case 4: { foundExit = dy > maxSize || maxy - miny + 1 >= MAXROOMSIZE; break; } //Up +y
                            case 5: { foundExit = dy < 0 || maxy - miny + 1 >= MAXROOMSIZE; break; } //Down -y
                        }
                        if (foundExit == true) {
                            if (Harmony.DEBUG == true) Console.WriteLine("Exit Found: Exposing Block");
                            return 1;
                        }

                        visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                        nBlock = blockAccessor.GetBlock(npos);
                        //check if we've seen this block already
                        if (currentVisited[visitedIndex] == iteration) {
                            //if we find an open air block that's already marked as visited; We will assume we're still inside the room and this isn't an exposing wall.
                            if (nBlock.Id == 0) {
                                if (Harmony.DEBUG == true) Console.WriteLine("Already visited block. ["+nBlock+" at position: "+npos+"] Internal Block");
                                return -1; 
                            }
                            //previously visited exposing walls will be treated as solid walls. (To avoid re-entering the room if it's horseshoe or donut shaped.)
                            else heatRetention = 1;
                        }
                        else {
                            //if it's a block we've not visited already
                            heatRetention = nBlock.GetRetention(npos, searchDirection.Opposite, EnumRetentionType.Heat);
                        }
                    }
                    //because we're no longer inside the room, an invalid pos will just be treated as a wall.
                    else { heatRetention = 1; }

                    if (heatRetention != 0) {
                        npos.Set(bpos);
                        //If we hit a wall of any kind change direction in case it's a fluke.
                        switch (searchDirection.Index) {
                            case 0: { searchDirection = BlockFacing.WEST; break; }  //North (-z) to WEST
                            case 1: { searchDirection = BlockFacing.UP; break; } //East (+x) to UP
                            case 2: { searchDirection = BlockFacing.EAST; break; }  //South (+z) to EAST
                            case 3: { searchDirection = BlockFacing.DOWN; break; } //West (-x) to DOWN
                            case 4: { searchDirection = BlockFacing.NORTH; break; }  //Up (+y) to NORTH
                            case 5: { searchDirection = BlockFacing.SOUTH; break; }    //Down (-y) to SOUTH
                        }
                        cycle++;
                    }
                    else {
                        bpos.Set(npos);
                    }
                    //If we have not hit an open wall, continue moving npos in the same searchDirection.
                }
                npos.Set(startPos); //return to the starting position of the search so we can iterate to the next face.
            }
            if (Harmony.DEBUG == true) Console.WriteLine("No exits found: Ventilation Block.");
            return 0; //we found no exit or issue. The exposing blocks will be considered 'ventilation blocks'
            #endregion
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
        const int ARRAYSIZE = 29;  // Note if this constant is increased beyond 32, the bitshifts for compressedPos in the bfsQueue.Enqueue() and .Dequeue() calls may need updating
        //readonly int[] currentVisited = new int[ARRAYSIZE * ARRAYSIZE * ARRAYSIZE]; // An array that contains integers that represent the compressed XYZ Pos coordinates of blocks that have been searched already.
        //readonly int[] skyLightXZChecked = new int[ARRAYSIZE * ARRAYSIZE]; // An array that represents the XZ positions of skylight blocks that have been identified.
        const int MAXROOMSIZE = 14; // Max size of what a room can be
        const int MAXCELLARSIZE = 7; // Max size of what a standard cellar can be
        const int ALTMAXCELLARSIZE = 9; // If any wall|floor is larger than 7 then 9 is what only one axis of the room could possibly be
        const int ALTMAXCELLARVOLUME = 150; // If having to use ALTMAX size, then the number of interior blocks (volume) can't be more than 150
        //int iteration = 0; // How many times the search loop has iterated.
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
        public static void PatchRoomRegFindRoom(RoomRegistry __instance, BlockPos pos, ref Room __result, int ___iteration, int[] ___currentVisited, int[] ___skyLightXZChecked, ICachingBlockAccessor ___blockAccessor, ICoreAPI ___api) {
            if (Harmony.DEBUG == true) FileLog.Log("FindRoom Patch");
            #region Use one of VintageStory's datastructures to quickly and performantly store block position data as a compressed integer value.
            QueueOfInt bfsQueue = new(); // Enqueue a single value with four separate components, assumed to be signed int, in the range of -128 to +127
            #endregion

            #region Use the 'MAXROOMSIZE' value to Enqueue the theoretical position of the block that'd be in the exact center of the room.
            int halfSize = (ARRAYSIZE - 1) / 2;
            int maxSize = halfSize + halfSize;
            bfsQueue.Enqueue(halfSize << 10 | halfSize << 5 | halfSize);
            #endregion

            #region Add the theoretical center block to the start of the 'vistedIndex' array and add '1' to the search 'iteration' value. Then, add that BlockPOS to the [1] index of the 'currentVisited' array.
            int visitedIndex = (halfSize * ARRAYSIZE + halfSize) * ARRAYSIZE + halfSize; // Center node
            int iteration = ++___iteration;
            ___currentVisited[visitedIndex] = iteration; //Uses the iteration count to represent the Index value that the current 'visitedIndex' blockPOS should be assigned to in the 'currentVisited' array.
            #endregion

            #region Establish the int variables that represent notable block/entity/condition types found during the search.            
            int coolingWallCount = 0; // [Vanilla] Represents blocks that qualify as temperature controlled for cellars.
            int nonCoolingWallCount = 0;

            int skyLightCount = 0; // [Vanilla] Represents ceiling blocks that count as skylights for greenhouses.
            int nonSkyLightCount = 0;

            int exitCount = 0; // [Vanilla] Reprsents the number times the search has gone beyond the MAXROOMSIZE value in any direction.

            int enclosingBlocks = 0; //blocks that are heat retaining.
            int exposingBlocks = 0; //blocks that aren't heat retaining, but still physically make a wall. (Fences, Stairs, slabs, crude doors, etc.)
            int ventilatedBlocks = 0; //blocks that would be considered exposing, but aren't exposed to an exit point for the room. (Without mods, these are just exposing blocks that don't count for certain checks.)
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
            BlockPos npos = new(Dimensions.NormalWorld);
            BlockPos bpos = new(Dimensions.NormalWorld);
            BlockPos returnPos = npos; //create a backup Pos in case we need to return.
            int dx, dy, dz; // Additionally creates 3 int variables representing the 'decompressed' x|y|z integer values
            #endregion
            if (Harmony.DEBUG == true) Console.WriteLine("== [Begining the search] ==");
            #region [THE SEARCH] Use a 'Floodfill' Breadth-First-Search sequence to identify the boundries and contents of the room. This while-loop will be modified from the original code to accomodate new room types.
            while (bfsQueue.Count > 0) {
                //if (Harmony.DEBUG == true) Console.WriteLine("= iteration: " + iteration + " =");

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
                    bool exposingConfirm = false;

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
                        //if (Harmony.DEBUG == true) Console.WriteLine("Invalid nPOS: Continue");
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

                        //if (Harmony.DEBUG == true) Console.WriteLine("solid wall: continue");
                        continue; //jump back to the top of the for loop.
                    }
                    #endregion

                    // TODO - Ensure that the nBlock that is discoverd to be an exposing block works with 'vent' blocks as well as the 'exposing' blocks.
                    #region If the nBlock block is an 'exposing' type block, then we need to confirm if it's an external wall.
                    // The block types 'stairs'/'fence'/'chisel'/'slab'/'door' could act as 'windows' that simply do not insulate the room.
                    // Only windows of 1x1 will operate as exposing walls, any wider/taller and it'll be treated as vanilla does.
                    else if (((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockBaseDoor)) && heatRetention == 0) {
                        if (Harmony.DEBUG == true) Console.WriteLine("= iteration: " + iteration + " =");
                        if (Harmony.DEBUG == true) Console.WriteLine("Exposing block found: " + nBlock + ", while searching: " + facing + ".\nIt is a " + nBlock.Class);
                        BlockPos checkPos = new(Dimensions.NormalWorld);
                        checkPos.Set(npos);
                        BlockFacing direction = BlockFacing.UP;
                        int cycles = 0;

                        switch (facing.Index) {
                            case 0: { direction = BlockFacing.WEST; break; }   //North (-z) to WEST
                            case 1: { direction = BlockFacing.UP; break; }   //East (+x) to SOUTH
                            case 2: { direction = BlockFacing.EAST; break; }   //South (+z) to EAST
                            case 3: { direction = BlockFacing.DOWN; break; }   //West (-x) to NORTH
                            case 4: { direction = BlockFacing.NORTH; break; }   //Up (+y) to NORTH
                            case 5: { direction = BlockFacing.SOUTH; break; }   //Down (-y) to SOUTH
                        }
                        Block checkBlock = ___blockAccessor.GetBlock(checkPos);
                        //Sequence to ensure we're inside an exposing block window that's 1x1x1.
                        while (cycles < 2) {
                            cycles++;
                            //move checkPos to the position horizontally adjacent to npos.
                            checkPos.Set(PosTraversal(direction, checkPos));
                            if (!___blockAccessor.IsValidPos(checkPos)) { break; }
                            allChunksLoaded &= ___blockAccessor.LastChunkLoaded;
                            checkBlock = ___blockAccessor.GetBlock(checkPos);
                            heatRetention = checkBlock.GetRetention(checkPos, direction.Opposite, EnumRetentionType.Heat);

                            //If the adjacent block is not heat retaining, then this block is not considered exposing and will be treated as an open block.
                            if (heatRetention == 0) { break; }
                            checkPos.Set(npos);

                            //move checkPos to the horizontal position opposite of npos.
                            checkPos.Set(PosTraversal(direction.Opposite, npos));
                            if (!___blockAccessor.IsValidPos(checkPos)) { break; }
                            allChunksLoaded &= ___blockAccessor.LastChunkLoaded;
                            checkBlock = ___blockAccessor.GetBlock(checkPos);
                            heatRetention = checkBlock.GetRetention(checkPos, direction, EnumRetentionType.Heat);

                            //If the adjacent block is not heat retaining, then this block is not considered exposing and will be treated as an open block.
                            if (heatRetention == 0) { break; }
                            checkPos.Set(npos);

                            //if we've cycled through all of Up/Down/Left/Right (cycles > 1), we found an exposing block surrounded by solid wall blocks.
                            if (cycles > 1) {
                                //check the block immedietly behind the exposing wall to ensure it's not solid or another exposing block.
                                checkPos.Set(PosTraversal(facing, npos));
                                if (!___blockAccessor.IsValidPos(checkPos)) { break; }
                                allChunksLoaded &= ___blockAccessor.LastChunkLoaded;
                                checkBlock = ___blockAccessor.GetBlock(checkPos);
                                heatRetention = checkBlock.GetRetention(checkPos, direction, EnumRetentionType.Heat);

                                //If the block behind the exposing wall is solid or another exposing wall, then we will treat this exposing block as vanilla would.
                                if (heatRetention != 0 || (checkBlock is BlockStairs) || (checkBlock is BlockFence) || (checkBlock is BlockSlab) || (checkBlock is BlockBaseDoor)) {
                                    break;
                                }
                                //perform an exit search
                                int exposingCount = ExitSearch(minx, miny, minz, maxx, maxy, maxz, 1, pos, checkPos, facing, ___iteration, ___currentVisited, ___blockAccessor);

                                if (exposingCount > 0) {
                                    //an exit was found, this block is an exposing wall block.
                                    exposingBlocks++;
                                    exposingConfirm = true;
                                    break;
                                }
                                else if (exposingCount == 0) {
                                    //no exit was found, this block is a ventelation block.
                                    ventilatedBlocks++;
                                    exposingConfirm = true;
                                    break;
                                }
                                //If exposingCount was negative, then this was not an outside facing wall and the block will be treated as vanilla would.
                            }

                            //Reaching this means we haven't cycled to Up and Down yet.
                            switch (facing.Index) {
                                case 0: { direction = BlockFacing.UP; break; }   //North (-z) to UP
                                case 1: { direction = BlockFacing.DOWN; break; }   //East (+x) to DOWN
                                case 2: { direction = BlockFacing.DOWN; break; }   //South (+z) to DOWN
                                case 3: { direction = BlockFacing.UP; break; }   //West (-x) to UP
                                case 4: { direction = BlockFacing.EAST; break; }   //Up (+y) to EAST
                                case 5: { direction = BlockFacing.WEST; break; }   //Down (-y) to WEST                                
                            }
                        }
                        if (exposingConfirm == true) {
                            if (Harmony.DEBUG == true) Console.WriteLine("Exposed Wall found at Pos: [" + npos + "]. \n ==Continuing foreach to skip adding this block to queue==");
                            continue;
                        }
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
                        //if (Harmony.DEBUG == true) Console.WriteLine("exit found: Continue");
                        continue;
                    }
                    #endregion

                    #region add the current XYZ pos data into the currentVisited array. If the current POS has already been visited, then move back to the top of the for loop.
                    visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                    //if (Harmony.DEBUG == true) Console.WriteLine("visitedIndex: " + visitedIndex);
                    if (___currentVisited[visitedIndex] == iteration) {
                        // continue if block position was already visited.
                        //if (Harmony.DEBUG == true) Console.WriteLine("Already Visited Block: Continue");
                        continue; 
                    }
                    //if (Harmony.DEBUG == true) Console.WriteLine("Adding ["+ nBlock +"] at position ["+npos+"] to visited Index ["+visitedIndex+"] for iteration ["+iteration+"]");
                    ___currentVisited[visitedIndex] = iteration; // If the block position has not been visited, add it to the currentVisited array, using the current iteration as the index value for the array.
                    #endregion

                    #region If the block is a new block, check that it qualifies for any relavent qualities. [Potentially modded checks entered here]
                    // We only need to check the skylight if it's a block position not already visited ...
                    int skyLightIndex = dx * ARRAYSIZE + dz; //the compressed X and Z position values of the skyLight collumn.
                    if (___skyLightXZChecked[skyLightIndex] < iteration)
                    {
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
                        if (___currentVisited[visitedIndex] == iteration) {
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

            if (Harmony.DEBUG == true) Console.WriteLine("Enclosing blocks = " + enclosingBlocks + " | Exposing blocks = " + exposingBlocks + " | Ventelating blocks = " + ventilatedBlocks);

            #region Return a RenRoom object with all the related data found by the method.
            RenRoom toReturn = new() {
                //obj counts
                CoolingWallCount = coolingWallCount,
                NonCoolingWallCount = nonCoolingWallCount,
                SkylightCount = skyLightCount,
                NonSkylightCount = nonSkyLightCount,
                EnclosingBlockCount = enclosingBlocks,
                ExposingBlockCount = exposingBlocks,
                VentilatedBlockCount = ventilatedBlocks,
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