using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#nullable disable

// This is a copy of the vanilla room registry, i'm hoping to take it and change it to the point of an overhaul.
namespace HazRoomRenovation.code.Systems {
    /// <summary> RenRoom (Renovated Room) - is an expanded upon Room class from vanilla that allows for more data to be checked and more behaviors/effects to be provided. </summary>
    public class RenRoom : Room {

        #region Vanilla variables
        /// <summary> The number of empty blocks found in the room's walls, floor, and ceiling. </summary>
        public new int ExitCount;
        /// <summary> The number of ciling blocks that are considered transparent enough to let sunlight through while still being insulated. </summary>
        public new int SkylightCount;
        /// <summary> The number of ciling blocks that do not let enough sunlight through to be considered a skylight. </summary>
        public new int NonSkylightCount;
        /// <summary> The number of wall blocks that are considered temperature retaining/insulating from outside heat. </summary>
        public new int CoolingWallCount;
        /// <summary> the number of wall blocks that are not considered temperature retaining/insulating from outside heat. </summary>
        public new int NonCoolingWallCount;

        /// <summary> If true, indicates room dimensions do not exceed recommended cellar dimensions of 7x7x7  (soft limit: slightly longer shapes with low overall volume also permitted) </summary>
        public new bool IsSmallRoom;

        /// <summary> A bounding box of the found room volume, but that doesn't mean this volumne is 100% room. You can check if a block inside inside is volume is part of the room with the PosInRoom byte array </summary>
        public new Cuboidi Location;
        public new byte[] PosInRoom;

        /// <summary> If greater than 0, a chunk is unloaded. Counts upwards and when it reaches a certain value, this room will be removed from the registry and re-checked: this allows valid fully loaded rooms to be detected quite quickly in the normal world loading process        /// The potential issue is a room with a container, on the very edge of the server's loaded world, with neighbouring chunks remaining unloaded for potentially a long time. This will never be loaded, so we don't want to recheck its status fully too often: not every tick, that would be too costly </summary>
        public new int AnyChunkUnloaded;
        #endregion

        #region modded numerical values
        /// <summary> number of blocks making up the walls/floor/ceiling can be considered heat retaining. </summary>
        public int InsulatedBlockCount;
        /// <summary> number of blocks making up the walls/floor/ceiling can NOT be considered heat retaining. </summary>
        public int ExposingBlockCount;
        /// <summary> numerical value representing the Y Position value of the rooms' lowest block. </summary>
        public int RoomWorldHeight;
        /// <summary> numerical value representing the level of heat retention a room has. </summary>
        public int Insulation;
        /// <summary> a numerical value that takes into consideration current world temp and heat/cold sources present. </summary>
        public int RoomTemp;
        /// <summary> the number of blockentities that can be considered 'heat sources' in the room. (firepits, heaters, etc.) </summary>
        public int HeatSources;
        /// <summary> the number of blockentities that can be considered 'cold sources' in the room. (iceblocks, coolers, etc.) </summary>
        public int ColdSources;
        /// <summary> a numerical value representing the amount of ambient mosture is in the air of the room. Influenced by the presence of rain, water blocks, season, and the climate's average rainfall. </summary>
        public int Humidity;
        /// <summary> the number of water blocks found in the room. </summary>
        public int WaterBlocks;

        //public int ForestCoverage; //a numerical value representing how much forest coverage is in the climate around the room. (Not sure if this is useful at the moment)
        //public int ShrubCoverage; //a numerical value representing how much shrubbery is in the climate around the room.
        //TODO: need to figure out a good way of flexibly looking for specific blocktypes/entities for room requirements, such as beds, anvils, troughs, etc. Perhaps roomtype can be a JSON with its own definitions for room limits.
        #endregion

        #region room information strings
        /// <summary> The specific kind of room that is identified by the room check. </summary>
        public string RoomType;
        //public string RoomName; //TODO find a way to retain some data for room locations. perhaps saving the position of a single door and considering it the 'main entrance' to a room per player's interaction.
        #endregion

        /// <summary> boolean check to verify if the room in question is fully loaded or if any number of chunks it is built on are currently unloaded. </summary>
        /// <param name="roomsList"></param>
        public bool IsFullyLoaded(ChunkRenRooms roomsList) {
            if (AnyChunkUnloaded == 0) return true;

            if (++AnyChunkUnloaded > 10) {
                roomsList.RemoveRoom(this);
            }
            return false;
        }

        /// <summary> A boolean check to verify if the given position is located within the room. </summary>
        /// <param name="pos"></param>
        public new bool Contains(BlockPos pos) {
            if (!Location.ContainsOrTouches(pos)) return false;

            int sizez = Location.Z2 - Location.Z1 + 1;
            int sizex = Location.X2 - Location.X1 + 1;

            int dx = pos.X - Location.X1;
            int dy = pos.Y - Location.Y1;
            int dz = pos.Z - Location.Z1;

            int index = (dy * sizez + dz) * sizex + dx;

            return (PosInRoom[index / 8] & (1 << (index % 8))) > 0;
        }
    }

    /// <summary> A class that manages a list of Renovated Rooms to be looked up and/or deleted. (Subject to change for data retention.) </summary>
    public class ChunkRenRooms {
        public List<RenRoom> Rooms = [];

        public object roomsLock = new();
        public void AddRoom(RenRoom room) {
            lock (roomsLock) {
                Rooms.Add(room);
            }
        }
        public void RemoveRoom(RenRoom room) {
            lock (roomsLock) {
                Rooms.Remove(room);
            }
        }

    }

    /// <summary> Class that identifies, establishes, and updates rooms when called. </summary>
    public class RenRoomRegistry : ModSystem {
        protected Dictionary<long, ChunkRenRooms> roomsByChunkIndex = [];
        protected object roomsByChunkIndexLock = new();

        const int chunksize = GlobalConstants.ChunkSize;
        int chunkMapSizeX;
        int chunkMapSizeZ;

        ICoreAPI api;

        [ThreadStatic]
        static ICachingBlockAccessor blockAccessor;   // [Original Dev note] We need a separate blockaccessor per thread, to prevent rare race conditions leading to crashes
        ICachingBlockAccessor BlockAccess {
            get {
                if (blockAccessor != null) return blockAccessor;

                blockAccessor = api.World.GetCachingBlockAccessor(false, false);
                disposableBlockAccessors[Environment.CurrentManagedThreadId] = blockAccessor; //[Original Dev note] System.Threading.Thread.CurrentThread.ManagedThreadId
                return blockAccessor;
            }
        }
        // [Original Dev note] Looks a bit heavy, but we write to this only once per game per thread; then later we can ensure that we dispose of all the blockAccessors even if the thread itself is *not* disposed (ThreadPool threads for example are not disposed)
        private System.Collections.Concurrent.ConcurrentDictionary<int, ICachingBlockAccessor> disposableBlockAccessors = new();

        public override bool ShouldLoad(EnumAppSide forSide) {
            return true;
        }

        public override void Start(ICoreAPI api) {
            base.Start(api);
            this.api = api;

            api.Event.ChunkDirty += Event_ChunkDirty;
        }

        public override void Dispose() {
            // [Original Dev note] This entire method designed to prevent memory leaks when the game exits: no thread should retain a CachingBlockAccessor

            blockAccessor?.Dispose();
            blockAccessor = null;

            foreach (var ba in disposableBlockAccessors.Values) {
                ba?.Dispose();
            }
            disposableBlockAccessors.Clear();
            disposableBlockAccessors = null;
        }

        public override void StartClientSide(ICoreClientAPI api) {
            api.Event.BlockTexturesLoaded += Init;
        }

        public override void StartServerSide(ICoreServerAPI api) {
            api.Event.SaveGameLoaded += Init;

            api.ChatCommands.GetOrCreate("debug")
                .BeginSubCommand("rooms")
                    .RequiresPrivilege(Privilege.controlserver)

                    .BeginSubCommand("list")
                        .HandleWith(OnRoomRegDbgCmdList)
                    .EndSubCommand()

                    .BeginSubCommand("hi")
                        .WithArgs(api.ChatCommands.Parsers.OptionalInt("rindex"))
                        .RequiresPlayer()
                        .HandleWith(OnRoomRegDbgCmdHi)
                    .EndSubCommand()

                    .BeginSubCommand("unhi")
                        .RequiresPlayer()
                        .HandleWith(OnRoomRegDbgCmdUnhi)
                    .EndSubCommand()
                .EndSubCommand()
                ;
        }

        private TextCommandResult OnRoomRegDbgCmdHi(TextCommandCallingArgs args) {
            int rindex = (int)args.Parsers[0].GetValue();
            var player = args.Caller.Player as IServerPlayer;
            BlockPos pos = player.Entity.Pos.XYZ.AsBlockPos;
            long index3d = MapUtil.Index3dL(pos.X / chunksize, pos.Y / chunksize, pos.Z / chunksize, chunkMapSizeX, chunkMapSizeZ);
            ChunkRenRooms chunkrooms;
            lock (roomsByChunkIndexLock) {
                roomsByChunkIndex.TryGetValue(index3d, out chunkrooms);
            }

            if (chunkrooms == null || chunkrooms.Rooms.Count == 0) {
                return TextCommandResult.Success("No rooms in this chunk");
            }

            if (chunkrooms.Rooms.Count - 1 < rindex || rindex < 0) {
                if (rindex == 0) {
                    return TextCommandResult.Success("No room at this index");
                }
                else {
                    return TextCommandResult.Success("Wrong index, select a number between 0 and " + (chunkrooms.Rooms.Count - 1));
                }
            }
            else {
                RenRoom room = chunkrooms.Rooms[rindex];

                if (args.Parsers[0].IsMissing) {
                    room = null;
                    foreach (var croom in chunkrooms.Rooms) {
                        if (croom.Contains(pos)) {
                            room = croom;
                            break;
                        }
                    }
                    if (room == null) {
                        return TextCommandResult.Success("No room at your location");
                    }
                }

                // Debug visualization
                List<BlockPos> poses = [];
                List<int> colors = [];

                int sizex = room.Location.X2 - room.Location.X1 + 1;
                int sizey = room.Location.Y2 - room.Location.Y1 + 1;
                int sizez = room.Location.Z2 - room.Location.Z1 + 1;

                for (int dx = 0; dx < sizex; dx++) {
                    for (int dy = 0; dy < sizey; dy++) {
                        for (int dz = 0; dz < sizez; dz++) {
                            int pindex = (dy * sizez + dz) * sizex + dx;

                            if ((room.PosInRoom[pindex / 8] & (1 << (pindex % 8))) > 0) {
                                poses.Add(new BlockPos(room.Location.X1 + dx, room.Location.Y1 + dy, room.Location.Z1 + dz));
                                colors.Add(ColorUtil.ColorFromRgba(room.ExitCount == 0 ? 0 : 100, room.ExitCount == 0 ? 100 : 0, Math.Min(255, rindex * 30), 150));
                            }
                        }
                    }
                }

                api.World.HighlightBlocks(player, 50, poses, colors);
            }
            return TextCommandResult.Success();
        }

        private TextCommandResult OnRoomRegDbgCmdUnhi(TextCommandCallingArgs args) {
            var player = args.Caller.Player as IServerPlayer;
            api.World.HighlightBlocks(player, 50, [], []);

            return TextCommandResult.Success();
        }

        private TextCommandResult OnRoomRegDbgCmdList(TextCommandCallingArgs args) {
            var player = args.Caller.Player as IServerPlayer;
            BlockPos pos = player.Entity.Pos.XYZ.AsBlockPos;
            long index3d = MapUtil.Index3dL(pos.X / chunksize, pos.Y / chunksize, pos.Z / chunksize, chunkMapSizeX, chunkMapSizeZ);
            ChunkRenRooms chunkrooms;
            lock (roomsByChunkIndexLock) {
                roomsByChunkIndex.TryGetValue(index3d, out chunkrooms);
            }

            if (chunkrooms == null || chunkrooms.Rooms.Count == 0) {
                return TextCommandResult.Success("No rooms here");
            }
            string response = chunkrooms.Rooms.Count + " Rooms here \n";

            lock (chunkrooms.roomsLock) {
                for (int i = 0; i < chunkrooms.Rooms.Count; i++) {
                    RenRoom room = chunkrooms.Rooms[i];
                    int sizex = room.Location.X2 - room.Location.X1 + 1;
                    int sizey = room.Location.Y2 - room.Location.Y1 + 1;
                    int sizez = room.Location.Z2 - room.Location.Z1 + 1;
                    response += string.Format("{0} - bbox dim: {1}/{2}/{3}, mid: {4}/{5}/{6}\n", i, sizex, sizey,
                        sizez, room.Location.X1 + sizex / 2f, room.Location.Y1 + sizey / 2f,
                        room.Location.Z1 + sizez / 2f);

                }
            }

            return TextCommandResult.Success(response);
        }

        private void Init() {
            chunkMapSizeX = api.World.BlockAccessor.MapSizeX / chunksize;
            chunkMapSizeZ = api.World.BlockAccessor.MapSizeZ / chunksize;
        }


        private void Event_ChunkDirty(Vec3i chunkCoord, IWorldChunk chunk, EnumChunkDirtyReason reason) {
            long index3d = MapUtil.Index3dL(chunkCoord.X, chunkCoord.Y, chunkCoord.Z, chunkMapSizeX, chunkMapSizeZ);
            Cuboidi cuboid;
            FastSetOfLongs set = [index3d];
            lock (roomsByChunkIndexLock) {
                roomsByChunkIndex.TryGetValue(index3d, out ChunkRenRooms chunkrooms);
                if (chunkrooms != null) {
                    set.Add(index3d);
                    for (int i = 0; i < chunkrooms.Rooms.Count; i++) {
                        cuboid = chunkrooms.Rooms[i].Location;
                        int x1 = cuboid.Start.X / chunksize;
                        int x2 = cuboid.End.X / chunksize;
                        int y1 = cuboid.Start.Y / chunksize;
                        int y2 = cuboid.End.Y / chunksize;
                        int z1 = cuboid.Start.Z / chunksize;
                        int z2 = cuboid.End.Z / chunksize;
                        set.Add(MapUtil.Index3dL(x1, y1, z1, chunkMapSizeX, chunkMapSizeZ));
                        if (z2 != z1) set.Add(MapUtil.Index3dL(x1, y1, z2, chunkMapSizeX, chunkMapSizeZ));
                        if (y2 != y1) {
                            set.Add(MapUtil.Index3dL(x1, y2, z1, chunkMapSizeX, chunkMapSizeZ));
                            if (z2 != z1) set.Add(MapUtil.Index3dL(x1, y2, z2, chunkMapSizeX, chunkMapSizeZ));
                        }
                        if (x2 != x1) {
                            set.Add(MapUtil.Index3dL(x2, y1, z1, chunkMapSizeX, chunkMapSizeZ));
                            if (z2 != z1) set.Add(MapUtil.Index3dL(x2, y1, z2, chunkMapSizeX, chunkMapSizeZ));
                            if (y2 != y1) {
                                set.Add(MapUtil.Index3dL(x2, y2, z1, chunkMapSizeX, chunkMapSizeZ));
                                if (z2 != z1) set.Add(MapUtil.Index3dL(x2, y2, z2, chunkMapSizeX, chunkMapSizeZ));
                            }
                        }
                    }
                }
                foreach (long index in set) roomsByChunkIndex.Remove(index);
            }
        }

        public RenRoom GetRoomForPosition(BlockPos pos) {
            long index3d = MapUtil.Index3dL(pos.X / chunksize, pos.Y / chunksize, pos.Z / chunksize, chunkMapSizeX, chunkMapSizeZ);

            ChunkRenRooms chunkrooms;
            RenRoom room;

            lock (roomsByChunkIndexLock) {
                roomsByChunkIndex.TryGetValue(index3d, out chunkrooms);
            }

            if (chunkrooms != null) {
                RenRoom firstEnclosedRoom = null;
                RenRoom firstOpenedRoom = null;

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

                room = FindRoomForPosition(pos, chunkrooms);
                chunkrooms.AddRoom(room);

                return room;
            }



            ChunkRenRooms rooms = new();
            room = FindRoomForPosition(pos, rooms);
            rooms.AddRoom(room);

            lock (roomsByChunkIndexLock) {
                roomsByChunkIndex[index3d] = rooms;
            }

            return room;
        }


        const int ARRAYSIZE = 29;  // [Original Dev note] Note if this constant is increased beyond 32, the bitshifts for compressedPos in the bfsQueue.Enqueue() and .Dequeue() calls may need updating
        readonly int[] currentVisited = new int[ARRAYSIZE * ARRAYSIZE * ARRAYSIZE];
        readonly int[] skyLightXZChecked = new int[ARRAYSIZE * ARRAYSIZE];
        const int MAXROOMSIZE = 14;
        const int MAXCELLARSIZE = 7;
        const int ALTMAXCELLARSIZE = 9;
        const int ALTMAXCELLARVOLUME = 150;
        int iteration = 0;


        private RenRoom FindRoomForPosition(BlockPos pos, ChunkRenRooms otherRooms) { // originally 'otherRooms' was unused, but it appears to exist for the purpose of quickly verifying if the position exists in the list of rooms.
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

            BlockAccess.Begin();

            bool allChunksLoaded = true;

            int minx = halfSize, miny = halfSize, minz = halfSize, maxx = halfSize, maxy = halfSize, maxz = halfSize;
            int posX = pos.X - halfSize;
            int posY = pos.Y - halfSize;
            int posZ = pos.Z - halfSize;
            BlockPos npos = new(posY);
            BlockPos bpos = new(posY);
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

                Block bBlock = BlockAccess.GetBlock(bpos);

                foreach (BlockFacing facing in BlockFacing.ALLFACES) {
                    facing.IterateThruFacingOffsets(npos);  // This must be the first command in the loop, to ensure all facings will be properly looped through regardless of any 'continue;' statements
                    int heatRetention = bBlock.GetRetention(bpos, facing, EnumRetentionType.Heat);

                    // We cannot exit current block, if the facing is heat retaining (e.g. chiselled block with solid side)
                    if (bBlock.Id != 0 && heatRetention != 0) {
                        if (heatRetention < 0) coolingWallCount -= heatRetention;
                        else nonCoolingWallCount += heatRetention;

                        continue;
                    }

                    if (!BlockAccess.IsValidPos(npos)) {
                        nonCoolingWallCount++;
                        continue;
                    }

                    Block nBlock = BlockAccess.GetBlock(npos);
                    allChunksLoaded &= BlockAccess.LastChunkLoaded;
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
                        int light = BlockAccess.GetLightLevel(npos, EnumLightLevelType.OnlySunLight);

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
