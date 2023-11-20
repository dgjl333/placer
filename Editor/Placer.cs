﻿using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public class Placer : EditorWindow

{
    [MenuItem("Tools/Placer")]
    public static void OpenWindow()
    {
        GetWindow<Placer>();
    }

    public float spawnRadius = 2f;
    public float spacing = 0.1f;
    public float deletionRadius = 2f;
    public int spawnCount = 7;
    public bool on = true;
    public bool randomRotation = false;
    public bool randomScale = false;
    public float scaleRange = 0.1f;
    public Color radiusColor = new Color(0.839f, 0.058f, 0.435f, 0.784f);
    public float offset = 0f;
    public bool keepRootRotation = false;
    public Mode mode = Mode.Scatter;

    public GameObject prefab = null;
    public Material previewMaterial;
    public Material deletionMaterial;

    SerializedObject so;
    SerializedProperty propRadius;
    SerializedProperty propSpacing;
    SerializedProperty propDeletionRadius;
    SerializedProperty propSpawnCount;
    SerializedProperty propPrefab;
    SerializedProperty propOffset;
    SerializedProperty propRandomRotation;
    SerializedProperty propScaleRange;
    SerializedProperty propRandomScale;
    SerializedProperty propPreviewMaterial;
    SerializedProperty propDeletionMaterial;
    SerializedProperty propColor;
    SerializedProperty propKeepRootRotation;


    [SerializeField] private GameObject originalPrefab;
    private Pose hitPoint;
    private float[] randValues;
    private List<Pose> poseList = new List<Pose>();
    private List<Vector2> randPoints;
    private bool showPreviewSetting = false;
    private float discThickness = 2f;
    private bool shift = false;
    private bool ctrl = false;
    private bool alt = false;
    private string activateText = "Activate";
    private string deactivateText = "Deactivate";
    private string currentText;

    private int controlID;

    private readonly float minRadius = 0f;
    private readonly float maxRadius = 50f;
    private readonly float minOffset = -5f;
    private readonly float maxOffset = 5f;

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
        Scatter,
        Place,
        Delete,
        None
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += DuringSceneGUI;
        Camera.main.depthTextureMode = DepthTextureMode.Depth;   //intersection shader need depth texture

        so = new SerializedObject(this);
        propRadius = so.FindProperty(nameof(spawnRadius));
        propDeletionRadius = so.FindProperty(nameof(deletionRadius));
        propSpawnCount = so.FindProperty(nameof(spawnCount));
        propPrefab = so.FindProperty(nameof(prefab));
        propRandomRotation = so.FindProperty(nameof(randomRotation));
        propPreviewMaterial = so.FindProperty(nameof(previewMaterial));
        propDeletionMaterial = so.FindProperty(nameof(deletionMaterial));
        propColor = so.FindProperty(nameof(radiusColor));
        propOffset = so.FindProperty(nameof(offset));
        propScaleRange = so.FindProperty(nameof(scaleRange));
        propKeepRootRotation = so.FindProperty(nameof(keepRootRotation));
        propRandomScale = so.FindProperty(nameof(randomScale));
        propSpacing = so.FindProperty(nameof(spacing));

        GenerateRandPoints();
        GenerateRandValues();
        currentText = on ? deactivateText : activateText;
        controlID = GUIUtility.GetControlID(FocusType.Passive);
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    private void OnGUI()
    {

        GUIContent[] toolIcons =
        {
                EditorGUIUtility.TrIconContent("TerrainInspector.TerrainToolAdd", "Scatter"),   
                EditorGUIUtility.TrIconContent("TerrainInspector.TerrainToolAdd", "Place"),
                EditorGUIUtility.TrIconContent("TerrainInspector.TerrainToolAdd", "Deletion"),
                EditorGUIUtility.TrIconContent("TerrainInspector.TerrainToolAdd", "None"),
            };
        so.Update();
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(currentText, GUILayout.MaxWidth(230), GUILayout.Height(20)))
        {
            on = !on;
            currentText = on ? deactivateText : activateText;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        if (on)
        {
            GUILayout.Space(15);
            mode = (Mode)GUILayout.Toolbar((int)mode, toolIcons);
            GUILayout.Space(20);
            if (mode == Mode.Scatter)
            {
                EditorGUI.BeginChangeCheck();
                float newRadius = EditorGUILayout.Slider("Radius", propRadius.floatValue, minRadius, maxRadius);
                if (EditorGUI.EndChangeCheck())
                {
                    propRadius.floatValue = newRadius;
                }
                EditorGUI.BeginChangeCheck();
                int newSpawnCount = EditorGUILayout.IntSlider("Spawn Count", propSpawnCount.intValue, 1, 50);
                if (EditorGUI.EndChangeCheck())
                {
                    propSpawnCount.intValue = newSpawnCount;
                    GenerateRandPoints();
                }
                EditorGUILayout.PropertyField(propSpacing);
                propSpacing.floatValue = Mathf.Max(0f, propSpacing.floatValue);
            }
            else if (mode == Mode.Delete)
            {
                EditorGUI.BeginChangeCheck();
                float newRadius = EditorGUILayout.Slider("Radius", propDeletionRadius.floatValue, minRadius, maxRadius);
                if (EditorGUI.EndChangeCheck())
                {
                    propDeletionRadius.floatValue = newRadius;
                }
            }

            EditorGUILayout.PropertyField(propOffset);
            if (mode != Mode.None)
            {
                EditorGUILayout.PropertyField(propPrefab);
            }
            GUILayout.Space(15);
            if (mode == Mode.Scatter || mode == Mode.Place)
            {
                EditorGUILayout.PropertyField(propKeepRootRotation);
                EditorGUILayout.PropertyField(propRandomRotation);
                EditorGUILayout.PropertyField(propRandomScale);
                if (randomScale)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginChangeCheck();
                    float newScale = EditorGUILayout.Slider("Influence", propScaleRange.floatValue, 0f, 1f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        propScaleRange.floatValue = newScale;
                    }
                    EditorGUI.indentLevel--;
                }
            }
            GUILayout.Space(20);
            showPreviewSetting = EditorGUILayout.Foldout(showPreviewSetting, "Preview Setting");
            if (showPreviewSetting)
            {
                EditorGUILayout.PropertyField(propPreviewMaterial);
                EditorGUILayout.PropertyField(propDeletionMaterial);
                EditorGUILayout.PropertyField(propColor);
            }
        }

        if (so.ApplyModifiedProperties())
        {
            if (prefab != null)
            {
                originalPrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefab);
            }
            else
            {
                originalPrefab = null;
            }
            ValidateRandPoints();
            GenerateRandValues();
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
        bool isInPrefabMode = (PrefabStageUtility.GetCurrentPrefabStage() != null);
        CheckInputToggleActive();
        if (!on || isInPrefabMode) return;
        Handles.zTest = CompareFunction.LessEqual;
        List<PointWithOrientation> pointList = new List<PointWithOrientation>();
        if (Event.current.type == EventType.MouseMove)
        {
            scene.Repaint();
        }

        KeyModifierCheck();
        RaycastToMousePosition(pointList, scene.camera);
        ScrollWheelCheck();
        if (ctrl)
        {
            DrawSnapObjectsPreview();
            SnapModeInputCheck();
        }
        switch (mode)
        {
            case Mode.Delete:
                if (ctrl || originalPrefab == null) return;
                DeleteModeInputCheck();
                List<GameObject> objsInDeletionRange = GetObjectsInDeletionRange();
                DrawDeletionPreviews(objsInDeletionRange);
                break;
            default:
                OccupyPoseList(pointList);
                NonDeleteModeInputCheck();
                if (originalPrefab != null && !ctrl)
                {
                    DrawObjectPreview(originalPrefab, false);
                }
                break;
        }
    }

    private void RaycastToMousePosition(List<PointWithOrientation> pointList, Camera cam)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            RaycastHit finalHit = hit;
            if (!ctrl) //non snap mode
            {
                switch (mode)
                {
                    case Mode.Delete:
                        if (IsObjectFromPrefab(hit.collider.gameObject, originalPrefab))
                        {
                            RaycastHit[] hits = Physics.RaycastAll(ray);
                            RaycastHit? finalHitNullable = GetValidHitExcludingSamePrefab(hits);
                            finalHit = finalHitNullable ?? finalHit;
                        }
                        HandleModeSpecificActions(finalHit, pointList, cam);
                        break;
                    default:
                        HandleModeSpecificActions(finalHit, pointList, cam);
                        break;
                }
            }
            else //exclude self
            {
                GameObject[] objs = Selection.gameObjects;
                bool isFromObject = objs.Any(o => IsFromObject(hit.collider.gameObject, o));
                if (isFromObject)
                {
                    RaycastHit[] hits = Physics.RaycastAll(ray);
                    RaycastHit? finalHitNullable = GetValidHitExcludingObjects(hits, objs);
                    finalHit = finalHitNullable ?? finalHit;
                }
                HandleModeSpecificActions(finalHit, pointList, cam);
            }
        }
    }

    private RaycastHit? GetValidHitExcludingObjects(RaycastHit[] hits, GameObject[] objs)
    {
        foreach (RaycastHit hit in hits)
        {
            bool isValid = !objs.Any(obj => IsFromObject(hit.collider.gameObject, obj));
            if (isValid)
            {
                return hit;
            }
        }
        return null;
    }

    private RaycastHit? GetValidHitExcludingSamePrefab(RaycastHit[] hits)
    {
        foreach (RaycastHit hit in hits)
        {
            if (!IsObjectFromPrefab(hit.collider.gameObject, originalPrefab))
            {
                return hit;
            }
        }
        return null;
    }

    private void HandleModeSpecificActions(RaycastHit hit, List<PointWithOrientation> pointList, Camera cam)
    {
        Vector3 hitNormal = hit.normal;
        Vector3 hitTangent = Vector3.Cross(hitNormal, cam.transform.up).normalized;
        Vector3 hitBitangent = Vector3.Cross(hitNormal, hitTangent);
        hitPoint.position = hit.point;
        hitPoint.rotation = Quaternion.LookRotation(hitTangent, hitNormal);
        if (ctrl || originalPrefab == null) return;
        switch (mode)
        {
            case Mode.Delete:
                {
                    DrawDisc(hit, GetInverseColor(radiusColor), deletionRadius, discThickness);
                    PointWithOrientation pointInfo = new PointWithOrientation(hit.point, hitTangent, hitNormal);
                    pointList.Add(pointInfo);
                    break;
                }
            case Mode.Scatter:
                {
                    DrawDisc(hit, radiusColor, spawnRadius, discThickness);
                    foreach (Vector2 p in randPoints)
                    {
                        Vector3 worldPos = GetWorldPosFromLocal(p, hit.point, hitTangent, hitNormal, hitBitangent);
                        Ray pointRay = new Ray(worldPos, -hitNormal);
                        float dist = GetObjectBoundingBoxSize(originalPrefab);
                        if (dist == -1f) return;
                        if (Physics.Raycast(pointRay, out RaycastHit pointHit, dist))
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
                    //DrawAxisGizmo(hit.point, hitTangent, hitNormal, hitBitangent);
                }
                break;
            default:
                break;
        }
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
                List<GameObject> deletingObjs = GetObjectsInDeletionRange();
                foreach (GameObject o in deletingObjs)
                {
                    Undo.DestroyObjectImmediate(o);
                }
                Event.current.Use();
            }
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

    private void KeyModifierCheck()
    {
        shift = (Event.current.modifiers & EventModifiers.Shift) != 0;
        ctrl = (Event.current.modifiers & EventModifiers.Control) != 0;
        alt = (Event.current.modifiers & EventModifiers.Alt) != 0;
    }

    private void NonDeleteModeInputCheck()
    {
        if (shift) //スナッププレビュー
        {
            CheckInputShiftDown();
        }
    }

    private void CheckInputShiftDown()
    {
        if (Event.current.isMouse && Event.current.type == EventType.MouseDown)
        {
            SpawnPrefabs(poseList);
            if (mode != Mode.None)
            {
                GUIUtility.hotControl = controlID;
            }
            Event.current.Use();
        }
        ReleaseHotControl();
    }

    private void SnapModeInputCheck()
    {
        if (Event.current.isMouse && Event.current.type == EventType.MouseDown && Event.current.button == 0) //leftクリック
        {
            SnapObjects();
            GUIUtility.hotControl = controlID;
            Event.current.Use();
        }
        ReleaseHotControl();
    }

    private void ScrollWheelCheck()
    {

        if (Event.current.type == EventType.ScrollWheel) //オフセット調整
        {
            if (shift && (mode == Mode.Scatter || mode == Mode.Delete))
            {
                AdjustRadius();
                UseEvent();
            }
            if (alt)
            {
                AdjustOffset();
                UseEvent();
            }

        }
        void UseEvent()
        {
            Event.current.Use();
            Repaint();
        }
    }

    private void AdjustOffset()
    {
        float scrollDir = Mathf.Sign(Event.current.delta.y);
        so.Update();
        float newValue = Mathf.Clamp(propOffset.floatValue - scrollDir * 0.1f, minOffset, maxOffset);
        propOffset.floatValue = newValue;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private void AdjustRadius()
    {
        float scrollDir = Mathf.Sign(Event.current.delta.y);
        float newValue;
        so.Update();
        switch (mode)
        {
            case Mode.Scatter:
                newValue = Mathf.Clamp(propRadius.floatValue * (1 - scrollDir * 0.04f), minRadius, maxRadius);
                propRadius.floatValue = newValue;
                ValidateRandPoints();
                break;
            case Mode.Delete:
                newValue = Mathf.Clamp(propDeletionRadius.floatValue * (1 - scrollDir * 0.04f), minRadius, maxRadius);
                propDeletionRadius.floatValue = newValue;
                break;
            default:
                break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private float GetObjectBoundingBoxSize(GameObject o)
    {
        Renderer renderer = o.GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds.size.y;
        }
        return -1f;
    }

    private Vector3 GetWorldPosFromLocal(Vector2 Localposition, Vector3 origin, Vector3 forward, Vector3 up, Vector3 right)
    {
        Matrix4x4 loccalToWorldMatrix = new Matrix4x4(
        new Vector4(forward.x, forward.y, forward.z, 0),
        new Vector4(up.x, up.y, up.z, 0),
        new Vector4(right.x, right.y, right.z, 0),
        new Vector4(origin.x, origin.y, origin.z, 1)
    );
        Vector3 pointLocal = new Vector3(Localposition.x * spawnRadius, 0.2f, Localposition.y * spawnRadius); // a little above surface
        return loccalToWorldMatrix.MultiplyPoint3x4(pointLocal);
    }
    private void OccupyPoseList(List<PointWithOrientation> pointList)
    {
        poseList.Clear();
        for (int i = 0; i < pointList.Count; i++)
        {
            Pose point;
            Quaternion rot = Quaternion.LookRotation(pointList[i].forward, pointList[i].up);
            point.rotation = randomRotation ? Quaternion.AngleAxis(randValues[i] * 360f, pointList[i].up) * rot : rot;
            point.position = pointList[i].position;
            poseList.Add(point);
        }
    }
    private void DrawAxisGizmo(Vector3 position, Vector3 forward, Vector3 up, Vector3 right)
    {
        float scale = HandleUtility.GetHandleSize(position) * 0.35f;
        Handles.color = Color.red;
        Handles.DrawAAPolyLine(5, position, position + forward * scale);
        Handles.color = Color.blue;
        Handles.DrawAAPolyLine(5, position, position + right * scale);
        Handles.color = Color.green;
        Handles.DrawAAPolyLine(5, position, position + up * scale);
    }

    private void ReleaseHotControl()
    {
        if (GUIUtility.hotControl == controlID && Event.current.type == EventType.MouseUp)
        {
            GUIUtility.hotControl = 0;
        }
    }

    private void DrawObjectPreview(GameObject go, bool isSnappedMode)
    {
        if (isSnappedMode)
        {
            Matrix4x4 localToWorld = Matrix4x4.TRS(hitPoint.position, hitPoint.rotation, Vector3.one);
            DrawMesh(go, localToWorld, true, false);
        }
        else
        {
            for (int i = 0; i < poseList.Count; i++)
            {
                Matrix4x4 localToWorld = Matrix4x4.TRS(poseList[i].position, poseList[i].rotation, Vector3.one);
                DrawMesh(go, localToWorld, !keepRootRotation, randomScale,i);
            }
        }
    }

    private List<GameObject> GetObjectsInDeletionRange()
    {
        List<GameObject> objsList = new List<GameObject>();
        bool hasCollider = (originalPrefab.GetComponent<Collider>() != null);
        if (hasCollider)
        {
            Collider[] colliders = Physics.OverlapSphere(hitPoint.position, deletionRadius);
            foreach (Collider c in colliders)
            {
                GameObject o = c.gameObject;
                if (originalPrefab == PrefabUtility.GetCorrespondingObjectFromSource(o))
                {
                    if (IsWithinDeletionHeightRange(o)) //height range 
                    {
                        objsList.Add(o);
                    }
                }
            }
            return objsList;
        }
        else
        {
            GameObject[] objs = PrefabUtility.FindAllInstancesOfPrefab(originalPrefab);
            foreach (GameObject o in objs)
            {
                float distance = Vector3.Distance(o.transform.position, hitPoint.position);
                if (distance < deletionRadius && IsWithinDeletionHeightRange(o))
                {
                    objsList.Add(o);

                }
            }
            return objsList;
        }
    }

    private void DrawDeletionPreviews(List<GameObject> objs)
    {
        if (deletionMaterial == null) return;
        deletionMaterial.SetPass(0);
        foreach (GameObject o in objs)
        {
            MeshFilter[] filters = o.GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                Graphics.DrawMeshNow(mesh, filter.transform.localToWorldMatrix);
            }
        }
    }

        private void DrawMesh(GameObject o, Matrix4x4 localToWorld, bool ignoreParentRotation, bool randScale, int randScaleIndex = -1)
        {
            if (previewMaterial == null) return;
            previewMaterial.SetPass(0);
            MeshFilter[] filters = o.GetComponentsInChildren<MeshFilter>();
            Matrix4x4 yAxisOffsetMatrix = Matrix4x4.TRS(new Vector3(0f, offset, 0f), Quaternion.identity, Vector3.one);
            Matrix4x4 ignoreParentPositionMatrix = Matrix4x4.TRS(-o.transform.position, Quaternion.identity, Vector3.one);
            Matrix4x4 ignoreParentMatrix = ignoreParentPositionMatrix;
            if (ignoreParentRotation)
            {
                Matrix4x4 ignoreParentRotationMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Inverse(o.transform.rotation), Vector3.one);
                ignoreParentMatrix = ignoreParentRotationMatrix * ignoreParentMatrix;
            }
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                Matrix4x4 childMatrix = filter.transform.localToWorldMatrix;
                Matrix4x4 outputMatrix = localToWorld * yAxisOffsetMatrix * ignoreParentMatrix * childMatrix;
                if (randScale)
                {
                    float scale = randValues[randScaleIndex] * 2 - 1;
                    scale *= scaleRange;
                    outputMatrix = outputMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * (1 + scale));
                }
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
        for (int i=0; i<poseList.Count; i++)
        {
            GameObject spawnObject = (GameObject)PrefabUtility.InstantiatePrefab(originalPrefab);
            Undo.RegisterCreatedObjectUndo(spawnObject, "Spawn Objects");
            spawnObject.transform.position = poseList[i].position + poseList[i].rotation * new Vector3(0f, offset, 0f);
            spawnObject.transform.rotation = keepRootRotation ? poseList[i].rotation * originalPrefab.transform.rotation : poseList[i].rotation;
            if (randomScale)
            {
                float scale = randValues[i] * 2 - 1;
                scale *= scaleRange;
                spawnObject.transform.localScale = spawnObject.transform.localScale * (1 + scale);
            }
        }
            GenerateRandPoints();
            GenerateRandValues();
    }

    private void GenerateRandValues()
    {
        randValues = new float[spawnCount];
        for (int i = 0; i < spawnCount; i++)
        {
            randValues[i] = Random.value;
        }
    }

    private void GenerateRandPoints()
    {
        int retryCount = 0;
        randPoints = new List<Vector2>();
        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 newPoint = Random.insideUnitCircle;
            while (!IsPointValid(newPoint))
            {
                if (retryCount > 20)
                {
                    Debug.Log("aaa");
                    return;
                }
                newPoint = Random.insideUnitCircle;
                retryCount++;
            }
            retryCount = 0;
            randPoints.Add(newPoint);
        }
    }

    private bool IsPointValid(Vector2 point)
    {
        return !randPoints.Any(existingPoint => Vector2.Distance(existingPoint, point) * spawnRadius < spacing);
    }

    private void ValidateRandPoints()
    {
        for (int i = 0; i < randPoints.Count - 1; i++)
        {
            for (int j = i + 1; j < randPoints.Count; j++)
            {
                float dist = Vector2.Distance(randPoints[i], randPoints[j]);
                if (dist * spawnRadius < spacing)
                {
                    GenerateRandPoints();
                    return;
                }
            }
        }
    }

    private void DrawDisc(RaycastHit hit, Color color, float radius, float thickness)
    {
        Handles.color = color;
        Handles.DrawWireDisc(hit.point, hit.normal, radius, thickness);
        Handles.color = Color.white;
    }

    private bool IsWithinDeletionHeightRange(GameObject o)
    {
        float size = GetObjectBoundingBoxSize(o);
        if (size == -1f) return false;
        float range = 2f * size;
        return DistancePlanePoint(hitPoint.rotation * Vector3.up, hitPoint.position, o.transform.position) < range;
    }

    private bool IsObjectFromPrefab(GameObject o, GameObject prefab)
    {
        GameObject rootObject = PrefabUtility.GetOutermostPrefabInstanceRoot(o);
        if (rootObject == null)
        {
            return PrefabUtility.GetCorrespondingObjectFromOriginalSource(o) == prefab;
        }
        else
        {
            return PrefabUtility.GetCorrespondingObjectFromOriginalSource(rootObject) == prefab;
        }
    }

    private bool IsFromObject(GameObject o, GameObject sourceObj)
    {
        GameObject rootObject = PrefabUtility.GetOutermostPrefabInstanceRoot(o);
        if (rootObject == null)
        {
            return o.GetInstanceID() == sourceObj.GetInstanceID();
        }
        else
        {
            return rootObject.GetInstanceID() == sourceObj.GetInstanceID();
        }
    }

    private float DistancePlanePoint(Vector3 planeNormal, Vector3 planePoint, Vector3 point)
    {
        return Mathf.Abs(Vector3.Dot(planeNormal, (point - planePoint)));
    }

    private Color GetInverseColor(Color color)
    {
        return new Color(1 - color.r, 1 - color.g, 1 - color.b, color.a);
    }

    private bool IsPrefab(GameObject o)
    {
        return o.scene.name == null;
    }
}