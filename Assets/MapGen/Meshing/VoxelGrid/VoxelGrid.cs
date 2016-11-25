﻿using Poly2Tri;
using System;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
public class VoxelGrid : MonoBehaviour
{
    private Mesh mesh;

    private List<Vector3> vertices;
    private List<Vector2> uvs;
    private List<int> triangles;

    public GameObject voxelPrefab;
    public int resolution;

    private Voxel[] voxels;
    private float voxelSize, gridSize;

    private Voxel dummyX, dummyY, dummyT;

    private Material[] voxelMaterials;

    public VoxelGrid xNeighbor, yNeighbor, xyNeighbor;

    public enum CornerType
    {
        Diamond,
        Square,
        Rounded
    }

    CornerType _cornerType = CornerType.Diamond;

    public CornerType cornerType
    {
        get
        {
            return _cornerType;
        }
        set
        {
            _cornerType = value;
            Refresh();
        }
    }

    bool _filledGaps = false;

    public bool filledGaps
    {
        get { return _filledGaps; }
        set
        {
            _filledGaps = value;
            Refresh();
        }
    }

    public void Initialize(int resolution, float size)
    {
        this.resolution = resolution;
        gridSize = size;
        voxelSize = size / resolution;
        voxels = new Voxel[resolution * resolution];
        voxelMaterials = new Material[voxels.Length];

        dummyX = new Voxel();
        dummyY = new Voxel();
        dummyT = new Voxel();

        for (int i = 0, y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++, i++)
            {
                CreateVoxel(i, x, y, x == 0 || x == resolution - 1 || y == 0 || y == resolution - 1);
            }
        }
        SetVoxelColors();

        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "VoxelGrid Mesh";
        vertices = new List<Vector3>();
        triangles = new List<int>();
        uvs = new List<Vector2>();
        Refresh();
    }

    private void CreateVoxel(int i, int x, int y, bool edge)
    {
        GameObject o = Instantiate(voxelPrefab) as GameObject;
        o.transform.parent = transform;
        o.transform.localPosition = new Vector3((x + 0.5f) * voxelSize, GameMap.tileHeight + 0.01f, (y + 0.5f) * -voxelSize);
        o.transform.localScale = Vector3.one * voxelSize * 0.1f;
        voxelMaterials[i] = o.GetComponent<MeshRenderer>().material;
        voxels[i] = new Voxel(x, y, voxelSize);
        voxels[i].edge = edge;
    }

    public void Apply(VoxelStencil stencil)
    {
        int xStart = Mathf.Max(stencil.XStart, 0);
        int xEnd = Mathf.Min(stencil.XEnd, resolution - 1);
        int yStart = Mathf.Max(stencil.YStart, 0);
        int yEnd = Mathf.Min(stencil.YEnd, resolution - 1);

        for (int y = yStart; y <= yEnd; y++)
        {
            int i = y * resolution + xStart;
            for (int x = xStart; x <= xEnd; x++, i++)
            {
                voxels[i].state = stencil.Apply(x, y, voxels[i].state);
            }
        }
        SetVoxelColors();
        Refresh();
    }

    private void SetVoxelColors()
    {
        for (int i = 0; i < voxels.Length; i++)
        {
            switch (voxels[i].state)
            {
                case Voxel.State.Empty:
                    voxelMaterials[i].color = Color.white;
                    break;
                case Voxel.State.Wall:
                    voxelMaterials[i].color = Color.blue;
                    break;
                case Voxel.State.Floor:
                    voxelMaterials[i].color = Color.yellow;
                    break;
                case Voxel.State.Intruded:
                    voxelMaterials[i].color = Color.red;
                    break;
                default:
                    voxelMaterials[i].color = Color.white;
                    break;
            }
        }
    }

    private void Refresh()
    {
        SetVoxelColors();
        Triangulate();
    }

    private void Triangulate()
    {
        vertices.Clear();
        triangles.Clear();
        mesh.Clear();
        uvs.Clear();

        wallPolygons.Clear();
        floorPolygons.Clear();

        if (xNeighbor != null)
        {
            dummyX.BecomeXDummyOf(xNeighbor.voxels[0], gridSize);
        }
        TriangulateCellRows();

        ConvertToMesh(wallPolygons.Polygons, GameMap.tileHeight);
        ConvertToMesh(floorPolygons.Polygons, GameMap.floorHeight);
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
    }

    private void TriangulateCellRows()
    {
        int cells = resolution - 1;
        for (int i = 0, y = 0; y < cells; y++, i++)
        {
            for (int x = 0; x < cells; x++, i++)
            {
                //if (x == 0)
                //    TriangulateLeftEdge(voxels[i], voxels[i + resolution]);
                //if (x == cells - 1)
                //    TriangulateRightEdge(voxels[i + 1], voxels[i + resolution + 1]);
                //if (y == 0)
                //    TriangulateTopEdge(voxels[i], voxels[i + 1]);
                //if (y == cells - 1)
                //    TriangulateBottomEdge(voxels[i + resolution], voxels[i + resolution + 1]);

                TriangulateCell(
                    voxels[i],
                    voxels[i + 1],
                    voxels[i + resolution],
                    voxels[i + resolution + 1]);
            }
        }
    }

    internal void Invert()
    {
        for (int i = 0; i < voxels.Length; i++)
        {
            switch (voxels[i].state)
            {
                case Voxel.State.Empty:
                    voxels[i].state = Voxel.State.Wall;
                    break;
                case Voxel.State.Wall:
                    voxels[i].state = Voxel.State.Empty;
                    break;
                case Voxel.State.Intruded:
                    break;
                default:
                    break;
            }
        }
        Refresh();
    }

    [Flags]
    enum Directions
    {
        None = 0,
        NorthWest = 1,
        NorthEast = 2,
        SouthWest = 4,
        SouthEast = 8,

        North = NorthWest | NorthEast,
        South = SouthWest | SouthEast,
        East = NorthEast | SouthEast,
        West = NorthWest | SouthWest,

        All = NorthWest | NorthEast | SouthWest | SouthEast
    }

    Directions RotateCW(Directions dir)
    {
        Directions output = Directions.None;
        if ((dir & Directions.NorthWest) == Directions.NorthWest)
            output |= Directions.NorthEast;
        if ((dir & Directions.NorthEast) == Directions.NorthEast)
            output |= Directions.SouthEast;
        if ((dir & Directions.SouthEast) == Directions.SouthEast)
            output |= Directions.SouthWest;
        if ((dir & Directions.SouthWest) == Directions.SouthWest)
            output |= Directions.NorthWest;
        return output;
    }

    Directions Rotate180(Directions dir)
    {
        return RotateCW(RotateCW(dir));
    }

    Directions RotateCCW(Directions dir)
    {
        return RotateCW(RotateCW(RotateCW(dir)));
    }


    private void TriangulateCell(Voxel northWest, Voxel northEast, Voxel southWest, Voxel southEast, Voxel.State state = Voxel.State.Wall)
    {
        var corner = _cornerType;
        bool intruded = false;
        if (northWest.state == Voxel.State.Intruded
            || northEast.state == Voxel.State.Intruded
            || southWest.state == Voxel.State.Intruded
            || southEast.state == Voxel.State.Intruded
            )
            intruded = true;
        if (intruded)
            corner = CornerType.Square;



        Directions walls = Directions.None;
        if (northWest.state == Voxel.State.Wall)
        {
            walls |= Directions.NorthWest;
        }
        if (northEast.state == Voxel.State.Wall)
        {
            walls |= Directions.NorthEast;
        }
        if (southWest.state == Voxel.State.Wall)
        {
            walls |= Directions.SouthWest;
        }
        if (southEast.state == Voxel.State.Wall)
        {
            walls |= Directions.SouthEast;
        }

        Directions wallFloors = walls;
        if (northWest.state == Voxel.State.Floor)
        {
            wallFloors |= Directions.NorthWest;
        }
        if (northEast.state == Voxel.State.Floor)
        {
            wallFloors |= Directions.NorthEast;
        }
        if (southWest.state == Voxel.State.Floor)
        {
            wallFloors |= Directions.SouthWest;
        }
        if (southEast.state == Voxel.State.Floor)
        {
            wallFloors |= Directions.SouthEast;
        }



        Directions edges = Directions.None;
        if (northWest.edge)
        {
            edges |= Directions.NorthWest;
        }
        if (northEast.edge)
        {
            edges |= Directions.NorthEast;
        }
        if (southWest.edge)
        {
            edges |= Directions.SouthWest;
        }
        if (southEast.edge)
        {
            edges |= Directions.SouthEast;
        }


        switch (wallFloors)
        {
            case Directions.None:
                return;
            case Directions.NorthWest:
            case Directions.North:
            case Directions.North | Directions.SouthWest:
            case Directions.NorthWest | Directions.SouthEast:
            case Directions.All:
                AddRotatedCell(
                    northWest, northWest.eastEdge,
                    northEast, northEast.southEdge,
                    southEast, southWest.eastEdge,
                    southWest, northWest.southEdge,
                    northWest.cornerPosition,
                    wallFloors, edges, walls, corner, intruded);
                break;
            case Directions.NorthEast:
            case Directions.East:
            case Directions.North | Directions.SouthEast:
            case Directions.NorthEast | Directions.SouthWest:
                AddRotatedCell(
                    northEast, northEast.southEdge,
                    southEast, southWest.eastEdge,
                    southWest, northWest.southEdge,
                    northWest, northWest.eastEdge,
                    northWest.cornerPosition,
                    RotateCCW(wallFloors), RotateCCW(edges), RotateCCW(walls), corner, intruded);
                break;
            case Directions.SouthEast:
            case Directions.South:
            case Directions.NorthEast | Directions.South:
                AddRotatedCell(
                    southEast, southWest.eastEdge,
                    southWest, northWest.southEdge,
                    northWest, northWest.eastEdge,
                    northEast, northEast.southEdge,
                    northWest.cornerPosition,
                    Rotate180(wallFloors), Rotate180(edges), Rotate180(walls), corner, intruded);
                break;
            case Directions.SouthWest:
            case Directions.West:
            case Directions.West | Directions.SouthEast:
                AddRotatedCell(
                    southWest, northWest.southEdge,
                    northWest, northWest.eastEdge,
                    northEast, northEast.southEdge,
                    southEast, southWest.eastEdge,
                    northWest.cornerPosition,
                    RotateCW(wallFloors), RotateCW(edges), RotateCW(walls), corner, intruded);
                break;
        }
    }

    private void AddRotatedCell(
        Voxel northWest,
        Vector3 north,
        Voxel northEast,
        Vector3 east,
        Voxel southEast,
        Vector3 south,
        Voxel southWest,
        Vector3 west,
        Vector3 center,
        Directions neighbors,
        Directions edges,
        Directions walls,
        CornerType corner,
        bool intruded)
    {
        switch (neighbors)
        {
            #region Outer Corner
            case Directions.NorthWest:
                if ((neighbors & edges) != neighbors)
                {
                    if(neighbors == walls)
                        AddCorner(wallPolygons, west, north, center, corner, WallType.Both);
                    else
                        AddCorner(floorPolygons, west, north, center, corner, WallType.Floor);

                }
                break;
            #endregion
            #region Straight Line
            case Directions.North:
                switch (edges)
                {
                    case Directions.North:
                    case Directions.North | Directions.SouthWest:
                    case Directions.North | Directions.SouthEast:
                        break;
                    case Directions.East:
                    case Directions.NorthEast | Directions.South:
                        if((walls & Directions.NorthWest) == Directions.NorthWest)
                            AddCorner(wallPolygons, west, north, center, CornerType.Square, WallType.Both);
                        else
                            AddCorner(floorPolygons, west, north, center, CornerType.Square, WallType.Floor);
                        break;
                    case Directions.West:
                    case Directions.West | Directions.SouthEast:
                        if((walls & Directions.NorthEast) == Directions.NorthEast)
                            AddCorner(wallPolygons, north, east, center, CornerType.Square, WallType.Both);
                        else
                            AddCorner(floorPolygons, north, east, center, CornerType.Square, WallType.Floor);
                        break;
                    default:
                        switch (walls)
                        {
                            case Directions.North:
                                AddStraight(wallPolygons, west, east, WallType.Both);
                                break;
                            case Directions.NorthWest:
                                AddStraight(floorPolygons, west, east, WallType.Floor);
                                AddCorner(floorPolygons, north, west, center, corner);
                                AddCorner(wallPolygons, west, north, center, corner, WallType.Wall);
                                break;
                            case Directions.NorthEast:
                                AddStraight(floorPolygons, west, east, WallType.Floor);
                                AddCorner(floorPolygons, east, north, center, corner);
                                AddCorner(wallPolygons, north, east, center, corner, WallType.Wall);
                                break;
                            default:
                                AddStraight(floorPolygons, west, east, WallType.Floor);
                                break;
                        }
                        break;
                }
                break;
            #endregion
            #region Inner Corner
            case Directions.North | Directions.West:
                switch (edges)
                {
                    case Directions.East:
                        {
                            Vector3 eastPoint = corner == CornerType.Diamond ? nudge(west, west, east) : east;
                            Vector3 southPoint = corner == CornerType.Diamond ? nudge(north, north, south) : south;
                            Vector3 northPoint = corner == CornerType.Diamond ? nudge(west, south, north) : north;
                            switch (walls)
                            {
                                case Directions.NorthWest:
                                    AddCorner(wallPolygons, west, north, center, corner);
                                    AddCorner(floorPolygons, northPoint, west, center, corner);
                                    AddStraight(floorPolygons, south, northPoint);
                                    break;
                                case Directions.SouthWest:
                                    AddCorner(wallPolygons, south, west, center, corner);
                                    AddCorner(floorPolygons, west, southPoint, center, corner);
                                    AddStraight(floorPolygons, southPoint, north);
                                    break;
                                case Directions.West:
                                case Directions.North | Directions.West:
                                    AddStraight(wallPolygons, south, north);
                                    break;

                                default:
                                    AddStraight(floorPolygons, south, north);
                                    break;
                            }
                        }
                        break;
                    case Directions.South:
                        AddStraight(wallPolygons, west, east);
                        break;
                    case Directions.North:
                        AddStraight(wallPolygons, east, west);
                        AddCorner(wallPolygons, south, east, center, corner);
                        break;
                    case Directions.West:
                        AddStraight(wallPolygons, north, south);
                        AddCorner(wallPolygons, south, east, center, corner);
                        break;
                    case Directions.North | Directions.SouthWest:
                        if (corner != CornerType.Square)
                        {
                            AddCorner(wallPolygons, south, east, center, corner);
                            AddCorner(wallPolygons, east, south, center, CornerType.Square);
                        }
                        break;
                    case Directions.North | Directions.SouthEast:
                        AddCorner(wallPolygons, south, west, center, CornerType.Square);
                        break;
                    case Directions.NorthEast | Directions.South:
                        AddCorner(wallPolygons, west, north, center, CornerType.Square);
                        break;
                    case Directions.NorthWest | Directions.South:
                        AddCorner(wallPolygons, north, east, center, CornerType.Square);
                        break;
                    default:
                        {
                            Vector3 eastPoint = corner == CornerType.Diamond ? nudge(west, west, east) : east;
                            Vector3 southPoint = corner == CornerType.Diamond ? nudge(north, north, south) : south;
                            switch (walls)
                            {
                                case Directions.NorthWest:
                                    AddCorner(wallPolygons, west, north, center, corner);
                                    AddCorner(floorPolygons, north, west, center, corner);
                                    AddCorner(floorPolygons, south, east, center, corner);
                                    break;
                                case Directions.NorthEast:
                                    AddCorner(wallPolygons, north, east, center, corner);
                                    AddCorner(floorPolygons, south, eastPoint, center, corner);
                                    AddCorner(floorPolygons, eastPoint, north, center, corner);
                                    break;
                                case Directions.SouthWest:
                                    AddCorner(wallPolygons, south, west, center, corner);
                                    AddCorner(floorPolygons, west, southPoint, center, corner);
                                    AddCorner(floorPolygons, southPoint, east, center, corner);
                                    break;
                                case Directions.North:
                                    AddStraight(wallPolygons, west, east);
                                    AddStraight(floorPolygons, eastPoint, west);
                                    AddCorner(floorPolygons, south, eastPoint, center, corner);
                                    break;
                                case Directions.West:
                                    AddStraight(wallPolygons, south, north);
                                    AddStraight(floorPolygons, north, southPoint);
                                    AddCorner(floorPolygons, southPoint, east, center, corner);
                                    break;
                                case Directions.NorthEast | Directions.SouthWest:
                                    AddCorner(wallPolygons, north, east, nudge(north, east, center), corner);
                                    AddCorner(wallPolygons, south, west, nudge(south, east, center), corner);
                                    AddCorner(floorPolygons, west, south, nudge(west, south, center), corner);
                                    AddCorner(floorPolygons, south, east, nudge(south, east, center), corner);
                                    AddCorner(floorPolygons, east, north, nudge(east, north, center), corner);
                                    break;
                                case Directions.North | Directions.West:
                                    AddCorner(wallPolygons, south, east, center, corner);
                                    break;
                                default:
                                    AddCorner(floorPolygons, south, east, center, corner);
                                    break;
                            }
                        }
                        break;
                }
                break;
            #endregion
            #region Saddle
            case Directions.NorthWest | Directions.SouthEast:
                {
                    var type = corner;
                    if (!_filledGaps)
                        type = CornerType.Diamond;
                    switch (edges)
                    {
                        case Directions.North:
                        case Directions.West:
                        case Directions.North | Directions.West:
                            switch (walls)
                            {
                                case Directions.NorthWest | Directions.SouthEast:
                                case Directions.SouthEast:
                                    AddCorner(wallPolygons, east, south, type == CornerType.Square ? nudge(east, south, center) : center, type, WallType.Both);
                                    break;
                                default:
                                    AddCorner(floorPolygons, east, south, type == CornerType.Square ? nudge(east, south, center) : center, type, WallType.Floor);
                                    break;
                            }
                            break;
                        case Directions.East:
                        case Directions.South:
                        case Directions.NorthEast | Directions.South:
                            switch (walls)
                            {
                                case Directions.NorthWest | Directions.SouthEast:
                                case Directions.NorthWest:
                                    AddCorner(wallPolygons, west, north, type == CornerType.Square ? nudge(west, north, center) : center, type, WallType.Both);
                                    break;
                                default:
                                    AddCorner(floorPolygons, west, north, type == CornerType.Square ? nudge(west, north, center) : center, type, WallType.Floor);
                                    break;
                            }
                            break;
                        default:
                                switch (walls)
                                {
                                    case Directions.NorthWest | Directions.SouthEast:
                                        AddCorner(wallPolygons, east, south, type == CornerType.Square ? nudge(east, south, center) : center, type, WallType.Both);
                                        AddCorner(wallPolygons, west, north, type == CornerType.Square ? nudge(west, north, center) : center, type, WallType.Both);
                                        break;
                                    case Directions.NorthWest:
                                        AddCorner(floorPolygons, east, south, type == CornerType.Square ? nudge(east, south, center) : center, type, WallType.Floor);
                                        AddCorner(wallPolygons, west, north, type == CornerType.Square ? nudge(west, north, center) : center, type, WallType.Both);
                                        break;
                                    case Directions.SouthEast:
                                        AddCorner(wallPolygons, east, south, type == CornerType.Square ? nudge(east, south, center) : center, type, WallType.Both);
                                        AddCorner(floorPolygons, west, north, type == CornerType.Square ? nudge(west, north, center) : center, type, WallType.Floor);
                                        break;
                                    default:
                                        AddCorner(floorPolygons, east, south, type == CornerType.Square ? nudge(east, south, center) : center, type, WallType.Floor);
                                        AddCorner(floorPolygons, west, north, type == CornerType.Square ? nudge(west, north, center) : center, type, WallType.Floor);
                                        break;
                                }
                            break;
                    }
                    break;
                }
            #endregion
            #region Center
            case Directions.All:
                switch (edges)
                {
                    case Directions.North | Directions.West:
                        AddCorner(wallPolygons, east, south, center, CornerType.Square);
                        break;
                    case Directions.North | Directions.East:
                        AddCorner(wallPolygons, south, west, center, CornerType.Square);
                        break;
                    case Directions.West | Directions.South:
                        AddCorner(wallPolygons, north, east, center, CornerType.Square);
                        break;
                    case Directions.East | Directions.South:
                        AddCorner(wallPolygons, west, north, center, CornerType.Square);
                        break;
                    case Directions.North:
                        AddStraight(wallPolygons, east, west);
                        break;
                    case Directions.West:
                        AddStraight(wallPolygons, north, south);
                        break;
                    case Directions.East:
                        AddStraight(wallPolygons, south, north);
                        break;
                    case Directions.South:
                        AddStraight(wallPolygons, west, east);
                        break;
                    default:
                        switch (walls)
                        {
                            case Directions.NorthWest:
                                AddCorner(wallPolygons, west, north, center, corner, WallType.Wall);
                                AddCorner(floorPolygons, north, west, center, corner);
                                break;
                            case Directions.NorthEast:
                                AddCorner(wallPolygons, north, east, center, corner, WallType.Wall);
                                AddCorner(floorPolygons, east, north, center, corner);
                                break;
                            case Directions.SouthEast:
                                AddCorner(wallPolygons, east, south, center, corner, WallType.Wall);
                                AddCorner(floorPolygons, south, east, center, corner);
                                break;
                            case Directions.SouthWest:
                                AddCorner(wallPolygons, south, west, center, corner, WallType.Wall);
                                AddCorner(floorPolygons, west, south, center, corner);
                                break;
                            case Directions.North:
                                AddStraight(wallPolygons, west, east, WallType.Wall);
                                AddStraight(floorPolygons, east, west, WallType.None);
                                break;
                            case Directions.East:
                                AddStraight(wallPolygons, north, south, WallType.Wall);
                                AddStraight(floorPolygons, south, north, WallType.None);
                                break;
                            case Directions.South:
                                AddStraight(wallPolygons, east, west, WallType.Wall);
                                AddStraight(floorPolygons, west, east, WallType.None);
                                break;
                            case Directions.West:
                                AddStraight(wallPolygons, south, north, WallType.Wall);
                                AddStraight(floorPolygons, north, south, WallType.None);
                                break;
                            default:
                                break;
                        }
                        break;
                }
                break;
            #endregion
            default:
                break;
        }
    }

    //The triangulator doesn't like points in the same place. This makes things just far enough apart that they work.
    Vector3 nudge(Vector3 a, Vector3 b, Vector3 center)
    {
        Vector3 average = (a + b) / 2;
        Vector3 direction = center - average;
        return center - (direction * 0.001f);
    }

    enum WallType
    {
        None,
        Floor,
        Wall,
        Both
    }

    private void AddCorner(ComplexPoly poly, Vector3 start, Vector3 end, Vector3 center, CornerType type, WallType wallType = WallType.None)
    {
        switch (type)
        {
            case CornerType.Diamond:
                break;
            case CornerType.Square:
                poly.AddLineSegment(start, center, end);
                AddWallMesh(wallType, start, center, end);
                return;
            case CornerType.Rounded:
                poly.AddLineSegment(
                    start,
                    (start + center) / 2,
                    (end + center) / 2,
                    end);
                AddWallMesh(wallType,
                    start,
                    (start + center) / 2,
                    (end + center) / 2,
                    end);
                return;
            default:
                break;
        }
        poly.AddLineSegment(start, end);
        AddWallMesh(wallType, start, end);
    }

    private void AddStraight(ComplexPoly poly, Vector3 a, Vector3 b, WallType wallType = WallType.None)
    {
        poly.AddLineSegment(a, b);
        AddWallMesh(wallType, a, b);
    }

    ComplexPoly wallPolygons = new ComplexPoly();
    ComplexPoly floorPolygons = new ComplexPoly();

    private void OnDrawGizmos()
    {
        wallPolygons.DrawGizmos(transform, Color.green, Color.blue, GameMap.tileHeight);
        floorPolygons.DrawGizmos(transform, Color.magenta, Color.yellow, GameMap.floorHeight);
    }


    void ConvertToMesh(PolygonSet polySet, float height)
    {
        Dictionary<TriangulationPoint, int> pointIndices = new Dictionary<TriangulationPoint, int>();
        P2T.Triangulate(polySet);
        foreach (var polygon in polySet.Polygons)
        {
            foreach (var triangle in polygon.Triangles)
            {
                for(int i = 2; i >= 0; i--)
                {
                    var point = triangle.Points[i];
                    int index;
                    if (!pointIndices.ContainsKey(point))
                    {
                        index = vertices.Count;
                        pointIndices[point] = index;
                        vertices.Add(new Vector3(point.Xf, height, point.Yf));
                        uvs.Add(new Vector2(point.Xf / GameMap.tileWidth, point.Yf / GameMap.tileWidth));
                    }
                    else
                        index = pointIndices[point];
                    triangles.Add(index);
                }
            }
        }
    }

    void AddWallMesh(WallType wallType, params Vector3[] points)
    {
        switch (wallType)
        {
            case WallType.Floor:
                AddWallMesh(GameMap.floorHeight, 0, points);
                break;
            case WallType.Wall:
                AddWallMesh(GameMap.tileHeight, GameMap.floorHeight, points);
                break;
            case WallType.Both:
                AddWallMesh(GameMap.tileHeight, 0, points);
                break;
            default:
                break;
        }
    }

    void AddWallMesh(float top, float bottom, params Vector3[] points)
    {
        float uvTop = top / GameMap.tileHeight;
        float uvBottom = bottom / GameMap.tileHeight;

        float length = 0;
        for(int i = 0; i < points.Length-1; i++)
        {
            length += (points[i] - points[i + 1]).magnitude;
        }

        int vertIndex = vertices.Count;
        float runningLength = 0;
        for (int i = 0; i < points.Length - 1; i++)
        {
            vertices.Add(new Vector3(points[i].x, top, points[i].z));
            vertices.Add(new Vector3(points[i].x, bottom, points[i].z));
            vertices.Add(new Vector3(points[i + 1].x, top, points[i + 1].z));
            vertices.Add(new Vector3(points[i + 1].x, bottom, points[i + 1].z));

            float thisLength = (points[i] - points[i + 1]).magnitude;

            uvs.Add(new Vector2(Mathf.Lerp(-0.5f, 0.5f, (runningLength / length)), uvTop));
            uvs.Add(new Vector2(Mathf.Lerp(-0.5f, 0.5f, (runningLength / length)), uvBottom));
            uvs.Add(new Vector2(Mathf.Lerp(-0.5f, 0.5f, ((runningLength + thisLength) / length)), uvTop));
            uvs.Add(new Vector2(Mathf.Lerp(-0.5f, 0.5f, ((runningLength + thisLength) / length)), uvBottom));

            runningLength += thisLength;

            triangles.Add(vertIndex + (i * 4) + 0);
            triangles.Add(vertIndex + (i * 4) + 2);
            triangles.Add(vertIndex + (i * 4) + 1);
            triangles.Add(vertIndex + (i * 4) + 1);
            triangles.Add(vertIndex + (i * 4) + 2);
            triangles.Add(vertIndex + (i * 4) + 3);
        }
    }
}