using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class GS : MonoBehaviour
{
    [SerializeField] private static Camera myCamera;
    [SerializeField] private static GameObject tile;

    public static Vector2Int tileCount;
    public static Vector2 cameraOccupationOffset;
    public static Vector2 maxOccupiedSpace;
    public static float gridScale;

    public static Vector3 gridPosition;
    public static Vector3 positionFixedToCamera;

    [SerializeField] private static Sprite[] defaultTile;
    [SerializeField] public static List<Sprite> tileSprites;

    public static Neighbours[] commonNeighboursDefinition;
    public static bool renderOnAspectChange;
    public static bool renderObscuredTiles;
    public static bool dynamicScaling;
    public static bool frustumCulling;
    public static bool alwaysUpdate;

    private static Dictionary<Vector3Int, string> grid = new();
    private static Dictionary<Vector3Int, Color> gridColors = new();
    private static List<string> spriteString = new();

    private static (Vector3Int pos, string data) hoveringTileString = (-Vector3Int.one, "");
    private static (Vector3Int pos, Color data) hoveringTileColor = (-Vector3Int.one, new());
    private static float leftBoundX, rightBoundX, upBoundY, downBoundY;
    private static List<bool> containsAlpha = new();
    private static List<Vector2> obscuredTiles = new();
    private static float aspectRatio = 0, oldAspect = 0;
    private static Vector2 tileSize;

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
    private void Start()
    {
        Draw.RefreshSprites();
        Draw.UpdateCameraValues();
    }
    private void Update()
    {
        if (renderOnAspectChange && oldAspect != myCamera.aspect)
            Draw.All();
        if (alwaysUpdate)
        {
            Draw.RefreshSprites();
            Draw.UpdateCameraValues();
            Draw.All();
        }
    }
    public static class Draw
    {
        public static void All()
        {
            RefreshSprites();
            Delete();
            obscuredTiles.RemoveAll(i => true);
            if (maxOccupiedSpace.x > 0 && maxOccupiedSpace.y > 0 && tileCount.x > 0 && tileCount.y > 0)
            {
                oldAspect = myCamera.aspect;
                for (int z = 0; z < defaultTile.Length; z++)
                    Grid(z);
            }
        }
        public static void Grid(int z)
        {
            Vector2 tileScale = Tiles.GetScale();
            tile.transform.localScale = new Vector3(tileScale.x, tileScale.y, 1);
            tileSize = tile.GetComponent<SpriteRenderer>().bounds.size;
            int startX = 0, endX = tileCount.x, startY = 0, endY = tileCount.y;
            if (frustumCulling)
                Tiles.RenderBounds(z, out startX, out startY, out endX, out endY);
            for (int i = startX; i < endX; i++)
            {
                for (int j = startY; j < endY; j++)
                {
                    if (renderObscuredTiles || z == 0 || !Tiles.Obscured(i, j, z))
                        Tile(i, j, z, tileScale);
                }
            }
        }
        public static void Tile(int x, int y, int z, Vector2 tileScale)
        {
            Vector3Int tileGridPosition = new(x, y, z);
            bool containsKey = grid.ContainsKey(tileGridPosition);
            if (containsKey || defaultTile.Length > 0)
            {
                Sprite tileSprite = defaultTile[z];
                if (grid.ContainsKey(tileGridPosition))
                    tileSprite = Tiles.GetSpriteByName(grid[tileGridPosition]);
                if (tileSprite != null)
                {
                    GameObject clonedTile = Instantiate(tile);
                    clonedTile.transform.position = global::GS.Tiles.Position(x, y, z);
                    clonedTile.transform.localScale = new Vector3(tileScale.x, tileScale.y, 1);
                    clonedTile.tag = "CloneTile";

                    SpriteRenderer renderer = clonedTile.GetComponent<SpriteRenderer>();
                    renderer.sprite = tileSprite;
                    TileColor(x, y, z, renderer);
                }
            }
        }
        public static void Tile(int x, int y, int z)
        {
            Tile(x, y, z, Tiles.GetScale());
        }
        public static void TileColor(int x, int y, int z, SpriteRenderer renderer)
        {
            Vector3Int location = new(x, y, z);
            try
            {
                renderer.color = gridColors[location];
            }
            catch { }
        }
        public static void TileColor(Vector3Int location, SpriteRenderer renderer)
        {
            TileColor(location.x, location.y, location.z, renderer);
        }
        public static Color HSVtoRGB(float h, float s, float v, float a = 100)
        {
            Color conversion = Color.HSVToRGB(h / 360f, s / 100f, v / 100f);
            return new(conversion.r, conversion.g, conversion.b, a / 100f);
        }
        public static void RefreshSprites()
        {
            foreach (Sprite spr in defaultTile)
            {
                if (!tileSprites.Contains(spr))
                    tileSprites.Add(spr);
            }
            foreach (Sprite spr in tileSprites)
            {
                string handledString = spr.ToString();
                try
                {
                    handledString = handledString.Substring(0, handledString.LastIndexOf("_0"));
                }
                catch { }
                if (!spriteString.Contains(handledString))
                    spriteString.Add(handledString);
                if (!renderObscuredTiles)
                    containsAlpha.Add(SpriteTransparent(spr));
            }
        }
        public static bool SpriteTransparent(Sprite spr)
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
        public static int GetIndexOfSprite(int x, int y, int z)
        {
            return spriteString.IndexOf(Data.Get(x, y, z));
        }
        public static void UpdateCameraValues()
        {
            Vector3 cameraPos = myCamera.transform.position;
            float orthSize = myCamera.orthographicSize;
            aspectRatio = myCamera.aspect;
            leftBoundX = cameraPos.x - orthSize * aspectRatio; rightBoundX = cameraPos.x + orthSize * aspectRatio;
            upBoundY = cameraPos.y + orthSize; downBoundY = cameraPos.y - orthSize;
            tileSize = tile.GetComponent<SpriteRenderer>().bounds.size;
        }
        public static void Delete()
        {
            GameObject[] cloneTiles = Object.All();
            foreach (GameObject obj in cloneTiles)
            {
                #if UNITY_EDITOR
                    DestroyImmediate(obj);
                #else
                    Destroy(obj);
                #endif
            }
        }
        public static void DeleteTileKeepData(int x, int y, int z)
        {
            try
            {
                Destroy(Object.Tile(x, y, z));
            }
            catch { }
        }
        public static void DeleteTile(int x, int y, int z)
        {
            DeleteTileKeepData(x, y, z);
            Clear.Tile(x, y, z);
        }
        public static void SetSprite(string set, int x, int y, int z)
        {
            Data.Set(set, x, y, z);
            GameObject tile = Object.Tile(x, y, z);
            if (tile != null)
            {
                Sprite tileSprite = Tiles.GetSpriteByName(set);
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
        public static void RefreshTile(int x, int y, int z)
        {
            if (!renderObscuredTiles)
            {
                z++;
                if (defaultTile.Length > z)
                {
                    if (!Tiles.Opacity(x, y, z - 1))
                    {
                        Dictionary<int, GameObject> layerGameObj = Object.GetAllTilesAtPosition(x, y);
                        DeleteTileKeepData(x, y, z);
                        while (z < defaultTile.Length)
                        {
                            if (Tiles.Opacity(x, y, z - 1) || Data.GetColor(x, y, z).a < 1f)
                                break;
                            if (!layerGameObj.ContainsKey(z))
                                Tile(x, y, z);
                            else
                            {
                                int currentIndex = GetIndexOfSprite(x, y, z);
                                if (currentIndex >= 0)
                                    layerGameObj[z].GetComponent<SpriteRenderer>().sprite = tileSprites[currentIndex];
                            }
                            z++;
                        }
                    }
                    else
                    {
                        while (z < defaultTile.Length)
                        {
                            DeleteTileKeepData(x, y, z);
                            z++;
                        }
                    }
                }
            }
        }
        public static void SetRefresh<T>(T set, int x, int y, int z)
        {
            if (Tiles.ContainedWithinGrid(new(x, y, z)))
            {
                if (set is string setString)
                {
                    if (Data.Get(x, y, z) != setString)
                    {
                        SetSprite(setString, x, y, z);
                        RefreshTile(x, y, z);
                    }
                }
                else if (set is Color setColor)
                {
                    if (Data.GetColor(x, y, z) != setColor)
                    {
                        SetSpriteColors(setColor, x, y, z);
                        RefreshTile(x, y, z);
                    }
                }
                else
                    Debug.Log("Tato funkce podporuje pouze string a Color!");
            }
        }
        public static void SetRefresh<T>(T set, Vector3Int location)
        {
            SetRefresh(set, location.x, location.y, location.z);
        }
        public static void SetSpriteColors(Color color, Vector3Int location)
        {
            Data.SetColor(color, location);
            GameObject obj = Object.Tile(location);
            if (obj != null)
                TileColor(location, obj.GetComponent<SpriteRenderer>());
        }
        public static void SetSpriteColors(Color color, int x, int y, int z)
        {
            SetSpriteColors(color, new(x, y, z));
        }
    }
    public static class Data
    {
        public static void Set<T>(T set, int x, int y, int z)
        {
            if (set is string setString)
            {
                if (grid.ContainsKey(new Vector3Int(x, y, z)))
                    grid[new(x, y, z)] = setString;
                else
                    grid.Add(new(x, y, z), setString);
            }
            else if (set is Color setColor)
            {
                Vector3Int location = new(x, y, z);
                if (gridColors.ContainsKey(location))
                    gridColors[location] = setColor;
                else
                    gridColors.Add(location, setColor);
            }
            else
                Debug.LogError("Set jen podporuje string a Color!");

        }
        public static void Set<T>(T set, Vector3Int location)
        {
            Set(set, location.x, location.y, location.z);
        }
        public static string Get(int x, int y, int z)
        {
            int length = defaultTile[z].ToString().LastIndexOf("_0");
            if (length < 0)
                length = 0;
            return grid.ContainsKey(new(x, y, z)) ? grid[new(x, y, z)] : defaultTile[z].ToString();
        }
        public static string Get(Vector3Int location)
        {
            return Get(location.x, location.y, location.z);
        }
        public static void SetColor(Color color, Vector3Int location)
        {
            SetColor(color, location.x, location.y, location.z);
        }
        public static void SetColor(Color color, int x, int y, int z)
        {
            Set(color, x, y, z);
        }
        public static Color GetColor(Vector3Int location)
        {
            if (gridColors.ContainsKey(location))
                return gridColors[location];
            else
                return new(1, 1, 1);
        }
        public static Color GetColor(int x, int y, int z)
        {
            return GetColor(new(x, y, z));
        }
        public static void ScatterTiles<T>(T tile, int count, int layer, List<Vector3Int> exceptions)
        {
            count = Math.Min(count, tileCount.x * tileCount.y - exceptions.Count);
            HashSet<Vector3> selectedPos = new();
            for (int i = 0; i < count; i++)
            {
                Vector3Int randomPos = -Vector3Int.one;
                do
                {
                    randomPos = new(UnityEngine.Random.Range(0, tileCount.x), UnityEngine.Random.Range(0, tileCount.y), layer);
                } while (!(!selectedPos.Contains(randomPos) && !exceptions.Contains(randomPos)));
                selectedPos.Add(randomPos);
                Set(tile, randomPos.x, randomPos.y, layer);
            }
            Draw.All();
        }
        public static void ScatterBeyondNeighbours<T>(T tile, int count, int layer, Vector2Int location)
        {
            Clear.DataLayer(layer);
            ScatterTiles(tile, count, layer, Tiles.GetNeighbours(new(location.x, location.y, layer)));
        }
        public static void FloodFill(string areaTile, string newTile, Vector3Int startingLocation, int modifiedLayer, Neighbours[] neighboringTilesDefinition = null, bool includeBordering = true)
        {
            if (Tiles.ContainedWithinGrid(startingLocation))
            {
                if (Get(startingLocation.x, startingLocation.y, startingLocation.z) == areaTile)
                {
                    if (neighboringTilesDefinition == null)
                        neighboringTilesDefinition = commonNeighboursDefinition;
                    Vector3Int handledLocation = new Vector3Int(startingLocation.x, startingLocation.y, modifiedLayer);
                    HashSet<Vector3Int> replacedTiles = new() { handledLocation };
                    Set(newTile, handledLocation);
                    List<Vector3Int> nextStepOptions = Tiles.GetNeighbours(startingLocation, new Neighbours[] { Neighbours.Adjacent, Neighbours.Diagonals });
                    while (nextStepOptions.Count > 0)
                    {
                        List<Vector3Int> addToList = new(), removeFromList = new();
                        foreach (Vector3Int tile in nextStepOptions)
                        {
                            if (!replacedTiles.Contains(tile) && (includeBordering || areaTile == Get(tile)))
                                Set(newTile, tile.x, tile.y, modifiedLayer);
                            if (areaTile == Get(tile) && !replacedTiles.Contains(tile))
                            {
                                addToList.AddRange(Tiles.GetNeighbours(tile, new Neighbours[] { Neighbours.Adjacent, Neighbours.Diagonals }).Except(replacedTiles));
                                replacedTiles.Add(tile);
                            }
                            else
                                removeFromList.Add(tile);
                        }
                        nextStepOptions.RemoveAll(item => removeFromList.Contains(item));
                        nextStepOptions.AddRange(addToList);
                    }
                    Draw.All();
                }
                else
                    Draw.SetRefresh(newTile, new(startingLocation.x, startingLocation.y, modifiedLayer));
            }
        }
    }
    public static class Clear
    {
        public static void Tile(int x, int y, int z)
        {
            grid.Remove(new(x, y, z));
        }
        private static void ClearLayerBuilder<TValue>(int layer, Dictionary<Vector3Int, TValue> grid)
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
        public static void Data()
        {
            grid.Clear();
        }
        public static void DataLayer(int layer)
        {
            ClearLayerBuilder(layer, grid);
        }
        public static void DataRender()
        {
            Data();
            Draw.All();
        }
        public static void Color()
        {
            gridColors.Clear();
        }
        public static void ColorLayer(int layer)
        {
            ClearLayerBuilder(layer, gridColors);
        }
        public static void ColorRender()
        {
            Color();
            Draw.All();
        }
        public static void All()
        {
            Data();
            Color();
        }
        public static void AllRender()
        {
            All();
            Draw.All();
        }
    }
    public static class Interact
    {
        public static Vector2Int Hover()
        {
            if (tileCount.x == 0 || tileCount.y == 0)
                return new();
            Vector2 topLeftMostTile = Tiles.Position(0, 0, 0);
            Vector2 realMouse = Input.mousePosition;
            if (!(realMouse.x >= 0 && realMouse.y >= 0 && realMouse.x <= Screen.width && realMouse.y <= Screen.height))
            {
                Debug.LogWarning("Není možno zjistit pozici myši, bude vracen -Vector2Int.one!");
                return -Vector2Int.one;
            }
            Vector2 mousePosition = myCamera.ScreenToWorldPoint(realMouse);
            return new((int)Math.Round((mousePosition.x - topLeftMostTile.x) / tileSize.x), (int)Math.Round((topLeftMostTile.y - mousePosition.y) / tileSize.y));

        }
        public static void GeneralInteraction<T>(T set, int z, bool interacting, bool interactOnlyWhenRendered)
        {
            Vector2Int hoveredTile = Hover();
            if ((!interactOnlyWhenRendered || Object.Tile(hoveredTile.x, hoveredTile.y, z) != null) && Tiles.ContainedWithinGrid(new(hoveredTile.x, hoveredTile.y, z)) && interacting)
                Draw.SetRefresh(set, new(hoveredTile.x, hoveredTile.y, z));
        }
        public static Vector2Int GetHoverOnCondition(bool condition, bool interactOnlyWhenRendered = true, int layer = 0)
        {
            Vector2Int hoveredTile = Hover();
            if (condition && (!interactOnlyWhenRendered || Object.Tile(hoveredTile.x, hoveredTile.y, layer)))
                return hoveredTile;
            return -Vector2Int.one;
        }

        public static void MouseInteraction<T>(T set, int z, Interactions interaction = Interactions.Click, Button button = Button.Left, bool interactOnlyWhenRendered = true)
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
        public static void SetRefreshTileHoveredOver<T>(T set, int z)
        {
            Vector2Int tilePosition = Hover();
            Vector3Int positionProper = new Vector3Int(tilePosition.x, tilePosition.y, z);
            Vector3Int previousTilePosition = hoveringTileString.pos;
            if (previousTilePosition == -Vector3Int.one)
                previousTilePosition = hoveringTileColor.pos;

            Type type = typeof(T);
            if (!(type == typeof(string) || type == typeof(Color)))
            {
                Debug.LogError("Tato funkce podporuje jen string a Color!");
                return;
            }
            GameObject obj = Object.Tile(positionProper);
            bool prevMouseOutOfBounds = previousTilePosition == -Vector3Int.one;
            if (obj == null)
            {
                if (!prevMouseOutOfBounds)
                {
                    if (type == typeof(string))
                    {
                        Draw.SetRefresh(hoveringTileString.data, previousTilePosition);
                        hoveringTileString = (-Vector3Int.one, "");
                    }
                    else if (type == typeof(Color))
                    {
                        Draw.SetRefresh(hoveringTileColor.data, previousTilePosition);
                        hoveringTileColor = (-Vector3Int.one, new());
                    }
                }
                return;
            }
            string name = Object.SpriteNameByObject(obj);
            if (previousTilePosition != positionProper)
            {
                if (type == typeof(string))
                {
                    if (!prevMouseOutOfBounds)
                        Draw.SetRefresh(hoveringTileString.data, previousTilePosition);
                    hoveringTileString = (positionProper, name);
                }
                else if (type == typeof(Color))
                {
                    if (!prevMouseOutOfBounds)
                        Draw.SetRefresh(hoveringTileColor.data, previousTilePosition);
                    hoveringTileColor = (positionProper, Data.GetColor(positionProper));
                }

            }
            else
                Draw.SetRefresh(set, positionProper);
        }
    }
    public static class Tiles
    {
        public static Vector3 Position(int x, int y, int z)
        {
            Vector3 resultPosition = new Vector3(gridPosition.x + x * tileSize.x, gridPosition.y - y * tileSize.y, gridPosition.z + z);
            if (positionFixedToCamera.x != 0)
            {
                float totalGridWidth = tileCount.x * tileSize.x;
                float centerOffX = (rightBoundX - leftBoundX - totalGridWidth) * cameraOccupationOffset.x;
                resultPosition.x = leftBoundX + x * tileSize.x + tileSize.x / 2 + centerOffX;
            }
            if (positionFixedToCamera.y != 0)
            {
                float totalGridHeight = tileCount.y * tileSize.y;
                float centerOffY = (upBoundY - downBoundY - totalGridHeight) * cameraOccupationOffset.y;
                resultPosition.y = upBoundY - y * tileSize.y - tileSize.y / 2 - centerOffY;
            }
            if (positionFixedToCamera.z != 0)
                resultPosition.z = z + myCamera.transform.position.z + 10;
            return resultPosition;
        }
        public static Vector2 GetScale()
        {
            Vector2 maxTileUnits = new Vector2(0, 2 * myCamera.orthographicSize * maxOccupiedSpace.y); maxTileUnits.x = maxTileUnits.y * myCamera.aspect / maxOccupiedSpace.y * maxOccupiedSpace.x;
            Vector2 possibleScale = new Vector2(maxTileUnits.x / tileCount.x, maxTileUnits.y / tileCount.y);
            float newScale = 3 * gridScale;
            if (dynamicScaling)
                newScale *= Math.Min(possibleScale.x, possibleScale.y);
            return new Vector2(newScale, newScale) / 0.96f;
        }
        public static void RenderBounds(int zLayer, out int startX, out int startY, out int endX, out int endY)
        {
            Vector3 gridTopLeft = Position(0, 0, zLayer), gridBottomRight = Position(tileCount.x - 1, tileCount.y - 1, zLayer);
            gridTopLeft.x -= tileSize.x / 2; gridTopLeft.y += tileSize.y / 2;
            gridBottomRight.x += tileSize.x / 2; gridBottomRight.y -= tileSize.y / 2;
            Vector3 cameraTopLeft = new Vector3(leftBoundX, upBoundY, zLayer), cameraBottomRight = new Vector3(rightBoundX, downBoundY, zLayer);

            startX = Math.Max(0, (int)Math.Floor((cameraTopLeft.x - gridTopLeft.x) / tileSize.x));
            startY = Math.Max(0, (int)Math.Floor((gridTopLeft.y - cameraTopLeft.y) / tileSize.y));
            endX = Math.Min(tileCount.x, tileCount.x - (int)Math.Floor((gridBottomRight.x - cameraBottomRight.x) / tileSize.x));
            endY = Math.Min(tileCount.y, tileCount.y - (int)Math.Floor((cameraBottomRight.y - gridBottomRight.y) / tileSize.y));
        }
        public static int GetIndex(Vector3Int location)
        {
            if (!ContainedWithinGrid(location))
                return -1;
            return location.y * tileCount.x + location.x + location.z * tileCount.x * tileCount.y;
        }
        public static Vector3Int GetLocation(int index)
        {
            if (index < 0 || index > tileCount.x * tileCount.y * defaultTile.Length)
                return -Vector3Int.one;
            return new(index % tileCount.x, (int)Math.Floor(index / (float)tileCount.x), (int)Math.Floor(index / (float)(tileCount.x * tileCount.y)));
        }
        public static bool ContainedWithinGrid(Vector3Int location)
        {
            return location.x >= 0 && location.y >= 0 && location.z >= 0 && location.x < tileCount.x && location.y < tileCount.y && location.z < defaultTile.Length;
        }
        public static List<Vector3Int> GetNeighbours(Vector3Int location, Neighbours[] neighbouringTilesDefiniton = null)
        {
            if (neighbouringTilesDefiniton == null)
                neighbouringTilesDefiniton = commonNeighboursDefinition;
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
                if (Tiles.ContainedWithinGrid(offsetV3))
                    borders.Add(offsetV3);
            }
            return borders;
        }
        public static int NeighbouringSpriteCount(string spriteName, Vector3Int location)
        {
            int count = 0;
            List<Vector3Int> neighbours = GetNeighbours(location);
            foreach (Vector3Int offsetedLoc in neighbours)
            {
                if (Data.Get(offsetedLoc.x, offsetedLoc.y, location.z) == spriteName)
                    count++;
            }
            return count;
        }
        public static Sprite GetSpriteByName(string spriteName)
        {
            for (int i = 0; i < spriteString.Count; i++)
            {
                if (spriteString[i] == spriteName)
                    return tileSprites[i];
            }
            return null;
        }
        public static bool Obscured(int x, int y, int z)
        {
            if (obscuredTiles.Contains(new(x, y)))
                return true;
            int index = Draw.GetIndexOfSprite(x, y, z - 1);
            if (index != -1 && !containsAlpha[index])
            {
                obscuredTiles.Add(new(x, y));
                return true;
            }
            return false;
        }
        public static bool Opacity(int x, int y, int z)
        {
            if (Data.GetColor(x, y, z).a < 1)
                return false;
            int spriteIndex = Draw.GetIndexOfSprite(x, y, z);
            return spriteIndex != -1 && !containsAlpha[spriteIndex];
        }
    }
    public static class Object
    {
        public static GameObject Tile(int x, int y, int z)
        {
            GameObject[] tiles = All();
            foreach (GameObject obj in tiles)
            {
                if (obj.transform.position == Tiles.Position(x, y, z))
                    return obj;
            }
            return null;
        }
        public static GameObject Tile(Vector3Int location)
        {
            return Tile(location.x, location.y, location.z);
        }
        public static Dictionary<int, GameObject> AllAtPosition(int x, int y)
        {
            GameObject[] tiles = All();
            Dictionary<int, GameObject> result = new();
            foreach (GameObject obj in tiles)
            {
                Vector3 posScan = obj.transform.position, posTile = Tiles.Position(x, y, 0);
                if (new Vector2(posScan.x, posScan.y) == new Vector2(posTile.x, posTile.y))
                {
                    result.Add((int)(posScan.z - posTile.z - 1), obj);
                }
            }
            return result;
        }
        public static GameObject[] All()
        {
            return GameObject.FindGameObjectsWithTag("CloneTile");
        }
        public static string SpriteNameByObject(GameObject obj)
        {
            Sprite spr = obj.GetComponent<SpriteRenderer>().sprite;
            int index = tileSprites.IndexOf(spr);
            if (index == -1)
                return "";
            return spriteString[index];
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
            GS.Draw.All();
        if (GUILayout.Button("Restore Tile Data to Defaults"))
            GS.Clear.AllRender();
        if (GUILayout.Button("Delete Grid"))
            GS.Draw.Delete();
        if (GUILayout.Button("Test Function A"))
        {
            GS.Data.ScatterBeyondNeighbours("mine", 50, 1, new());
        }
    }
}
