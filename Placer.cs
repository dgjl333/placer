using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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
    public bool randomRotation = false;
    public Color radiusColor = new Color(0.839f, 0.058f, 0.435f, 1f);
    public float offset = 0f;
    public bool keepRootRotation = false;
    public Mode mode = Mode.Place;


    public GameObject prefab = null;
    public Material previewMaterial;

    SerializedObject so;
    SerializedProperty propRadius;
    SerializedProperty propSpawnCount;
    SerializedProperty propPrefab;
    SerializedProperty propOffset;
    SerializedProperty propRandomRotation;
    SerializedProperty propPreviewMaterial;
    SerializedProperty propColor;
    SerializedProperty propKeepRootRotation;
    SerializedProperty propMode;


    private Pose hitPoint;
    private GameObject originalPrefab;
    private float[] randEulerArray = new float[20];
    private List<Pose> poseList = new List<Pose>();
    private Vector2[] randPoints;
    private float raycastOffset = 1.5f;
    private static int maxSpawnCount = 100;
    private bool showPreviewSetting = false;
    private float discThickness = 2f;
    private bool shift = false;
    private bool ctrl = false;

    private string activateText = "Activate";
    private string deactivateText = "Deactivate";
    private string currentText;

    private struct PointWithOrientation
    {
        public Vector3 position;
        public Vector3 forward;
        public Vector3 up;

        public PointWithOrientation(Vector3 position, Vector3 forward, Vector3 up)
        {
            this.position = position;
            this.forward = forward;
            this.up = up;
        }
    }

    public enum Mode
    {
        Delete,
        Scatter,
        Place,
        None
    }
    private void OnEnable()
    {
        so = new SerializedObject(this);
        propRadius = so.FindProperty(nameof(radius));
        propSpawnCount = so.FindProperty(nameof(spawnCount));
        propPrefab = so.FindProperty(nameof(prefab));
        propRandomRotation = so.FindProperty(nameof(randomRotation));
        propPreviewMaterial = so.FindProperty(nameof(previewMaterial));
        propColor = so.FindProperty(nameof(radiusColor));
        propOffset = so.FindProperty(nameof(offset));
        propKeepRootRotation = so.FindProperty(nameof(keepRootRotation));
        propMode = so.FindProperty(nameof(mode));
        SceneView.duringSceneGui += DuringSceneGUI;
        GenerateRandomPoints();
        RefreshRandEulerArray();
        currentText = on ? deactivateText : activateText;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    private void OnGUI()
    {
        so.Update();
        if (GUILayout.Button(currentText))
        {
            on = !on;
            currentText = on ? deactivateText : activateText;
        }
        if (on)
        {
            GUILayout.Space(15);
            EditorGUILayout.PropertyField(propRadius);
            propRadius.floatValue = Mathf.Max(1f, propRadius.floatValue);
            EditorGUILayout.PropertyField(propSpawnCount);
            propSpawnCount.intValue = Mathf.Clamp(propSpawnCount.intValue, 1, maxSpawnCount);
            EditorGUILayout.PropertyField(propPrefab);
            EditorGUILayout.PropertyField(propOffset);
            EditorGUILayout.PropertyField(propRandomRotation);
            EditorGUILayout.PropertyField(propKeepRootRotation);
            EditorGUILayout.PropertyField(propMode);
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
            }
            GenerateRandomPoints();
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
        CheckInputToggleActive();
        if (!on) return;
        Handles.zTest = CompareFunction.LessEqual;
        List<PointWithOrientation> pointList = new List<PointWithOrientation>();
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        if (Event.current.type == EventType.MouseMove)
        {
            scene.Repaint();
        }
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            HandleModeSpecificActions(hit, pointList, scene.camera);
        }

        KeyModifierCheck();
        if (ctrl)
        {
            DrawSnapObjectsPreview();
            SnapModeInputCheck();
        }
        switch (mode)
        {
            case Mode.Delete:
                DeleteModeInputCheck();
                break;
            default:
                OccupyPoseList(pointList);
                NonDeleteModeInputCheck();
                break;
        }
    }
    private void HandleModeSpecificActions(RaycastHit hit, List<PointWithOrientation> pointList, Camera cam)
    {
        Vector3 hitNormal = hit.normal;
        Vector3 hitTangent = Vector3.Cross(hitNormal, cam.transform.up).normalized;
        Vector3 hitBitangent = Vector3.Cross(hitNormal, hitTangent);
        hitPoint.position = hit.point;
        hitPoint.rotation = Quaternion.LookRotation(hitTangent, hitNormal);
        switch (mode)
        {
            case Mode.Delete:
                {
                    DrawDisc(hit, InverseColor(radiusColor), discThickness * 2f);
                    PointWithOrientation pointInfo = new PointWithOrientation(hit.point, hitTangent, hitNormal);
                    pointList.Add(pointInfo);
                    break;
                }
            case Mode.Scatter:
                {
                    DrawDisc(hit, radiusColor, discThickness);
                    foreach (Vector2 p in randPoints)
                    {
                        Vector3 worldPos = GetWorldPosFromLocal(p, hit.point, hitTangent, hitNormal, hitBitangent, raycastOffset);
                        Ray pointRay = new Ray(worldPos, -hitNormal);
                        if (Physics.Raycast(pointRay, out RaycastHit pointHit))
                        {
                            Vector3 forward = Vector3.Cross(pointHit.normal, cam.transform.up).normalized;
                            Vector3 up = pointHit.normal;
                            PointWithOrientation lookdirection = new PointWithOrientation(pointHit.point, forward, up);
                            pointList.Add(lookdirection);
                        }
                    }
                }
                break;
            case Mode.Place:
                {
                    PointWithOrientation pointInfo = new PointWithOrientation(hit.point, hitTangent, hitNormal);
                    pointList.Add(pointInfo);
                    float scale = Vector3.Distance(cam.transform.position, hit.point) * 0.05f;
                    DrawAxisGizmo(hit.point, hitTangent, hitNormal, hitBitangent, scale);
                }
                break;
            default:
                break;
        }
    }
    private void KeyModifierCheck()
    {
        shift = (Event.current.modifiers & EventModifiers.Shift) != 0;
        ctrl = (Event.current.modifiers & EventModifiers.Control) != 0;
    }
    private void DrawSnapObjectsPreview()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            DrawObjectPreview(go, true);
        }
    }

    private void DeleteModeInputCheck()
    {

        if (shift && Event.current.isMouse && Event.current.type == EventType.MouseDown)
        {
            if (originalPrefab != null)
            {
                DeleteAroundPoint();
                Event.current.Use();
            }
        }
    }

    private void RefreshRandEulerArray()
    {
        for (int i = 0; i < randEulerArray.Length; i++)
        {
            float angle = Random.value * 360f;
            randEulerArray[i] = angle;
        }
    }

    private float GetObjectBoundingBoxSize(GameObject o)
    {
        Renderer renderer = o.GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds.size.y;
        }
        return 0f;
    }

    private float GetRandomEulerFromArray(int index)
    {
        return randEulerArray[index % randEulerArray.Length];
    }

    private Color InverseColor(Color color)
    {
        return new Color(1 - color.r, 1 - color.g, 1 - color.b, color.a);
    }
    private void NonDeleteModeInputCheck()
    {

        if (originalPrefab != null && !ctrl)
        {
            DrawObjectPreview(originalPrefab, false);
        }
        if (shift) //スナッププレビュー
        {
            CheckInputShiftDown();

        }
    }
    private void CheckInputToggleActive()
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.ScrollLock) //オンオフ切り替え
        {
            on = !on;
            currentText = on ? deactivateText : activateText;
            Repaint();
        }
    }

    private Vector3 GetWorldPosFromLocal(Vector2 Localposition, Vector3 origin, Vector3 forward, Vector3 up, Vector3 right, float yAxisOffset)
    {
        Matrix4x4 loccalToWorldMatrix = new Matrix4x4(
        new Vector4(forward.x, forward.y, forward.z, 0),
        new Vector4(up.x, up.y, up.z, 0),
        new Vector4(right.x, right.y, right.z, 0),
        new Vector4(origin.x, origin.y, origin.z, 1)
    );
        Vector3 pointLocal = new Vector3(Localposition.x, yAxisOffset, Localposition.y) * radius;
        return loccalToWorldMatrix.MultiplyPoint3x4(pointLocal);
    }
    private void OccupyPoseList(List<PointWithOrientation> looksList)
    {
        poseList.Clear();
        for (int i = 0; i < looksList.Count; i++)
        {
            Pose point;
            Quaternion rot = Quaternion.LookRotation(looksList[i].forward, looksList[i].up);
            point.rotation = randomRotation ? Quaternion.AngleAxis(GetRandomEulerFromArray(i), looksList[i].up) * rot : rot;
            point.position = looksList[i].position;
            poseList.Add(point);
        }
    }
    private void DrawAxisGizmo(Vector3 position, Vector3 forward, Vector3 up, Vector3 right, float scale)
    {
        Handles.color = Color.red;
        Handles.DrawAAPolyLine(5, position, position + forward * scale);
        Handles.color = Color.blue;
        Handles.DrawAAPolyLine(5, position, position + right * scale);
        Handles.color = Color.green;
        Handles.DrawAAPolyLine(5, position, position + up * scale);
    }

    private void AdjustOffset()
    {
        float scrollDir = Mathf.Sign(Event.current.delta.y);
        so.Update();
        propOffset.floatValue -= scrollDir * 0.1f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private bool IsPrefab(GameObject o)
    {
        return o.scene.name == null;
    }

    private void CheckInputShiftDown()
    {
        if (Event.current.isMouse && Event.current.type == EventType.MouseDown)
        {
            SpawnPrefabs(poseList);
            Event.current.Use();
        }
    }
    private void SnapModeInputCheck()
    {
        if (Event.current.type == EventType.ScrollWheel) //オフセット調整
        {
            AdjustOffset();
            Event.current.Use();
            Repaint();
        }
        if (Event.current.isMouse && Event.current.type == EventType.MouseDown && Event.current.button == 0) //leftクリック
        {
            SnapObjects();
            Event.current.Use();
        }
    }
    private void DeleteAroundPoint()
    {
        bool hasCollider = (originalPrefab.GetComponent<Collider>() != null);
        if (hasCollider)
        {
            Collider[] colliders = Physics.OverlapSphere(hitPoint.position, radius);
            foreach (Collider c in colliders)
            {
                GameObject o = c.gameObject;

                if (originalPrefab == PrefabUtility.GetCorrespondingObjectFromSource(o))
                {
                    if (IsWithinDeletionHeightRange(o)) //height range to delete
                    {
                        Undo.DestroyObjectImmediate(o);
                    }
                }
            }
        }
        else
        {
            GameObject[] objs = PrefabUtility.FindAllInstancesOfPrefab(originalPrefab);
            foreach (GameObject o in objs)
            {
                float distance = Vector3.Distance(o.transform.position, hitPoint.position);
                if (distance < radius && IsWithinDeletionHeightRange(o))
                {
                    Undo.DestroyObjectImmediate(o);
                }
            }
        }

    }

    private bool IsWithinDeletionHeightRange(GameObject o)
    {
        float deletionHeightBound = 2f * GetObjectBoundingBoxSize(originalPrefab);
        return DistancePlanePoint(hitPoint.rotation * Vector3.up, hitPoint.position, o.transform.position) < deletionHeightBound;
    }

    private void DrawObjectPreview(GameObject go, bool isSnappedMode)
    {
        if (isSnappedMode)
        {
            Matrix4x4 localToWorld = Matrix4x4.TRS(hitPoint.position, hitPoint.rotation, Vector3.one);
            DrawMesh(go, localToWorld, true);
        }
        else
        {
            for (int i = 0; i < poseList.Count; i++)
            {
                Matrix4x4 localToWorld = Matrix4x4.TRS(poseList[i].position, poseList[i].rotation, Vector3.one);
                DrawMesh(go, localToWorld, !keepRootRotation);
            }
        }

    }

    private void DrawMesh(GameObject go, Matrix4x4 localToWorld, bool ignoreParentRotation)
    {

        MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>();
        previewMaterial.SetPass(0);
        Matrix4x4 yAxisOffsetMatrix = Matrix4x4.TRS(new Vector3(0f, offset, 0f), Quaternion.identity, Vector3.one);
        Matrix4x4 ignoreParentPositionMatrix = Matrix4x4.TRS(-go.transform.position, Quaternion.identity, Vector3.one);
        Matrix4x4 ignoreParentMatrix = ignoreParentPositionMatrix;
        if (ignoreParentRotation)
        {
            Matrix4x4 ignoreParentRotationMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Inverse(go.transform.rotation), Vector3.one);
            ignoreParentMatrix = ignoreParentRotationMatrix * ignoreParentMatrix;
        }
        foreach (MeshFilter filter in filters)
        {
            Mesh mesh = filter.sharedMesh;
            Matrix4x4 childMatrix = filter.transform.localToWorldMatrix;
            Matrix4x4 outputMatrix = localToWorld * yAxisOffsetMatrix * ignoreParentMatrix * childMatrix;
            Graphics.DrawMeshNow(mesh, outputMatrix);
        }
    }

    private void SnapObjects()
    {
        if (Selection.gameObjects.Length == 0) return;
        for (int i = 0; i < Selection.gameObjects.Length; i++)
        {
            Undo.RecordObject(Selection.gameObjects[i].transform, "Snap");
            Selection.gameObjects[i].transform.position = hitPoint.position + hitPoint.rotation * new Vector3(0f, offset, 0f);
            Selection.gameObjects[i].transform.rotation = hitPoint.rotation;

        }
    }
    private void SpawnPrefabs(List<Pose> poseList)
    {
        if (originalPrefab == null) return;
        foreach (Pose point in poseList)
        {
            GameObject spawnObject = (GameObject)PrefabUtility.InstantiatePrefab(originalPrefab);
            Undo.RegisterCreatedObjectUndo(spawnObject, "Spawn Objects");
            spawnObject.transform.position = point.position + point.rotation * new Vector3(0f, offset, 0f);
            spawnObject.transform.rotation = keepRootRotation ? point.rotation * originalPrefab.transform.rotation : point.rotation;

        }
        if (mode == Mode.Scatter)
        {
            GenerateRandomPoints();
        }
        if (randomRotation)
        {
            RefreshRandEulerArray();
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

    private void DrawDisc(RaycastHit hit, Color color, float thickness)
    {
        Handles.color = color;
        Handles.DrawWireDisc(hit.point, hit.normal, radius, thickness);
        Handles.color = Color.white;
    }

    public static float DistancePlanePoint(Vector3 planeNormal, Vector3 planePoint, Vector3 point)
    {
        return Mathf.Abs(Vector3.Dot(planeNormal, (point - planePoint)));
    }
}