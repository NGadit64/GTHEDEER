using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AreaData
{
    public int row;
    public int col;

    public bool exists = true;

    public bool isGenerated = false;

    public int backgroundIndex;

    public List<Vector2> treePositions = new List<Vector2>();

    [System.NonSerialized] public GameObject areaObject;
}