﻿using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class Placer : EditorWindow

{
    [MenuItem("Tools/Placer")]
    public static void OpenWindow()
    {
        GetWindow<Placer>();
    }

    public float radius = 2f;
    public int spawnCount = 7;
    public bool on = true;
    public bool random = false;
    public bool randomRotation = false;
    public Color radiusColor = new Color(0.839f, 0.058f, 0.435f, 1f);
    public float offset = 0f;
    public bool deletion = false;

    public GameObject prefab = null;
    public Material previewMaterial;

    SerializedObject so;
    SerializedProperty propRadius;
    SerializedProperty propSpawnCount;
    SerializedProperty propOn;
    SerializedProperty propPrefab;
    SerializedProperty propOffset;
    SerializedProperty propRandom;
    SerializedProperty propRandomRotation;
    SerializedProperty propPreviewMaterial;
    SerializedProperty propColor;
    SerializedProperty propDeletion;

    private Point hitPoint;
    private GameObject originalPrefab;
    private float[] randArray = new float[maxSpawnCount];
    private List<Point> pointList = new List<Point>();
    private Vector2[] randPoints;
    private float raycastOffset = 1.5f;
    private static int maxSpawnCount = 100;
    private bool showPreviewSetting = false;
    private struct LookInfo
    {
        public Vector3 position;
        public Vector3 forward;
        public Vector3 up;
    }
    private struct Point
    {
        public Vector3 position;
        public Quaternion rotation;
    }
    private void OnEnable()
    {
        so = new SerializedObject(this);
        propRadius = so.FindProperty(nameof(radius));
        propSpawnCount = so.FindProperty(nameof(spawnCount));
        propOn = so.FindProperty(nameof(on));
        propPrefab = so.FindProperty(nameof(prefab));
        propRandom = so.FindProperty(nameof(random));
        propRandomRotation = so.FindProperty(nameof(randomRotation));
        propPreviewMaterial = so.FindProperty(nameof(previewMaterial));
        propColor = so.FindProperty(nameof(radiusColor));
        propOffset = so.FindProperty(nameof(offset));
        propDeletion = so.FindProperty(nameof(deletion));


        SceneView.duringSceneGui += DuringSceneGUI;
        GenerateRandomPoints();
        GetNewRandomValue();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    private void OnGUI()
    {
        so.Update();
        EditorGUILayout.PropertyField(propOn);
        if (on)
        {
            EditorGUILayout.PropertyField(propRadius);
            propRadius.floatValue = Mathf.Max(1f, propRadius.floatValue);
            EditorGUILayout.PropertyField(propSpawnCount);
            propSpawnCount.intValue = Mathf.Clamp(propSpawnCount.intValue, 1, maxSpawnCount);
            EditorGUILayout.PropertyField(propPrefab);
            EditorGUILayout.PropertyField(propOffset);
            EditorGUILayout.PropertyField(propDeletion);
            EditorGUILayout.PropertyField(propRandom);
            EditorGUILayout.PropertyField(propRandomRotation);
            GUILayout.Space(20);
            showPreviewSetting = EditorGUILayout.Foldout(showPreviewSetting, "Preview Setting");
            if (showPreviewSetting)
            {
                EditorGUILayout.PropertyField(propPreviewMaterial);
                EditorGUILayout.PropertyField(propColor);
            }
        }

        if (so.ApplyModifiedProperties())
        {
            if (prefab != null)
            {
                originalPrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefab);
                print(originalPrefab);
            }
            GenerateRandomPoints();
            GetNewRandomValue();
            SceneView.RepaintAll();
        }
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0) //左クリックでUNFOCUS
        {
            GUI.FocusControl(null);
            Repaint();
        }
    }
    private void DuringSceneGUI(SceneView scene)
    {

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.ScrollLock) //オンオフ切り替え
        {
            so.Update();
            propOn.boolValue = !propOn.boolValue;
            so.ApplyModifiedPropertiesWithoutUndo();
            Repaint();
        }
        if (!on)
        {
            return;
        }

        Handles.zTest = CompareFunction.LessEqual;
        List<LookInfo> looksList = new List<LookInfo>();
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        if (Event.current.type == EventType.MouseMove)
        {
            scene.Repaint();
        }

        Camera cam = scene.camera;
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 hitNormal = hit.normal;
            Vector3 hitTangent = Vector3.Cross(hitNormal, cam.transform.up).normalized;
            Vector3 hitBitangent = Vector3.Cross(hitNormal, hitTangent);
            hitPoint.position = hit.point;
            hitPoint.rotation = Quaternion.LookRotation(hitTangent, hitNormal);
            if (random || deletion)
            {
                DrawDisc(hit);
                if (random)
                {
                    foreach (Vector2 p in randPoints)
                    {
                        Matrix4x4 loccalToWorldMatrix = new Matrix4x4(
                            new Vector4(hitTangent.x, hitTangent.y, hitTangent.z, 0),
                            new Vector4(hitNormal.x, hitNormal.y, hitNormal.z, 0),
                            new Vector4(hitBitangent.x, hitBitangent.y, hitBitangent.z, 0),
                            new Vector4(hit.point.x, hit.point.y, hit.point.z, 1)
                            );
                        Vector3 pointLocal = new Vector3(p.x, raycastOffset, p.y) * radius;
                        Vector3 pointWorld = loccalToWorldMatrix.MultiplyPoint3x4(pointLocal);
                        Ray pointRay = new Ray(pointWorld, -hitNormal);
                        if (Physics.Raycast(pointRay, out RaycastHit pointHit))
                        {
                            LookInfo lookdirection;
                            lookdirection.forward = Vector3.Cross(pointHit.normal, cam.transform.up).normalized;
                            lookdirection.up = pointHit.normal;
                            lookdirection.position = pointHit.point;
                            looksList.Add(lookdirection);
                        }
                    }
                }

            }
            else
            {
                LookInfo lookInfo;
                lookInfo.forward = hitTangent;
                lookInfo.up = hitNormal;
                lookInfo.position = hit.point;
                looksList.Add(lookInfo);
                float scale = Vector3.Distance(cam.transform.position, hit.point) * 0.05f;
                Handles.color = Color.red;
                Handles.DrawAAPolyLine(5, hit.point, hit.point + hitTangent * scale);
                Handles.color = Color.blue;
                Handles.DrawAAPolyLine(5, hit.point, hit.point + hitBitangent * scale);
                Handles.color = Color.green;
                Handles.DrawAAPolyLine(5, hit.point, hit.point + hitNormal * scale);
            }
        }


        if (deletion)
        {
            bool ctrl = (Event.current.modifiers & EventModifiers.Control) != 0;
            if (ctrl && Event.current.isMouse && Event.current.type == EventType.MouseDown)
            {
                if (originalPrefab != null)
                {
                    DeleteAroundPoint(hit);
                    Event.current.Use();
                }
            }
        }
        else
        {
            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag || Event.current.type == EventType.ScrollWheel)
            {
                pointList.Clear();
                for (int i = 0; i < looksList.Count; i++)
                {
                    Point point;
                    if (randomRotation)
                    {
                        point.rotation = Quaternion.AngleAxis(randArray[i] * 360f, looksList[i].up) * Quaternion.LookRotation(looksList[i].forward, looksList[i].up);
                    }
                    else
                    {
                        point.rotation = Quaternion.LookRotation(looksList[i].forward, looksList[i].up);
                    }
                    point.position = looksList[i].position;
                    pointList.Add(point);

                }
            }

            bool shift = (Event.current.modifiers & EventModifiers.Shift) != 0;
            bool ctrl = (Event.current.modifiers & EventModifiers.Control) != 0;

            if (originalPrefab != null && !shift)
            {
                DrawPreview(originalPrefab, false);
            }
            if (shift) //スナッププレビュー
            {
                foreach (GameObject go in Selection.gameObjects)
                {
                    DrawPreview(go, true);
                }

                if (Event.current.isMouse && Event.current.type == EventType.MouseDown && Event.current.button == 0) //leftクリック
                {

                    Event.current.Use();
                    SnapObjects(pointList);
                }
            }
            if (ctrl)
            {
                if (Event.current.type == EventType.ScrollWheel) //オフセット調整
                {
                    float scrollDir = Mathf.Sign(Event.current.delta.y);
                    so.Update();
                    propOffset.floatValue -= scrollDir * 0.1f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Event.current.Use();
                    Repaint();
                }

                if (Event.current.isMouse && Event.current.type == EventType.MouseDown)
                {
                    SpawnPrefabs(pointList);
                    Event.current.Use();
                }
            }
        }


    }

    private bool IsPrefab(GameObject o)
    {
        return o.scene.name == null;
    }

    private void DeleteAroundPoint(RaycastHit hit)
    {
        Collider[] colliders = Physics.OverlapSphere(hit.point, radius);
        foreach (Collider c in colliders)
        {
            GameObject o = c.gameObject;

            if (originalPrefab == PrefabUtility.GetCorrespondingObjectFromSource(o))
            {
                if (DistancePlanePoint(hit.normal, hit.point, o.transform.position) < 1f) //height range to delete
                {
                    Undo.DestroyObjectImmediate(o);
                }
            }
        }
    }
    private void DrawPreview(GameObject go, bool isSnappedMode)
    {
        for (int i = 0; i < pointList.Count; i++)
        {
            Matrix4x4 localToWorld;
            if (isSnappedMode)
            {
                localToWorld = Matrix4x4.TRS(hitPoint.position, hitPoint.rotation, Vector3.one);
            }
            else
            {
                localToWorld = Matrix4x4.TRS(pointList[i].position, pointList[i].rotation, Vector3.one);
            }
            MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>();
            previewMaterial.SetPass(0);
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                Matrix4x4 childMatrix = filter.transform.localToWorldMatrix;
                Matrix4x4 yAxisOffsetMatrix = Matrix4x4.TRS(new Vector3(0f, offset, 0f), Quaternion.identity, Vector3.one);
                Matrix4x4 ignoreParentPositionMatrix = Matrix4x4.TRS(-go.transform.position, Quaternion.identity, Vector3.one);
                if (isSnappedMode)
                {
                    ignoreParentPositionMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Inverse(go.transform.rotation), Vector3.one) * ignoreParentPositionMatrix;
                }
                Matrix4x4 outputMatrix = localToWorld * yAxisOffsetMatrix * ignoreParentPositionMatrix * childMatrix;
                Graphics.DrawMeshNow(mesh, outputMatrix);
            }
            if (isSnappedMode)
            {
                break;
            }
        }
    }


    private void SnapObjects(List<Point> pointList)
    {
        if (Selection.gameObjects.Length == 0)
        {
            return;
        }
        for (int i = 0; i < Selection.gameObjects.Length; i++)
        {
            Undo.RecordObject(Selection.gameObjects[i].transform, "Snap");
            Selection.gameObjects[i].transform.position = hitPoint.position + hitPoint.rotation * new Vector3(0f, offset, 0f);
            Selection.gameObjects[i].transform.rotation = hitPoint.rotation;

        }
    }
    private void SpawnPrefabs(List<Point> pointList)
    {
        if (originalPrefab == null)
        {
            return;
        }
        foreach (Point point in pointList)
        {
            GameObject spawnObject = (GameObject)PrefabUtility.InstantiatePrefab(originalPrefab);
            Undo.RegisterCreatedObjectUndo(spawnObject, "Spawn Objects");
            spawnObject.transform.position = point.position + point.rotation * new Vector3(0f, offset, 0f);
            spawnObject.transform.rotation = point.rotation * originalPrefab.transform.rotation;

        }
        if (random)
        {
            GenerateRandomPoints();
            GetNewRandomValue();
        }
    }
    private void GenerateRandomPoints()
    {
        randPoints = new Vector2[spawnCount];
        for (int i = 0; i < spawnCount; i++)
        {
            randPoints[i] = Random.insideUnitCircle;

        }
    }

    private void GetNewRandomValue()
    {
        Shuffle(randArray);
    }
    public static void Shuffle<T>(T[] array)
    {
        int n = array.Length;
        while (n > 1)
        {
            int k = Random.Range(0, n);
            n--;
            T temp = array[n];
            array[n] = array[k];
            array[k] = temp;
        }
    }
    private void DrawDisc(RaycastHit hit)
    {
        Handles.color = radiusColor;
        Handles.DrawWireDisc(hit.point, hit.normal, radius);
        Handles.color = Color.white;
    }

    public static float DistancePlanePoint(Vector3 planeNormal, Vector3 planePoint, Vector3 point)
    {

        return Mathf.Abs(Vector3.Dot(planeNormal, (point - planePoint)));
    }
    public static void print(System.Object o)
    {
        Debug.Log(o);
    }
}