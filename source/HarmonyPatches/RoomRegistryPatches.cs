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
        /// A method used to search through several 'exposing' block types to see if they form a wall or if they're just a block with no consequence.
        /// Will return 0 if no wall is detected, -1 if we leave the roomSize limits, -404 if any of the BlockPos found are invalid, or a positive number if it found exposing wall block(s).
        /// </summary>
        /// <param name="maxSize"></param>
        /// <param name="miny"></param>
        /// <param name="seedPos"></param>
        /// <param name="startPos"></param>
        /// <param name="forwardFace"></param>
        /// <param name="iteration"></param>
        /// <param name="currentVisited"></param>
        /// <param name="blockAccessor"></param>
        /// <param name="bookmarkPos"></param>
        /// <param name="exitPos"></param>
        /// <returns></returns>
        public static int ExposingSearch(int minx, int miny, int minz, int maxx, int maxy, int maxz, BlockPos seedPos, BlockPos startPos, BlockFacing forwardFace, int iteration, int[] currentVisited, ICachingBlockAccessor blockAccessor, out BlockPos exitStartPos) {
            int halfSize = (ARRAYSIZE - 1) / 2;
            int maxSize = halfSize + halfSize;
            BlockFacing altfacing = BlockFacing.UP;
            BlockFacing facing = BlockFacing.UP;
            BlockPos npos = startPos, anchorPos = startPos, returnPos = startPos;
            exitStartPos = startPos;
            Block anchorBlock = blockAccessor.GetBlock(startPos), nBlock;
            int posX = seedPos.X - halfSize;
            int posY = seedPos.Y - halfSize;
            int posZ = seedPos.Z - halfSize;
            int dy = startPos.Y - posY,
                dx = startPos.X - posX,
                dz = startPos.Z - posZ;
            int heatRetention,
                visitedIndex,
                potentialExposed = 1;
            bool altPath = false;
            bool roofFloor = false;
            if (forwardFace == BlockFacing.UP || forwardFace == BlockFacing.DOWN) roofFloor = true;

            //make sure current bBlock gets marked for the search.
            heatRetention = anchorBlock.GetRetention(startPos, facing, EnumRetentionType.Heat);
            visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
            if (currentVisited[visitedIndex] > 0 || currentVisited[visitedIndex] != iteration) {
                //If we've performed an exposing search on this block already, mark this as a negative value of the current iteration.
                //We'll use negative numbers to indicate blocks that have been searched by this method.
                currentVisited[visitedIndex] = -iteration;
            }

            //make sure above block isn't open air. if roof/floor, check west.
            if (roofFloor == true) facing = BlockFacing.WEST;
            if (Harmony.DEBUG == true) Console.WriteLine("Check above block isn't open air. \n" + "before iterate npos: " + npos + " | facing: " + facing);            
            npos.Set(PosTraversal(facing, npos));            
            if (Harmony.DEBUG == true) Console.WriteLine("after iterate npos: " + npos + " | facing: " + facing);
            if (blockAccessor.IsValidPos(npos)) {
                dy = npos.Y - posY;
                dx = npos.X - posX;
                dz = npos.Z - posZ;

                #region Update the current min|max x|y|z value if the current dx|dy|dz value are less|greater than the recorded min|max x|y|z value
                if (dx < minx) minx = dx;
                else if (dx > maxx) maxx = dx;

                if (dy < miny) miny = dy;
                else if (dy > maxy) maxy = dy;

                if (dz < minz) minz = dz;
                else if (dz > maxz) maxz = dz;
                #endregion

                switch (facing.Index) {
                    case 0: { if (dz < 0 || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; } //North -z
                    case 1: { if (dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; }//East +x
                    case 2: { if (dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; }//South +z
                    case 3: { if (dx < 0 || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; } //West -x
                    case 4: { if (dy > maxSize || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Up +y
                    case 5: { if (dy < 0 || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Down -y
                }
                nBlock = blockAccessor.GetBlock(npos);
                heatRetention = nBlock.GetRetention(npos, facing.Opposite, EnumRetentionType.Heat);

                if (nBlock.Id == 0) { //above block is open Air; Invalidated exposing wall block search. 
                    potentialExposed = 0;
                    return potentialExposed;
                }
                else if (heatRetention == 0 && ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockBaseDoor))) {
                    //above block is exposing.
                    visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                    if (currentVisited[visitedIndex] > 0 || currentVisited[visitedIndex] != iteration) {
                        //If we've performed an exposing search on this block already, mark this as a negative value of the current iteration.
                        //We'll use negative numbers to indicate blocks that have been searched by this method.
                        currentVisited[visitedIndex] = -iteration;
                    }
                    returnPos.Set(npos); //we want to remember where we left off behind us.
                    potentialExposed++;
                }
                //if neither of these, then we assume it is a solid block.
            }
            else {
                return -404; //if we hit an invalid position, return a negative number to let the main search know its invalid and should add to noncoolingblocks and execute a 'continue'.
            }

            //check if we're above an open block. If roof or floor, check to east.
            if (roofFloor == true) { facing = BlockFacing.EAST; altfacing = BlockFacing.WEST; }
            else { facing = BlockFacing.DOWN; }
            npos.Set(startPos);
            if (Harmony.DEBUG == true) Console.WriteLine("Check below block isn't open air. \n" + "before iterate npos: " + npos + " | facing: " + facing);
            npos.Set(PosTraversal(facing, npos));
            if (Harmony.DEBUG == true) Console.WriteLine("after iterate npos: " + npos + " | facing: " + facing);
            if (blockAccessor.IsValidPos(npos)) {
                dy = npos.Y - posY;
                dx = npos.X - posX;
                dz = npos.Z - posZ;

                #region Update the current min|max x|y|z value if the current dx|dy|dz value are less|greater than the recorded min|max x|y|z value
                if (dx < minx) minx = dx;
                else if (dx > maxx) maxx = dx;

                if (dy < miny) miny = dy;
                else if (dy > maxy) maxy = dy;

                if (dz < minz) minz = dz;
                else if (dz > maxz) maxz = dz;
                #endregion

                switch (facing.Index) {
                    case 0: { if (dz < 0 || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; } //North -z
                    case 1: { if (dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; }//East +x
                    case 2: { if (dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; }//South +z
                    case 3: { if (dx < 0 || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; } //West -x
                    case 4: { if (dy > maxSize || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Up +y
                    case 5: { if (dy < 0 || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Down -y
                }

                nBlock = blockAccessor.GetBlock(npos);
                heatRetention = nBlock.GetRetention(npos, facing.Opposite, EnumRetentionType.Heat);

                ///TODO figure out why the block accessor is returing "air"
                ///.
                ///.
                ///.


                if (nBlock.Id == 0) { //below block is open Air; Invalidated exposing wall block search. 
                    potentialExposed = 0;
                    return potentialExposed;
                }
                else if (heatRetention == 0 && ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockBaseDoor))) {
                    //below block is exposing.
                    visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                    if (currentVisited[visitedIndex] > 0 || currentVisited[visitedIndex] != iteration) { //if we've haven't searched this pos yet.
                        currentVisited[visitedIndex] = -iteration;
                    }
                    altPath = true;
                    altfacing = facing;
                    returnPos.Set(npos); //we want to remember where we left off behind us.
                    potentialExposed++;
                }
                //if neither of these, then we assume it is a solid block.
            }
            else {
                return -404;
            }
            //make sure the block behind us isn't open air, and that only one facing has another exposing block. If Roof/Floor, check North
            if (roofFloor == true) { facing = BlockFacing.NORTH; }
            else { facing = forwardFace.Opposite; }
            npos.Set(startPos);
            if (Harmony.DEBUG == true) Console.WriteLine("Check behind block isn't open air. \n" + "before iterate npos: " + npos + " | facing: " + facing);
            npos.Set(PosTraversal(facing, npos));
            if (Harmony.DEBUG == true) Console.WriteLine("after iterate npos: " + npos + " | facing: " + facing);
            if (blockAccessor.IsValidPos(npos)) {
                dy = npos.Y - posY;
                dx = npos.X - posX;
                dz = npos.Z - posZ;

                #region Update the current min|max x|y|z value if the current dx|dy|dz value are less|greater than the recorded min|max x|y|z value
                if (dx < minx) minx = dx;
                else if (dx > maxx) maxx = dx;

                if (dy < miny) miny = dy;
                else if (dy > maxy) maxy = dy;

                if (dz < minz) minz = dz;
                else if (dz > maxz) maxz = dz;
                #endregion

                switch (facing.Index) {
                    case 0: { if (dz < 0 || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; } //North -z
                    case 1: { if (dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; }//East +x
                    case 2: { if (dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; }//South +z
                    case 3: { if (dx < 0 || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; } //West -x
                    case 4: { if (dy > maxSize || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Up +y
                    case 5: { if (dy < 0 || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Down -y
                }

                nBlock = blockAccessor.GetBlock(npos);
                heatRetention = nBlock.GetRetention(npos, facing.Opposite, EnumRetentionType.Heat);


                if (nBlock.Id == 0) { 
                    //backward block is open; Invalidated exposing wall block search. 
                    potentialExposed = 0;
                    return potentialExposed;
                }
                else if (heatRetention == 0 && ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockBaseDoor))) {
                    //behind block is exposing.
                    visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                    if (currentVisited[visitedIndex] > 0 || currentVisited[visitedIndex] != iteration) { //if we've haven't searched this pos yet.
                        currentVisited[visitedIndex] = -iteration;
                    }
                    if (potentialExposed > 1) { 
                        //if the above/below block was already exposing, then this exposing block exceeds the allowed number of exposing blocks to qualify as a viable wall block.
                        potentialExposed = 0;
                        return potentialExposed;
                    }
                    else { 
                        //if we made it this far, then the foward block being exposed means we must follow it until we hit an open block or boundry.
                        potentialExposed++;
                    }
                    altPath = true;
                    altfacing = facing;
                    returnPos.Set(npos); //we want to remember where we left off behind us.
                    npos.Set(startPos); //return to original spot to check forward direction.
                }
                else {//if neither of these, then we assume the forward block is solid.
                    if (potentialExposed > 1) {
                        facing = altfacing;     //If we found an exposed block above, this will set us to Up, if insted it was below, this will be Down.
                    }
                    npos.Set(returnPos); //move the nBlock back to returnPos, which is either at the start position, or in an exposing block above/below it.
                }
            }
            else {
                return -404;
            }
            //we've now determined which block we intend to start from for this exposing block search, and which facing we should head.
            #region Navigate through the remaining exposed blocks, if any.
            if (potentialExposed > 1 || altPath == true) { //Check if we have more iterating to do.
                if (altPath == true) {
                    facing = altfacing;
                    anchorPos.Set(returnPos); //main block moving through the wall.
                    npos.Set(returnPos); //block used to check adjacent and next forward block.
                    switch (facing.Index) {
                        case 0: // North (-Z)
                            altfacing = BlockFacing.EAST;
                            break;
                        case 1: // East (+X)
                            altfacing = BlockFacing.SOUTH;
                            break;
                        case 2: // South (+Z)
                            altfacing = BlockFacing.WEST;
                            break;
                        case 3: // West (-X)
                            altfacing = BlockFacing.NORTH;
                            break;
                        case 4: // Up (+Y)
                            altfacing = BlockFacing.WEST;
                            break;
                        case 5: // Down (-Y)
                            altfacing = BlockFacing.EAST;
                            break;
                    }
                }
                else {
                    switch (forwardFace.Index) {
                        case 0: // North (-Z)
                            facing = BlockFacing.NORTH;
                            altfacing = BlockFacing.EAST;
                            break;
                        case 1: // East (+X)
                            facing = BlockFacing.EAST;
                            altfacing = BlockFacing.SOUTH;
                            break;
                        case 2: // South (+Z)
                            facing = BlockFacing.SOUTH;
                            altfacing = BlockFacing.WEST;
                            break;
                        case 3: // West (-X)
                            facing = BlockFacing.WEST;
                            altfacing = BlockFacing.NORTH;
                            break;
                        case 4: // Up (+Y)
                            facing = BlockFacing.SOUTH;
                            altfacing = BlockFacing.WEST;
                            break;
                        case 5: // Down (-Y)
                            facing = BlockFacing.NORTH;
                            altfacing = BlockFacing.EAST;
                            break;
                    }
                }
                //check side block for non-solid blocks.
                if (Harmony.DEBUG == true) Console.WriteLine("Check side block isn't also exposing. \n" + "before iterate npos: " + npos + " | altfacing: " + altfacing);
                npos.Set(PosTraversal(altfacing, npos));
                if (Harmony.DEBUG == true) Console.WriteLine("after iterate npos: " + npos + " | altfacing: " + altfacing);
                if (blockAccessor.IsValidPos(npos)) {
                    dy = npos.Y - posY;
                    dx = npos.X - posX;
                    dz = npos.Z - posZ;

                    #region Update the current min|max x|y|z value if the current dx|dy|dz value are less|greater than the recorded min|max x|y|z value
                    if (dx < minx) minx = dx;
                    else if (dx > maxx) maxx = dx;

                    if (dy < miny) miny = dy;
                    else if (dy > maxy) maxy = dy;

                    if (dz < minz) minz = dz;
                    else if (dz > maxz) maxz = dz;
                    #endregion

                    switch (facing.Index) {
                        case 0: { if (dz < 0 || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; } //North -z
                        case 1: { if (dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; }//East +x
                        case 2: { if (dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; }//South +z
                        case 3: { if (dx < 0 || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; } //West -x
                        case 4: { if (dy > maxSize || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Up +y
                        case 5: { if (dy < 0 || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Down -y
                    }

                    nBlock = blockAccessor.GetBlock(npos);
                    heatRetention = nBlock.GetRetention(npos, altfacing.Opposite, EnumRetentionType.Heat);

                    if (heatRetention == 0) { //adjacent block is not solid. 
                        potentialExposed = 0; //invalidated search.
                        return potentialExposed;
                    }
                }
                else {
                    return -404;
                }
                npos.Set(anchorPos); //return to current pos.

                //check other side block for non-solid blocks.
                if (Harmony.DEBUG == true) Console.WriteLine("Check other side block is solid. \n" + "before iterate npos: " + npos + " | altfacing: " + altfacing);
                npos.Set(PosTraversal(altfacing.Opposite, npos));
                if (Harmony.DEBUG == true) Console.WriteLine("after iterate npos: " + npos + " | altfacing: " + altfacing);
                if (blockAccessor.IsValidPos(npos)) {
                    dy = npos.Y - posY;
                    dx = npos.X - posX;
                    dz = npos.Z - posZ;

                    #region Update the current min|max x|y|z value if the current dx|dy|dz value are less|greater than the recorded min|max x|y|z value
                    if (dx < minx) minx = dx;
                    else if (dx > maxx) maxx = dx;

                    if (dy < miny) miny = dy;
                    else if (dy > maxy) maxy = dy;

                    if (dz < minz) minz = dz;
                    else if (dz > maxz) maxz = dz;
                    #endregion

                    switch (facing.Index) {
                        case 0: { if (dz < 0 || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; } //North -z
                        case 1: { if (dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; }//East +x
                        case 2: { if (dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; }//South +z
                        case 3: { if (dx < 0 || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; } //West -x
                        case 4: { if (dy > maxSize || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Up +y
                        case 5: { if (dy < 0 || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Down -y
                    }
                    nBlock = blockAccessor.GetBlock(npos);
                    heatRetention = nBlock.GetRetention(npos, altfacing.Opposite, EnumRetentionType.Heat);

                    if (heatRetention == 0) { //adjacent block is not solid. 
                        potentialExposed = 0; //invalidated search.
                        return potentialExposed;
                    }
                }
                else {
                    return -404;
                }
                npos.Set(anchorPos); //return to current pos.

                if (Harmony.DEBUG == true) Console.WriteLine("Move facing direction. \n" + "before iterate npos: " + npos + " | facing: " + facing);
                npos.Set(PosTraversal(facing, npos)); //move the facing direction we want.
                if (Harmony.DEBUG == true) Console.WriteLine("after iterate npos: " + npos + " | facing: " + facing);
                heatRetention = nBlock.GetRetention(npos, facing.Opposite, EnumRetentionType.Heat);
                if (!blockAccessor.IsValidPos(npos)) { return -404; }
                while (potentialExposed > 0 && heatRetention == 0 && ((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockDoor))) { //while the next block is exposing.
                    potentialExposed++; //we've found another potential exposing block.

                    //check side for non-solid block.
                    if (Harmony.DEBUG == true) Console.WriteLine("Check side for non-solid block. \n" + "before iterate npos: " + npos + " | altfacing: " + altfacing);
                    npos.Set(PosTraversal(altfacing, npos));
                    if (Harmony.DEBUG == true) Console.WriteLine("after iterate npos: " + npos + " | altfacing: " + altfacing);
                    if (blockAccessor.IsValidPos(npos)) {
                        dy = npos.Y - posY;
                        dx = npos.X - posX;
                        dz = npos.Z - posZ;

                        #region Update the current min|max x|y|z value if the current dx|dy|dz value are less|greater than the recorded min|max x|y|z value
                        if (dx < minx) minx = dx;
                        else if (dx > maxx) maxx = dx;

                        if (dy < miny) miny = dy;
                        else if (dy > maxy) maxy = dy;

                        if (dz < minz) minz = dz;
                        else if (dz > maxz) maxz = dz;
                        #endregion

                        switch (facing.Index) {
                            case 0: { if (dz < 0 || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; } //North -z
                            case 1: { if (dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; }//East +x
                            case 2: { if (dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; }//South +z
                            case 3: { if (dx < 0 || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; } //West -x
                            case 4: { if (dy > maxSize || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Up +y
                            case 5: { if (dy < 0 || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Down -y
                        }
                        nBlock = blockAccessor.GetBlock(npos);
                        heatRetention = nBlock.GetRetention(npos, altfacing.Opposite, EnumRetentionType.Heat);

                        if (heatRetention == 0) { //adjacent block is not solid. 
                            potentialExposed = 0; //invalidated search.
                            return potentialExposed;
                        }
                    }
                    else {
                        return -404;
                    }
                    npos.Set(anchorPos); //return bpos to current pos.

                    //check other side for non-solid block.
                    if (Harmony.DEBUG == true) Console.WriteLine("Check other side for non-solid block. \n" + "before iterate npos: " + npos + " | altfacing: " + altfacing);
                    npos.Set(PosTraversal(altfacing.Opposite, npos));
                    if (Harmony.DEBUG == true) Console.WriteLine("after iterate npos: " + npos + " | altfacing: " + altfacing);
                    if (blockAccessor.IsValidPos(npos)) {
                        dy = npos.Y - posY;
                        dx = npos.X - posX;
                        dz = npos.Z - posZ;

                        #region Update the current min|max x|y|z value if the current dx|dy|dz value are less|greater than the recorded min|max x|y|z value
                        if (dx < minx) minx = dx;
                        else if (dx > maxx) maxx = dx;

                        if (dy < miny) miny = dy;
                        else if (dy > maxy) maxy = dy;

                        if (dz < minz) minz = dz;
                        else if (dz > maxz) maxz = dz;
                        #endregion

                        switch (facing.Index) {
                            case 0: { if (dz < 0 || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; } //North -z
                            case 1: { if (dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; }//East +x
                            case 2: { if (dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; }//South +z
                            case 3: { if (dx < 0 || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; } //West -x
                            case 4: { if (dy > maxSize || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Up +y
                            case 5: { if (dy < 0 || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Down -y
                        }
                        nBlock = blockAccessor.GetBlock(npos);
                        heatRetention = nBlock.GetRetention(npos, altfacing.Opposite, EnumRetentionType.Heat);

                        if (heatRetention == 0) { //adjacent block is not solid. 
                            potentialExposed = 0; //invalidated search.
                            return potentialExposed;
                        }
                    }
                    else {
                        return -404;
                    }
                    npos.Set(anchorPos); //return bpos to current pos.

                    if (Harmony.DEBUG == true) Console.WriteLine("Move facing direction. \n" + "before iterate npos: " + npos + " | facing: " + facing);
                    npos.Set(PosTraversal(facing, npos)); //move the facing direction we want.
                    if (Harmony.DEBUG == true) Console.WriteLine("after iterate npos: " + npos + " | facing: " + facing);
                    if (blockAccessor.IsValidPos(npos)) {
                        dy = npos.Y - posY;
                        dx = npos.X - posX;
                        dz = npos.Z - posZ;

                        #region Update the current min|max x|y|z value if the current dx|dy|dz value are less|greater than the recorded min|max x|y|z value
                        if (dx < minx) minx = dx;
                        else if (dx > maxx) maxx = dx;

                        if (dy < miny) miny = dy;
                        else if (dy > maxy) maxy = dy;

                        if (dz < minz) minz = dz;
                        else if (dz > maxz) maxz = dz;
                        #endregion

                        switch (facing.Index) {
                            case 0: { if (dz < 0 || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; } //North -z
                            case 1: { if (dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; }//East +x
                            case 2: { if (dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; }//South +z
                            case 3: { if (dx < 0 || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; } //West -x
                            case 4: { if (dy > maxSize || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Up +y
                            case 5: { if (dy < 0 || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Down -y
                        }
                        nBlock = blockAccessor.GetBlock(npos);
                        heatRetention = nBlock.GetRetention(npos, facing, EnumRetentionType.Heat);
                        anchorPos.Set(npos);

                        visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                        if (currentVisited[visitedIndex] > 0 || currentVisited[visitedIndex] != iteration) { 
                            //if we've haven't searched this pos yet.
                            currentVisited[visitedIndex] = -iteration;
                        }

                        if (nBlock.Id == 0) { 
                            //next block is open air. 
                            potentialExposed = 0; //invalidated search.
                            return potentialExposed;
                        }
                        else if (heatRetention != 0 && altPath == true) {
                            //if we finally hit a solid wall while we were going in reverse.
                            //jump back to return point and head in the opposite direction.
                            exitStartPos.Set(npos);
                            npos.Set(startPos);
                            anchorPos.Set(startPos);
                            facing = facing.Opposite;

                            if (Harmony.DEBUG == true) Console.WriteLine("Check reverse direction after altpath. \n" + "before iterate npos: " + npos + " | facing: " + facing);
                            npos.Set(PosTraversal(facing, npos)); //move the facing direction we want.
                            if (Harmony.DEBUG == true) Console.WriteLine("after iterate npos: " + npos + " | facing: " + facing);
                            if (blockAccessor.IsValidPos(npos)) {
                                dy = npos.Y - posY;
                                dx = npos.X - posX;
                                dz = npos.Z - posZ;

                                #region Update the current min|max x|y|z value if the current dx|dy|dz value are less|greater than the recorded min|max x|y|z value
                                if (dx < minx) minx = dx;
                                else if (dx > maxx) maxx = dx;

                                if (dy < miny) miny = dy;
                                else if (dy > maxy) maxy = dy;

                                if (dz < minz) minz = dz;
                                else if (dz > maxz) maxz = dz;
                                #endregion

                                switch (facing.Index) {
                                    case 0: { if (dz < 0 || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; } //North -z
                                    case 1: { if (dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; }//East +x
                                    case 2: { if (dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE) { return -1; } break; }//South +z
                                    case 3: { if (dx < 0 || maxx - minx + 1 >= MAXROOMSIZE) { return -1; } break; } //West -x
                                    case 4: { if (dy > maxSize || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Up +y
                                    case 5: { if (dy < 0 || maxy - miny + 1 >= MAXROOMSIZE) { return -1; } break; } //Down -y
                                }
                                nBlock = blockAccessor.GetBlock(npos);
                                heatRetention = nBlock.GetRetention(npos, facing, EnumRetentionType.Heat);

                                visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                                if (currentVisited[visitedIndex] > 0 || currentVisited[visitedIndex] != iteration) { //if we've haven't searched this pos yet.
                                    currentVisited[visitedIndex] = -iteration;
                                }

                                if (nBlock.Id == 0) { //next block is open air. 
                                    potentialExposed = 0; //invalidated search.
                                    return potentialExposed;
                                }
                            }

                        }
                    }
                    else {
                        //if the next block isn't valid then we can't continue the search anyway.
                        return -404;
                    } 
                }
            }
            //If we make it here, then we have successfully found a wall with a complete set of exposing blocks inside it. 
            #endregion
            return potentialExposed;
        }

        /// <summary>
        /// A simple search casting in linear directions to see if there's any immediatly obvoius exit beyond a specific point, checking the directions Forward/Up/Down/Left/Right from the provided 'startPos'.
        /// Returns 0 if no exit was found. Returns 1 if an exit was found. Returns -1 if a previously visited internal air block was somehow found.  Returns -2 if the wall has a solid wall directly behind it. Returns -3 if an invalid pos was found.
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
        public static int ExitSearch(int minx, int miny, int minz, int maxx, int maxy, int maxz, int exposedBlocks, BlockPos seedPos, BlockPos startPos, BlockFacing forwardFace, BlockFacing wallDirection, int iteration, int[] currentVisited, ICachingBlockAccessor blockAccessor, out int exposedConfirm) {
            int halfSize = (ARRAYSIZE - 1) / 2;
            int maxSize = halfSize + halfSize;
            BlockPos npos = startPos, bpos = startPos, returnPos = startPos;
            Block bBlock = blockAccessor.GetBlock(bpos), nBlock = bBlock;
            int posY = seedPos.Y - halfSize,
                posX = seedPos.X - halfSize,
                posZ = seedPos.Z - halfSize;
            int dy, dx, dz;
            int heatRetention;
            int visitedIndex;
            int wallCheck = 0,
                wallTotal = exposedBlocks;
            bool foundExit = false;
            exposedConfirm = exposedBlocks;
            //Check each exposed block to ensure it's not immediately followed by a solid wall. Each exposing block with a wall outside of it will not be considered exposing.
            while (wallCheck < wallTotal) {
                //initial check of the first block beyond the wall.
                npos.Set(PosTraversal(forwardFace, npos));
                wallCheck++;
                if (!blockAccessor.IsValidPos(npos)) {
                    //if we hit an invalid POS beyond the wall, count it as solid and move to the next.
                    --exposedConfirm;
                    if (exposedConfirm == 0) { return -2; }
                    npos.Set(bpos);
                    //move down the wall to check the next block.
                    bpos.Set(PosTraversal(wallDirection, bpos));
                    if (!blockAccessor.IsValidPos(bpos)) { return -3; } //if we hit an invalid POS, return -3.
                    continue;
                }
                dy = npos.Y - posY;
                dx = npos.X - posX;
                dz = npos.Z - posZ;

                #region Update the current min|max x|y|z value if the current dx|dy|dz value are less|greater than the recorded min|max x|y|z value
                if (dx < minx) minx = dx;
                else if (dx > maxx) maxx = dx;

                if (dy < miny) miny = dy;
                else if (dy > maxy) maxy = dy;

                if (dz < minz) minz = dz;
                else if (dz > maxz) maxz = dz;
                #endregion

                switch (forwardFace.Index) {
                    case 0: { if (dz < 0 || maxz - minz + 1 >= MAXROOMSIZE) { foundExit = true;} break; } //North -z
                    case 1: { if (dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE) { foundExit = true;} break; }//East +x
                    case 2: { if (dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE) { foundExit = true;} break; }//South +z
                    case 3: { if (dx < 0 || maxx - minx + 1 >= MAXROOMSIZE) { foundExit = true;} break; } //West -x
                    case 4: { if (dy > maxSize || maxy - miny + 1 >= MAXROOMSIZE) { foundExit = true;} break; } //Up +y
                    case 5: { if (dy < 0 || maxy - miny + 1 >= MAXROOMSIZE) { foundExit = true;} break; } //Down -y
                }

                if (wallCheck == wallTotal) returnPos.Set(npos); //if this is the last check, then this will be where the exit search starts.
                nBlock = blockAccessor.GetBlock(npos);
                heatRetention = nBlock.GetRetention(npos, forwardFace.Opposite, EnumRetentionType.Heat);

                visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                if (currentVisited[visitedIndex] == iteration) { //If we find a block we've already visited OUTSIDE of this wall, and it's positive, then we assume it to be an internal block.
                    return -1;
                }
                if (heatRetention != 0) { --exposedConfirm; }
                if (exposedConfirm == 0) return -2; //if we go through all the blocks and determine that there is no openings behind them, then they're not exposing at all. return -2.
                //move down the wall to check the next block.
                bpos.Set(PosTraversal(wallDirection, bpos));
                npos.Set(bpos);
                if (!blockAccessor.IsValidPos(bpos)) { return -3; } //if we hit an invalid POS inside the wall, return -3.

            }
            if (foundExit == true) {
                //if we found an exit during the wall check, return 1.
                return 1;
            }
            #region Linear searches extending from the 'returnPos' in all 5 directions not pointed towards the room.
            BlockFacing searchDirection = forwardFace;
            foreach (BlockFacing facing in BlockFacing.ALLFACES) { //will cycle through all faces of the returnPos block (will skip the face opposite of 'forwardFace').
                if (searchDirection == forwardFace.Opposite) continue;
                npos.Set(returnPos);
                int cycle = 0;
                searchDirection = facing;
                while (cycle < 3) { //When hitting a wall, try to 'snake' around in the other directions, incase there's not an immediately obvious hole.                    
                    npos.Set(PosTraversal(searchDirection, npos));
                    if (blockAccessor.IsValidPos(npos)) {
                        //because we're no longer inside the room, and invalid pos will just be treated as a wall.
                        dy = npos.Y - posY;
                        dx = npos.X - posX;
                        dz = npos.Z - posZ;

                        #region Update the current min|max x|y|z value if the current dx|dy|dz value are less|greater than the recorded min|max x|y|z value
                        if (dx < minx) minx = dx;
                        else if (dx > maxx) maxx = dx;

                        if (dy < miny) miny = dy;
                        else if (dy > maxy) maxy = dy;

                        if (dz < minz) minz = dz;
                        else if (dz > maxz) maxz = dz;
                        #endregion

                        switch (searchDirection.Index) {
                            case 0: { if (dz < 0 || maxz - minz + 1 >= MAXROOMSIZE) { return 1; } break; } //North -z
                            case 1: { if (dx > maxSize || maxx - minx + 1 >= MAXROOMSIZE) { return 1; } break; }//East +x
                            case 2: { if (dz > maxSize || maxz - minz + 1 >= MAXROOMSIZE) { return 1; } break; }//South +z
                            case 3: { if (dx < 0 || maxx - minx + 1 >= MAXROOMSIZE) { return 1; } break; } //West -x
                            case 4: { if (dy > maxSize || maxy - miny + 1 >= MAXROOMSIZE) { return 1; } break; } //Up +y
                            case 5: { if (dy < 0 || maxy - miny + 1 >= MAXROOMSIZE) { return 1; } break; } //Down -y
                        }
                        visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                        if (currentVisited[visitedIndex] == iteration) { //if we find a block that's already marked as visited.
                            return -1;
                        }
                        else if (Math.Abs(currentVisited[visitedIndex]) == iteration) { heatRetention = 1; } //previously visited exposing walls will be treated as solid walls.
                        else {
                            nBlock = blockAccessor.GetBlock(npos);
                            heatRetention = nBlock.GetRetention(npos, forwardFace.Opposite, EnumRetentionType.Heat);
                        }
                    }
                    else { heatRetention = 1; }
                    //IF we hit a wall of any kind change direction in case it's a fluke.
                    if (heatRetention != 0) {
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
                }
                npos.Set(returnPos); //return to the starting position of the search so we can iterate to the next face.
            }
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
                    else if (((nBlock is BlockStairs) || (nBlock is BlockFence) || (nBlock is BlockSlab) || (nBlock is BlockBaseDoor)) && heatRetention == 0) {
                        nonCoolingWallCount++;
                        if (___currentVisited[visitedIndex] != iteration || ___currentVisited[visitedIndex] > 0) {
                            //I need to figure out why the index appears to be filled out before the blocks are actu... oh wait, i bet they get added when initially checked.
                            // if we have not already visited this exposing block, use it to perform an exposing wall check.
                            // if we HAVE visited this block already, then skip the search and proceed as normal.
                            BlockFacing direction = BlockFacing.SOUTH;
                            switch (facing.Index) {
                                case 0: // North (-Z)
                                    direction = BlockFacing.EAST;
                                    break;
                                case 1: // East (+X)
                                    direction = BlockFacing.SOUTH;
                                    break;
                                case 2: // South (+Z)
                                    direction = BlockFacing.WEST;
                                    break;
                                case 3: // West (-X)
                                    direction = BlockFacing.NORTH;
                                    break;
                                case 4: // Up (+Y)
                                    direction = BlockFacing.UP;
                                    break;
                                case 5: // Down (-Y)
                                    direction = BlockFacing.DOWN;
                                    break;
                            }

                            int exposingValue = ExposingSearch(minx, miny, minz, maxx, maxy, maxz, pos, npos, direction, iteration, ___currentVisited, ___blockAccessor, out BlockPos startPos);

                            if (exposingValue == -404) {
                                //invalid block location
                                nonCoolingWallCount++;
                                continue;
                            }
                            else if (exposingValue < 0) {
                                //exit found during search
                                exitCount++;
                                continue;
                            }
                            else if (exposingValue > 0) {
                                //an exposing wall was located.
                                int exitValue = ExitSearch(minx, miny, minz, maxx, maxy, maxz, exposingValue, pos, startPos, facing, direction, iteration, ___currentVisited, ___blockAccessor, out int exposedConfirm);
                                //an exit was found.
                                if (exitValue > 0) { exposingBlocks += exposedConfirm; }
                                //internal block was found.
                                else if (exitValue == -1) { exposingValue = 0; }
                                //no space found beyond exposing blocks. Or, an Invalid POS was found during the wall search.
                                else if (exitValue == -2 || exitValue == -3) { nonCoolingWallCount += exposedConfirm; }
                                //no exit found, but the otherside is mostly enclosed.
                                else { ventilatedBlocks += exposedConfirm; }
                            }
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
                        continue;
                    }
                    #endregion

                    #region add the current XYZ pos data into the currentVisited array. If the current POS has already been visited, then move back to the top of the for loop.
                    visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                    if (Math.Abs(___currentVisited[visitedIndex]) == iteration) continue;   // continue if block position was already visited
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
                        if (Math.Abs(___currentVisited[visitedIndex]) == iteration) {
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