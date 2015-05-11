﻿using UnityEngine;
using System.Collections;
using DFHack;
using System.Collections.Generic;
using RemoteFortressReader;

// A store for a section of map tiles.
// One large instance is used as main storage for the game map;
// Smaller slices are used temporary storage for meshing.

// Read tiles with store[DFCoord] or store[int, int, int], using world coordinates. You'll get a Tile?.
// Modify tiles like so: store.InitOrModifyTile(DFCoord, tiletype: 4, water_level: 2), and so on.
// (It's deliberately impossible to wholly overwrite tiles, so that we can't accidentally set tiles
// to have incorrect positions or containers.)

public class MapDataStore {

    // Static stuff

    // The "main" map, used in the main Unity thread.
    static MapDataStore _main;
    public static MapDataStore Main {
        get {
            // TODO throw exception if accessed from non-main-thread.
            return _main;
        }
        private set {
            if (_main != null && value != null) throw new UnityException("Main Map already initialized!");
            _main = value;
        }
    }
    // Called from DFConnection.InitStatics
    public static void InitMainMap(int xSize, int ySize, int zSize) {
        MapSize = new DFCoord(xSize, ySize, zSize);
        _main = new MapDataStore(new DFCoord(0,0,0), MapSize);
    }

    // The size of the whole map.
    public static DFCoord MapSize { get; private set; }
    public static bool InMapBounds (DFCoord coord) {
        return 0 <= coord.x && coord.x < MapSize.x &&
                0 <= coord.y && coord.y < MapSize.y &&
                0 <= coord.z && coord.z < MapSize.z;
    }

    // Used for dynamic material loading and stuff.
    public static List<Tiletype> tiletypeTokenList { private get; set; }
    // Used to index into water/magma arrays in a few places.
    public const int l_water = 0;
    public const int l_magma = 1;



    // Instance stuff

    // The size of this slice of the map
    public readonly DFCoord SliceSize;
    // The origin of this slice of the map; subtracted from indices when indexed
    // (So that code using slices of the map can use the same coordinates as when
    // accessing the main map)
    public readonly DFCoord SliceOrigin;
    // The data
    Tile[,,] tiles;
    // An array for whether a tile is present or not; index with PresentIndex
    // (We use this since it's cheaper than storing an array of Tile?s)
    BitArray tilesPresent;

    private MapDataStore() {}

    public MapDataStore(DFCoord origin, DFCoord sliceSize) {
        if (sliceSize.x < 0 || sliceSize.x > MapSize.x || 
            sliceSize.y < 0 || sliceSize.y > MapSize.y || 
            sliceSize.z < 0 || sliceSize.z > MapSize.z) {
            throw new UnityException("Can't have a map slice outside the map!");
        }
        SliceSize = sliceSize;
        tiles = new Tile[SliceSize.x, SliceSize.y, SliceSize.z];
        tilesPresent = new BitArray(PresentIndex(SliceSize.x-1, SliceSize.y-1, SliceSize.z-1)+1);
    }

    // Things to read and modify the map
    // Note: everything takes coordinates in world / DF space
    public Tile? this[int x, int y, int z] {
        get {
            return this[new DFCoord(x,y,z)];
        }
    }

    public Tile? this[DFCoord coord] {
        get {
            DFCoord local = WorldToLocalSpace(coord);
            if (InSliceBoundsLocal(local.x, local.y, local.z) && tilesPresent[PresentIndex(local.x, local.y, local.z)]) {
                return tiles[local.x, local.y, local.z];
            } else {
                return null;
            }
        }
    }

    public int GetLiquidLevel(DFCoord coord, int liquidIndex) {
        var tile = this[coord];
        if (tile == null) return 0;
        switch (liquidIndex) {
        case l_water:
            return tile.Value.waterLevel;
        case l_magma:
            return tile.Value.magmaLevel;
        default:
            throw new UnityException("No liquid with index "+liquidIndex);
        }
    }

    public void SetLiquidLevel(DFCoord coord, int liquidIndex, int liquidLevel) {
        switch (liquidIndex) {
        case l_water:
            InitOrModifyTile(coord, waterLevel: liquidLevel);
            return;
        case l_magma:
            InitOrModifyTile(coord, magmaLevel: liquidLevel);
            return;
        default:
            throw new UnityException("No liquid with index "+liquidIndex);
        }
    }

    public void InitOrModifyTile(DFCoord coord,
                           int? tileType = null,
                           MatPairStruct? material = null,
                           MatPairStruct? base_material = null,
                           MatPairStruct? layer_material = null,
                           MatPairStruct? vein_material = null,
                           int? waterLevel = null,
                           int? magmaLevel = null)
    {
        DFCoord local = WorldToLocalSpace(coord);
        if (!InSliceBoundsLocal(local.x, local.y, local.z)) {
            throw new UnityException("Can't modify tile outside of slice");
        }
        int presentIndex = PresentIndex (local.x, local.y, local.z);
        if (!tilesPresent[presentIndex]) {
            tilesPresent[presentIndex] = true;
            tiles[local.x, local.y, local.z] = new Tile(this, coord);
        }
        tiles[local.x, local.y, local.z].Modify(tileType, material, base_material, layer_material, vein_material, waterLevel, magmaLevel);
    }

    public void Clear() {
        tilesPresent.SetAll(false);
        for (int x = 0; x < SliceSize.x; x++) {
            for (int y = 0; y < SliceSize.y; y++) {
                for (int z = 0; z < SliceSize.z; z++) {
                    tiles[x,y,z] = new Tile(this, LocalToWorldSpace(new DFCoord(x,y,z)));
                }
            }
        }
    }

    // Helpers
    public DFCoord WorldToLocalSpace(DFCoord coord) {
        return coord - SliceOrigin;
    }
    public DFCoord LocalToWorldSpace(DFCoord coord) {
        return coord + SliceOrigin;
    }
    // These take local space coordinates:
    bool InSliceBoundsLocal(int x, int y, int z) {
        return 0 <= x && x < SliceSize.x &&
                0 <= y && y < SliceSize.y &&
                0 <= z && z < SliceSize.z;
    }
    int PresentIndex(int x, int y, int z) {
        return x * SliceSize.y * SliceSize.z + y * SliceSize.z + z;
    }

    // The data for a single tile of the map.
    // Nested struct because it depends heavily on its container.
    public struct Tile
    {
        public Tile(MapDataStore container, DFCoord position) {
            this.container = container;
            this.position = position;
            tileType = default(int);
            material = default(MatPairStruct);
            base_material = default(MatPairStruct);
            layer_material = default(MatPairStruct);
            vein_material = default(MatPairStruct);
            waterLevel = default(int);
            magmaLevel = default(int);
        }

        public MapDataStore container;
        public DFCoord position;
        public int tileType;
        public MatPairStruct material;
        public MatPairStruct base_material;
        public MatPairStruct layer_material;
        public MatPairStruct vein_material;
        public int waterLevel;
        public int magmaLevel;

        public TiletypeShape shape { get { return tiletypeTokenList [tileType].shape; } }
        public TiletypeMaterial tiletypeMaterial { get { return tiletypeTokenList [tileType].material; } }
        public TiletypeSpecial special { get { return tiletypeTokenList [tileType].special; } }
        public TiletypeVariant variant { get { return tiletypeTokenList [tileType].variant; } }
        public string direction { get { return tiletypeTokenList [tileType].direction; } }

        public void Modify (int? tileType = null,
                           MatPairStruct? material = null,
                           MatPairStruct? base_material = null,
                           MatPairStruct? layer_material = null,
                           MatPairStruct? vein_material = null,
                           int? waterLevel = null,
                           int? magmaLevel = null)
        {
            if (tileType != null) {
                this.tileType = tileType.Value;
            }
            if (material != null) {
                this.material = material.Value;
            }
            if (base_material != null) {
                this.base_material = base_material.Value;
            }
            if (layer_material != null) {
                this.layer_material = layer_material.Value;
            }
            if (vein_material != null) {
                this.vein_material = vein_material.Value;
            }
            if (waterLevel != null) {
                this.waterLevel = waterLevel.Value;
            }
            if (magmaLevel != null) {
                this.magmaLevel = magmaLevel.Value;
            }
        }
        public bool isWall {
            get {
                switch (shape) {
                case TiletypeShape.WALL:
                case TiletypeShape.FORTIFICATION:
                case TiletypeShape.BROOK_BED:
                case TiletypeShape.TREE_SHAPE:
                    return true;
                default:
                    return false;
                }
            }
        }
        public bool isFloor {
            get {
                switch (shape) {
                case TiletypeShape.RAMP:
                case TiletypeShape.FLOOR:
                case TiletypeShape.BOULDER:
                case TiletypeShape.PEBBLES:
                case TiletypeShape.BROOK_TOP:
                case TiletypeShape.SAPLING:
                case TiletypeShape.SHRUB:
                case TiletypeShape.BRANCH:
                case TiletypeShape.TRUNK_BRANCH:
                    return true;
                default:
                    return false;
                }
            }
        }
        public MapDataStore.Tile? north {
            get {
                return container[position.x, position.y - 1, position.z];
            }
        }
        public MapDataStore.Tile? south {
            get {
                return container[position.x, position.y + 1, position.z];
            }
        }
        public MapDataStore.Tile? east {
            get {
                return container[position.x + 1, position.y, position.z];
            }
        }
        public MapDataStore.Tile? west {
            get {
                return container[position.x - 1, position.y, position.z];
            }
        }
        public MapDataStore.Tile? up {
            get {
                return container[position.x, position.y, position.z + 1];
            }
        }
        public MapDataStore.Tile? down {
            get {
                return container[position.x, position.y, position.z - 1];
            }
        }
    }
}
