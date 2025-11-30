using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;
using static OpenTK.Graphics.OpenGL.GL;

#nullable disable

// This is a 'copy' of the vanilla RoomRegistry.cs file. I am intending to expand upon the functionality of it by inheriting the original functions, and patching every reference in the vanilla code to use these instead.
namespace hazroomrenovation.source.Code {
    /// <summary> RenRoom (Renovated Room) - inherits the Room class from vanilla and allows for more data to be checked and more behaviors/effects to be provided. 
    /// </summary>
    public class RenRoom : Room {

        #region Vanilla variables
        ///// <summary> The number of times the search traversed outside the maxSize bounding box. </summary>
        //public new int ExitCount;
        ///// <summary> The number of ciling blocks that are considered transparent enough to let sunlight through while still being insulated. </summary>
        //public new int SkylightCount;
        ///// <summary> The number of ciling blocks that do not let enough sunlight through to be considered a skylight. </summary>
        //public new int NonSkylightCount;
        ///// <summary> The number of wall blocks that are considered temperature retaining/insulating inside the room. </summary>
        //public new int CoolingWallCount;
        ///// <summary> the number of wall blocks that are not considered temperature retaining/insulating inside the room. </summary>
        //public new int NonCoolingWallCount;

        ///// <summary> If true, indicates room dimensions do not exceed recommended cellar dimensions of 7x7x7  (soft limit: slightly longer shapes with low overall volume also permitted) </summary>
        //public new bool IsSmallRoom;

        ///// <summary> A bounding box of the found room volume, but that doesn't mean this volumne is 100% room. You can check if a block inside inside is volume is part of the room with the PosInRoom byte array </summary>
        //public new Cuboidi Location;
        //public new byte[] PosInRoom;

        ///// <summary> If greater than 0, a chunk is unloaded. Counts upwards and when it reaches a certain value, this room will be removed from the registry and re-checked: this allows valid fully loaded rooms to be detected quite quickly in the normal world loading process        /// The potential issue is a room with a container, on the very edge of the server's loaded world, with neighbouring chunks remaining unloaded for potentially a long time. This will never be loaded, so we don't want to recheck its status fully too often: not every tick, that would be too costly </summary>
        //public new int AnyChunkUnloaded;
        #endregion

        #region modded numerical values
        /// <summary> number of blocks making up the walls/floor/ceiling that can be considered solid or insulating. </summary>
        public int EnclosingBlockCount;
        /// <summary> number of blocks making up the walls/floor/ceiling can NOT be considered solid or insulating. </summary>
        public int ExposingBlockCount;
        /// <summary> number of blocks making up the walls/floor/ceiling that are NOT solid/insulating, but aren't exposed to an 'exit' and thus do not count towards the exposing block count. </summary>
        public int VentilatedBlockCount;
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

        //TODO: need to figure out a good way of flexibly looking for specific blocktypes/entities for room requirements, such as beds, anvils, troughs, etc. Perhaps roomtype can be a JSON with its own definitions for room limits.
        //public int ForestCoverage; //a numerical value representing how much forest coverage is in the climate around the room. (Not sure if this is useful at the moment)
        //public int ShrubCoverage; //a numerical value representing how much shrubbery is in the climate around the room.
        #endregion

        #region Modded Roomtype checks (Temp, will be refactored when room behaviors are figured out.)
        //bools - Note: The vanilla room's 'IsSmallRoom' bool value is the RenRoom 'IsCellar' check
        public bool IsEnclosedRenRoom;
        public bool IsGreenHouseRenRoom;        
        //calculated values dependant on bools
        public float SkyLightProportion;
        public float CellarProportion;
        public float tempSourceModifier;
        //room effect values
        public int Roomness; // Note, will need to change the lang settings to accomodate a variable temp bonus. vanilla has '+5' written into each language file.
        #endregion

        #region room information strings
        /// <summary> The specific kind of room that is identified by the room check. 
        /// </summary>
        public string RoomType;
        //public string RoomName; //TODO find a way to retain some data for room locations. perhaps saving the position of a single door and considering it the 'main entrance' to a room per player's interaction.
        #endregion

        /// <summary> boolean check to verify if the room in question is fully loaded or if any number of chunks it is built on are currently unloaded. </summary>
        /// <param name="roomsList"></param>
        //public bool IsFullyLoaded(ChunkRenRooms roomsList) {
        //    if (AnyChunkUnloaded == 0) return true;

        //    if (++AnyChunkUnloaded > 10) {
        //        roomsList.RemoveRoom(this);
        //    }
        //    return false;
        //}

        /// <summary> A boolean check to verify if the given position is located within the room. </summary>
        /// <param name="pos"></param>
        //public bool Contains(BlockPos pos) {
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
}
