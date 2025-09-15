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
    [HarmonyPatch]
    public class RoomRegistryPatches {

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
        public static void PatchRoomRegFindRoom(BlockPos pos, ref Room __result, int ___iteration, int[] ___currentVisited, int[] ___skyLightXZChecked, ICachingBlockAccessor ___blockAccessor, ICoreAPI ___api) {
            if (Harmony.DEBUG == true) FileLog.Log("FindRoom Patch");
            Console.WriteLine("Running the patched FindRoomForPosition");
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

            int enclosingBlockCount = 0; // [RenRoom] The number of blocks that either help to encase the room. (Solid wall) or keep it exposed/ventalated. (block with gaps, like fences, slabs, stairs, etc.)
            int exposingBlockCount = 0;
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
            int dx, dy, dz; // Additionally creates 3 int variables representing the 'decompressed' x|y|z integer values
            #endregion

            #region [THE SEARCH] Use a 'Floodfill' Breadth-First-Search sequence to identify the boundries and contents of the room. This while-loop will be modified from the original code to accomodate new room types.
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
                        if (heatRetention < 0) { coolingWallCount -= heatRetention; enclosingBlockCount++; }
                        else { nonCoolingWallCount += heatRetention; enclosingBlockCount++; }

                        continue; //jump back to the top of the for loop.
                    } // Since this is a wall, the block types 'stairs'/'fence'/'chisel'/'slab' are important because the wall might not be heatRetaining, but it is still a 'wall' of sorts.
                    else if (nBlock is (BlockStairs or BlockFence or BlockSlab) && heatRetention == 0) {
                        nonCoolingWallCount++; exposingBlockCount++;
                        continue; //jump back to the top of the for loop.
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
                            if (dz < minz) outsideCube = dz < 0 || maxz - minz + 1 >= MAXROOMSIZE;
                            break;
                        case 1: // East
                            if (dx > maxx) outsideCube = dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE;
                            break;
                        case 2: // South
                            if (dz > maxz) outsideCube = dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE;
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
                    if (outsideCube) {
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

            Console.WriteLine("enclosing blocks = "+enclosingBlockCount+" | exposingBlockCount = "+exposingBlockCount);

            #region Return a RenRoom object with all the related data found by the method.
            __result = new RenRoom() {
                CoolingWallCount = coolingWallCount,
                NonCoolingWallCount = nonCoolingWallCount,
                SkylightCount = skyLightCount,
                NonSkylightCount = nonSkyLightCount,
                EnclosingBlockCount = enclosingBlockCount,
                ExposingBlockCount = exposingBlockCount,
                ExitCount = exitCount,
                AnyChunkUnloaded = allChunksLoaded ? 0 : 1,
                Location = new Cuboidi(posX + minx, posY + miny, posZ + minz, posX + maxx, posY + maxy, posZ + maxz),
                PosInRoom = posInRoom,
                IsSmallRoom = isCellar && exitCount == 0
            };
            #endregion
        }
    }
}