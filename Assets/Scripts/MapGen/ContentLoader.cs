﻿using hqx;
using MaterialStore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.UI;

public enum MatBasic
{
    INVALID = -1,
    INORGANIC = 0,
    AMBER = 1,
    CORAL = 2,
    GREEN_GLASS = 3,
    CLEAR_GLASS = 4,
    CRYSTAL_GLASS = 5,
    ICE = 6,
    COAL = 7,
    POTASH = 8,
    ASH = 9,
    PEARLASH = 10,
    LYE = 11,
    MUD = 12,
    VOMIT = 13,
    SALT = 14,
    FILTH = 15,
    FILTH_FROZEN = 16,
    UNKOWN_FROZEN = 17,
    GRIME = 18,
    ICHOR = 20,
    LEATHER = 37,
    BLOOD_1 = 39,
    BLOOD_2 = 40,
    BLOOD_3 = 41,
    BLOOD_4 = 42,
    BLOOD_5 = 43,
    BLOOD_6 = 44,
    BLOOD_NAMED = 242,
    PLANT = 419,
    WOOD = 420,
    PLANTCLOTH = 421,

    // filthy hacks to get interface stuff
    DESIGNATION = -2,
    CONSTRUCTION = -3,

}

public class ContentLoader : MonoBehaviour
{
    [SerializeField]
    private ProgressBar mainProgressBar;
    [SerializeField]
    private ProgressBar subProgressBar;

    public void Start()
    {
        DFConnection.RegisterConnectionCallback(Initialize);
    }

    public static ContentLoader Instance { get; private set; }

    public static MatBasic lookupMaterialType(string value)
    {
        if (value == null)
            return MatBasic.INVALID;
        switch (value)
        {
            case "Stone":
                return MatBasic.INORGANIC;
            case "Metal":
                return MatBasic.INORGANIC;
            case "Inorganic":
                return MatBasic.INORGANIC;
            case "GreenGlass":
                return MatBasic.GREEN_GLASS;
            case "Wood":
                return MatBasic.WOOD;
            case "Plant":
                return MatBasic.PLANT;
            case "Ice":
                return MatBasic.ICE;
            case "ClearGlass":
                return MatBasic.CLEAR_GLASS;
            case "CrystalGlass":
                return MatBasic.CRYSTAL_GLASS;
            case "PlantCloth":
                return MatBasic.PLANTCLOTH;
            case "Leather":
                return MatBasic.LEATHER;
            case "Vomit":
                return MatBasic.VOMIT;
            case "Designation":
                return MatBasic.DESIGNATION;
            case "Construction":
                return MatBasic.CONSTRUCTION;
            default:
                return MatBasic.INVALID;
        }
    }

    internal static Coroutine StartStaticCoroutine(IEnumerator enumerator)
    {
        return _routiner.StartCoroutine(enumerator);
    }

    static List<Func<IEnumerator>> LoadCallbacks = new List<Func<IEnumerator>>();

    internal static void RegisterLoadCallback(Func<IEnumerator> coroutine)
    {
        LoadCallbacks.Add(coroutine);
    }

    public Texture2DArray PatternTextureArray { get; private set; }
    public float PatternTextureDepth { get; private set; }

    public TextureStorage shapeTextureStorage { get; private set; }
    public TextureStorage specialTextureStorage { get; private set; }

    /// <summary>
    /// Get the color associated with a material, falling back to DF state color.
    /// </summary>
    /// <param name="mat"></param>
    /// <returns></returns>
    public static Color GetColor(MatPairStruct mat)
    {
        MaterialTextureSet textureSet;
        if (MaterialCollection.Instance.TryGetValue(mat, out textureSet))
        {
            if(!textureSet.useDFColor)
                return textureSet.color;
        }
        if (!MaterialRaws.Instance.ContainsKey(mat))
            return new Color32(128, 128, 128, 128);
        var stateColor = MaterialRaws.Instance[mat].state_color;
        if (stateColor == null)
            return new Color32(128, 128, 128, 128);
        return new Color32((byte)stateColor.red, (byte)stateColor.green, (byte)stateColor.blue, 128);
    }

    public enum GrowthPeriod
    {
        Fresh,
        Current,
        Stale
    }

    public static Color GetGrowthColor(GrowthPeriod period, int growthID, MatPairStruct mat)
    {
        if (period == GrowthPeriod.Fresh)
            return GetColor(mat);
        if (mat.mat_index >= DFConnection.Instance.NetPlantRawList.plant_raws.Count)
            return GetColor(mat);
        if (growthID >= DFConnection.Instance.NetPlantRawList.plant_raws[mat.mat_index].growths.Count)
            return GetColor(mat);

        var growth = DFConnection.Instance.NetPlantRawList.plant_raws[mat.mat_index].growths[growthID];
        int currentTicks = TimeHolder.DisplayedTime.CurrentYearTicks;

        RemoteFortressReader.GrowthPrint freshPrints = null;
        RemoteFortressReader.GrowthPrint currentPrint = null;
        RemoteFortressReader.GrowthPrint stalePrint = null;

        int minTicks = int.MaxValue;
        int maxTicks = int.MinValue;

        foreach (var print in growth.prints)
        {
            if ((print.timing_start == -1 || print.timing_start <= currentTicks) && (print.timing_end == -1 || print.timing_end >= currentTicks))
                currentPrint = print;
            if(print.timing_start == -1 || print.timing_start < minTicks)
            {
                minTicks = print.timing_start;
                freshPrints = print;
            }
            if (print.timing_end == -1 || print.timing_end > maxTicks)
            {
                maxTicks = print.timing_end;
                stalePrint = print;
            }
        }
        if (currentPrint == null)
            currentPrint = stalePrint;
        switch (period)
        {
            case GrowthPeriod.Current:
                return DfColor.MorphColor(GetColor(mat), freshPrints.color, currentPrint.color);
            case GrowthPeriod.Stale:
                return DfColor.MorphColor(GetColor(mat), freshPrints.color, stalePrint.color);
        }
        return GetColor(mat);
    }

    /// <summary>
    /// Gets a color for a given item type and material combination, including any seasonal variations.
    /// </summary>
    /// <param name="item">Item type</param>
    /// <param name="mat">Material</param>
    /// <returns></returns>
    public static Color GetColor(MatPairStruct item, MatPairStruct mat)
    {
        switch (item.mat_type)
        {
            case 55://PLANT_GROWTH
                {
                    return GetGrowthColor(GrowthPeriod.Current, item.mat_index, mat);
                }
            default:
                break;
        }
        return GetColor(mat);
    }

    /// <summary>
    /// Get the material color of an item including dye
    /// </summary>
    /// <param name="item">item to get the color from</param>
    /// <returns></returns>
    public static Color GetColor(RemoteFortressReader.Item item)
    {
        var color = GetColor(item.type, item.material);
        var dye = item.dye;
        if (dye == null)
            return color;
        if (!Application.isPlaying)
        {
            if (dye.red + dye.green + dye.blue == 0)
                return color;
        }
        color *= (Color)new Color32((byte)dye.red, (byte)dye.green, (byte)dye.blue, 255);
        return color;
    }

    public static int GetPatternIndex(MatPairStruct mat)
    {
        MaterialTextureSet textureSet;
        if (MaterialCollection.Instance.TryGetValue(mat, out textureSet))
        {
            return textureSet.patternIndex;
        }
        if (Instance == null)
            return 0;
        return Instance.DefaultMatTexIndex;
    }

    public static int GetShapeIndex(MatPairStruct mat)
    {
        MaterialTextureSet textureSet;
        if (MaterialCollection.Instance.TryGetValue(mat, out textureSet))
        {
            return textureSet.shapeIndex;
        }
        if (Instance == null)
            return 0;
        return Instance.DefaultShapeTexIndex;
    }

    public int DefaultMatTexIndex { get; private set; }

    public float DefaultMatTexArrayIndex
    {
        get
        {
            return 0;
        }
    }



    public int DefaultShapeTexIndex { get; private set; }

    public Matrix4x4 DefaultShapeTexTransform
    {
        get
        {
            return shapeTextureStorage.getUVTransform(DefaultShapeTexIndex);
        }
    }

    public int DefaultSpecialTexIndex { get; private set; }

    public Matrix4x4 DefaultSpecialTexTransform
    {
        get
        {
            return specialTextureStorage.getUVTransform(DefaultSpecialTexIndex);
        }
    }


    public TileConfiguration<NormalContent> ShapeTextureConfiguration { get; private set; }
    public TileConfiguration<MeshContent> TileMeshConfiguration { get; private set; }
    public TileConfiguration<MeshContent> GrowthMeshConfiguration { get; private set; }
    public TileConfiguration<LayerContent> MaterialLayerConfiguration { get; private set; }
    //public TileConfiguration<MeshContent> BuildingMeshConfiguration { get; private set; }
    //public TileConfiguration<NormalContent> BuildingShapeTextureConfiguration { get; private set; }
    public TileConfiguration<MeshContent> DesignationMeshConfiguration { get; private set; }
    public TileConfiguration<MeshContent> CollisionMeshConfiguration { get; private set; }
    //public TileConfiguration<MeshContent> BuildingCollisionMeshConfiguration { get; private set; }
    public TileConfiguration<MeshContent> ItemMeshConfiguration { get; private set; }

    [SerializeField]
    private CreatureSpriteManager _spriteManager = new CreatureSpriteManager();
    private static ContentLoader _routiner;

    public CreatureSpriteManager SpriteManager { get { return _spriteManager; } }

    public void Awake()
    {
        _routiner = this;

        shapeTextureStorage = new TextureStorage();
        specialTextureStorage = new TextureStorage();

        Debug.Log("Compiling material shape textures.");
        shapeTextureStorage.AddTextureArray(Resources.Load<Texture2DArray>("shapeTextures"));

        DefaultShapeTexIndex = shapeTextureStorage.AddTexture(CreateFlatTexture(new Color(1f, 0.5f, 1f, 0.5f)));
        DefaultSpecialTexIndex = specialTextureStorage.AddTexture(Texture2D.blackTexture);
    }

    public static Texture2D CreateFlatTexture(Color color)
    {
        Texture2D tex = new Texture2D(4, 4);
        var pix = tex.GetPixels();
        for (int i = 0; i < pix.Length; i++)
        {
            pix[i] = color;
        }
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }
    public static Texture2D LoadTexture(XAttribute textureAtt, XElement elemtype, Color defaultColor, bool linear = false)
    {
        if (textureAtt == null)
            return CreateFlatTexture(defaultColor);

        string texturePath = Path.Combine(Path.GetDirectoryName(new Uri(elemtype.BaseUri).LocalPath), textureAtt.Value);
        texturePath = Path.GetFullPath(texturePath);
        if (!File.Exists(texturePath))
        {
            Debug.LogError("File not found: " + texturePath);
            return CreateFlatTexture(defaultColor);
        }

        byte[] patternData = File.ReadAllBytes(texturePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false, linear);
        texture.LoadImage(patternData);
        GameSettings.ClampToMaxSize(texture);
        texture.name = texturePath;
        if (texture.width * 4 <= GameSettings.Instance.rendering.maxTextureSize && texture.height * 4 <= GameSettings.Instance.rendering.maxTextureSize)
        {
            HqxSharp.Scale4(texture);
        }
        else if (texture.width * 3 <= GameSettings.Instance.rendering.maxTextureSize && texture.height * 3 <= GameSettings.Instance.rendering.maxTextureSize)
        {
            HqxSharp.Scale3(texture);
        }
        else if (texture.width * 2 <= GameSettings.Instance.rendering.maxTextureSize && texture.height * 2 <= GameSettings.Instance.rendering.maxTextureSize)
        {
            HqxSharp.Scale2(texture);
        }
        return texture;
    }


    public void Initialize()
    {
        StartCoroutine(LoadAssets());
    }

    IEnumerator LoadAssets()
    {
        System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        mainProgressBar.gameObject.SetActive(true);
        subProgressBar.gameObject.SetActive(true);
        watch.Start();
        if(GameMap.Instance != null)
            GameMap.Instance.ShowHelp();
        PatternTextureArray = Resources.Load<Texture2DArray>("patternTextures");
        PatternTextureDepth = PatternTextureArray.depth;
        Shader.SetGlobalTexture("_MatTexArray", PatternTextureArray);
        float totalItems = LoadCallbacks.Count + 2; //Streaming assets, finalizations, and load callbacks.
        float doneItems = 0;
        mainProgressBar.SetProgress(doneItems / totalItems);
        yield return StartCoroutine(ParseContentIndexFile(Application.streamingAssetsPath + "/index.txt"));
        doneItems++;
        mainProgressBar.SetProgress(doneItems / totalItems);
        yield return StartCoroutine(FinalizeTextureAtlases());
        MaterialCollection.Init();
        Instance = this;

        foreach (var callback in LoadCallbacks)
        {
            doneItems++;
            mainProgressBar.SetProgress(doneItems / totalItems);
            if (callback != null)
                yield return StartCoroutine(callback());
        }
        Debug.Log("Done!");
        watch.Stop();
        Debug.Log("Took a total of " + watch.ElapsedMilliseconds + "ms to load all XML files.");
        Debug.Log(string.Format("loaded {0} meshes, and {1} shape textures.", MeshContent.NumCreated, shapeTextureStorage.AtlasTexture.depth));
        Debug.Log("Loading Complete. Press ESC to change settings or leave feedback. Have a nice day!");
        Debug.Log("Screen is " + Screen.dpi + "dpi.");
        if (GameMap.Instance != null)
            GameMap.Instance.HideHelp();
        DFConnection.Instance.NeedNewBlocks = true;
        mainProgressBar.gameObject.SetActive(false);
        subProgressBar.gameObject.SetActive(false);

        yield break;
    }

    IEnumerator ParseContentIndexFile(string path)
    {
        mainProgressBar.SetProgress("Loading Index File: " + path);

        string line;
        List<string> fileArray = new List<string>(); //This allows us to parse the file in reverse.
        StreamReader file = new StreamReader(path);
        while ((line = file.ReadLine()) != null)
        {
            line = line.Trim(); //remove trailing spaces
            if (string.IsNullOrEmpty(line))
                continue;
            if (line[0] == '#') //Allow comments
                continue;

            fileArray.Add(line);
        }
        file.Close();
        string filePath;

        for (int i = fileArray.Count - 1; i >= 0; i--)
        {
            try
            {
                filePath = Path.Combine(Path.GetDirectoryName(path), fileArray[i]);
            }
            catch (Exception)
            {
                continue; //Todo: Make an error message here
            }
            subProgressBar.SetProgress(1.0f - (i / (float)fileArray.Count));
            if (Directory.Exists(filePath)) //if it's a directory, just parse the contents
            {
                yield return ParseContentDirectory(filePath);
            }
            else
            {
                switch (Path.GetExtension(filePath))
                {
                    case ".txt":
                        StreamReader mightBeRaw = new StreamReader(filePath);
                        //first check if it's a DF raw file.
                        if (mightBeRaw.ReadLine() == Path.GetFileNameWithoutExtension(filePath))
                            yield return StartCoroutine(ParseContentRawFile(filePath));
                        else
                            yield return StartCoroutine(ParseContentIndexFile(filePath));
                        break;
                    case ".xml":
                        yield return StartCoroutine(ParseContentXMLFile(filePath));
                        break;
                    default:
                        break;
                }
            }
        }
        yield return null;
    }

    IEnumerator ParseContentXMLFile(string path)
    {
        subProgressBar.SetProgress("Loading XML File: " + path);
        XElement doc = XElement.Load(path, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);
        while (doc != null)
        {
            switch (doc.Name.LocalName)
            {
                case "shapeTextures":
                    if (ShapeTextureConfiguration == null)
                        ShapeTextureConfiguration = TileConfiguration<NormalContent>.GetFromRootElement(doc, "shapeTexture");
                    yield return StartCoroutine(ShapeTextureConfiguration.AddSingleContentConfig(doc, shapeTextureStorage));
                    break;
                case "tileMeshes":
                    if (TileMeshConfiguration == null)
                        TileMeshConfiguration = TileConfiguration<MeshContent>.GetFromRootElement(doc, "tileMesh");
                    yield return StartCoroutine(TileMeshConfiguration.AddSingleContentConfig(doc, new MeshContent.TextureStorageContainer(null, shapeTextureStorage, specialTextureStorage)));
                    break;
                case "materialLayers":
                    if (MaterialLayerConfiguration == null)
                        MaterialLayerConfiguration = TileConfiguration<LayerContent>.GetFromRootElement(doc, "materialLayer");
                    yield return StartCoroutine(MaterialLayerConfiguration.AddSingleContentConfig(doc));
                    break;
                case "growthMeshes":
                    if (GrowthMeshConfiguration == null)
                        GrowthMeshConfiguration = TileConfiguration<MeshContent>.GetFromRootElement(doc, "growthMesh");
                    yield return StartCoroutine(GrowthMeshConfiguration.AddSingleContentConfig(doc, new MeshContent.TextureStorageContainer(null, shapeTextureStorage, specialTextureStorage)));
                    break;
                case "designationMeshes":
                    if (DesignationMeshConfiguration == null)
                        DesignationMeshConfiguration = TileConfiguration<MeshContent>.GetFromRootElement(doc, "designationMesh");
                    yield return StartCoroutine(DesignationMeshConfiguration.AddSingleContentConfig(doc, new MeshContent.TextureStorageContainer(null, shapeTextureStorage, specialTextureStorage)));
                    break;
                case "collisionMeshes":
                    if (CollisionMeshConfiguration == null)
                        CollisionMeshConfiguration = TileConfiguration<MeshContent>.GetFromRootElement(doc, "collisionMesh");
                    yield return StartCoroutine(CollisionMeshConfiguration.AddSingleContentConfig(doc, new MeshContent.TextureStorageContainer(null, shapeTextureStorage, specialTextureStorage)));
                    break;
                case "itemMeshes":
                    if (ItemMeshConfiguration == null)
                        ItemMeshConfiguration = TileConfiguration<MeshContent>.GetFromRootElement(doc, "itemMesh");
                    yield return StartCoroutine(ItemMeshConfiguration.AddSingleContentConfig(doc, null));
                    break;
                default:
                    break;
            }
            doc = doc.NextNode as XElement;
        }
        yield return null;
    }

    IEnumerator ParseContentRawFile(string path)
    {
        mainProgressBar.SetProgress("Loading Raw File: " + path);
        var tokenList = RawLoader.SplitRawFileText(File.ReadAllText(path));
        var tokenEnumerator = tokenList.GetEnumerator();
        try
        {
            if (tokenEnumerator.MoveNext()) // Because they always start at -1.
            {
                if (tokenEnumerator.Current.Token == "OBJECT")
                {
                    switch (tokenEnumerator.Current.Parameters[0])
                    {
                        case "GRAPHICS":
                            yield return StartCoroutine(SpriteManager.ParseGraphics(tokenEnumerator, path));
                            break;
                        default:
                            Debug.Log("Unhandled Token: " + tokenEnumerator.Current.Parameters[0]);
                            break;
                    }
                }
                else
                    Debug.Log("Unexpected Token: " + tokenEnumerator.Current.Token);
            }
        }
        finally
        {
            tokenEnumerator.Dispose();
        }
        yield return null;
    }

    IEnumerator ParseContentDirectory(string path)
    {
        foreach (var file in Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories))
        {
            StreamReader mightBeRaw = new StreamReader(file);
            //first check if it's a DF raw file.
            if (mightBeRaw.ReadLine() == Path.GetFileNameWithoutExtension(file))
                yield return ParseContentRawFile(file);
        }
        yield return null;
    }

    IEnumerator FinalizeTextureAtlases()
    {
        mainProgressBar.SetProgress("Compacting textures...");
        subProgressBar.SetProgress(0.0f / 3.0f, "Building shape textures...");
        yield return null;
        shapeTextureStorage.CompileTextures("ShapeTexture", TextureFormat.RGBA32, new Color(1.0f, 0.5f, 0.0f, 0.5f), true);
        subProgressBar.SetProgress(1.0f / 3.0f, "Building special textures...");
        yield return null;
        specialTextureStorage.CompileTextures("SpecialTexture");

        subProgressBar.SetProgress(2.0f / 3.0f, "Updating Material Manager...");
        yield return null;

        Vector4 arrayCount = new Vector4(PatternTextureDepth, shapeTextureStorage.Count, specialTextureStorage.Count, shapeTextureStorage.Count);
        if (MaterialManager.Instance)
        {
            MaterialManager.Instance.SetTexture("_ShapeMap", shapeTextureStorage.AtlasTexture);
            MaterialManager.Instance.SetTexture("_BumpMap", shapeTextureStorage.AtlasTexture);
            MaterialManager.Instance.SetTexture("_SpecialTex", specialTextureStorage.AtlasTexture);
            MaterialManager.Instance.SetVector("_TexArrayCount", arrayCount);
        }
        mainProgressBar.SetProgress("Finalizing low detail creature sprites");
        SpriteManager.subProgressBar = subProgressBar;
        yield return StartCoroutine(SpriteManager.FinalizeSprites());
        //get rid of any un-used textures left over.
        Resources.UnloadUnusedAssets();
        GC.Collect();
        yield return null;
    }

    static Dictionary<int, Material> transparentMaterialVersions = new Dictionary<int, Material>();
    public const long LoadFrameTimeout = 16;

    public static Material getFinalMaterial(Material material, float alpha)
    {
        int instanceID = material.GetInstanceID();
        if (!transparentMaterialVersions.ContainsKey(instanceID) || transparentMaterialVersions[instanceID] == null)
        {
            switch(material.shader.name)
            {
                case "Standard":
                    Debug.LogWarning(material.name + " Has a standard shader!");
                    transparentMaterialVersions[instanceID] = material;
                    break;
                case "Art/ArtImageVertexOpaque":
                    transparentMaterialVersions[instanceID] = material;
                    break;
                case "Art/SingleImage":
                    {
                        var mat = new Material(material);
                        mat.shader = Shader.Find("Art/SingleImage Transparent");
                        mat.name += " Transparent";
                        transparentMaterialVersions[instanceID] = mat;
                    }
                    break;
                default:
                    {
                        var mat = new Material(material);
                        mat.shader = Shader.Find("Shader Graphs/Objects Transparent");
                        mat.name += " Transparent";
                        transparentMaterialVersions[instanceID] = mat; 
                    }
                    break;
            }
        }

        if (alpha < 0.5f)
            return transparentMaterialVersions[instanceID];
        else
            return material;
    }
}
