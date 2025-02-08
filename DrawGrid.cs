using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;
using static UnityEditor.FilePathAttribute;

public class DrawGrid : MonoBehaviour
{
    [SerializeField] private Camera myCamera;
    [SerializeField] private GameObject tile;

    public Vector2Int tileCount;
    public Vector2 cameraOccupationOffset;
    public Vector2 maxOccupiedSpace;
    public float gridScale;

    public Vector3 gridPosition;
    public Vector3 positionFixedToCamera;

    [SerializeField] private Sprite[] defaultCell;
    [SerializeField] public List<Sprite> tileSprites;

    public byte alphaThreshold;
    public bool renderObscuredTiles;
    public bool dynamicScaling;
    public bool frustumCulling;
    public bool alwaysUpdate;

    private Dictionary<Vector3Int, string> grid = new();
    private Dictionary<Vector3Int, Color> gridColors = new();
    private List<string> spriteString = new();

    private (Vector3Int, string, string) hoveringTile = (-Vector3Int.one, "", "");
    private float leftBoundX, rightBoundX, upBoundY, downBoundY;
    private List<bool> containsAlpha = new();
    private List<List<bool>> obscuredTiles = new();
    private float aspectRatio = 0;
    private Vector2 tileSize;

    private void Awake()
    {
        RefreshSprites();
        UpdateCameraValues();
        tileSize = tile.GetComponent<SpriteRenderer>().bounds.size;
    }
    private void Update()
    {
        if (alwaysUpdate)
        {
            RefreshSprites();
            UpdateCameraValues();
            Render();
        }
    }
    private void RefreshSprites()
    {
        foreach (Sprite spr in defaultCell)
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
                containsAlpha.Add(ContainsTransparentPixel(spr));
        }
    }
    private bool ContainsTransparentPixel(Sprite spr)
    {
        if (spr == null)
            return true;
        Color32[] pixels = spr.texture.GetPixels32();
        foreach (Color32 pixel in pixels)
        {
            if (pixel.a <= alphaThreshold)
                return true;
        }
        return false;
    }
    private void RenderGrid(int z)
    {
        Vector2 tileScale = GetTileScale();
        tile.transform.localScale = new Vector3(tileScale.x, tileScale.y, 1);
        tileSize = tile.GetComponent<SpriteRenderer>().bounds.size;
        int startX = 0, endX = tileCount.x, startY = 0, endY = tileCount.y;
        if (frustumCulling)
            RenderableCellsForFrustumCulling(z, out startX, out startY, out endX, out endY);
        for (int i = startX; i < endX; i++)
        {
            if (z == 0)
                obscuredTiles.Add(new());
            for (int j = startY; j < endY; j++)
            {
                if (z == 0)
                    obscuredTiles.Last().Add(false);
                if (renderObscuredTiles || z == 0 || !IsTileObscured(i, j, z))
                    DrawCell(i, j, z, tileScale);
            }
        }
    }
    private Vector2 GetTileScale()
    {
        Vector2 maxTileUnits = new Vector2(0, 2 * myCamera.orthographicSize * maxOccupiedSpace.y); maxTileUnits.x = maxTileUnits.y * myCamera.aspect / maxOccupiedSpace.y * maxOccupiedSpace.x;
        Vector2 possibleScale = new Vector2(maxTileUnits.x / tileCount.x, maxTileUnits.y / tileCount.y);
        float newScale = 3 * gridScale;
        if (dynamicScaling)
            newScale *= Math.Min(possibleScale.x, possibleScale.y);
        return new Vector2(newScale, newScale) / 0.96f;
    }
    private bool IsTileObscured(int x, int y, int z)
    {
        if (obscuredTiles[x][y])
            return true;
        int index = GetIndexOfSprite(x, y, z-1);
        if (index != -1 && !containsAlpha[index])
        {
            obscuredTiles[x][y] = true;
            return true;
        }
        return false;
    }
    private void RenderableCellsForFrustumCulling(int zLayer, out int startX, out int startY, out int endX, out int endY)
    {
        Vector3 gridTopLeft = CalculateCellPosition(0, 0, zLayer), gridBottomRight = CalculateCellPosition(tileCount.x - 1, tileCount.y - 1, zLayer);
        gridTopLeft.x -= tileSize.x / 2; gridTopLeft.y += tileSize.y / 2;
        gridBottomRight.x += tileSize.x / 2; gridBottomRight.y -= tileSize.y / 2;
        Vector3 cameraTopLeft = new Vector3(leftBoundX, upBoundY, zLayer), cameraBottomRight = new Vector3(rightBoundX, downBoundY, zLayer);

        startX = Math.Max(0, (int)Math.Floor((cameraTopLeft.x - gridTopLeft.x) / tileSize.x));
        startY = Math.Max(0, (int)Math.Floor((gridTopLeft.y - cameraTopLeft.y) / tileSize.y));
        endX = Math.Min(tileCount.x, tileCount.x - (int)Math.Floor((gridBottomRight.x - cameraBottomRight.x) / tileSize.x));
        endY = Math.Min(tileCount.y, tileCount.y - (int)Math.Floor((cameraBottomRight.y - gridBottomRight.y) / tileSize.y));
    }
    private Vector3 CalculateCellPosition(int i, int j, int z)
    {
        Vector3 resultPosition = new Vector3(gridPosition.x + i * tileSize.x, gridPosition.y - j * tileSize.y, gridPosition.z + z);
        if (positionFixedToCamera.x != 0)
        {
            float totalGridWidth = tileCount.x * tileSize.x;
            float centerOffX = (rightBoundX - leftBoundX - totalGridWidth) * cameraOccupationOffset.x;
            resultPosition.x = leftBoundX + i * tileSize.x + tileSize.x / 2 + centerOffX;
        }
        if (positionFixedToCamera.y != 0)
        {
            float totalGridHeight = tileCount.y * tileSize.y;
            float centerOffY = (upBoundY - downBoundY - totalGridHeight) * cameraOccupationOffset.y;
            resultPosition.y = upBoundY - j * tileSize.y - tileSize.y / 2 - centerOffY;
        }
        if (positionFixedToCamera.z != 0)
            resultPosition.z = z + myCamera.transform.position.z + 10;
        return resultPosition;
    }
    private int GetIndexOfSprite(int x, int y, int z)
    {
        return spriteString.IndexOf(Get(x, y, z));
    }
    private void UpdateCameraValues()
    {
        Vector3 cameraPos = myCamera.transform.position;
        float orthSize = myCamera.orthographicSize;
        aspectRatio = myCamera.aspect;
        leftBoundX = cameraPos.x - orthSize * aspectRatio; rightBoundX = cameraPos.x + orthSize * aspectRatio;
        upBoundY = cameraPos.y + orthSize; downBoundY = cameraPos.y - orthSize;
    }
    private void HandleTileColors(int x, int y, int z, SpriteRenderer renderer)
    {
        Vector3Int location = new(x, y, z);
        try
        {
            renderer.color = gridColors[location];
        } catch { }
    }
    public void DrawCell(int x, int y, int z, Vector2 tileScale)
    {
        Vector3Int tileGridPosition = new(x, y, z);
        bool containsKey = grid.ContainsKey(tileGridPosition);
        if (containsKey || defaultCell.Length > 0)
        {
            Sprite tileSprite = defaultCell[z];
            if (grid.ContainsKey(tileGridPosition))
                tileSprite = GetSpriteByName(grid[tileGridPosition]);
            if (tileSprite != null)
            {
                GameObject clonedTile = Instantiate(tile);
                clonedTile.transform.position = CalculateCellPosition(x, y, z);
                clonedTile.transform.localScale = new Vector3(tileScale.x, tileScale.y, 1);
                clonedTile.tag = "CloneTile";

                SpriteRenderer renderer = clonedTile.GetComponent<SpriteRenderer>();
                renderer.sprite = tileSprite;
                HandleTileColors(x, y, z, renderer);
            }
        }
    }
    public void DrawCell(int x, int y, int z)
    {
        DrawCell(x, y, z, GetTileScale());
    }
    public Sprite GetSpriteByName(string spriteName)
    {
        for (int i = 0; i < spriteString.Count; i++)
        {
            if (spriteString[i] == spriteName)
                return tileSprites[i];
        }
        return null;
    }
    public void Set(string set, int x, int y, int z)
    {
        grid[new(x, y, z)] = set;
    }
    public string Get(int x, int y, int z)
    {
        int length = defaultCell[z].ToString().LastIndexOf("_0");
        if (length < 0)
            length = 0;
        return grid.ContainsKey(new(x, y, z)) ? grid[new(x, y, z)] : defaultCell[z].ToString().Substring(0, length);
    }
    public void Default(int x, int y, int z)
    {
        grid.Remove(new(x, y, z));
    }
    public void Clear()
    {
        grid.Clear();
    }
    public void ClearLayer(int layer)
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
    public void ClearRender()
    {
        Clear();
        Render();
    }
    public void ClearLayerRender(int layer)
    {
        ClearLayer(layer);
        Render();
    }
    public void Render()
    {
        RefreshSprites();
        Delete();
        obscuredTiles.RemoveAll(i => true);
        if (maxOccupiedSpace.x > 0 && maxOccupiedSpace.y > 0 && tileCount.x > 0 && tileCount.y > 0)
        {
            for (int z = 0; z < defaultCell.Length; z++)
                RenderGrid(z);
        }
    }
    public void Delete()
    {
        GameObject[] cloneTiles = GetAllTileGameObjects();
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
            Destroy(GetTileGameObject(x, y, z));
        }
        catch { }
    }
    public void DeleteTile(int x, int y, int z)
    {
        DeleteTileKeepData(x, y, z);
        Default(x, y, z);
    }
    public GameObject GetTileGameObject(int x, int y, int z)
    {
        GameObject[] tiles = GetAllTileGameObjects();
        foreach (GameObject obj in tiles)
        {
            if (obj.transform.position == CalculateCellPosition(x, y, z))
                return obj;
        }
        return null;
    }
    public GameObject GetTileGameObject(Vector3Int location)
    {
        return GetTileGameObject(location.x, location.y, location.z);
    }
    public Dictionary<int, GameObject> GetLayerOfGameObjects(int x, int y)
    {
        GameObject[] tiles = GetAllTileGameObjects();
        Dictionary<int, GameObject> result = new();
        foreach (GameObject obj in tiles)
        {
            Vector3 posScan = obj.transform.position, posTile = CalculateCellPosition(x, y, 0);
            if (new Vector2(posScan.x, posScan.y) == new Vector2(posTile.x, posTile.y))
            {
                result.Add((int)(posScan.z - posTile.z - 1), obj);
            }
        }
        return result;
    }
    public GameObject[] GetAllTileGameObjects()
    {
        return GameObject.FindGameObjectsWithTag("CloneTile");
    }
    public bool CheckTileTransparency(int x, int y, int z)
    {
        int spriteIndex = GetIndexOfSprite(x, y, z);
        return spriteIndex != -1 && !containsAlpha[spriteIndex];
    }
    public void SetSprite(string set, int x, int y, int z)
    {
        Set(set, x, y, z);
        GameObject tile = GetTileGameObject(x, y, z);
        if (tile != null)
        {
            Sprite tileSprite = GetSpriteByName(set);
            if (tileSprite != null)
            {
                SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
                renderer.sprite = tileSprite;
                HandleTileColors(x, y, z, renderer);
            }
            else
                Destroy(tile);
        }
        else
            DrawCell(x, y, z);
    }
    public void RefreshTile(int x, int y, int z)
    {
        if (!renderObscuredTiles)
        {
            z++;
            if (defaultCell.Length > z)
            {
                if (!CheckTileTransparency(x, y, z - 1))
                {
                    Dictionary<int, GameObject> layerGameObj = GetLayerOfGameObjects(x, y);
                    DeleteTileKeepData(x, y, z);
                    while (z < defaultCell.Length)
                    {
                        if (CheckTileTransparency(x, y, z - 1))
                            break;
                        if (!layerGameObj.ContainsKey(z))
                            DrawCell(x, y, z);
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
                    while (z < defaultCell.Length)
                    {
                        DeleteTileKeepData(x, y, z);
                        z++;
                    }
                }
            }
        }
    }
    public void SetRefresh(string set, int x, int y, int z)
    {
        if (Get(x, y, z) != set)
        {
            SetSprite(set, x, y, z);
            RefreshTile(x, y, z);
        }        
    }
    public void SetRefresh(string set, Vector3Int location)
    {
        SetRefresh(set, location.x, location.y, location.z);
    }
    public bool IsContainedWithinGrid(Vector3Int location)
    {
        return location.x >= 0 && location.y >= 0 && location.z >= 0 && location.x < tileCount.x && location.y < tileCount.y && location.z < defaultCell.Length;
    }
    public Vector2Int Hover()
    {
        Vector2 topLeftMostTile = CalculateCellPosition(0, 0, 0); topLeftMostTile.x -= tileSize.x / 2; topLeftMostTile.y += tileSize.y / 2;
        Vector2 mousePosition = myCamera.ScreenToWorldPoint(Input.mousePosition);
        if (mousePosition.x < topLeftMostTile.x)
            topLeftMostTile.x++;
        if (mousePosition.y > topLeftMostTile.y)
            topLeftMostTile.y--;

        return new((int)((mousePosition.x - topLeftMostTile.x) / tileSize.x), (int)((topLeftMostTile.y - mousePosition.y) / tileSize.y));
    }
    public void GeneralInteraction(string set, int z, bool interacting, bool interactOnlyWhenRendered = true)
    {
        Vector2Int hoveredTile = Hover();
        if ((!interactOnlyWhenRendered || GetTileGameObject(hoveredTile.x, hoveredTile.y, z) != null) && IsContainedWithinGrid(new(hoveredTile.x, hoveredTile.y, z)) && interacting)
            SetRefresh(set, hoveredTile.x, hoveredTile.y, z);
    }
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
    public void MouseInteraction(string set, int z, Interactions interaction = Interactions.Click, Button button = Button.Left, bool interactOnlyWhenRendered = true)
    {
        switch (interaction)
        {
            case Interactions.Hover:
                GeneralInteraction(set, z, true, interactOnlyWhenRendered); break;
            case Interactions.Click:
                GeneralInteraction(set, z, Input.GetMouseButtonDown((int)button), interactOnlyWhenRendered); break;
            case Interactions.Hold:
                GeneralInteraction(set, z, Input.GetMouseButton((int)button), interactOnlyWhenRendered); break;
            case Interactions.Release:
                GeneralInteraction(set, z, Input.GetMouseButtonUp((int)button), interactOnlyWhenRendered); break;
        }
    }
    public void RefreshSetTileHoveredOver(string set, int z)
    {
        Vector2Int tilePosition = Hover();
        Vector3Int positionProper = new Vector3Int(tilePosition.x, tilePosition.y, z);
        Vector3Int previousTilePosition = hoveringTile.Item1;

        GameObject obj = GetTileGameObject(positionProper);
        bool prevMouseOutOfBounds = previousTilePosition == -Vector3Int.one;
        if (obj == null)
        {
            if (!prevMouseOutOfBounds)
            {
                SetRefresh(hoveringTile.Item2, previousTilePosition);
                hoveringTile = (-Vector3Int.one, "", "");
            }
            return;
        }
        string name = GetNameByObject(obj);
        if (previousTilePosition != positionProper)
        {
            if (!prevMouseOutOfBounds)
                SetRefresh(name, previousTilePosition);
            hoveringTile = (positionProper, name, set);
        } else
            SetRefresh(set, positionProper);
    }
    public string GetNameByObject(GameObject obj)
    {
        Sprite spr = obj.GetComponent<SpriteRenderer>().sprite;
        int index = tileSprites.IndexOf(spr);
        if (index == -1)
            return "";
        return spriteString[index];
    }
    public void SetColor(int x, int y, int z, Color color)
    {
        Vector3Int location = new(x, y, z);
        if (gridColors.ContainsKey(location))
            gridColors[location] = color;
        else
            gridColors.Add(location, color);
    }
    public Color GetColor(int x, int y, int z)
    {
        Vector3Int location = new(x, y, z);
        if (gridColors.ContainsKey(location))
            return gridColors[location];
        else
            return new(1, 1, 1);
    }
    public void ClearColor()
    {
        gridColors.Clear();
    }
    public void ClearColorRender()
    {
        ClearColor();
        Render();
    }
    public void SetSpriteColors(int x, int y, int z, Color color)
    {
        SetColor(x, y, z, color);
        HandleTileColors(x, y, z, GetTileGameObject(x, y, z).GetComponent<SpriteRenderer>());
    }
}
[CustomEditor(typeof(DrawGrid))]
public class ButtonDrawGrid : Editor
{
    public override void OnInspectorGUI()
    {
        DrawGrid wantedClass = (DrawGrid)target;
        base.OnInspectorGUI();
        if (GUILayout.Button("Update Grid"))
            wantedClass.Render();
        if (GUILayout.Button("Restore Tile Data to Defaults"))
            wantedClass.ClearRender();
        if (GUILayout.Button("Delete Grid"))
            wantedClass.Delete();
        if (GUILayout.Button("Test Function A"))
        {
            wantedClass.SetSpriteColors(0, 0, 0, new(0, 1, 1));
        }
        if (GUILayout.Button("Test Function B"))
        {
            wantedClass.ClearColorRender();
        }
    }
}
