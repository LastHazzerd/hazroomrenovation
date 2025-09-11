using HarmonyLib;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using static OpenTK.Graphics.OpenGL.GL;

#nullable disable

// This is a 'copy' of the vanilla RoomRegistry.cs file. I am intending to expand upon the functionality of it by inheriting the original functions, and patching every reference in the vanilla code to use these instead.
namespace hazroomrenovation.source.Systems {
    /// <summary> RenRoom (Renovated Room) - inherits the Room class from vanilla and allows for more data to be checked and more behaviors/effects to be provided. 
    /// </summary>
    public class RenRoom : Room {

        //#region Vanilla variables
        ///// <summary> The number of empty blocks found in the room's walls, floor, and ceiling. 
        ///// </summary>
        //public new int ExitCount;
        ///// <summary> The number of ciling blocks that are considered transparent enough to let sunlight through while still being insulated. 
        ///// </summary>
        //public new int SkylightCount;
        ///// <summary> The number of ciling blocks that do not let enough sunlight through to be considered a skylight. 
        ///// </summary>
        //public new int NonSkylightCount;
        ///// <summary> The number of wall blocks that are considered temperature retaining/insulating from outside heat. 
        ///// </summary>
        //public new int CoolingWallCount;
        ///// <summary> the number of wall blocks that are not considered temperature retaining/insulating from outside heat. 
        ///// </summary>
        //public new int NonCoolingWallCount;

        ///// <summary> If true, indicates room dimensions do not exceed recommended cellar dimensions of 7x7x7  (soft limit: slightly longer shapes with low overall volume also permitted) 
        ///// </summary>
        //public new bool IsSmallRoom;

        ///// <summary> A bounding box of the found room volume, but that doesn't mean this volumne is 100% room. You can check if a block inside inside is volume is part of the room with the PosInRoom byte array 
        ///// </summary>
        //public new Cuboidi Location;
        //public new byte[] PosInRoom;

        ///// <summary> If greater than 0, a chunk is unloaded. Counts upwards and when it reaches a certain value, this room will be removed from the registry and re-checked: this allows valid fully loaded rooms to be detected quite quickly in the normal world loading process        /// The potential issue is a room with a container, on the very edge of the server's loaded world, with neighbouring chunks remaining unloaded for potentially a long time. This will never be loaded, so we don't want to recheck its status fully too often: not every tick, that would be too costly 
        ///// </summary>
        //public new int AnyChunkUnloaded;
        //#endregion

        #region modded numerical values
        /// <summary> number of blocks making up the walls/floor/ceiling can be considered heat retaining. 
        /// </summary>
        public int InsulatedBlockCount;
        /// <summary> number of blocks making up the walls/floor/ceiling can NOT be considered heat retaining. 
        /// </summary>
        public int ExposingBlockCount;
        /// <summary> numerical value representing the Y Position value of the rooms' lowest block. 
        /// </summary>
        public int RoomWorldHeight;
        /// <summary> numerical value representing the level of heat retention a room has. 
        /// </summary>
        public int Insulation;
        /// <summary> a numerical value that takes into consideration current world temp and heat/cold sources present. 
        /// </summary>
        public int RoomTemp;
        /// <summary> the number of blockentities that can be considered 'heat sources' in the room. (firepits, heaters, etc.) 
        /// </summary>
        public int HeatSources;
        /// <summary> the number of blockentities that can be considered 'cold sources' in the room. (iceblocks, coolers, etc.) 
        /// </summary>
        public int ColdSources;
        /// <summary> a numerical value representing the amount of ambient mosture is in the air of the room. Influenced by the presence of rain, water blocks, season, and the climate's average rainfall. 
        /// </summary>
        public int Humidity;
        /// <summary> the number of water blocks found in the room. 
        /// </summary>
        public int WaterBlocks;

        //TODO: need to figure out a good way of flexibly looking for specific blocktypes/entities for room requirements, such as beds, anvils, troughs, etc. Perhaps roomtype can be a JSON with its own definitions for room limits.
        //public int ForestCoverage; //a numerical value representing how much forest coverage is in the climate around the room. (Not sure if this is useful at the moment)
        //public int ShrubCoverage; //a numerical value representing how much shrubbery is in the climate around the room.
        #endregion

        #region room information strings
        /// <summary> The specific kind of room that is identified by the room check. 
        /// </summary>
        public string RoomType;
        //public string RoomName; //TODO find a way to retain some data for room locations. perhaps saving the position of a single door and considering it the 'main entrance' to a room per player's interaction.
        #endregion

        /// <summary> boolean check to verify if the room in question is fully loaded or if any number of chunks it is built on are currently unloaded. </summary>
        /// <param name="roomsList"></param>
        //public bool IsFullyLoaded(ChunkRooms roomsList) {
        //    if (AnyChunkUnloaded == 0) return true;

        //    if (++AnyChunkUnloaded > 10) {
        //        roomsList.RemoveRoom(this);
        //    }
        //    return false;
        //}

        /// <summary> A boolean check to verify if the given position is located within the room. </summary>
        /// <param name="pos"></param>
        //public new bool Contains(BlockPos pos) {
        //    if (!Location.ContainsOrTouches(pos)) return false;

        //    int sizez = Location.Z2 - Location.Z1 + 1;
        //    int sizex = Location.X2 - Location.X1 + 1;

        //    int dx = pos.X - Location.X1;
        //    int dy = pos.Y - Location.Y1;
        //    int dz = pos.Z - Location.Z1;

        //    int index = (dy * sizez + dz) * sizex + dx;

        //    return (PosInRoom[index / 8] & 1 << index % 8) > 0;
        //}
    }

    /// <summary> A class that manages a list of Renovated Rooms to be looked up and/or deleted. (Subject to change for data retention.) 
    /// </summary>
    //public class ChunkRenRooms : ChunkRooms {
    //    //public List<RenRoom> Rooms = [];

    //    //public object roomsLock = new();
    //    public void AddRoom(RenRoom room) {
    //        lock (roomsLock) {
    //            Rooms.Add(room);
    //        }
    //    }
    //    public void RemoveRoom(RenRoom room) {
    //        lock (roomsLock) {
    //            Rooms.Remove(room);
    //        }
    //    }

    //}

    /// <summary> Inherits the vanilla RoomRegistry class, used to record a RenRoom's Fields and saves it to the Chunk list.
    /// </summary>
    public class RenRoomRegistry : RoomRegistry {
        const int chunksize = GlobalConstants.ChunkSize;

        /// <summary>
        /// The Room Renovation mod's modifed version of the original RoomRegistry's 'GetRoomForPosition' method.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="ogInst"></param>
        /// <param name="chunksize"></param>
        /// <param name="chunkMapSizeX"></param>
        /// <param name="chunkMapSizeZ"></param>
        /// <returns></returns>
        public Room GetRenRoomForPosition(BlockPos pos, ICachingBlockAccessor blockAccess, ICoreAPI api, int chunkMapSizeX, int chunkMapSizeZ) { //, ICachingBlockAccessor blockAccess, ICoreAPI api) { //, int chunksize, int chunkMapSizeX, int chunkMapSizeZ, ICachingBlockAccessor blockAccess, ICoreAPI api) {            
            //if (Harmony.DEBUG == true) { System.Diagnostics.Debug.WriteLine("RenRoomRegistry is running 'GetRoom' method."); }
            
            
            long index3d = MapUtil.Index3dL(pos.X / chunksize, pos.Y / chunksize, pos.Z / chunksize, chunkMapSizeX, chunkMapSizeZ);

            ChunkRooms chunkrooms;
            Room room;

            lock (roomsByChunkIndexLock) {
                roomsByChunkIndex.TryGetValue(index3d, out chunkrooms);
            }

            if (chunkrooms != null) {
                Room firstEnclosedRoom = null;
                Room firstOpenedRoom = null;

                for (int i = 0; i < chunkrooms.Rooms.Count; i++) {
                    room = chunkrooms.Rooms[i];
                    if (room.Contains(pos)) {
                        if (firstEnclosedRoom == null && room.ExitCount == 0) {
                            firstEnclosedRoom = room;
                        }
                        if (firstOpenedRoom == null && room.ExitCount > 0) {
                            firstOpenedRoom = room;
                        }
                    }
                }

                if (firstEnclosedRoom != null && firstEnclosedRoom.IsFullyLoaded(chunkrooms)) return firstEnclosedRoom;
                if (firstOpenedRoom != null && firstOpenedRoom.IsFullyLoaded(chunkrooms)) return firstOpenedRoom;

                room = FindRenRoomForPosition(pos, blockAccess, api);
                chunkrooms.AddRoom(room);

                return room;
            }

            // Original code: ChunkRooms rooms = new ChunkRooms();
            ChunkRooms rooms = new();
            room = FindRenRoomForPosition(pos, blockAccess, api);
            rooms.AddRoom(room);

            lock (roomsByChunkIndexLock) {
                roomsByChunkIndex[index3d] = rooms;
            }

            return room;
        }

        #region constants for FindRoom
        // These variables are direct copies of the original RoomRegistry, they are private, but i don't believe they require the original RoomRegistry's instance.
        const int ARRAYSIZE = 29;  // Note if this constant is increased beyond 32, the bitshifts for compressedPos in the bfsQueue.Enqueue() and .Dequeue() calls may need updating
        readonly int[] currentVisited = new int[ARRAYSIZE * ARRAYSIZE * ARRAYSIZE];
        readonly int[] skyLightXZChecked = new int[ARRAYSIZE * ARRAYSIZE];
        const int MAXROOMSIZE = 14;
        const int MAXCELLARSIZE = 7;
        const int ALTMAXCELLARSIZE = 9;
        const int ALTMAXCELLARVOLUME = 150;
        int iteration = 0;
        #endregion

        /// <summary>
        /// The Renovation mod's version of the original RoomRegistry's FindRoom method.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="otherRooms"></param>
        /// <returns></returns>
        private RenRoom FindRenRoomForPosition(BlockPos pos, ICachingBlockAccessor blockAccess, ICoreAPI api) { //, ChunkRooms otherRooms, ICachingBlockAccessor blockAccess, ICoreAPI api) {
            //because this method is called ONLY by the GetRoom method above, and that method is called via a harmony patch to the base class, I need to ensure the base class' instances of private params/fields get passed to these methods.
            //if (Harmony.DEBUG == true) { System.Diagnostics.Debug.WriteLine("RenRoomRegistry is running 'FindRenRoom' method."); }
            //Original Code: QueueOfInt bfsQueue = new QueueOfInt()
            QueueOfInt bfsQueue = new();

            int halfSize = (ARRAYSIZE - 1) / 2;
            int maxSize = halfSize + halfSize;
            bfsQueue.Enqueue(halfSize << 10 | halfSize << 5 | halfSize);

            int visitedIndex = (halfSize * ARRAYSIZE + halfSize) * ARRAYSIZE + halfSize; // Center node
            int iteration = ++this.iteration;
            currentVisited[visitedIndex] = iteration;

            int coolingWallCount = 0;
            int nonCoolingWallCount = 0;

            int skyLightCount = 0;
            int nonSkyLightCount = 0;
            int exitCount = 0;

            blockAccess.Begin();

            bool allChunksLoaded = true;

            int minx = halfSize, miny = halfSize, minz = halfSize, maxx = halfSize, maxy = halfSize, maxz = halfSize;
            int posX = pos.X - halfSize;
            int posY = pos.Y - halfSize;
            int posZ = pos.Z - halfSize;
            //Original Code: BlockPos npos = new BlockPos();
            //Original Code: BlockPos bpos = new BlockPos();
            BlockPos npos = new(Dimensions.NormalWorld);
            BlockPos bpos = new(Dimensions.NormalWorld);
            int dx, dy, dz;

            while (bfsQueue.Count > 0) {
                int compressedPos = bfsQueue.Dequeue();
                dx = compressedPos >> 10;
                dy = (compressedPos >> 5) & 0x1f;
                dz = compressedPos & 0x1f;
                npos.Set(posX + dx, posY + dy, posZ + dz);
                bpos.Set(npos);

                if (dx < minx) minx = dx;
                else if (dx > maxx) maxx = dx;

                if (dy < miny) miny = dy;
                else if (dy > maxy) maxy = dy;

                if (dz < minz) minz = dz;
                else if (dz > maxz) maxz = dz;

                Block bBlock = blockAccess.GetBlock(bpos);

                foreach (BlockFacing facing in BlockFacing.ALLFACES) {
                    facing.IterateThruFacingOffsets(npos);  // This must be the first command in the loop, to ensure all facings will be properly looped through regardless of any 'continue;' statements
                    int heatRetention = bBlock.GetRetention(bpos, facing, EnumRetentionType.Heat);

                    // We cannot exit current block, if the facing is heat retaining (e.g. chiselled block with solid side)
                    if (bBlock.Id != 0 && heatRetention != 0) {
                        if (heatRetention < 0) coolingWallCount -= heatRetention;
                        else nonCoolingWallCount += heatRetention;

                        continue;
                    }

                    if (!blockAccess.IsValidPos(npos)) {
                        nonCoolingWallCount++;
                        continue;
                    }

                    Block nBlock = blockAccess.GetBlock(npos);
                    allChunksLoaded &= blockAccess.LastChunkLoaded;
                    heatRetention = nBlock.GetRetention(npos, facing.Opposite, EnumRetentionType.Heat);

                    // We hit a wall, no need to scan further
                    if (heatRetention != 0) {
                        if (heatRetention < 0) coolingWallCount -= heatRetention;
                        else nonCoolingWallCount += heatRetention;

                        continue;
                    }

                    // Compute the new dx, dy, dz offsets for npos
                    dx = npos.X - posX;
                    dy = npos.Y - posY;
                    dz = npos.Z - posZ;

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


                    visitedIndex = (dx * ARRAYSIZE + dy) * ARRAYSIZE + dz;
                    if (currentVisited[visitedIndex] == iteration) continue;   // continue if block position was already visited
                    currentVisited[visitedIndex] = iteration;

                    // We only need to check the skylight if it's a block position not already visited ...
                    int skyLightIndex = dx * ARRAYSIZE + dz;
                    if (skyLightXZChecked[skyLightIndex] < iteration) {
                        skyLightXZChecked[skyLightIndex] = iteration;
                        int light = blockAccess.GetLightLevel(npos, EnumLightLevelType.OnlySunLight);

                        if (light >= api.World.SunBrightness - 1) {
                            skyLightCount++;
                        }
                        else {
                            nonSkyLightCount++;
                        }
                    }

                    bfsQueue.Enqueue(dx << 10 | dy << 5 | dz);
                }
            }



            int sizex = maxx - minx + 1;
            int sizey = maxy - miny + 1;
            int sizez = maxz - minz + 1;

            byte[] posInRoom = new byte[(sizex * sizey * sizez + 7) / 8];

            int volumeCount = 0;
            for (dx = 0; dx < sizex; dx++) {
                for (dy = 0; dy < sizey; dy++) {
                    visitedIndex = ((dx + minx) * ARRAYSIZE + (dy + miny)) * ARRAYSIZE + minz;
                    for (dz = 0; dz < sizez; dz++) {
                        if (currentVisited[visitedIndex + dz] == iteration) {
                            int index = (dy * sizez + dz) * sizex + dx;

                            posInRoom[index / 8] = (byte)(posInRoom[index / 8] | (1 << (index % 8)));
                            volumeCount++;
                        }
                    }
                }
            }

            bool isCellar = sizex <= MAXCELLARSIZE && sizey <= MAXCELLARSIZE && sizez <= MAXCELLARSIZE;
            if (!isCellar && volumeCount <= ALTMAXCELLARVOLUME) {
                isCellar = sizex <= ALTMAXCELLARSIZE && sizey <= MAXCELLARSIZE && sizez <= MAXCELLARSIZE
                    || sizex <= MAXCELLARSIZE && sizey <= ALTMAXCELLARSIZE && sizez <= MAXCELLARSIZE
                    || sizex <= MAXCELLARSIZE && sizey <= MAXCELLARSIZE && sizez <= ALTMAXCELLARSIZE;
            }


            return new RenRoom() {
                CoolingWallCount = coolingWallCount,
                NonCoolingWallCount = nonCoolingWallCount,
                SkylightCount = skyLightCount,
                NonSkylightCount = nonSkyLightCount,
                ExitCount = exitCount,
                AnyChunkUnloaded = allChunksLoaded ? 0 : 1,
                Location = new Cuboidi(posX + minx, posY + miny, posZ + minz, posX + maxx, posY + maxy, posZ + maxz),
                PosInRoom = posInRoom,
                IsSmallRoom = isCellar && exitCount == 0
            };
        }
    }
}
