using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class GS : MonoBehaviour
{
    private List<string> spriteString = new();
    private float aspectRatio = 0, oldAspect = 0;

    [SerializeField] private Camera myCamera;
    [SerializeField] private GameObject tile;
    [SerializeField] private Sprite[] defaultTile;
    [SerializeField] public List<Sprite> tileSprites;
    public Neighbours[] commonNeighboursDefinition = new Neighbours[] { Neighbours.Adjacent, Neighbours.Diagonals };

    public Vector2Int tileCount;
    public Vector2 cameraOccupationOffset;
    public Vector2 maxOccupiedSpace;
    public float gridScale;

    public Vector3 gridPosition;
    public Vector3 positionFixedToCamera;

    public bool renderOnAspectChange;
    public bool renderObscuredTiles;
    public bool dynamicScaling;
    public bool frustumCulling;
    public bool alwaysUpdate;

    private Dictionary<Vector3Int, string> grid = new();
    private Dictionary<Vector3Int, Color> gridColors = new();

    private (Vector3Int pos, string data) hoveringTileString = (-Vector3Int.one, "");
    private (Vector3Int pos, Color data) hoveringTileColor = (-Vector3Int.one, new());
    private float leftBoundX, rightBoundX, upBoundY, downBoundY;
    private List<bool> containsAlpha = new();
    private List<Vector2> obscuredTiles = new();
    private Vector2 tileSize;

    public enum Interactions
    {
        Hover,
        Click,
        Hold,
        Release
    }
    public enum Button
    {
        Left,
        Right,
        Middle
    }
    public enum Neighbours
    {
        Self,
        Adjacent,
        Diagonals
    }
    public enum DataChangedTo
    {
        First,
        Second,
        Error
    }
    private static GS instance;
    public static GS I
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<GS>();
                if (instance == null)
                    Debug.LogError("Žádný GameObject nemá komponent GS.cs!");
            }
            return instance;
        }
    }
    public draw Draw { get; private set; }
    public data Data { get; private set; }
    public clear Clear { get; private set; }
    public interact Interact { get; private set; }
    public tiles Tiles { get; private set; }
    public objects Object { get; private set; }
    private void Awake()
    {
        if (instance == null)
            instance = this;
        else if (instance != this)
        {
            Debug.LogWarning($"Lze mít pouze 1 GameObject, a tak bude tento GameObject: \"{gameObject}\" smazán");
            Destroy(gameObject);
        }
        Draw = new();
        Data = new();
        Clear = new();
        Interact = new();
        Tiles = new();
        Object = new();
    }
    private void Start()
    {
        I.Draw.RefreshSprites();
        I.Draw.UpdateCameraValues();
    }
    private void Update()
    {
        if (renderOnAspectChange && oldAspect != myCamera.aspect)
            I.Draw.All();
        if (alwaysUpdate)
        {
            I.Draw.RefreshSprites();
            I.Draw.UpdateCameraValues();
            I.Draw.All();
        }
    }
    public class draw
    {
        public void All()
        {
            RefreshSprites();
            Delete();
            I.obscuredTiles.RemoveAll(i => true);
            if (I.maxOccupiedSpace.x > 0 && I.maxOccupiedSpace.y > 0 && I.tileCount.x > 0 && I.tileCount.y > 0)
            {
                I.oldAspect = I.myCamera.aspect;
                for (int z = 0; z < I.defaultTile.Length; z++)
                    Grid(z);
            }
        }
        public void Grid(int z)
        {
            Vector2 tileScale = I.Tiles.GetScale();
            I.tile.transform.localScale = new Vector3(tileScale.x, tileScale.y, 1);
            I.tileSize = I.tile.GetComponent<SpriteRenderer>().bounds.size;
            int startX = 0, endX = I.tileCount.x, startY = 0, endY = I.tileCount.y;
            if (I.frustumCulling)
                I.Tiles.RenderBounds(z, out startX, out startY, out endX, out endY);
            for (int i = startX; i < endX; i++)
            {
                for (int j = startY; j < endY; j++)
                {
                    if (I.renderObscuredTiles || z == 0 || !I.Tiles.Obscured(i, j, z))
                        Tile(i, j, z, tileScale);
                }
            }
        }
        public void Tile(int x, int y, int z, Vector2 tileScale)
        {
            Vector3Int tileGridPosition = new(x, y, z);
            if (I.Object.Tile(tileGridPosition) != null)
                DeleteTileKeepData(x, y, z);
            bool containsKey = I.grid.ContainsKey(tileGridPosition);
            if (containsKey || I.defaultTile.Length > 0)
            {
                Sprite tileSprite = I.defaultTile[z];
                if (I.grid.ContainsKey(tileGridPosition))
                    tileSprite = I.Tiles.GetSpriteByName(I.grid[tileGridPosition]);
                if (tileSprite != null)
                {
                    GameObject clonedTile = Instantiate(I.tile);
                    clonedTile.transform.position = I.Tiles.Position(x, y, z);
                    clonedTile.transform.localScale = new Vector3(tileScale.x, tileScale.y, 1);
                    clonedTile.tag = "CloneTile";

                    SpriteRenderer renderer = clonedTile.GetComponent<SpriteRenderer>();
                    renderer.sprite = tileSprite;
                    TileColor(x, y, z, renderer);
                }
            }
        }
        public void Tile(int x, int y, int z)
        {
            Tile(x, y, z, I.Tiles.GetScale());
        }
        public void TileColor(int x, int y, int z, SpriteRenderer renderer)
        {
            Vector3Int location = new(x, y, z);
            try
            {
                renderer.color = I.gridColors[location];
            }
            catch { }
        }
        public void TileColor(Vector3Int location, SpriteRenderer renderer)
        {
            TileColor(location.x, location.y, location.z, renderer);
        }
        public Color HSVtoRGB(float h, float s, float v, float a = 100)
        {
            Color conversion = Color.HSVToRGB(h / 360f, s / 100f, v / 100f);
            return new(conversion.r, conversion.g, conversion.b, a / 100f);
        }
        public void RefreshSprites()
        {
            foreach (Sprite spr in I.defaultTile)
            {
                if (!I.tileSprites.Contains(spr))
                    I.tileSprites.Add(spr);
            }
            foreach (Sprite spr in I.tileSprites)
            {
                string handledString = spr.ToString();
                try
                {
                    handledString = handledString.Substring(0, handledString.LastIndexOf("_0"));
                }
                catch { }
                if (!I.spriteString.Contains(handledString))
                    I.spriteString.Add(handledString);
                if (!I.renderObscuredTiles)
                    I.containsAlpha.Add(SpriteTransparent(spr));
            }
        }
        public bool SpriteTransparent(Sprite spr)
        {
            if (spr == null)
                return true;
            Color32[] pixels = spr.texture.GetPixels32();
            foreach (Color32 pixel in pixels)
            {
                if (pixel.a < 255)
                    return true;
            }
            return false;
        }
        public int GetIndexOfSprite(int x, int y, int z)
        {
            return I.spriteString.IndexOf(I.Data.Get(x, y, z));
        }
        public void UpdateCameraValues()
        {
            Vector3 cameraPos = I.myCamera.transform.position;
            float orthSize = I.myCamera.orthographicSize;
            I.aspectRatio = I.myCamera.aspect;
            I.leftBoundX = cameraPos.x - orthSize * I.aspectRatio; I.rightBoundX = cameraPos.x + orthSize * I.aspectRatio;
            I.upBoundY = cameraPos.y + orthSize; I.downBoundY = cameraPos.y - orthSize;
            I.tileSize = I.tile.GetComponent<SpriteRenderer>().bounds.size;
        }
        public void Delete()
        {
            GameObject[] cloneTiles = I.Object.All();
            foreach (GameObject obj in cloneTiles)
            {
                #if UNITY_EDITOR
                    DestroyImmediate(obj);
                #else
                    Destroy(obj);
                #endif
            }
        }
        public void DeleteTileKeepData(int x, int y, int z)
        {
            try
            {
                Destroy(I.Object.Tile(x, y, z));
            }
            catch { }
        }
        public void DeleteTile(int x, int y, int z)
        {
            DeleteTileKeepData(x, y, z);
            I.Clear.Tile(x, y, z);
        }
        public void SetSprite(string set, int x, int y, int z)
        {
            I.Data.Set(set, x, y, z);
            GameObject tile = I.Object.Tile(x, y, z);
            if (tile != null)
            {
                Sprite tileSprite = I.Tiles.GetSpriteByName(set);
                if (tileSprite != null)
                {
                    SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
                    renderer.sprite = tileSprite;
                    TileColor(x, y, z, renderer);
                }
                else
                    Destroy(tile);
            }
            else
                Tile(x, y, z);
        }
        public void RefreshTile(int x, int y, int z)
        {
            if (!I.renderObscuredTiles)
            {
                z++;
                if (I.defaultTile.Length > z)
                {
                    if (!I.Tiles.Opacity(x, y, z - 1))
                    {
                        Dictionary<int, GameObject> layerGameObj = I.Object.AllAtPosition(x, y);
                        DeleteTileKeepData(x, y, z);
                        while (z < I.defaultTile.Length)
                        {
                            if (I.Tiles.Opacity(x, y, z - 1) || I.Data.GetColor(x, y, z).a < 1f)
                                break;
                            if (!layerGameObj.ContainsKey(z))
                                Tile(x, y, z);
                            else
                            {
                                int currentIndex = GetIndexOfSprite(x, y, z);
                                if (currentIndex >= 0)
                                    layerGameObj[z].GetComponent<SpriteRenderer>().sprite = I.tileSprites[currentIndex];
                            }
                            z++;
                        }
                    }
                    else
                    {
                        while (z < I.defaultTile.Length)
                        {
                            DeleteTileKeepData(x, y, z);
                            z++;
                        }
                    }
                }
            }
        }
        public void SetRefresh<T>(T set, int x, int y, int z)
        {
            if (I.Tiles.ContainedWithinGrid(new(x, y, z)))
            {
                if (set is string setString)
                {
                    if (I.Data.Get(x, y, z) != setString)
                    {
                        SetSprite(setString, x, y, z);
                        RefreshTile(x, y, z);
                    }
                }
                else if (set is Color setColor)
                {
                    if (I.Data.GetColor(x, y, z) != setColor)
                    {
                        SetSpriteColors(setColor, x, y, z);
                        RefreshTile(x, y, z);
                    }
                }
                else
                    Debug.Log("Tato funkce podporuje pouze string a Color!");
            }
        }
        public void SetRefresh<T>(T set, Vector3Int location)
        {
            SetRefresh(set, location.x, location.y, location.z);
        }
        public void SetSpriteColors(Color color, Vector3Int location)
        {
            I.Data.SetColor(color, location);
            GameObject obj = I.Object.Tile(location);
            if (obj != null)
                TileColor(location, obj.GetComponent<SpriteRenderer>());
        }
        public void SetSpriteColors(Color color, int x, int y, int z)
        {
            SetSpriteColors(color, new(x, y, z));
        }
    }
    public class data
    {
        public void Set<T>(T set, int x, int y, int z)
        {
            if (set is string setString)
            {
                if (I.grid.ContainsKey(new Vector3Int(x, y, z)))
                    I.grid[new(x, y, z)] = setString;
                else
                    I.grid.Add(new(x, y, z), setString);
            }
            else if (set is Color setColor)
            {
                Vector3Int location = new(x, y, z);
                if (I.gridColors.ContainsKey(location))
                    I.gridColors[location] = setColor;
                else
                    I.gridColors.Add(location, setColor);
            }
            else
                Debug.LogError("Set jen podporuje string a Color!");

        }
        public void Set<T>(T set, Vector3Int location)
        {
            Set(set, location.x, location.y, location.z);
        }
        public string Get(int x, int y, int z)
        {
            try
            {
                string defaultTile = I.defaultTile[z].ToString();
                return I.grid.ContainsKey(new(x, y, z)) ? I.grid[new(x, y, z)] : defaultTile.Substring(0, defaultTile.LastIndexOf("_0"));
            }
            catch
            {
                return "";
            }
        }
        public string Get(Vector3Int location)
        {
            return Get(location.x, location.y, location.z);
        }
        public void SetColor(Color color, Vector3Int location)
        {
            SetColor(color, location.x, location.y, location.z);
        }
        public void SetColor(Color color, int x, int y, int z)
        {
            Set(color, x, y, z);
        }
        public Color GetColor(Vector3Int location)
        {
            if (I.gridColors.ContainsKey(location))
                return I.gridColors[location];
            else
                return new(1, 1, 1);
        }
        public Color GetColor(int x, int y, int z)
        {
            return GetColor(new(x, y, z));
        }
        public DataChangedTo SetOscillate(string set1, string set2, Vector3Int location, string none = null)
        {
            if (none == null)
                none = set1;
            string tileData = I.Data.Get(location);
            if (tileData == set1)
            {
                Set(set2, location);
                return DataChangedTo.Second;
            } else if (tileData == set2)
            {
                Set(set1, location);
                return DataChangedTo.First;
            }
            Set(none, location);
            return DataChangedTo.Error;
        }
        public DataChangedTo SetOscillateRefresh(string set1, string set2, Vector3Int location, string none = null)
        {
            DataChangedTo result = SetOscillate(set1, set2, location, none);
            I.Draw.All();
            return result;
        }
        public int ScatterTiles<T>(T tile, int count, int layer, List<Vector3Int> exceptions)
        {
            count = Math.Min(count, I.tileCount.x * I.tileCount.y - exceptions.Count);
            HashSet<Vector3> selectedPos = new();
            for (int i = 0; i < count; i++)
            {
                Vector3Int randomPos = -Vector3Int.one;
                do
                {
                    randomPos = new(UnityEngine.Random.Range(0, I.tileCount.x), UnityEngine.Random.Range(0, I.tileCount.y), layer);
                } while (!(!selectedPos.Contains(randomPos) && !exceptions.Contains(randomPos)));
                selectedPos.Add(randomPos);
                Set(tile, randomPos.x, randomPos.y, layer);
            }
            I.Draw.All();
            return count;
        }
        public int ScatterBeyondNeighbours<T>(T tile, int count, int layer, Vector2Int location)
        {
            I.Clear.DataLayer(layer);
            return ScatterTiles(tile, count, layer, I.Tiles.GetNeighbours(new(location.x, location.y, layer)));
        }
        public int FloodFill(string areaTile, string[] newTiles, Vector3Int startingLocation, int[] modifiedLayers, (string tile, int layer) tileLayerTracker = default, Neighbours[] neighboringTilesDefinition = null, bool includeBordering = true)
        {
            if (newTiles.Length != modifiedLayers.Length)
            {
                Debug.LogError("Délka polí 'newTiles' a 'modifiedLayers' se musí rovnat!");
                return 0;
            }
            int tileLayerCounter = 0;
            if (I.Tiles.ContainedWithinGrid(startingLocation))
            {
                if (Get(startingLocation.x, startingLocation.y, startingLocation.z) == areaTile)
                {
                    if (neighboringTilesDefinition == null)
                        neighboringTilesDefinition = I.commonNeighboursDefinition;
                    HashSet<Vector3Int> replacedTiles = new();
                    List<Vector3Int> nextStepOptions = I.Tiles.GetNeighbours(startingLocation, new Neighbours[] { Neighbours.Adjacent, Neighbours.Diagonals });
                    while (nextStepOptions.Count > 0)
                    {
                        List<Vector3Int> addToList = new(), removeFromList = new();
                        foreach (Vector3Int tile in nextStepOptions)
                        {
                            if (!replacedTiles.Contains(tile) && (includeBordering || areaTile == Get(tile)))
                            {
                                if (I.Data.Get(tile.x, tile.y, tileLayerTracker.layer) == tileLayerTracker.tile)
                                    tileLayerCounter++;
                                for (int i = 0; i < modifiedLayers.Length; i++)
                                    Set(newTiles[i], tile.x, tile.y, modifiedLayers[i]);
                            }
                            if (areaTile == Get(tile) && !replacedTiles.Contains(tile))
                            {
                                addToList.AddRange(I.Tiles.GetNeighbours(tile, new Neighbours[] { Neighbours.Adjacent, Neighbours.Diagonals }).Except(replacedTiles));
                                replacedTiles.Add(tile);
                            }
                            else
                                removeFromList.Add(tile);
                        }
                        nextStepOptions.RemoveAll(item => removeFromList.Contains(item));
                        nextStepOptions.AddRange(addToList);
                    }
                    I.Draw.All();
                }
                else if (includeBordering)
                {
                    if (I.Data.Get(startingLocation.x, startingLocation.y, tileLayerTracker.layer) == tileLayerTracker.tile)
                        tileLayerCounter++;
                    for (int i = 0; i < modifiedLayers.Length; i++)
                        I.Draw.SetRefresh(newTiles[i], startingLocation.x, startingLocation.y, modifiedLayers[i]);
                }
            }
            return tileLayerCounter;
        }
        public void FloodFill(string areaTile, string newTile, Vector3Int startingLocation, int modifedLayer, (string tile, int layer) tileLayerTracker = default, Neighbours[] neighboringTilesDefinition = null, bool includeBordering = true)
        {
            FloodFill(areaTile, new string[] {newTile}, startingLocation, new int[] {modifedLayer}, tileLayerTracker);
        }
    }
    public class clear
    {
        public void Tile(int x, int y, int z)
        {
            I.grid.Remove(new(x, y, z));
        }
        private void ClearLayerBuilder<TValue>(int layer, Dictionary<Vector3Int, TValue> grid)
        {
            List<Vector3Int> keysToRemove = new();
            foreach (Vector3Int key in grid.Keys)
            {
                if (key.z == layer)
                    keysToRemove.Add(key);
            }
            foreach (Vector3Int key in keysToRemove)
                grid.Remove(key);
        }
        public void Data()
        {
            I.grid.Clear();
        }
        public void DataLayer(int layer)
        {
            ClearLayerBuilder(layer, I.grid);
        }
        public void DataRender()
        {
            Data();
            I.Draw.All();
        }
        public void Color()
        {
            I.gridColors.Clear();
        }
        public void ColorLayer(int layer)
        {
            ClearLayerBuilder(layer, I.gridColors);
        }
        public void ColorRender()
        {
            Color();
            I.Draw.All();
        }
        public void All()
        {
            Data();
            Color();
        }
        public void AllRender()
        {
            All();
            I.Draw.All();
        }
    }
    public class interact
    {
        public Vector2Int Hover()
        {
            if (I.tileCount.x == 0 || I.tileCount.y == 0)
                return new();
            Vector2 topLeftMostTile = I.Tiles.Position(0, 0, 0);
            Vector2 realMouse = Input.mousePosition;
            if (!(realMouse.x >= 0 && realMouse.y >= 0 && realMouse.x <= Screen.width && realMouse.y <= Screen.height))
                return -Vector2Int.one;
            Vector2 mousePosition = I.myCamera.ScreenToWorldPoint(realMouse);
            return new((int)Math.Round((mousePosition.x - topLeftMostTile.x) / I.tileSize.x), (int)Math.Round((topLeftMostTile.y - mousePosition.y) / I.tileSize.y));

        }
        public void GeneralInteraction<T>(T set, int z, bool interacting, bool interactOnlyWhenRendered = true)
        {
            Vector2Int hoveredTile = Hover();
            if ((!interactOnlyWhenRendered || I.Object.Tile(hoveredTile.x, hoveredTile.y, z) != null) && I.Tiles.ContainedWithinGrid(new(hoveredTile.x, hoveredTile.y, z)) && interacting)
                I.Draw.SetRefresh(set, new(hoveredTile.x, hoveredTile.y, z));
        }
        public Vector2Int GetHoverOnCondition(bool condition, int layer, bool interactOnlyWhenRendered = true)
        {
            Vector2Int hoveredTile = Hover();
            if (condition && (!interactOnlyWhenRendered || I.Object.Tile(hoveredTile.x, hoveredTile.y, layer)))
                return hoveredTile;
            return -Vector2Int.one;
        }

        public void MouseInteraction<T>(T set, int z, Interactions interaction = Interactions.Click, Button button = Button.Left, bool interactOnlyWhenRendered = true)
        {
            bool isInteracting = interaction switch
            {
                Interactions.Hover => true,
                Interactions.Click => Input.GetMouseButtonDown((int)button),
                Interactions.Hold => Input.GetMouseButton((int)button),
                Interactions.Release => Input.GetMouseButtonUp((int)button),
                _ => false
            };
            GeneralInteraction(set, z, isInteracting, interactOnlyWhenRendered);
        }
        public void SetRefreshTileHoveredOver<T>(T set, int z)
        {
            Vector2Int tilePosition = Hover();
            Vector3Int positionProper = new Vector3Int(tilePosition.x, tilePosition.y, z);
            Vector3Int previousTilePosition = I.hoveringTileString.pos;
            if (previousTilePosition == -Vector3Int.one)
                previousTilePosition = I.hoveringTileColor.pos;

            Type type = typeof(T);
            if (!(type == typeof(string) || type == typeof(Color)))
            {
                Debug.LogError("Tato funkce podporuje jen string a Color!");
                return;
            }
            GameObject obj = I.Object.Tile(positionProper);
            bool prevMouseOutOfBounds = previousTilePosition == -Vector3Int.one;
            if (obj == null)
            {
                if (!prevMouseOutOfBounds)
                {
                    if (type == typeof(string))
                    {
                        I.Draw.SetRefresh(I.hoveringTileString.data, previousTilePosition);
                        I.hoveringTileString = (-Vector3Int.one, "");
                    }
                    else if (type == typeof(Color))
                    {
                        I.Draw.SetRefresh(I.hoveringTileColor.data, previousTilePosition);
                        I.hoveringTileColor = (-Vector3Int.one, new());
                    }
                }
                return;
            }
            string name = I.Object.SpriteName(obj);
            if (previousTilePosition != positionProper)
            {
                if (type == typeof(string))
                {
                    if (!prevMouseOutOfBounds)
                        I.Draw.SetRefresh(I.hoveringTileString.data, previousTilePosition);
                    I.hoveringTileString = (positionProper, name);
                }
                else if (type == typeof(Color))
                {
                    if (!prevMouseOutOfBounds)
                        I.Draw.SetRefresh(I.hoveringTileColor.data, previousTilePosition);
                    I.hoveringTileColor = (positionProper, I.Data.GetColor(positionProper));
                }

            }
            else
                I.Draw.SetRefresh(set, positionProper);
        }
    }
    public class tiles
    {
        public Vector3 Position(int x, int y, int z)
        {
            Vector3 resultPosition = new Vector3(I.gridPosition.x + x * I.tileSize.x, I.gridPosition.y - y * I.tileSize.y, I.gridPosition.z + z);
            if (I.positionFixedToCamera.x != 0)
            {
                float totalGridWidth = I.tileCount.x * I.tileSize.x;
                float centerOffX = (I.rightBoundX - I.leftBoundX - totalGridWidth) * I.cameraOccupationOffset.x;
                resultPosition.x = I.leftBoundX + x * I.tileSize.x + I.tileSize.x / 2 + centerOffX;
            }
            if (I.positionFixedToCamera.y != 0)
            {
                float totalGridHeight = I.tileCount.y * I.tileSize.y;
                float centerOffY = (I.upBoundY - I.downBoundY - totalGridHeight) * I.cameraOccupationOffset.y;
                resultPosition.y = I.upBoundY - y * I.tileSize.y - I.tileSize.y / 2 - centerOffY;
            }
            if (I.positionFixedToCamera.z != 0)
                resultPosition.z = z + I.myCamera.transform.position.z + 10;
            return resultPosition;
        }
        public Vector2 GetScale()
        {
            Vector2 maxTileUnits = new Vector2(0, 2 * I.myCamera.orthographicSize * I.maxOccupiedSpace.y); maxTileUnits.x = maxTileUnits.y * I.myCamera.aspect / I.maxOccupiedSpace.y * I.maxOccupiedSpace.x;
            Vector2 possibleScale = new Vector2(maxTileUnits.x / I.tileCount.x, maxTileUnits.y / I.tileCount.y);
            float newScale = 3 * I.gridScale;
            if (I.dynamicScaling)
                newScale *= Math.Min(possibleScale.x, possibleScale.y);
            return new Vector2(newScale, newScale) / 0.96f;
        }
        public void RenderBounds(int zLayer, out int startX, out int startY, out int endX, out int endY)
        {
            Vector3 gridTopLeft = Position(0, 0, zLayer), gridBottomRight = Position(I.tileCount.x - 1, I.tileCount.y - 1, zLayer);
            gridTopLeft.x -= I.tileSize.x / 2; gridTopLeft.y += I.tileSize.y / 2;
            gridBottomRight.x += I.tileSize.x / 2; gridBottomRight.y -= I.tileSize.y / 2;
            Vector3 cameraTopLeft = new Vector3(I.leftBoundX, I.upBoundY, zLayer), cameraBottomRight = new Vector3(I.rightBoundX, I.downBoundY, zLayer);

            startX = Math.Max(0, (int)Math.Floor((cameraTopLeft.x - gridTopLeft.x) / I.tileSize.x));
            startY = Math.Max(0, (int)Math.Floor((gridTopLeft.y - cameraTopLeft.y) / I.tileSize.y));
            endX = Math.Min(I.tileCount.x, I.tileCount.x - (int)Math.Floor((gridBottomRight.x - cameraBottomRight.x) / I.tileSize.x));
            endY = Math.Min(I.tileCount.y, I.tileCount.y - (int)Math.Floor((cameraBottomRight.y - gridBottomRight.y) / I.tileSize.y));
        }
        public int GetIndex(Vector3Int location)
        {
            if (!ContainedWithinGrid(location))
                return -1;
            return location.y * I.tileCount.x + location.x + location.z * I.tileCount.x * I.tileCount.y;
        }
        public Vector3Int GetLocation(int index)
        {
            if (index < 0 || index > I.tileCount.x * I.tileCount.y * I.defaultTile.Length)
                return -Vector3Int.one;
            return new(index % I.tileCount.x, (int)Math.Floor(index / (float)I.tileCount.x), (int)Math.Floor(index / (float)(I.tileCount.x * I.tileCount.y)));
        }
        public bool ContainedWithinGrid(Vector3Int location)
        {
            return location.x >= 0 && location.y >= 0 && location.z >= 0 && location.x < I.tileCount.x && location.y < I.tileCount.y && location.z < I.defaultTile.Length;
        }
        public List<Vector3Int> GetNeighbours(Vector3Int location, Neighbours[] neighbouringTilesDefiniton = null)
        {
            if (neighbouringTilesDefiniton == null)
                neighbouringTilesDefiniton = I.commonNeighboursDefinition;
            List<Vector3Int> borders = new();
            List<Vector2Int> offsets = new();
            foreach (Neighbours neigh in neighbouringTilesDefiniton)
            {
                List<Vector2Int> neighbouringTiles = neigh switch
                {
                    Neighbours.Self => new List<Vector2Int> { new(0, 0) },
                    Neighbours.Adjacent => new List<Vector2Int> { new(-1, 0), new(1, 0), new(0, -1), new(0, 1) },
                    Neighbours.Diagonals => new List<Vector2Int> { new(-1, -1), new(-1, 1), new(1, -1), new(1, 1) },
                    _ => new()
                };
                offsets.AddRange(neighbouringTiles);
            }
            for (int i = 0; i < offsets.Count; i++)
            {
                Vector3Int offsetV3 = new(location.x + offsets[i].x, location.y + offsets[i].y, location.z);
                if (I.Tiles.ContainedWithinGrid(offsetV3))
                    borders.Add(offsetV3);
            }
            return borders;
        }
        public int NeighbouringSpriteCount(string spriteName, Vector3Int location)
        {
            int count = 0;
            List<Vector3Int> neighbours = GetNeighbours(location);
            foreach (Vector3Int offsetedLoc in neighbours)
            {
                if (I.Data.Get(offsetedLoc.x, offsetedLoc.y, location.z) == spriteName)
                    count++;
            }
            return count;
        }
        public Sprite GetSpriteByName(string spriteName)
        {
            for (int i = 0; i < I.spriteString.Count; i++)
            {
                if (I.spriteString[i] == spriteName)
                    return I.tileSprites[i];
            }
            return null;
        }
        public bool Obscured(int x, int y, int z)
        {
            if (I.obscuredTiles.Contains(new(x, y)))
                return true;
            int index = I.Draw.GetIndexOfSprite(x, y, z - 1);
            if (index != -1 && !I.containsAlpha[index])
            {
                I.obscuredTiles.Add(new(x, y));
                return true;
            }
            return false;
        }
        public bool Opacity(int x, int y, int z)
        {
            if (I.Data.GetColor(x, y, z).a < 1)
                return false;
            int spriteIndex = I.Draw.GetIndexOfSprite(x, y, z);
            return spriteIndex != -1 && !I.containsAlpha[spriteIndex];
        }
    }
    public class objects
    {
        public GameObject Tile(int x, int y, int z)
        {
            GameObject[] tiles = All();
            foreach (GameObject obj in tiles)
            {
                if (obj.transform.position == I.Tiles.Position(x, y, z))
                    return obj;
            }
            return null;
        }
        public GameObject Tile(Vector3Int location)
        {
            return Tile(location.x, location.y, location.z);
        }
        public Dictionary<int, GameObject> AllAtPosition(int x, int y)
        {
            GameObject[] tiles = All();
            Dictionary<int, GameObject> result = new();
            foreach (GameObject obj in tiles)
            {
                Vector3 posScan = obj.transform.position, posTile = I.Tiles.Position(x, y, 0);
                if (new Vector2(posScan.x, posScan.y) == new Vector2(posTile.x, posTile.y))
                    result.Add((int)(posScan.z - posTile.z - 1), obj);
            }
            return result;
        }
        public GameObject[] All()
        {
            return GameObject.FindGameObjectsWithTag("CloneTile");
        }
        public string SpriteName(GameObject obj)
        {
            Sprite spr = obj.GetComponent<SpriteRenderer>().sprite;
            int index = I.tileSprites.IndexOf(spr);
            if (index == -1)
                return "";
            return I.spriteString[index];
        }
    }
}
[CustomEditor(typeof(GS))]
public class ButtonDrawGrid : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Update Grid"))
            GS.I.Draw.All();
        if (GUILayout.Button("Restore Tile Data to Defaults"))
            GS.I.Clear.AllRender();
        if (GUILayout.Button("Delete Grid"))
            GS.I.Draw.Delete();
        if (GUILayout.Button("Test Function A"))
        {
            GS.I.Data.ScatterBeyondNeighbours("mine", 50, 1, new());
        }
    }
}
