using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class Placer : EditorWindow
{
    [MenuItem("Tools/Placer")]
    public static void OpenWindow()
    {
        GetWindow<Placer>();
    }

    public float spawnRadius = 3f;
    public float rotationOffset = 0f;
    public float spacing = 0.1f;
    public float deletionRadius = 1f;
    public int spawnCount = 5;
    public bool on = true;
    public bool randomRotation = false;
    public float randAngle = 180f;
    public bool randomScale = false;
    public float scaleRange = 0.1f;
    public float heightOffset = 0f;
    public bool keepRootRotation = false;
    public Color radiusColor = new Color(0.866f, 0.160f, 0.498f, 1f);
    public Mode mode = Mode.Scatter;

    public GameObject prefab;
    public Material previewMaterial;
    public Material deletionMaterial;

    SerializedObject so;
    SerializedProperty propRadius;
    SerializedProperty propRotationOffset;
    SerializedProperty propSpacing;
    SerializedProperty propDeletionRadius;
    SerializedProperty propSpawnCount;
    SerializedProperty propPrefab;
    SerializedProperty propHeightOffset;
    SerializedProperty propRandomRotation;
    SerializedProperty propRandAngle;
    SerializedProperty propScaleRange;
    SerializedProperty propRandomScale;
    SerializedProperty propPreviewMaterial;
    SerializedProperty propDeletionMaterial;
    SerializedProperty propColor;
    SerializedProperty propKeepRootRotation;

    private PrefabInfo prefabInfo;
    private Pose hitPoint;
    private List<Pose> poseList = new List<Pose>();
    private RandPoints randPoints;
    private float[] randValues;
    private bool showPreviewSetting = false;
    private bool shift = false;
    private bool ctrl = false;
    private bool alt = false;
    private bool isInPrefabMode = false;
    private string activateText = "Activate";
    private string deactivateText = "Deactivate";
    private string currentText;
    private float GizmoWidth = 3.5f;
    private int controlID;
    private readonly float minRadius = 0f;
    private readonly float maxRadius = 50f;
    private readonly float minOffset = -5f;
    private readonly float maxOffset = 5f;

    [SerializeField] private GUIContent[] toolIcons;
    [SerializeField] private bool isInit = true;

    private struct PrefabInfo
    {
        public GameObject originalPrefab;
        public bool hasCollider;
        public List<GameObject> cachedAllInstancedInScene;

        public bool RequireList()
        {
            return !(hasCollider || originalPrefab == null);
        }
    }

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

    public struct RandPoints
    {
        public List<Vector2> points;
        public float minDistance;
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
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        SceneView.duringSceneGui += DuringSceneGUI;
        GetProperties();
        GenerateRandPoints();
        GenerateRandValues();
        currentText = on ? deactivateText : activateText;
        controlID = GUIUtility.GetControlID(FocusType.Passive);

        LoadData();
        LoadAssets(isInit);
        isInit = false;

        UpdatePrefabInfo();
    }

    private void OnDisable()
    {
        SaveData();
        SceneView.duringSceneGui -= DuringSceneGUI;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
    }

    private void GetProperties()
    {
        so = new SerializedObject(this);
        propRadius = so.FindProperty(nameof(spawnRadius));
        propDeletionRadius = so.FindProperty(nameof(deletionRadius));
        propSpawnCount = so.FindProperty(nameof(spawnCount));
        propPrefab = so.FindProperty(nameof(prefab));
        propRandomRotation = so.FindProperty(nameof(randomRotation));
        propPreviewMaterial = so.FindProperty(nameof(previewMaterial));
        propDeletionMaterial = so.FindProperty(nameof(deletionMaterial));
        propColor = so.FindProperty(nameof(radiusColor));
        propHeightOffset = so.FindProperty(nameof(heightOffset));
        propScaleRange = so.FindProperty(nameof(scaleRange));
        propKeepRootRotation = so.FindProperty(nameof(keepRootRotation));
        propRandomScale = so.FindProperty(nameof(randomScale));
        propSpacing = so.FindProperty(nameof(spacing));
        propRandAngle = so.FindProperty(nameof(randAngle));
        propRotationOffset = so.FindProperty(nameof(rotationOffset));
    }

    private void OnGUI()
    {
        so.Update();
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(currentText, GUILayout.MaxWidth(230), GUILayout.Height(20)))
        {
            on = !on;
            currentText = on ? deactivateText : activateText;
            if (on)
            {
                OnTurnOn();
            }
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        if (on)
        {
            GUILayout.Space(15);
            EditorGUI.BeginChangeCheck();
            mode = (Mode)GUILayout.Toolbar((int)mode, toolIcons);
            if (EditorGUI.EndChangeCheck())
            {
                if (mode == Mode.Delete)
                {
                    OnDeleteMode();
                }
            }
            GUILayout.Space(20);
            if (mode == Mode.Scatter)
            {
                EditorGUI.BeginChangeCheck();
                float newRadius = EditorGUILayout.Slider("Radius", propRadius.floatValue, minRadius, maxRadius);
                if (EditorGUI.EndChangeCheck())
                {
                    propRadius.floatValue = newRadius;
                    ValidateRandPoints();
                }
                EditorGUI.BeginChangeCheck();
                int newSpawnCount = EditorGUILayout.IntSlider("Spawn Count", propSpawnCount.intValue, 1, 50);
                if (EditorGUI.EndChangeCheck())
                {
                    propSpawnCount.intValue = newSpawnCount;
                    GenerateRandPoints();
                }
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(propSpacing);
                propSpacing.floatValue = Mathf.Max(0f, propSpacing.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    ValidateRandPoints();
                }
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

            EditorGUILayout.PropertyField(propHeightOffset);
            if (mode != Mode.None)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(propPrefab);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdatePrefabInfo();
                }
            }

            GUILayout.Space(15);
            if (mode == Mode.Scatter || mode == Mode.Place)
            {
                EditorGUILayout.PropertyField(propRotationOffset);
                propRotationOffset.floatValue = Mathf.Clamp(propRotationOffset.floatValue, 0f, 360f);
                EditorGUILayout.PropertyField(propKeepRootRotation);
                EditorGUILayout.PropertyField(propRandomRotation);
                if (randomRotation)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(propRandAngle, new GUIContent("Euler Angle"));
                    propRandAngle.floatValue = Mathf.Clamp(propRandAngle.floatValue, 0f, 360f);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space(10);
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
            SceneView.RepaintAll();
        }
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)   //unfocus the window when click elsewhere
        {
            GUI.FocusControl(null);
            Repaint();
        }
    }

    private void DuringSceneGUI(SceneView scene)
    {
        isInPrefabMode = (PrefabStageUtility.GetCurrentPrefabStage() != null);
        if (!on || isInPrefabMode) return;
        Handles.zTest = CompareFunction.LessEqual;
        List<PointWithOrientation> pointList = new List<PointWithOrientation>();
        bool isSnappedMode = ctrl;
        if (Event.current.type == EventType.MouseMove)
        {
            scene.Repaint();
        }

        KeyModifierCheck();
        if (mode != Mode.None || ctrl)
        {
            RaycastToMousePosition(pointList, scene.camera, isSnappedMode);
        }
        ScrollWheelCheck();
        if (isSnappedMode)
        {
            DrawSnapObjectsPreview();
            SnapModeInputCheck();
        }
        switch (mode)
        {
            case Mode.Delete:
                if (isSnappedMode || prefabInfo.originalPrefab == null) return;
                DeleteModeInputCheck();
                List<GameObject> objsInDeletionRange = GetObjectsInDeletionRange();
                DrawDeletionPreviews(objsInDeletionRange);
                break;
            default:
                OccupyPoseList(pointList);
                NonDeleteModeInputCheck();
                if (prefabInfo.originalPrefab != null && !isSnappedMode)
                {
                    DrawObjectPreview(prefabInfo.originalPrefab, false);
                }
                break;
        }
        Handles.zTest = CompareFunction.Always;
    }

    private void RaycastToMousePosition(List<PointWithOrientation> pointList, Camera cam, bool isSnappedMode)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            RaycastHit finalHit = hit;
            if (!isSnappedMode) //non snap mode
            {
                switch (mode)
                {
                    case Mode.Delete:
                        if (IsObjectFromPrefab(hit.collider.gameObject, prefabInfo.originalPrefab))
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
            if (!IsObjectFromPrefab(hit.collider.gameObject, prefabInfo.originalPrefab))
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
        if (hitTangent.sqrMagnitude < 0.001f)
        {
            hitTangent = Vector3.Cross(hitNormal, cam.transform.right).normalized;
        }
        Vector3 hitBitangent = Vector3.Cross(hitNormal, hitTangent);
        hitPoint.position = hit.point;
        hitPoint.rotation = Quaternion.AngleAxis(rotationOffset, hitNormal) * Quaternion.LookRotation(hitTangent, hitNormal);
        if (ctrl || prefabInfo.originalPrefab == null) return;
        switch (mode)
        {
            case Mode.Delete:
                {
                    DrawRange(hit, GetInverseColor(radiusColor), deletionRadius);
                    PointWithOrientation pointInfo = new PointWithOrientation(hit.point, hitTangent, hitNormal);
                    pointList.Add(pointInfo);
                    break;
                }
            case Mode.Scatter:
                {
                    DrawRange(hit, radiusColor, spawnRadius);
                    DrawAxisGizmo(hit.point, hitTangent, hitNormal, hitBitangent);
                    float raycastOffset = GetRaycastOffset();
                    float raycastmaxDistance = GetRaycastMaxDistance();
                    foreach (Vector2 p in randPoints.points)
                    {
                        Vector3 worldPos = GetWorldPosFromLocal(p, hit.point, hitTangent, hitNormal, hitBitangent, spawnRadius);
                        Ray pointRay = new Ray(worldPos + hitNormal * raycastOffset, -hitNormal);
                        if (Physics.Raycast(pointRay, out RaycastHit pointHit, raycastmaxDistance))
                        {
                            Vector3 forward = Vector3.Cross(pointHit.normal, cam.transform.up).normalized;
                            if (forward.sqrMagnitude < 0.001f)
                            {
                                forward = Vector3.Cross(pointHit.normal, cam.transform.right).normalized;
                            }
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
                    DrawAxisGizmo(hit.point, hitTangent, hitNormal, hitBitangent);
                }
                break;
            default:
                break;
        }
    }

    private float GetRaycastOffset()
    {
        return spawnRadius / 3f;
    }

    private float GetRaycastMaxDistance()
    {
        return GetRaycastOffset() * 3f;
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
        if (shift && Event.current.isMouse && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            if (prefabInfo.originalPrefab != null)
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
        if (Event.current.isMouse && Event.current.type == EventType.MouseDown && Event.current.button == 0) //left click
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
        if (Event.current.isMouse && Event.current.type == EventType.MouseDown && Event.current.button == 0)
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
        float newValue = Mathf.Clamp(propHeightOffset.floatValue - scrollDir * 0.1f, minOffset, maxOffset);
        so.Update();
        propHeightOffset.floatValue = newValue;
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

    private Vector3 GetWorldPosFromLocal(Vector2 Localposition, Vector3 origin, Vector3 forward, Vector3 up, Vector3 right, float radius)
    {
        Matrix4x4 loccalToWorldMatrix = new Matrix4x4(
        new Vector4(forward.x, forward.y, forward.z, 0),
        new Vector4(up.x, up.y, up.z, 0),
        new Vector4(right.x, right.y, right.z, 0),
        new Vector4(origin.x, origin.y, origin.z, 1)
    );
        Vector3 pointLocal = new Vector3(Localposition.x, 0f, Localposition.y) * radius;
        return loccalToWorldMatrix.MultiplyPoint3x4(pointLocal);
    }

    private void OccupyPoseList(List<PointWithOrientation> pointList)
    {
        poseList.Clear();
        for (int i = 0; i < pointList.Count; i++)
        {
            Pose point;
            Quaternion rot = Quaternion.LookRotation(pointList[i].forward, pointList[i].up);
            if (randomRotation)
            {
                point.rotation = Quaternion.AngleAxis(randValues[i] * randAngle + rotationOffset, pointList[i].up) * rot;
            }
            else
            {
                point.rotation = Quaternion.AngleAxis(rotationOffset, pointList[i].up) * rot;
            }
            point.position = pointList[i].position;
            poseList.Add(point);
        }
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
                DrawMesh(go, localToWorld, !keepRootRotation, randomScale, i);
            }
        }
    }

    private List<GameObject> GetObjectsInDeletionRange()
    {
        List<GameObject> objsList = new List<GameObject>();
        if (prefabInfo.hasCollider)
        {
            Collider[] colliders = Physics.OverlapSphere(hitPoint.position, deletionRadius);
            foreach (Collider c in colliders)
            {
                GameObject o = c.gameObject;
                if (prefabInfo.originalPrefab == PrefabUtility.GetCorrespondingObjectFromSource(o))
                {
                    if (IsWithinDeletionHeightRange(o)) //height range 
                    {
                        objsList.Add(o);
                    }
                }
            }
        }
        else
        {
            foreach (GameObject o in prefabInfo.cachedAllInstancedInScene)
            {
                if (o == null) continue;   // prevent error when deleting 
                float distance = Vector3.Distance(o.transform.position, hitPoint.position);
                if (distance < deletionRadius && IsWithinDeletionHeightRange(o))
                {
                    objsList.Add(o);
                }
            }
        }
        return objsList;
    }

    private List<GameObject> FindAllInstancesOfPrefab(GameObject prefab)
    {
        List<GameObject> foundInstances = new List<GameObject>();
        GameObject[] allObjects = (GameObject[])FindObjectsOfType(typeof(GameObject));
        foreach (GameObject obj in allObjects)
        {
            if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(obj) == prefab)
            {
                foundInstances.Add(obj);
            }
        }
        return foundInstances;
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
        Matrix4x4 yAxisOffsetMatrix = Matrix4x4.TRS(new Vector3(0f, heightOffset, 0f), Quaternion.identity, Vector3.one);
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
            Selection.gameObjects[i].transform.position = hitPoint.position + hitPoint.rotation * new Vector3(0f, heightOffset, 0f);
            Selection.gameObjects[i].transform.rotation = hitPoint.rotation;
        }
    }

    private void SpawnPrefabs(List<Pose> poseList)
    {
        if (prefabInfo.originalPrefab == null) return;
        for (int i = 0; i < poseList.Count; i++)
        {
            GameObject spawnObject = (GameObject)PrefabUtility.InstantiatePrefab(prefabInfo.originalPrefab);
            Undo.RegisterCreatedObjectUndo(spawnObject, "Spawn Objects");
            spawnObject.transform.position = poseList[i].position + poseList[i].rotation * new Vector3(0f, heightOffset, 0f);
            spawnObject.transform.rotation = keepRootRotation ? poseList[i].rotation * prefabInfo.originalPrefab.transform.rotation : poseList[i].rotation;
            if (randomScale)
            {
                float scale = randValues[i] * 2 - 1; //-1 ~ 1
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
        randPoints.points = new List<Vector2>();
        randPoints.minDistance = float.MaxValue;
        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 newPoint = Random.insideUnitCircle;
            while (!IsPointValid(newPoint))
            {
                if (retryCount > 20)
                {
                    return;
                }
                newPoint = Random.insideUnitCircle;
                retryCount++;
            }
            retryCount = 0;
            randPoints.points.Add(newPoint);
        }
    }

    private bool IsPointValid(Vector2 point)
    {
        foreach (Vector2 existingPoint in randPoints.points)
        {
            float distance = Vector2.Distance(existingPoint, point);
            if (distance * spawnRadius < spacing)
            {
                return false;
            }
            randPoints.minDistance = Mathf.Min(randPoints.minDistance, distance);
        }
        return true;
    }

    private void ValidateRandPoints()
    {
        float currentPointAmout = randPoints.points.Count;
        if (currentPointAmout < spawnCount || randPoints.minDistance * spawnRadius < spacing)
        {
            GenerateRandPoints();
        }
    }

    private void DrawRange(RaycastHit hit, Color color, float radius)
    {
        int segments = 63;
        Vector3[] points = new Vector3[segments];
        Vector3 forward = Vector3.Cross(hit.normal, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.Cross(hit.normal, Vector3.right).normalized;
        }
        Vector3 right = Vector3.Cross(forward, hit.normal);
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments;
            float angle = 2 * Mathf.PI * t;
            Vector2 pointLS = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector3 pointWS = GetWorldPosFromLocal(pointLS, hit.point, forward, hit.normal, right, radius);
            points[i] = pointWS;
        }
        Vector3?[] surfaceHits = new Vector3?[segments + 1];
        float raycastOffset = GetRaycastOffset();
        float raycastmaxDistance = GetRaycastMaxDistance();
        for (int i = 0; i < segments; i++)
        {
            Ray pointRay = new Ray(points[i] + hit.normal * raycastOffset, -hit.normal);
            if (Physics.Raycast(pointRay, out RaycastHit pointHit, raycastmaxDistance))
            {
                surfaceHits[i] = pointHit.point;
            }
            else
            {
                surfaceHits[i] = null;
            }
        }
        for (int i = 0; i < segments; i++)  //connect last element to first
        {
            if (surfaceHits[i] != null)
            {
                surfaceHits[segments] = surfaceHits[i];
                break;
            }
        }
        DrawLinesOnSurface(surfaceHits, color);
    }

    private void DrawLinesOnSurface(Vector3?[] surfaceHits, Color color) //draw dotted line when null point in between
    {
        Handles.color = color;
        Vector3? lastPoint = null;
        bool isNull = false;
        for (int i = 0; i < surfaceHits.Length; i++)
        {
            if (surfaceHits[i] == null)
            {
                isNull = true;
                continue;
            }
            if (lastPoint == null)
            {
                lastPoint = surfaceHits[i];
                continue;
            }
            if (!isNull)
            {
                Handles.DrawAAPolyLine(GizmoWidth, (Vector3)lastPoint, (Vector3)surfaceHits[i]);
            }
            else
            {
                Handles.DrawDottedLine((Vector3)lastPoint, (Vector3)surfaceHits[i], 7);
            }
            lastPoint = surfaceHits[i];
            isNull = false;
        }
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

    private void DrawAxisGizmo(Vector3 position, Vector3 forward, Vector3 up, Vector3 right)
    {
        float scale = HandleUtility.GetHandleSize(position) * 0.2f;
        forward = Quaternion.AngleAxis(rotationOffset, up) * forward;
        right = Quaternion.AngleAxis(rotationOffset, up) * right;
        Handles.zTest = CompareFunction.Always;
        Handles.color = Color.blue;
        Handles.DrawAAPolyLine(GizmoWidth, position, position + forward * scale);
        Handles.color = Color.red;
        Handles.DrawAAPolyLine(GizmoWidth, position, position + right * scale);
        Handles.color = Color.green;
        Handles.DrawAAPolyLine(GizmoWidth, position, position + up * scale);
    }

    private void LoadAssets(bool forceReload)
    {
        if (forceReload || previewMaterial == null || deletionMaterial == null || toolIcons == null || TypeErrorCheck())
        {
            string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
            string parentPath = Path.GetDirectoryName(path);
            previewMaterial = (Material)AssetDatabase.LoadAssetAtPath(parentPath + "/Material/Preview.mat", typeof(Material));
            deletionMaterial = (Material)AssetDatabase.LoadAssetAtPath(parentPath + "/Material/Deletion.mat", typeof(Material));
            toolIcons = new GUIContent[]
            {
                new GUIContent((Texture)AssetDatabase.LoadAssetAtPath(parentPath + "/Texture/brush.png", typeof(Texture)),"Scatter"),
                new GUIContent((Texture)AssetDatabase.LoadAssetAtPath(parentPath + "/Texture/pen.png", typeof(Texture)),"Place"),
                new GUIContent((Texture)AssetDatabase.LoadAssetAtPath(parentPath + "/Texture/rubber.png", typeof(Texture)),"Delete"),
                new GUIContent((Texture)AssetDatabase.LoadAssetAtPath(parentPath + "/Texture/snap.png", typeof(Texture)),"None"),
            };
        }
    }

    private bool TypeErrorCheck()
    {
        return previewMaterial.GetType() != typeof(Material) || deletionMaterial.GetType() != typeof(Material);
    }

    private void LoadData()
    {
        string data = EditorPrefs.GetString(this.GetType().ToString(), JsonUtility.ToJson(this, false));
        JsonUtility.FromJsonOverwrite(data, this);
    }

    private void SaveData()
    {
        string data = JsonUtility.ToJson(this, false);
        EditorPrefs.SetString(this.GetType().ToString(), data);
    }

    private void UpdatePrefabInfo()
    {
        GameObject obj = (GameObject)propPrefab.objectReferenceValue;
        if (obj != null)
        {
            prefabInfo.originalPrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(obj);
            prefabInfo.hasCollider = (prefabInfo.originalPrefab.GetComponent<Collider>() != null);
        }
        else
        {
            prefabInfo.originalPrefab = null;
        }
        if (!prefabInfo.hasCollider && prefabInfo.originalPrefab != null)
        {
            UpdateDeletionObjectsList();
        }
    }
    private void UpdateDeletionObjectsList()
    {
        prefabInfo.cachedAllInstancedInScene = FindAllInstancesOfPrefab(prefabInfo.originalPrefab);
    }

    private void OnTurnOn()
    {
        if (prefabInfo.RequireList() && mode == Mode.Delete)
        {
            UpdateDeletionObjectsList();
        }
    }

    private void OnDeleteMode()
    {
        if (prefabInfo.RequireList())
        {
            UpdateDeletionObjectsList();
        }
    }

    private void OnHierarchyChanged()
    {
        if (!on || mode != Mode.Delete || !prefabInfo.RequireList()) return;
        UpdateDeletionObjectsList();
    }

}