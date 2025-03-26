using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

[ExecuteAlways]
public class Maze : MonoBehaviour, ISerializationCallbackReceiver
{
    [Serializable]
    public class CellMaterial 
    {
        public Color color = new Color(1,1,1,1);
        public Material floorMaterial;
        public Material wallMaterial;
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
                   (floorMaterial == c.floorMaterial) &&
                   (wallMaterial == c.wallMaterial) &&
                   (ceilingMaterial == c.ceilingMaterial);
        }

        public override int GetHashCode()
        {
            return (color, floorMaterial, wallMaterial, ceilingMaterial).GetHashCode();
        }
    }

    [Serializable]
    public  class SerializedCell
    {
        public Vector2Int pos;

        public int index;

        public SerializedCell(Vector2Int _pos, int _index)
        {   
            pos = _pos;
            index = _index;
        }
    }

    private class InspectorRenderProperties
    {
        // These are things that are modified in the inspector that impact
        // the render.  We keep track of what we used at the last render
        // so that we can detect when anything changes that requires a
        // re-render.

        public float height;
        public List<CellMaterial> cellMaterials = new List<CellMaterial>();

        public InspectorRenderProperties(float _height, List<CellMaterial> _cellMaterials)
        {
            height = _height;
            cellMaterials = new List<CellMaterial>();
            for (int i = 0; i < _cellMaterials.Count; i++)
            {
                cellMaterials.Add(new CellMaterial(_cellMaterials[i]));
            }
        }

        public bool checkAndUpdate(float _height, List<CellMaterial> _cellMaterials)
        {
            bool changeDetected = false;

            // Check the height
            if (height != _height)
            {
                changeDetected = true;
            }

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
                    Debug.Log("Iterating " + i);
                    if (cellMaterials[i] != _cellMaterials[i])
                    {
                        changeDetected = true;
                    }
                }
            }

            if (changeDetected)
            {
                height = _height;
                cellMaterials = new List<CellMaterial>();
                for (int i = 0; i < _cellMaterials.Count; i++)
                {
                    cellMaterials.Add(new CellMaterial(_cellMaterials[i]));
                }
            }
            
            return changeDetected;
        }
    }


    public Vector2Int start { get { return m_start; } set { m_start = value;}}
    [SerializeField] private Vector2Int m_start = new Vector2Int(-10,-10);
    
    public Vector2Int span { get { return m_span; } set
        {
            if (value.x > 0) { m_span.x = value.x;}
            if (value.y > 0) { m_span.y = value.y;}
        }
    }

    [SerializeField] private Vector2Int m_span = new Vector2Int(20,20);

    public float height { get { return m_height; } set { if (value>0) { m_height = value;}}}
    [SerializeField] private float m_height = 3;

    [SerializeField] public List<CellMaterial> cellMaterials = new List<CellMaterial>();

    [NonSerialized] private Dictionary<UnityEngine.Vector2Int,int> cells = new Dictionary<UnityEngine.Vector2Int,int>();

    [SerializeField] private List<SerializedCell> serializedCells = new List<SerializedCell>();
    private bool regenerateCellList = false;

    private InspectorRenderProperties renderProperties;

    void OnValidate()
    {
        m_span.x = Math.Max(m_span.x, 1);
        m_span.y = Math.Max(m_span.y, 1);
        m_height = Math.Max(m_height,0);

        if (renderProperties != null)
        {
            if (renderProperties.checkAndUpdate(height, cellMaterials))
            {
                Debug.Log("Maze has changed");
                needMeshRegen = true;
            }
        }
    }

    private Mesh mesh;
    private bool needMeshRegen = true;

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
        renderProperties = new InspectorRenderProperties(height, cellMaterials);
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
            if (p.Value < cellMaterials.Count)
            {
                // Generate the Floor
                int floorIndex = p.Value * 3;
                makeFloorOrCeiling(vertices, uvs, triangles[floorIndex], 
                                new Vector2(p.Key.x, p.Key.y),     new Vector2(p.Key.x, p.Key.y+1),
                                new Vector2(p.Key.x+1, p.Key.y+1), new Vector2(p.Key.x+1, p.Key.y), 0f);

                if (height > 0)
                {
                    // Generate the Ceiling
                    int ceilingIndex = p.Value * 3 + 2;
                    makeFloorOrCeiling(vertices, uvs, triangles[ceilingIndex], 
                                    new Vector2(p.Key.x+1, p.Key.y+1), new Vector2(p.Key.x, p.Key.y+1),
                                    new Vector2(p.Key.x, p.Key.y),     new Vector2(p.Key.x+1, p.Key.y), height);

                    // East wall
                    if (!containsValidCell(new Vector2Int(p.Key.x-1, p.Key.y)))
                    {
                        makeWall(vertices, uvs, triangles[p.Value*3+1], p.Key.x,p.Key.y,p.Key.x,p.Key.y+1);
                    }

                    // North wall
                    if (!containsValidCell(new Vector2Int(p.Key.x, p.Key.y+1)))
                    {
                        makeWall(vertices, uvs, triangles[p.Value*3+1], p.Key.x,p.Key.y+1,p.Key.x+1,p.Key.y+1);
                    }

                    // East wall
                    if (!containsValidCell(new Vector2Int(p.Key.x+1, p.Key.y)))
                    {
                        makeWall(vertices, uvs, triangles[p.Value*3+1], p.Key.x+1,p.Key.y+1,p.Key.x+1,p.Key.y);
                    }

                    // South wall
                    if (!containsValidCell(new Vector2Int(p.Key.x, p.Key.y-1)))
                    {
                        makeWall(vertices, uvs, triangles[p.Value*3+1], p.Key.x+1,p.Key.y,p.Key.x,p.Key.y);
                    }
                }
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
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
        if (mesh.vertexCount > 0 )
        {
            Unwrapping.GenerateSecondaryUVSet(mesh);
        }
    }

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

    private void makeWall(List<Vector3> vertices, List<Vector2> uvs, List<int>triangles, float x1, float y1, float x2, float y2)
    {
        int vindex = vertices.Count;
        vertices.Add( new Vector3(x1, 0, y1));
        vertices.Add( new Vector3(x1, height, y1));
        vertices.Add( new Vector3(x2, height, y2));
        vertices.Add( new Vector3(x2, 0, y2));

        uvs.Add(new Vector2(0,0));
        uvs.Add(new Vector2(0,height));
        uvs.Add(new Vector2(1,height));
        uvs.Add(new Vector2(1,0));
        
        triangles.Add(vindex);
        triangles.Add(vindex+1);
        triangles.Add(vindex+2);
        triangles.Add(vindex);
        triangles.Add(vindex+2);
        triangles.Add(vindex+3);
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

        GetComponent<MeshRenderer>().SetMaterials( new List<Material>(newMaterials));
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
