using UnityEngine;
using UnityEditor;
using System;

[CustomEditor(typeof(Maze))]
public class MazeEditor : Editor {
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        
    }

    static private Mesh floorPlate = null;
    static private Mesh invalidFloorPlate = null;

    private static void ensureFloorMeshes()
    {
        if (floorPlate == null)
        {
            floorPlate = AssetDatabase.LoadAssetAtPath<Mesh>("Packages/com.geraintnorth.mazebuilder/Meshes/FloorPlate.obj");
        }
        if (invalidFloorPlate == null)
        {
            invalidFloorPlate = AssetDatabase.LoadAssetAtPath<Mesh>("Packages/com.geraintnorth.mazebuilder/Meshes/InvalidFloorPlate.obj");
        }
    }

    private void doLeftSlider(Maze maze)
    {
        Vector3 startXpos = new Vector3(maze.startX, 0, maze.startY + (float)maze.spanY/2);
        float handleSize = HandleUtility.GetHandleSize(maze.transform.TransformPoint(startXpos));

        Vector3 startXdelta = Handles.Slider(maze.transform.TransformPoint(startXpos),
                                             maze.transform.TransformDirection(Vector3.left),
                                             handleSize, Handles.ArrowHandleCap, 1f);
        startXdelta = maze.transform.InverseTransformPoint(startXdelta);

        if (startXdelta != startXpos)
        {
            int delta = (int)Math.Floor(startXdelta.x - startXpos.x);
            if ( delta != 0 && (((int)maze.spanX - delta) > 0) )
            {
                maze.startX += delta;
                maze.spanX = maze.spanX - delta;
            }
        }
    }
    private void doRightSlider(Maze maze)
    {
        Vector3 spanXpos = new Vector3(maze.startX + maze.spanX, 0, maze.startY + (float)maze.spanY/2);
        float handleSize = HandleUtility.GetHandleSize(maze.transform.TransformPoint(spanXpos));

        Vector3 spanXdelta = Handles.Slider(maze.transform.TransformPoint(spanXpos),
                                            maze.transform.TransformDirection(Vector3.right),
                                            handleSize, Handles.ArrowHandleCap, 1f);
        spanXdelta = maze.transform.InverseTransformPoint(spanXdelta);

        if (spanXdelta != spanXpos)
        {
            int delta = (int)Math.Floor(spanXdelta.x - spanXpos.x);
            if ( delta != 0 && (((int)maze.spanX + delta) > 0) )
            {
                maze.spanX = maze.spanX + delta;
            }
        }
    }

    private void doBackwardSlider(Maze maze)
    {
        Vector3 startYpos = new Vector3(maze.startX + (float)maze.spanX/2, 0, maze.startY);
        float handleSize = HandleUtility.GetHandleSize(maze.transform.TransformPoint(startYpos));

        Vector3 startYdelta = Handles.Slider(maze.transform.TransformPoint(startYpos),
                                             maze.transform.TransformDirection(Vector3.back),
                                             handleSize, Handles.ArrowHandleCap, 1f);
        startYdelta = maze.transform.InverseTransformPoint(startYdelta);

        if (startYdelta != startYpos)
        {
            int delta = (int)Math.Floor(startYdelta.z - startYpos.z);
            if ( delta != 0 && (((int)maze.spanY - delta) > 0) )
            {
                maze.startY += delta;
                maze.spanY = maze.spanY - delta;
            }
        }
    }

    private void doForwardSlider(Maze maze)
    {
        Vector3 spanYpos = new Vector3(maze.startX + (float)maze.spanX/2, 0, maze.startY + maze.spanY);
        float handleSize = HandleUtility.GetHandleSize(maze.transform.TransformPoint(spanYpos));

        Vector3 spanYdelta = Handles.Slider(maze.transform.TransformPoint(spanYpos),
                                            maze.transform.TransformDirection(Vector3.forward),
                                            handleSize, Handles.ArrowHandleCap, 1f);
        spanYdelta = maze.transform.InverseTransformPoint(spanYdelta);

        if (spanYdelta != spanYpos)
        {
            int delta = (int)Math.Floor(spanYdelta.z - spanYpos.z);
            if ( delta != 0 && (((int)maze.spanY + delta) > 0) )
            {
                maze.spanY = maze.spanY + delta;
            }
        }
    }

    void OnSceneGUI()
    {
        Maze maze = (Maze)target;

        // Scale sliders
        doLeftSlider(maze);
        doRightSlider(maze);
        doBackwardSlider(maze);
        doForwardSlider(maze);

        for ( var x = maze.startX; x < maze.startX + maze.spanX; x++)
        {
            for ( var y = maze.startY; y < maze.startY + maze.spanY; y++)
            {
                Vector3 pos = maze.transform.TransformPoint(new UnityEngine.Vector3(x+0.5f, 0.05f, y+0.5f));
                Quaternion rot = maze.transform.rotation * UnityEngine.Quaternion.AngleAxis(90f, UnityEngine.Vector3.right);

                if ( Handles.Button(pos, rot, 0.4f, 0.4f, Handles.CircleHandleCap) )
                {
                    Vector2Int key = new Vector2Int(x,y);
                    if (maze.containsCell(key))
                    {
                        int currentIndex = maze.getCell(key);
                        if (currentIndex >= (maze.cellMaterials.Count-1))
                        {
                            maze.removeCell(key);
                        }
                        else
                        {
                            maze.setCell(key, currentIndex + 1);
                        }
                    }
                    else
                    {
                        if (maze.cellMaterials.Count > 0)
                        {
                            maze.setCell(key, 0);
                        }
                    }
                }
            }
        }  
    }
    
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    static void DrawGizmoForMazeUnselected(Maze maze, GizmoType gizmoType)
    {
        Gizmos.matrix = maze.transform.localToWorldMatrix;
        Gizmos.color = Color.grey;
        Gizmos.DrawWireCube( new Vector3(maze.startX + maze.spanX/2f,maze.height/2f,maze.startY +maze.spanY/2f), new Vector3(maze.spanX,maze.height,maze.spanY));
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    static void DrawGizmoForMaze(Maze maze, GizmoType gizmoType)
    {
        ensureFloorMeshes();

        for ( var x = maze.startX; x < maze.startX + maze.spanX; x++)
        {
            for ( var y = maze.startY; y < maze.startY + maze.spanY; y++)
            {
                Vector2Int key = new Vector2Int(x,y);
                
                Vector3 pos = new Vector3(x+0.5f, 0.05f, y+0.5f);

                Color cellColor = Color.gray;

                Mesh floorMesh = floorPlate;
                if (maze.containsCell(key))
                {
                    int materialIndex = maze.getCell(key);
                    if (materialIndex < maze.cellMaterials.Count)
                    {
                        cellColor = maze.cellMaterials[materialIndex].color;
                    }
                    else
                    {
                        floorMesh = invalidFloorPlate;
                        cellColor = Color.magenta;
                    }
                }
                Gizmos.color = cellColor;
                Gizmos.DrawMesh(floorMesh, maze.transform.TransformPoint(pos), maze.transform.rotation, maze.transform.localScale);
            }
        }  
    }
}
