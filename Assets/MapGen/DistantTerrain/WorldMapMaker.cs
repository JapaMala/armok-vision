﻿using RemoteFortressReader;
using System.Collections.Generic;
using UnityEngine;
using UnityExtension;
using DFHack;
using System;

public class WorldMapMaker : MonoBehaviour
{
    public float scale = 0.01f;
    public int width;
    public int height;
    public string worldName;
    public string worldNameEnglish;
    RegionTile[,] regionTiles;
    bool[,] cumulusMedium;
    bool[,] cumulusMulti;
    bool[,] cumulusNimbus;
    bool[,] stratusAlto;
    bool[,] stratusProper;
    bool[,] stratusNimbus;
    bool[,] cirrus;
    bool[,] fogMist;
    bool[,] fogNormal;
    bool[,] fogThick;

    RegionMaps regionMaps;
    WorldMap worldMap;

    public CloudMaker cloudPrafab;

    CloudMaker cumulusMediumClouds;
    CloudMaker cumulusMultiClouds;
    Dictionary<int, CloudMaker> cumulusNimbusClouds;
    CloudMaker stratusAltoClouds;
    CloudMaker stratusProperClouds;
    Dictionary<int, CloudMaker> stratusNimbusClouds;
    CloudMaker cirrusClouds;

    Dictionary<DFCoord2d, RegionMaker> DetailRegions = new Dictionary<DFCoord2d, RegionMaker>();
    public RegionMaker regionPrefab;

    public MeshFilter terrainPrefab;
    List<MeshFilter> terrainChunks = new List<MeshFilter>();

    public MeshFilter waterPrefab;
    public MeshFilter regionWaterPrefab;
    List<MeshFilter> waterChunks = new List<MeshFilter>();

    public Material terrainMat;

    int CoordToIndex(int x, int y)
    {
        return x + y * width;
    }

    void CopyFromRemote(WorldMap remoteMap)
    {
        if (remoteMap == null)
        {
            Debug.Log("Didn't get world map!");
            return;
        }
        if (GameSettings.Instance.rendering.distantTerrainDetail == GameSettings.LandscapeDetail.Off)
            return;
        width = remoteMap.WorldWidth;
        height = remoteMap.WorldHeight;
        worldName = remoteMap.Name;
        worldNameEnglish = remoteMap.NameEnglish;
        transform.position = new Vector3(
        ((-remoteMap.CenterX * 48) - 0.5f) * GameMap.tileWidth,
        0,
        ((remoteMap.CenterY * 48) + 0.5f) * GameMap.tileWidth);

        terrainMat.SetFloat("_Scale", scale);
        terrainMat.SetFloat("_SeaLevel", (99 * GameMap.tileHeight) + transform.position.y);

        InitArrays();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int index = y * remoteMap.WorldWidth + x;
                regionTiles[x,y] = remoteMap.RegionTiles[index];
                if (GameSettings.Instance.rendering.drawClouds)
                {
                    cumulusMedium[x, y] = remoteMap.Clouds[index].Cumulus == CumulusType.CumulusMedium;
                    cumulusMulti[x, y] = remoteMap.Clouds[index].Cumulus == CumulusType.CumulusMulti;
                    cumulusNimbus[x, y] = remoteMap.Clouds[index].Cumulus == CumulusType.CumulusNimbus;
                    stratusAlto[x, y] = remoteMap.Clouds[index].Stratus == StratusType.StratusAlto;
                    stratusProper[x, y] = remoteMap.Clouds[index].Stratus == StratusType.StratusProper;
                    stratusNimbus[x, y] = remoteMap.Clouds[index].Stratus == StratusType.StratusNimbus;
                    cirrus[x, y] = remoteMap.Clouds[index].Cirrus;
                    fogMist[x, y] = remoteMap.Clouds[index].Fog == FogType.FogMist;
                    fogNormal[x, y] = remoteMap.Clouds[index].Fog == FogType.FogNone;
                    fogThick[x, y] = remoteMap.Clouds[index].Fog == FogType.F0GThick;
                }
            }
        if(ContentLoader.Instance != null)
            GenerateMesh();


        if (GameSettings.Instance.rendering.drawClouds)
            GenerateClouds();
        //Debug.Log("Loaded World: " + worldNameEnglish);
    }

    DFTime lastUpdateTime;
    private int chunkIndex;
    private int waterChunkIndex;

    void CopyClouds(WorldMap remoteMap)
    {
        if (remoteMap == null)
        {
            Debug.Log("Didn't get world map!");
            return;
        }
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int index = y * remoteMap.WorldWidth + x;
                cumulusMedium[x, y] = remoteMap.Clouds[index].Cumulus == CumulusType.CumulusMedium;
                cumulusMulti[x, y] = remoteMap.Clouds[index].Cumulus == CumulusType.CumulusMulti;
                cumulusNimbus[x, y] = remoteMap.Clouds[index].Cumulus == CumulusType.CumulusNimbus;
                stratusAlto[x, y] = remoteMap.Clouds[index].Stratus == StratusType.StratusAlto;
                stratusProper[x, y] = remoteMap.Clouds[index].Stratus == StratusType.StratusProper;
                stratusNimbus[x, y] = remoteMap.Clouds[index].Stratus == StratusType.StratusNimbus;
                cirrus[x, y] = remoteMap.Clouds[index].Cirrus;
                fogMist[x, y] = remoteMap.Clouds[index].Fog == FogType.FogMist;
                fogNormal[x, y] = remoteMap.Clouds[index].Fog == FogType.FogNormal;
                fogThick[x, y] = remoteMap.Clouds[index].Fog == FogType.F0GThick;
            }
        GenerateClouds();
    }

    void InitArrays()
    {
        if (regionTiles == null)
            regionTiles = new RegionTile[width, height];

        if (cumulusMedium == null)
            cumulusMedium = new bool[width, height];

        if (cumulusMulti == null)
            cumulusMulti = new bool[width, height];

        if (cumulusNimbus == null)
            cumulusNimbus = new bool[width, height];

        if (stratusAlto == null)
            stratusAlto = new bool[width, height];

        if (stratusProper == null)
            stratusProper = new bool[width, height];

        if (stratusNimbus == null)
            stratusNimbus = new bool[width, height];

        if (cirrus == null)
            cirrus = new bool[width, height];

        if (fogMist == null)
            fogMist = new bool[width, height];

        if (fogNormal == null)
            fogNormal = new bool[width, height];

        if (fogThick == null)
            fogThick = new bool[width, height];
    }

    // Does about what you'd think it does.
    void Start()
    {
        enabled = false;

        DFConnection.RegisterConnectionCallback(this.OnConnectToDF);
    }

    void OnConnectToDF()
    {
        enabled = true;
        regionMaps = DFConnection.Instance.PopRegionMapUpdate();
        worldMap = DFConnection.Instance.PopWorldMapUpdate();
        if (regionMaps != null && worldMap != null)
        {
            GenerateRegionMeshes();
        }
        if (worldMap != null)
        {
            CopyFromRemote(worldMap);
        }
    }

    void Update()
    {
        if (ContentLoader.Instance == null)
            return;
        regionMaps = DFConnection.Instance.PopRegionMapUpdate();
        worldMap = DFConnection.Instance.PopWorldMapUpdate();
        if (regionMaps != null && worldMap != null)
        {
            GenerateRegionMeshes();
            GenerateMesh();
        }
        if (worldMap != null)
        {
            if (DFConnection.Instance.HasWorldMapPositionChanged())
            {
                CopyFromRemote(worldMap);
            }
            else
            {
                if(GameSettings.Instance.rendering.drawClouds)
                    CopyClouds(worldMap);
            }
        }
    }

    void GenerateRegionMeshes()
    {
        if (GameSettings.Instance.rendering.distantTerrainDetail == GameSettings.LandscapeDetail.Off)
            return;

        foreach (RegionMap map in regionMaps.RegionMaps_)
        {
            DFCoord2d pos = new DFCoord2d(map.MapX, map.MapY);
            RegionMaker region;
            if (!DetailRegions.ContainsKey(pos))
            {
                region = Instantiate(regionPrefab);
                region.transform.parent = transform;
                region.transform.localPosition = RegionToUnityCoords(map.MapX, map.MapY, 0);
                DetailRegions[pos] = region;
            }
            else
                region = DetailRegions[pos];
            region.CopyFromRemote(map, worldMap);
            region.name = region.worldNameEnglish;
        }
    }

    Vector3 RegionToUnityCoords(int x, int y, int z)
    {
        return new Vector3(
            x * 48 * 16 * GameMap.tileWidth,
            z * GameMap.tileHeight,
            -y * 48 * 16 * GameMap.tileWidth
            );
    }

    void GenerateMesh()
    {
        if (width == 1 && height == 1)
            return;
        int length = width * height * 4;
        List<Vector3> vertices = new List<Vector3>(length);
        List<Color> colors = new List<Color>(length);
        List<Vector2> uvs = new List<Vector2>(length);
        List<Vector2> uv2s = new List<Vector2>(length);
        List<Vector2> uv3s = new List<Vector2>(length);
        List<int> triangles = new List<int>(length);

        List<Vector3> waterVerts = new List<Vector3>();
        List<Vector2> waterUvs = new List<Vector2>();
        List<int> waterTris = new List<int>();

        foreach(MeshFilter mf in terrainChunks)
        {
            if (mf.mesh != null)
                mf.mesh.Clear();
        }
        foreach(MeshFilter mf in waterChunks)
        {
            if (mf.mesh != null)
                mf.mesh.Clear();
        }

        chunkIndex = 0;
        waterChunkIndex = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (DetailRegions.ContainsKey(new DFCoord2d(x, y)))
                    continue;


                //If the vertex lists are already full, make a chunk with what we have, and keep going
                if (vertices.Count >= (65535 - 20))
                {
                    FinalizeGeometryChunk(vertices, colors, uvs, uv2s, uv3s, triangles);
                }

                //If the vertex lists are already full, make a chunk with what we have, and keep going
                if (waterVerts.Count >= (65535 - 20))
                {
                    FinalizeWaterGeometryChunk(waterVerts, waterUvs, waterTris);
                }


                MapDataStore.Tile fakeTile = new MapDataStore.Tile(null, new DFCoord(0, 0, 0));

                fakeTile.material = regionTiles[x, y].SurfaceMaterial;

                ColorContent colorContent;
                ContentLoader.Instance.ColorConfiguration.GetValue(fakeTile, MeshLayer.StaticMaterial, out colorContent);
                Color terrainColor = colorContent.color;

                Color plantColor = Color.black;
                float grassPercent = Mathf.Pow(regionTiles[x, y].Vegetation / 100.0f, 0.25F);
                float treePercent = Mathf.Pow(regionTiles[x, y].Vegetation / 100.0f, 0.5F);

                foreach (var item in regionTiles[x, y].PlantMaterials)
                {
                    fakeTile.material = item;
                    ContentLoader.Instance.ColorConfiguration.GetValue(fakeTile, MeshLayer.StaticMaterial, out colorContent);
                    plantColor += colorContent.color;
                }
                if (regionTiles[x, y].PlantMaterials.Count == 0)
                    grassPercent = 0;
                else
                    plantColor /= regionTiles[x, y].PlantMaterials.Count;

                Color treeColor = Color.black;
                int treeCount = 0;
                foreach (var tree in regionTiles[x,y].TreeMaterials)
                {
                    int plantIndex = tree.MatIndex;
                    if (tree.MatType != 419
                        || DFConnection.Instance.NetPlantRawList == null
                        || DFConnection.Instance.NetPlantRawList.PlantRaws.Count <= plantIndex)
                        continue;
                    var treeMat = tree;
                    foreach (var growth in DFConnection.Instance.NetPlantRawList.PlantRaws[plantIndex].Growths)
                    {
                        int currentTicks = TimeHolder.DisplayedTime.CurrentYearTicks;
                        if ((growth.TimingStart != -1 && growth.TimingStart > currentTicks) || (growth.TimingEnd != -1 && growth.TimingEnd < currentTicks))
                            continue;
                        treeMat = growth.Mat;
                        break;
                    }
                    fakeTile.material = treeMat;
                    if (ContentLoader.Instance.ColorConfiguration.GetValue(fakeTile, MeshLayer.StaticMaterial, out colorContent))
                    {
                        treeColor += colorContent.color;
                        treeCount++;
                    }
                }
                if (treeCount == 0)
                    treePercent = 0;
                else
                    treeColor /= treeCount;



                terrainColor = Color.Lerp(terrainColor, plantColor, grassPercent);
                terrainColor = Color.Lerp(terrainColor, treeColor, treePercent);

                Vector2 biome = new Vector2(regionTiles[x, y].Rainfall, 100 - regionTiles[x, y].Drainage) / 100;

                Vector3 vert1 = RegionToUnityCoords(x, y, regionTiles[x, y].Elevation);
                Vector3 vert2 = RegionToUnityCoords(x + 1, y + 1, regionTiles[x, y].Elevation);

                bool snow = regionTiles[x, y].Snow > 0;

                RegionMaker.AddHorizontalQuad(vert1, vert2, biome, terrainColor, vertices, colors, uvs, uv2s, uv3s, triangles, snow);

                if (regionTiles[x, y].Elevation < regionTiles[x, y].WaterElevation)
                {
                    vert1 = RegionToUnityCoords(x, y, regionTiles[x, y].WaterElevation);
                    vert2 = RegionToUnityCoords(x + 1, y + 1, regionTiles[x, y].WaterElevation);

                    RegionMaker.AddHorizontalQuad(vert1, vert2, biome, terrainColor, waterVerts, null, waterUvs, null, null, waterTris, false);
                }

                int north = 0;
                if (y > 0 && !DetailRegions.ContainsKey(new DFCoord2d(x, y - 1)))
                    north = regionTiles[x, y - 1].Elevation;
                if (north < regionTiles[x, y].Elevation)
                {
                    vert1 = (new Vector3(x * 48 * 16 * GameMap.tileWidth, regionTiles[x, y].Elevation * GameMap.tileHeight, -y * 48 * 16 * GameMap.tileWidth)) * scale;
                    vert2 = (new Vector3((x + 1) * 48 * 16 * GameMap.tileWidth, north * GameMap.tileHeight, -y * 48 * 16 * GameMap.tileWidth)) * scale;

                    RegionMaker.AddVerticalQuad(vert1, vert2, biome, terrainColor, vertices, colors, uvs, uv2s, uv3s, triangles, snow);
                }

                int south = 0;
                if (y < height - 1 && !DetailRegions.ContainsKey(new DFCoord2d(x, y + 1)))
                    south = regionTiles[x, y + 1].Elevation;
                if (south < regionTiles[x, y].Elevation)
                {
                    vert1 = (new Vector3((x + 1) * 48 * 16 * GameMap.tileWidth, regionTiles[x, y].Elevation * GameMap.tileHeight, -(y + 1) * 48 * 16 * GameMap.tileWidth)) * scale;
                    vert2 = (new Vector3(x * 48 * 16 * GameMap.tileWidth, south * GameMap.tileHeight, -(y + 1) * 48 * 16 * GameMap.tileWidth)) * scale;

                    RegionMaker.AddVerticalQuad(vert1, vert2, biome, terrainColor, vertices, colors, uvs, uv2s, uv3s, triangles, snow);
                }

                int east = 0;
                if (x < width - 1 && !DetailRegions.ContainsKey(new DFCoord2d(x + 1, y)))
                    east = regionTiles[x + 1, y].Elevation;
                if (east < regionTiles[x, y].Elevation)
                {
                    vert1 = (new Vector3((x + 1) * 48 * 16 * GameMap.tileWidth, regionTiles[x, y].Elevation * GameMap.tileHeight, -y * 48 * 16 * GameMap.tileWidth)) * scale;
                    vert2 = (new Vector3((x + 1) * 48 * 16 * GameMap.tileWidth, east * GameMap.tileHeight, -(y + 1) * 48 * 16 * GameMap.tileWidth)) * scale;

                    RegionMaker.AddVerticalQuad(vert1, vert2, biome, terrainColor, vertices, colors, uvs, uv2s, uv3s, triangles, snow);
                }
                int west = 0;
                if (x > 0 && !DetailRegions.ContainsKey(new DFCoord2d(x - 1, y)))
                    west = regionTiles[x - 1, y].Elevation;
                if (west < regionTiles[x, y].Elevation)
                {
                    vert1 = (new Vector3(x * 48 * 16 * GameMap.tileWidth, regionTiles[x, y].Elevation * GameMap.tileHeight, -(y + 1) * 48 * 16 * GameMap.tileWidth)) * scale;
                    vert2 = (new Vector3(x * 48 * 16 * GameMap.tileWidth, west * GameMap.tileHeight, -y * 48 * 16 * GameMap.tileWidth)) * scale;

                    RegionMaker.AddVerticalQuad(vert1, vert2, biome, terrainColor, vertices, colors, uvs, uv2s, uv3s, triangles, snow);
                }
            }
        }

        FinalizeGeometryChunk(vertices, colors, uvs, uv2s, uv3s, triangles);

        FinalizeWaterGeometryChunk(waterVerts, waterUvs, waterTris);
    }

    private void FinalizeWaterGeometryChunk(List<Vector3> waterVerts, List<Vector2> waterUvs, List<int> waterTris)
    {
        if (waterChunks.Count <= waterChunkIndex)
        {
            waterChunks.Add(Instantiate(waterPrefab));
            waterChunks[waterChunkIndex].transform.parent = transform;
            waterChunks[waterChunkIndex].gameObject.name = "WaterChunk" + waterChunkIndex;
            waterChunks[waterChunkIndex].transform.localPosition = Vector3.zero;
        }
        MeshFilter mf = waterChunks[waterChunkIndex];

        if (mf.mesh == null)
            mf.mesh = new Mesh();

        Mesh waterMesh = mf.mesh;

        waterMesh.vertices = waterVerts.ToArray();
        waterMesh.uv = waterUvs.ToArray();
        waterMesh.triangles = waterTris.ToArray();

        waterMesh.RecalculateNormals();
        waterMesh.RecalculateTangents();

        mf.mesh = waterMesh;

        waterVerts.Clear();
        waterUvs.Clear();
        waterTris.Clear();
        waterChunkIndex++;
    }

    private void FinalizeGeometryChunk(List<Vector3> vertices, List<Color> colors, List<Vector2> uvs, List<Vector2> uv2s, List<Vector2> uv3s, List<int> triangles)
    {
        if (terrainChunks.Count <= chunkIndex)
        {
            terrainChunks.Add(Instantiate(terrainPrefab));
            terrainChunks[chunkIndex].transform.parent = transform;
            terrainChunks[chunkIndex].gameObject.name = "TerrainChunk" + chunkIndex;
            terrainChunks[chunkIndex].transform.localPosition = Vector3.zero;
        }
        MeshFilter mf = terrainChunks[chunkIndex];

        if (mf.mesh == null)
            mf.mesh = new Mesh();

        Mesh terrainMesh = mf.mesh;

        terrainMesh.vertices = vertices.ToArray();
        terrainMesh.colors = colors.ToArray();
        terrainMesh.uv = uvs.ToArray();
        terrainMesh.uv2 = uv2s.ToArray();
        terrainMesh.uv3 = uv3s.ToArray();
        terrainMesh.triangles = triangles.ToArray();

        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateTangents();

        mf.mesh = terrainMesh;

        vertices.Clear();
        colors.Clear();
        uvs.Clear();
        uv2s.Clear();
        uv3s.Clear();
        triangles.Clear();
        chunkIndex++;
    }

    void GenerateClouds()
    {
        cumulusMediumClouds = MakeCloud(cumulusMediumClouds, 1250, cumulusMedium, "cumulusMedium");
        cumulusMultiClouds = MakeCloud(cumulusMultiClouds, 5000, cumulusMulti, "cumulusMulti");
        if (cumulusNimbusClouds == null) cumulusNimbusClouds = new Dictionary<int, CloudMaker>();
        for (int i = 1875; i <= 6250; i += 300)
            cumulusNimbusClouds[i] = MakeCloud(cumulusNimbusClouds.ContainsKey(i) ? cumulusNimbusClouds[i] : null, i, cumulusNimbus, "cumulusNimbus");
        stratusAltoClouds = MakeCloud(stratusAltoClouds, 6250, stratusAlto, "stratusAlto");
        stratusProperClouds = MakeCloud(stratusProperClouds, 1875, stratusProper, "stratusProper");
        if (stratusNimbusClouds == null) stratusNimbusClouds = new Dictionary<int, CloudMaker>();
        for (int i = 625; i <= 1875; i += 300)
            stratusNimbusClouds[i] = MakeCloud(stratusNimbusClouds.ContainsKey(i) ? stratusNimbusClouds[i] : null, i, stratusNimbus, "stratusNimbus");
        cirrusClouds = MakeCloud(cirrusClouds, 6250, cirrus, "cirrus");
    }

    CloudMaker MakeCloud(CloudMaker original, float height, bool[,] cloudMap, string name)
    {
        if (original == null)
        {
            original = Instantiate(cloudPrafab);
            original.scale = scale;
            original.GenerateMesh(cloudMap);
            original.name = name;
            original.transform.parent = transform;
            original.transform.localPosition = new Vector3(0, height * GameMap.tileHeight * scale);
        }
        else
            original.UpdateClouds(cloudMap);
        return original;
    }
}
