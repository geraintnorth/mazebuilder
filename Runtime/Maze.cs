using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

#if UNITY_EDITOR

[ExecuteAlways]
public class Maze : MonoBehaviour, ISerializationCallbackReceiver
{
    /*
      This class represents the properties of a Cell type.
    */
    [Serializable]
    public class CellMaterial 
    {
        // This is the Color used in the editor - not visible at runtime
        public Color color = new Color(1,1,1,1);

        // The height of the cell ceiling.  Must always be >= 0.
        public float ceilingHeight = 3;

        // The height of the cell floor.  Must always be >= 0.
        public float floorHeight = 0;

        // Material to use for the floor.  Floor is not drawn if this material is not set.
        public Material floorMaterial;
        // Material to use for the floor.  Even if not set, will attempt to render
        public Material wallMaterial;

        // Material to use for the ceiling.  Ceiling is not drawn if this material is not set.
        public Material ceilingMaterial;

        public CellMaterial(CellMaterial _cellMaterial)
        {
            color = _cellMaterial.color;
            floorMaterial = _cellMaterial.floorMaterial;
            wallMaterial = _cellMaterial.wallMaterial;
            ceilingMaterial = _cellMaterial.ceilingMaterial;
        }

        public static bool operator == (CellMaterial a, CellMaterial b){
            return a.Equals(b);
        }

        public static bool operator != (CellMaterial a, CellMaterial b){
            return !a.Equals(b);
        }
        
        public override bool Equals(System.Object obj)
        {
            if (obj == null)
                return false;

            CellMaterial c = obj as CellMaterial;
            if ((System.Object)c == null)
                return false;

            return (color == c.color) &&
                   (ceilingHeight == c.ceilingHeight) && 
                   (floorHeight == c.floorHeight) && 
                   (floorMaterial == c.floorMaterial) &&
                   (wallMaterial == c.wallMaterial) &&
                   (ceilingMaterial == c.ceilingMaterial);
        }

        public override int GetHashCode()
        {
            return (color, ceilingHeight, floorHeight, floorMaterial, wallMaterial, ceilingMaterial).GetHashCode();
        }

        public void validate()
        {
            ceilingHeight = Math.Max(ceilingHeight,0f);
            floorHeight = Math.Max(floorHeight,0f);
        }
    }

    /*
      Ideally we keep all the Cell info in a Map that associates an x,y position with
      the index of the cell material to use, but Unity can't serialize Maps.  We
      therefore have to also maintain a of Cells that can be serialized.  This class
      exists to store the mapping in a list that we can serialize.
    */
    [Serializable]
    public  class SerializedCell
    {
        // The coordinates of the cell that we represent
        public Vector2Int pos;

        // The material index to associate with this cell
        public int index;

        public SerializedCell(Vector2Int _pos, int _index)
        {   
            pos = _pos;
            index = _index;
        }
    }

    /*
      This class stores the properties that impact the maze's mesh.  We keep track
      of what these were when we last generated the mesh, so we can tell when we
      need to rebuild the mesh.
    */
    private class InspectorRenderProperties
    {
        // A list of all the materials - if we change materials we need to regenerate
        // the mesh so that it picks them up.
        public List<CellMaterial> cellMaterials = new List<CellMaterial>();

        public InspectorRenderProperties(List<CellMaterial> _cellMaterials)
        {
            cellMaterials = new List<CellMaterial>();
            for (int i = 0; i < _cellMaterials.Count; i++)
            {
                cellMaterials.Add(new CellMaterial(_cellMaterials[i]));
            }
        }

        /*
          Compares the stored properties with the new ones and updates the stored ones.
          Returns true if a change was detected, indicating that we need to regenerate
          the maze mesh. 
        */
        public bool checkAndUpdate(List<CellMaterial> _cellMaterials)
        {
            bool changeDetected = false;

            // Check the list lengths
            if (cellMaterials.Count != _cellMaterials.Count)
            {
                changeDetected = true;
            }
            else
            {
                // Check the list items
                for (int i = 0; i < cellMaterials.Count; i++)
                {
                    if (cellMaterials[i] != _cellMaterials[i])
                    {
                        changeDetected = true;
                    }
                }
            }

            if (changeDetected)
            {
                // If there was a change, copy the new stuff across.
                cellMaterials = new List<CellMaterial>();
                for (int i = 0; i < _cellMaterials.Count; i++)
                {
                    cellMaterials.Add(new CellMaterial(_cellMaterials[i]));
                }
            }
            
            return changeDetected;
        }
    }


    // The editable region doesn't have to span the whole maze - we store the region
    // with a 'start' vector, and a 'span' vector.  The span vector must be positive in
    // both directions.
    public Vector2Int start { get {return m_start;} set {m_start = value;}}
    [SerializeField] private Vector2Int m_start = new Vector2Int(-10,-10);
    
    public float maxHeight { get { return 3;}}

    public Vector2Int span { get {return m_span;} set
        {
            if (value.x > 0) { m_span.x = value.x;}
            if (value.y > 0) { m_span.y = value.y;}
        }
    }
    [SerializeField] private Vector2Int m_span = new Vector2Int(20,20);

    // The list of CellMaterial objects.  The index in the cells Dictionary tells us the index
    // into this list of each cell.
    [SerializeField] public List<CellMaterial> cellMaterials = new List<CellMaterial>();

    // This is the main structure that we use to track which cells are in use and which material
    // each one uses.
    [NonSerialized] private Dictionary<UnityEngine.Vector2Int,int> cells = new Dictionary<UnityEngine.Vector2Int,int>();

    // Although it's better to use a Dictionary to store cell data, we can't serialize Dictionaries,
    // so we have to maintain a list of the same data.  We generate this list from the Dictionary
    // whenever we need to serialize, and we read it back into the Dictionary when we deserialize.
    [SerializeField] [HideInInspector] private List<SerializedCell> serializedCells = new List<SerializedCell>();

    // Whenever we add, remove or modify something in the map, we set this flag so that we know to
    // regenerate the serializable cell list.
    private bool regenerateCellList = false;

    private InspectorRenderProperties renderProperties;

    /*
      Ensures that the elements of span are always positive integers and that the
      height is > 1. 
    */
    void OnValidate()
    {
        m_span.x = Math.Max(m_span.x, 1);
        m_span.y = Math.Max(m_span.y, 1);

        foreach(CellMaterial mat in cellMaterials)
        {
            mat.validate();
        }

        if (renderProperties != null)
        {
            if (renderProperties.checkAndUpdate(cellMaterials))
            {
                needMeshRegen = true;
            }
        }
    }

    private Mesh mesh;
    private bool needMeshRegen = false;

    public bool containsCell(Vector2Int key)
    {
        return cells.ContainsKey(key);
    }

    public bool containsValidCell(Vector2Int key)
    {
        return cells.ContainsKey(key) && cells[key] < cellMaterials.Count;
    } 

    public int getCell(Vector2Int key)
    {
        return cells[key];
    }

    public void setCell(Vector2Int key, int materialIndex)
    {
        EditorUtility.SetDirty(this);
        Undo.RecordObject(this, "modification of cell (" + key.x + "," + key.y + ")");
        needMeshRegen = true;
        regenerateCellList = true;
        cells[key] = materialIndex;
    }

    public void removeCell(Vector2Int key)
    {
        EditorUtility.SetDirty(this);;
        Undo.RecordObject(this, "modification of cell (" + key.x + "," + key.y + ")");
        needMeshRegen = true;
        regenerateCellList = true;
        cells.Remove(key);
    }

    void Start()
    {
        renderProperties = new InspectorRenderProperties(cellMaterials);
    }

    // Update is called once per frame

    void Update()
    {
        if (needMeshRegen)
        {
            needMeshRegen = false;
            regenerateMesh();
        }
    }

    private float getCellHeightOrZero(int x, int y, bool isCeiling)
    {   
        Vector2Int key = new Vector2Int(x,y);
        if (containsValidCell(key))
        {
            int index = getCell(key);
            if (index < cellMaterials.Count)
            {
                return isCeiling ? cellMaterials[index].ceilingHeight : cellMaterials[index].floorHeight;
            }
            else
            {
                // Cell points to an undefined index, we won't render it.
                return 0;
            }
        }
        else
        {
            return 0f;
        }
    }
    /*
      Creates a new mesh, called in Update if something has flagged that the mesh
      needs to be regenerated (i.e. the user changed something).
    */
    private void regenerateMesh()
    {
        syncMaterials();
        mesh = new Mesh();
        

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        // Three submeshes for each cell type, to match the materials.
        // Order is floor, wall and ceiling.
        List<int>[] triangles = new List<int>[cellMaterials.Count*3];
        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] = new List<int>();
        }

        foreach(KeyValuePair<Vector2Int, int> p in cells)
        {
            // Only generate if this cell references a valid material - cells can be out-of-bounds
            // if the user deletes an entry from the cellMaterials list that was in use. 
            if (p.Value < cellMaterials.Count)
            {
                CellMaterial mat = cellMaterials[p.Value];

                // Only generate the floor if there's a material for it.
                if (mat.floorMaterial != null)
                {
                    int floorIndex = p.Value * 3;
                    makeFloorOrCeiling(vertices, uvs, triangles[floorIndex], 
                                    new Vector2(p.Key.x, p.Key.y),     new Vector2(p.Key.x, p.Key.y+1),
                                    new Vector2(p.Key.x+1, p.Key.y+1), new Vector2(p.Key.x+1, p.Key.y), mat.floorHeight);
                }

                // Only generate walls or ceiling if the hight is set.
                if (mat.ceilingHeight > 0)
                {
                    // Only generate the ceiling if there's a material for it.
                    if (mat.ceilingMaterial != null)
                    {
                        int ceilingIndex = p.Value * 3 + 2;
                        makeFloorOrCeiling(vertices, uvs, triangles[ceilingIndex], 
                                        new Vector2(p.Key.x+1, p.Key.y+1), new Vector2(p.Key.x, p.Key.y+1),
                                        new Vector2(p.Key.x, p.Key.y),     new Vector2(p.Key.x+1, p.Key.y), mat.ceilingHeight);
                    }
                }
                // Generate walls to the floors

                // East wall
                makeWall(vertices, uvs, triangles[p.Value*3+1], p.Key.x,p.Key.y,p.Key.x,p.Key.y+1,
                         mat.floorHeight, getCellHeightOrZero(p.Key.x-1, p.Key.y, false) );

                // North wall
                makeWall(vertices, uvs, triangles[p.Value*3+1], p.Key.x,p.Key.y+1,p.Key.x+1,p.Key.y+1,
                         mat.floorHeight, getCellHeightOrZero(p.Key.x, p.Key.y+1, false));

                // East wall
                makeWall(vertices, uvs, triangles[p.Value*3+1], p.Key.x+1,p.Key.y+1,p.Key.x+1,p.Key.y,
                         mat.floorHeight, getCellHeightOrZero(p.Key.x+1, p.Key.y, false));

                // South wall
                makeWall(vertices, uvs, triangles[p.Value*3+1], p.Key.x+1,p.Key.y,p.Key.x,p.Key.y,
                         mat.floorHeight, getCellHeightOrZero(p.Key.x, p.Key.y-1, false));


                // Generate walls to the ceilings

                // East wall
                makeWall(vertices, uvs, triangles[p.Value*3+1], p.Key.x,p.Key.y,p.Key.x,p.Key.y+1,
                         getCellHeightOrZero(p.Key.x-1, p.Key.y, true), mat.ceilingHeight);

                // North wall
                makeWall(vertices, uvs, triangles[p.Value*3+1], p.Key.x,p.Key.y+1,p.Key.x+1,p.Key.y+1,
                         getCellHeightOrZero(p.Key.x, p.Key.y+1, true), mat.ceilingHeight);

                // East wall
                makeWall(vertices, uvs, triangles[p.Value*3+1], p.Key.x+1,p.Key.y+1,p.Key.x+1,p.Key.y,
                         getCellHeightOrZero(p.Key.x+1, p.Key.y, true), mat.ceilingHeight);

                // South wall
                makeWall(vertices, uvs, triangles[p.Value*3+1], p.Key.x+1,p.Key.y,p.Key.x,p.Key.y,
                         getCellHeightOrZero(p.Key.x, p.Key.y-1, true), mat.ceilingHeight);
            }
        } 

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = triangles.Length;
        
        // Generate each submesh
        for (int i = 0; i < triangles.Length; i++)
        {
            mesh.SetTriangles(triangles[i], i);
        }

        mesh.RecalculateNormals();
        GetComponentInParent<MeshFilter>().sharedMesh = mesh;
        GetComponentInParent<MeshCollider>().sharedMesh = mesh;

        // Generate secondary UVs for lightmaps.
        if (mesh.vertexCount > 0 )
        {
            Unwrapping.GenerateSecondaryUVSet(mesh);
        }
    }


    /*
      Generates a square from four 2D points on the X-Z plane, with a height indicating the Y values.
      Wind the vertices clockwise in the direction you want it to be visible from.
      It updates the passed-in list of vertices, UVs and triangles.
    */
    private void makeFloorOrCeiling(List<Vector3> vertices, List<Vector2> uvs, List<int>triangles,
                                    Vector2 a, Vector2 b, Vector2 c, Vector2 d, float height)
    {
        int vindex = vertices.Count;
        vertices.Add( new Vector3(a.x, height, a.y));
        vertices.Add( new Vector3(b.x, height, b.y));
        vertices.Add( new Vector3(c.x, height, c.y));
        vertices.Add( new Vector3(d.x, height, d.y));

        uvs.Add(new Vector2(0,0));  
        uvs.Add(new Vector2(0,1));
        uvs.Add(new Vector2(1,1));
        uvs.Add(new Vector2(1,0));
        
        triangles.Add(vindex);
        triangles.Add(vindex+1);
        triangles.Add(vindex+2);
        triangles.Add(vindex);
        triangles.Add(vindex+2);
        triangles.Add(vindex+3);
    }

    private void makeWall(List<Vector3> vertices, List<Vector2> uvs, List<int>triangles, float x1, float y1, float x2, float y2,
                          float other_y, float this_y)
    {
        if (other_y < this_y)
        {
            int vindex = vertices.Count;
            vertices.Add( new Vector3(x1, other_y, y1));
            vertices.Add( new Vector3(x1, this_y, y1));
            vertices.Add( new Vector3(x2, this_y, y2));
            vertices.Add( new Vector3(x2, other_y, y2));

            uvs.Add(new Vector2(0,other_y));
            uvs.Add(new Vector2(0,this_y));
            uvs.Add(new Vector2(1,this_y));
            uvs.Add(new Vector2(1,other_y));
            
            triangles.Add(vindex);
            triangles.Add(vindex+1);
            triangles.Add(vindex+2);
            triangles.Add(vindex);
            triangles.Add(vindex+2);
            triangles.Add(vindex+3);
        }
    }

    private void syncMaterials()
    {
        // Each texture type has three materials: floor, wall and ceiling.

        Material[] newMaterials = new Material[cellMaterials.Count*3];
        for (int i = 0; i < cellMaterials.Count; i++)
        {
            newMaterials[i*3] = cellMaterials[i].floorMaterial;
            newMaterials[i*3+1] = cellMaterials[i].wallMaterial;
            newMaterials[i*3+2] = cellMaterials[i].ceilingMaterial;
        }

        GetComponentInParent<MeshRenderer>().SetMaterials( new List<Material>(newMaterials));
    }

    public void OnBeforeSerialize()
    {
        if (regenerateCellList)
        {
            regenerateCellList = false;
            serializedCells = new List<SerializedCell>();

            foreach(KeyValuePair<Vector2Int, int> p in cells)
            {
                SerializedCell newSerialCell = new SerializedCell(p.Key, p.Value);
                serializedCells.Add(newSerialCell);
            }
        }
    }

    public void OnAfterDeserialize()
    {
        cells = new Dictionary<Vector2Int, int>();
        foreach(SerializedCell cell in serializedCells)
        {
            Vector2Int key = cell.pos;
            cells[key] = cell.index;
        }
        needMeshRegen = true;
    }
}
#else
class Maze : MonoBehaviour
{

}
#endif