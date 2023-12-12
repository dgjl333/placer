using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static Dg.Placer.RandData;

namespace Dg
{
    internal class Placer : EditorWindow
    {
        [MenuItem("Tools/Placer")]
        public static void OpenWindow()
        {
            GetWindow<Placer>();
        }

        public bool on = true;
        public Mode mode = Mode.Scatter;
        public float spawnRadius = 3f;
        public float rotationOffset = 0f;
        public float spacing = 0.1f;
        public float deletionRadius = 1f;
        public int spawnCount = 5;
        public float randAngle = 180f;
        public float randScaleMin = 0.9f;
        public float randScaleMax = 1.1f;
        public float randHeightMin = 0f;
        public float randHeightMax = 2f;
        public float heightOffset = 0f;
        public float scatterHeightTolerance = 2f;
        public bool keepRootRotation = false;
        public bool randScale = false;
        public bool randRotation = false;
        public bool randHeight = false;
        public bool alignWithWorldAxis = true;
        public Color radiusColor = new Color(0.866f, 0.160f, 0.498f, 1f);

        private GameObject prefab;
        private Material previewMaterial;
        private Material deletionMaterial;
        private GUIContent[] toolIcons;

        private SerializedObject so;
        private SerializedProperty propRadius;
        private SerializedProperty propRotationOffset;
        private SerializedProperty propSpacing;
        private SerializedProperty propDeletionRadius;
        private SerializedProperty propSpawnCount;
        private SerializedProperty propHeightOffset;
        private SerializedProperty propRandRotation;
        private SerializedProperty propRandAngle;
        private SerializedProperty propRandScale;
        private SerializedProperty propRandScaleMin;
        private SerializedProperty propRandScaleMax;
        private SerializedProperty propRandHeightMin;
        private SerializedProperty propRandHeightMax;
        private SerializedProperty propColor;
        private SerializedProperty propKeepRootRotation;
        private SerializedProperty propScatterHeightTolerance;
        private SerializedProperty propAlignWithWorldAxis;
        private SerializedProperty propRandHeight;

        public bool isShowAdvancedSetting = false;
        public bool isShowRandSetting = true;
        [SerializeField] private string prefabLocation = null;
        private PrefabErrorMode prefabError = PrefabErrorMode.None;
        private Pose hitPoint;
        private List<Pose> poseList = new List<Pose>();
        private PrefabInfo prefabInfo;
        private bool shift = false;
        private bool ctrl = false;
        private bool alt = false;
        private bool isInPrefabMode = false;
        private int controlID;

        private readonly float minRadius = 0f;
        private readonly float maxRadius = 50f;
        private readonly float minOffset = -5f;
        private readonly float maxOffset = 5f;
        private readonly float GizmoWidth = 3f;
        private readonly int discSegment = 64;

        internal static class RandData
        {
            public static class RandPoints
            {
                public static List<Vector2> points;
                public static float minDistance;

                public static void GenerateRandPoints(int count, float spawnRadius, float spacing)
                {
                    int retryCount = 0;
                    points = new List<Vector2>();
                    minDistance = float.MaxValue;
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 newPoint = Random.insideUnitCircle;
                        while (!IsPointValid(newPoint, spawnRadius, spacing))
                        {
                            if (retryCount > 20)
                            {
                                return;
                            }
                            newPoint = Random.insideUnitCircle;
                            retryCount++;
                        }
                        retryCount = 0;
                        points.Add(newPoint);
                    }
                }

                public static void ValidateRandPoints(int count, float spawnRadius, float spacing)
                {
                    float currentPointAmout = points.Count;
                    if (currentPointAmout < count || minDistance * spawnRadius < spacing)
                    {
                        GenerateRandPoints(count, spawnRadius, spacing);
                    }
                }

                private static bool IsPointValid(Vector2 point, float spawnRadius, float spacing)
                {
                    foreach (Vector2 existingPoint in points)
                    {
                        float distance = Vector2.Distance(existingPoint, point);
                        if (distance * spawnRadius < spacing)
                        {
                            return false;
                        }
                        minDistance = Mathf.Min(minDistance, distance);
                    }
                    return true;
                }
            }

            private static float[] randValues;

            public static void GenerateRandValues(int count)
            {
                randValues = new float[count];
                for (int i = 0; i < count; i++)
                {
                    randValues[i] = Random.value;
                }
            }

            public static float GetRandScale(int index, float scaleMin, float scaleMax)
            {
                float rand = GetRandValue(index);
                return Mathf.Lerp(scaleMin, scaleMax, rand);
            }

            public static float GetRandRotation(int index, float angleMax)
            {
                float rand = GetRandValue(index);
                return rand * angleMax;
            }

            public static float GetRandHeight(int index, float heightMin, float heightMax)
            {
                float rand = GetRandValue(index);
                return Mathf.Lerp(heightMin, heightMax, rand);
            }

            private static float GetRandValue(int index) => randValues[index % randValues.Length];
        }
        private struct HitInfo
        {
            public Vector3 Tangent { get; private set; }
            public Vector3 BiTangent { get; private set; }
            public Vector3 Normal { get; private set; }

            public HitInfo(Vector3 normal, Camera cam, bool alignWithWorldAxis)
            {
                Vector3 hitTangent;
                if (alignWithWorldAxis)
                {
                    hitTangent = Vector3.Cross(Vector3.right, normal).normalized;
                    if (hitTangent.sqrMagnitude < 0.001f)
                    {
                        hitTangent = Vector3.Cross(Vector3.up, normal).normalized;
                    }
                }
                else
                {
                    hitTangent = Vector3.Cross(cam.transform.right, normal).normalized;
                    if (hitTangent.sqrMagnitude < 0.001f)
                    {
                        hitTangent = Vector3.Cross(cam.transform.up, normal).normalized;
                    }
                }
                this.Normal = normal;
                this.Tangent = hitTangent;
                this.BiTangent = Vector3.Cross(normal, hitTangent);
            }
        }

        private struct PrefabInfo
        {
            public GameObject originalPrefab;
            public bool hasCollider;
            public IEnumerable<GameObject> cachedAllInstancedInScene;
            public bool requireList => !(hasCollider || originalPrefab == null);
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


        public enum PrefabErrorMode
        {
            None,
            NotAnPrefab,
            NotOuterMostPrefab
        }

        public enum Mode
        {
            Scatter,
            Place,
            Delete,
            Snap
        }

        private Dictionary<PrefabErrorMode, string> errorTable = new Dictionary<PrefabErrorMode, string>()
        {
            { PrefabErrorMode.NotAnPrefab, "KNotPrefabError" },
            { PrefabErrorMode.NotOuterMostPrefab, "KNotOuterMostPrefabError" }
        };

        private void Awake()
        {
            if (Camera.main != null)
            {
                Camera.main.depthTextureMode = DepthTextureMode.Depth;   //shader need depth texture
            }
            LoadData();
            LoadPrefab();
            LoadAssets();
        }

        private void OnEnable()
        {
            LoadAssets();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            SceneView.duringSceneGui += DuringSceneGUI;
            GetProperties();
            RandPoints.GenerateRandPoints(spawnCount, spawnRadius, spacing);
            GenerateRandValues(spawnCount);
            controlID = GUIUtility.GetControlID(FocusType.Passive);
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
            propRandRotation = so.FindProperty(nameof(randRotation));
            propColor = so.FindProperty(nameof(radiusColor));
            propHeightOffset = so.FindProperty(nameof(heightOffset));
            propRandScaleMin = so.FindProperty(nameof(randScaleMin));
            propRandScaleMax = so.FindProperty(nameof(randScaleMax));
            propRandHeightMin = so.FindProperty(nameof(randHeightMin));
            propRandHeightMax = so.FindProperty(nameof(randHeightMax));
            propKeepRootRotation = so.FindProperty(nameof(keepRootRotation));
            propRandScale = so.FindProperty(nameof(randScale));
            propSpacing = so.FindProperty(nameof(spacing));
            propRandAngle = so.FindProperty(nameof(randAngle));
            propRotationOffset = so.FindProperty(nameof(rotationOffset));
            propScatterHeightTolerance = so.FindProperty(nameof(scatterHeightTolerance));
            propAlignWithWorldAxis = so.FindProperty(nameof(alignWithWorldAxis));
            propRandHeight = so.FindProperty(nameof(randHeight));
        }

        private void OnGUI()
        {
            so.Update();
            DrawHeader();
            if (on)
            {
                DrawToolBar();
                switch (mode)
                {
                    case Mode.Scatter:
                        DrawAlignWithWorldAxis();
                        DrawSpawnRadius();
                        DrawSpawnCount();
                        DrawSpacing();
                        DrawHeightOffset();
                        DrawRotationOffset();
                        DrawPrefab();
                        DrawRandom();
                        break;
                    case Mode.Place:
                        DrawAlignWithWorldAxis();
                        DrawSpawnRadius();
                        DrawHeightOffset();
                        DrawRotationOffset();
                        DrawPrefab();
                        DrawRandom();
                        break;
                    case Mode.Delete:
                        DrawAlignWithWorldAxis();
                        DrawDeleteRadius();
                        DrawHeightOffset();
                        DrawPrefab();
                        break;
                    case Mode.Snap:
                        DrawAlignWithWorldAxis();
                        DrawHeightOffset();
                        DrawRotationOffset();
                        break;
                    default:
                        break;
                }
                DrawAdvanced(); 
            }
            if (so.ApplyModifiedProperties())
            {
                SceneView.RepaintAll();
            }

            void DrawHeader()
            {
                GUILayout.Space(10);
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    string currentText = on ? GetText("KDeactivate") : GetText("KActivate");
                    string currentLang = LanguageSetting.language.ToString();
                    GUIStyle langButton = new GUIStyle(GUI.skin.button);
                    langButton.fontStyle = FontStyle.Bold;
                    langButton.fontSize = (int)(langButton.fontSize * 0.9f);
                    if (GUILayout.Button(currentLang, langButton, GUILayout.MaxWidth(40), GUILayout.MaxHeight(20)))
                    {
                        LanguageSetting.SwitchLanguage();
                        LoadAssets();
                    }
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(currentText, GUILayout.MaxWidth(230), GUILayout.Height(20)))
                    {
                        on = !on;
                        if (on)
                        {
                            OnTurnOn();
                        }
                    }
                    GUILayout.FlexibleSpace();
                }
            }

            void DrawToolBar()
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
            }

            void DrawSpawnRadius()
            {
                EditorGUI.BeginChangeCheck();
                float newRadius = EditorGUILayout.Slider(GetText("KRadius"), propRadius.floatValue, minRadius, maxRadius);
                if (EditorGUI.EndChangeCheck())
                {
                    propRadius.floatValue = newRadius;
                    RandPoints.ValidateRandPoints(spawnCount, spawnRadius, spacing);
                }
            }

            void DrawDeleteRadius()
            {
                EditorGUI.BeginChangeCheck();
                float newRadius = EditorGUILayout.Slider(GetText("KRadius"), propDeletionRadius.floatValue, minRadius, maxRadius);
                if (EditorGUI.EndChangeCheck())
                {
                    propDeletionRadius.floatValue = newRadius;
                }
            }

            void DrawSpawnCount()
            {
                EditorGUI.BeginChangeCheck();
                int newSpawnCount = EditorGUILayout.IntSlider(GetText("KSpawnCount"), propSpawnCount.intValue, 1, 50);
                if (EditorGUI.EndChangeCheck())
                {
                    propSpawnCount.intValue = newSpawnCount;
                    GenerateRandValues(spawnCount);
                    RandPoints.GenerateRandPoints(spawnCount, spawnRadius, spacing);
                }
            }

            void DrawSpacing()
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(propSpacing, new GUIContent(GetText("KMinSpacing")));
                propSpacing.floatValue = Mathf.Max(0f, propSpacing.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    RandPoints.ValidateRandPoints(spawnCount, spawnRadius, spacing);
                }
            }

            void DrawHeightOffset()
            {
                EditorGUILayout.PropertyField(propHeightOffset, new GUIContent(GetText("KHeightOffset")));

            }

            void DrawAlignWithWorldAxis()
            {
                EditorGUILayout.PropertyField(propAlignWithWorldAxis, new GUIContent(GetText("KAlignWithWorld")));
            }

            void DrawPrefab()
            {
                EditorGUI.BeginChangeCheck();
                prefab = (GameObject)EditorGUILayout.ObjectField(GetText("KPrefab"), prefab, typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                {
                    ValidatePrefab();
                    UpdatePrefabInfo();
                }
                if (prefabError != PrefabErrorMode.None)
                {
                    using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField(GetText(errorTable[prefabError]), EditorStyles.wordWrappedLabel);
                    }
                }
                GUILayout.Space(15);
            }

            void DrawRotationOffset()
            {
                EditorGUILayout.PropertyField(propRotationOffset, new GUIContent(GetText("KRotationOffset")));
                propRotationOffset.floatValue = Mathf.Clamp(propRotationOffset.floatValue, 0f, 360f);
            }

            void DrawRandom()
            {
                isShowRandSetting = EditorGUILayout.Foldout(isShowRandSetting, GetText("KRandSetting"));
                if (isShowRandSetting)
                {
                    EditorGUILayout.PropertyField(propRandRotation, new GUIContent(GetText("KRandRotation")));
                    if (randRotation)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            EditorGUILayout.PropertyField(propRandAngle, new GUIContent(GetText("KEulerAngle")));
                            propRandAngle.floatValue = Mathf.Clamp(propRandAngle.floatValue, 0f, 360f);
                        }
                    }
                    EditorGUILayout.PropertyField(propRandScale, new GUIContent(GetText("KRandScale")));
                    if (randScale)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            EditorGUILayout.PropertyField(propRandScaleMin, new GUIContent(GetText("KMinScale")));
                            propRandScaleMin.floatValue = Mathf.Max(0.01f, propRandScaleMin.floatValue);
                            EditorGUILayout.PropertyField(propRandScaleMax, new GUIContent(GetText("KMaxScale")));
                            propRandScaleMax.floatValue = Mathf.Max(0.01f, propRandScaleMax.floatValue);
                        }
                    }
                    EditorGUILayout.PropertyField(propRandHeight, new GUIContent(GetText("KRandHeight")));
                    if (randHeight)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            EditorGUILayout.PropertyField(propRandHeightMin, new GUIContent(GetText("KMinHeight")));
                            EditorGUILayout.PropertyField(propRandHeightMax, new GUIContent(GetText("KMaxHeight")));
                        }
                    }
                }
                GUILayout.Space(20);
            }

            void DrawAdvanced()
            {
                isShowAdvancedSetting = EditorGUILayout.Foldout(isShowAdvancedSetting, GetText("KAdvancedSetting"));
                if (isShowAdvancedSetting)
                {
                    EditorGUILayout.PropertyField(propColor, new GUIContent(GetText("KRadiusColor")));
                    EditorGUILayout.PropertyField(propKeepRootRotation, new GUIContent(GetText("KRootRotation")));
                    EditorGUI.BeginChangeCheck();
                    float newTolerance = EditorGUILayout.Slider(GetText("KTolerance"), propScatterHeightTolerance.floatValue, 0f, 8f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        propScatterHeightTolerance.floatValue = newTolerance;
                    }
                }
            }
        }

        private void DuringSceneGUI(SceneView sceneView)
        {
            isInPrefabMode = (PrefabStageUtility.GetCurrentPrefabStage() != null);
            if (!on || isInPrefabMode) return;
            Handles.zTest = CompareFunction.LessEqual;
            List<PointWithOrientation> pointList = new List<PointWithOrientation>();
            bool isSnappedMode = ctrl;
            if (Event.current.type == EventType.MouseMove)
            {
                sceneView.Repaint();
            }
            KeyModifierCheck();
            if (mode != Mode.Snap || ctrl)
            {
                RaycastToMousePosition(pointList, sceneView.camera, isSnappedMode);
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
                    IEnumerable<GameObject> objsInDeletionRange = GetObjectsInDeletionRange();
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
            if (!Physics.Raycast(ray, out RaycastHit hit)) return;
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
            HitInfo hitInfo = new HitInfo(hit.normal, cam, alignWithWorldAxis);
            hitPoint.position = hit.point;
            hitPoint.rotation = Quaternion.AngleAxis(rotationOffset, hitInfo.Normal) * Quaternion.LookRotation(hitInfo.Tangent, hitInfo.Normal);
            if (ctrl || prefabInfo.originalPrefab == null) return;
            switch (mode)
            {
                case Mode.Delete:
                    {
                        DrawRange(hit, radiusColor, deletionRadius);
                        PointWithOrientation pointInfo = new PointWithOrientation(hit.point, hitInfo.Tangent, hitInfo.Normal);
                        pointList.Add(pointInfo);
                        break;
                    }
                case Mode.Scatter:
                    {
                        DrawRange(hit, radiusColor, spawnRadius);
                        DrawAxisGizmo(hit.point, hitInfo.Tangent, hitInfo.Normal, hitInfo.BiTangent);
                        float raycastOffset = GetRaycastOffset();
                        float raycastmaxDistance = GetRaycastMaxDistance();
                        foreach (Vector2 p in RandPoints.points)
                        {
                            Vector3 worldPos = GetWorldPosFromLocal(p, hit.point, hitInfo.Tangent, hitInfo.Normal, hitInfo.BiTangent, spawnRadius);
                            Ray pointRay = new Ray(worldPos + hitInfo.Normal * raycastOffset, -hitInfo.Normal);
                            if (Physics.Raycast(pointRay, out RaycastHit scatterHit, raycastmaxDistance))
                            {
                                HitInfo scatterHitInfo = new HitInfo(scatterHit.normal, cam, alignWithWorldAxis);
                                PointWithOrientation lookdirection = new PointWithOrientation(scatterHit.point, scatterHitInfo.Tangent, scatterHitInfo.Normal); ;
                                pointList.Add(lookdirection);
                            }
                        }
                    }
                    break;
                case Mode.Place:
                    {
                        PointWithOrientation pointInfo = new PointWithOrientation(hit.point, hitInfo.Tangent, hitInfo.Normal);
                        pointList.Add(pointInfo);
                        DrawAxisGizmo(hit.point, hitInfo.Tangent, hitInfo.Normal, hitInfo.BiTangent);
                    }
                    break;
                default:
                    break;
            }
        }

        private float GetRaycastOffset()
        {
            return 0.1f + (spawnRadius * scatterHeightTolerance) / 9f;
        }

        private float GetRaycastMaxDistance()
        {
            return GetRaycastOffset() * 2f + GetObjectBoundingBoxSize(prefab) / 2f;
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
                    DeleteObjects();
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
            if (shift) 
            {
                CheckInputShiftDown();
            }
        }

        private void CheckInputShiftDown()
        {
            if (Event.current.isMouse && Event.current.type == EventType.MouseDown && Event.current.button == 0) //left click
            {
                SpawnPrefabs(poseList);
                if (mode != Mode.Snap)
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
            if (Event.current.type == EventType.ScrollWheel) 
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

        private void DeleteObjects()
        {
            IEnumerable<GameObject> deletingObjs = GetObjectsInDeletionRange();
            foreach (GameObject o in deletingObjs)
            {
                Undo.DestroyObjectImmediate(o);
            }
            Event.current.Use();
        }

        private void AdjustOffset()
        {
            float scrollDir = GetScrollDirection();
            float newValue = Mathf.Clamp(propHeightOffset.floatValue - scrollDir * 0.1f, minOffset, maxOffset);
            so.Update();
            propHeightOffset.floatValue = newValue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private void AdjustRadius()
        {
            float scrollDir = GetScrollDirection();
            float newValue;
            so.Update();
            switch (mode)
            {
                case Mode.Scatter:
                    newValue = Mathf.Clamp(propRadius.floatValue * (1 - scrollDir * 0.04f), minRadius, maxRadius);
                    propRadius.floatValue = newValue;
                    RandPoints.ValidateRandPoints(spawnCount, spawnRadius, spacing);
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

        private float GetScrollDirection()
        {
            Vector2 xy = Event.current.delta;
            float delta = Mathf.Abs(xy.x) > Mathf.Abs(xy.y) ? xy.x : xy.y;   //newer Unity when holding shift the x and y are flipped
            return Mathf.Sign(delta);
        }

        private float GetObjectBoundingBoxSize(GameObject obj)
        {
            if (obj == null) return -1f;
            Renderer renderer = obj.GetComponent<Renderer>();
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
                if (randRotation)
                {
                    point.rotation = Quaternion.AngleAxis(GetRandRotation(i, randAngle) + rotationOffset, pointList[i].up) * rot;
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

        private void DrawObjectPreview(GameObject obj, bool isSnappedMode)
        {
            if (isSnappedMode)
            {
                Matrix4x4 localToWorld = Matrix4x4.TRS(hitPoint.position, hitPoint.rotation, Vector3.one);
                DrawMesh(obj, localToWorld, true, false, false);
            }
            else
            {
                for (int i = 0; i < poseList.Count; i++)
                {
                    Matrix4x4 localToWorld = Matrix4x4.TRS(poseList[i].position, poseList[i].rotation, Vector3.one);
                    DrawMesh(obj, localToWorld, !keepRootRotation, randScale, randHeight, i);
                }
            }
        }

        private IEnumerable<GameObject> GetObjectsInDeletionRange()
        {
            List<GameObject> objsList = new List<GameObject>();
            if (prefabInfo.hasCollider)
            {
                Collider[] colliders = Physics.OverlapSphere(hitPoint.position, deletionRadius);
                foreach (Collider c in colliders)
                {
                    GameObject obj = c.gameObject;
                    if (prefabInfo.originalPrefab == PrefabUtility.GetCorrespondingObjectFromSource(obj))
                    {
                        if (IsWithinDeletionHeightRange(obj))
                        {
                            objsList.Add(obj);
                        }
                    }
                }
            }
            else
            {
                foreach (GameObject obj in prefabInfo.cachedAllInstancedInScene)
                {
                    if (obj == null) continue;   // prevent error when deleting 
                    float distance = Vector3.Distance(obj.transform.position, hitPoint.position);
                    if (distance < deletionRadius && IsWithinDeletionHeightRange(obj))
                    {
                        objsList.Add(obj);
                    }
                }
            }
            return objsList;
        }

        private IEnumerable<GameObject> FindAllInstancesOfPrefab(GameObject prefab)
        {
            List<GameObject> foundInstances = new List<GameObject>();
            GameObject[] allObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject obj in allObjects)
            {
                if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(obj) == prefab)
                {
                    foundInstances.Add(obj);
                }
            }
            return foundInstances;
        }

        private void DrawDeletionPreviews(IEnumerable<GameObject> objs)
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

        private void DrawMesh(GameObject o, Matrix4x4 localToWorld, bool ignoreParentRotation, bool randScale, bool randHeight, int randValueIndex = -1)
        {
            if (previewMaterial == null) return;
            previewMaterial.SetPass(0);
            MeshFilter[] filters = o.GetComponentsInChildren<MeshFilter>();
            float height;
            height = randHeight ? GetRandHeight(randValueIndex, randHeightMin, randHeightMax) + heightOffset : heightOffset;
            Matrix4x4 yAxisOffsetMatrix = Matrix4x4.TRS(new Vector3(0f, height, 0f), Quaternion.identity, Vector3.one);
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
                    float scale = GetRandScale(randValueIndex, randScaleMin, randScaleMax);
                    outputMatrix = outputMatrix * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
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
                float height;
                height = randHeight ? GetRandHeight(i, randHeightMin, randHeightMax) + heightOffset : heightOffset;
                spawnObject.transform.position = poseList[i].position + poseList[i].rotation * new Vector3(0f, height, 0f);
                spawnObject.transform.rotation = keepRootRotation ? poseList[i].rotation * prefabInfo.originalPrefab.transform.rotation : poseList[i].rotation;
                if (randScale)
                {
                    float scale = GetRandScale(i, randScaleMin, randScaleMax);
                    spawnObject.transform.localScale = spawnObject.transform.localScale * scale;
                }
            }
            RandPoints.GenerateRandPoints(spawnCount, spawnRadius, spacing);
            GenerateRandValues(spawnCount);
        }

        private void DrawRange(RaycastHit hit, Color color, float radius)
        {
            int segment = discSegment - 1;
            Vector3[] points = new Vector3[segment];
            Vector3 forward = Vector3.Cross(hit.normal, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.Cross(hit.normal, Vector3.right).normalized;
            }
            Vector3 right = Vector3.Cross(forward, hit.normal);
            for (int i = 0; i < segment; i++)
            {
                float t = i / (float)segment;
                float angle = 2 * Mathf.PI * t;
                Vector2 pointLS = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector3 pointWS = GetWorldPosFromLocal(pointLS, hit.point, forward, hit.normal, right, radius);
                points[i] = pointWS;
            }
            Vector3?[] surfaceHits = new Vector3?[segment + 1];
            float raycastOffset = GetRaycastOffset();
            float raycastmaxDistance = GetRaycastMaxDistance();
            for (int i = 0; i < segment; i++)
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
            for (int i = 0; i < segment; i++)  //connect last element to first
            {
                if (surfaceHits[i] != null)
                {
                    surfaceHits[segment] = surfaceHits[i];
                    break;
                }
            }
            DrawLinesOnSurface(surfaceHits, color);
        }

        private void DrawLinesOnSurface(Vector3?[] surfaceHits, Color color)  //draw dotted line when null point in between
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
                    Handles.DrawLine( (Vector3)lastPoint, (Vector3)surfaceHits[i], GizmoWidth);


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
            float distanceToSurface = DistancePlanePoint(hitPoint.rotation * Vector3.up, hitPoint.position, o.transform.position);
            if (randHeight)
            {
                return distanceToSurface < range + Mathf.Abs(randHeightMax - randHeightMin); 
            }
            else
            {
                return distanceToSurface < range;
            }
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

        private void DrawAxisGizmo(Vector3 position, Vector3 forward, Vector3 up, Vector3 right)
        {
            float scale = HandleUtility.GetHandleSize(position) * 0.25f;
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

        private void LoadAssets()
        {
            string path = GetPath();
            previewMaterial = (Material)AssetDatabase.LoadAssetAtPath(path + "/Material/Preview.mat", typeof(Material));
            deletionMaterial = (Material)AssetDatabase.LoadAssetAtPath(path + "/Material/Deletion.mat", typeof(Material));
            bool isDarkTheme = EditorGUIUtility.isProSkin;
            if (isDarkTheme)
            {
                toolIcons = new GUIContent[]
                {
                new GUIContent((Texture)AssetDatabase.LoadAssetAtPath(path + "/Texture/brush_dark.png", typeof(Texture)), GetText("KScatter")),
                new GUIContent((Texture)AssetDatabase.LoadAssetAtPath(path + "/Texture/pen_dark.png", typeof(Texture)), GetText("KPlace")),
                new GUIContent((Texture)AssetDatabase.LoadAssetAtPath(path + "/Texture/rubber_dark.png", typeof(Texture)), GetText("KDelete")),
                new GUIContent((Texture)AssetDatabase.LoadAssetAtPath(path + "/Texture/snap_dark.png", typeof(Texture)), GetText("KSnap")),
                };
            }
            else
            {
                toolIcons = new GUIContent[]
                {
                new GUIContent((Texture)AssetDatabase.LoadAssetAtPath(path + "/Texture/brush_light.png", typeof(Texture)), GetText("KScatter")),
                new GUIContent((Texture)AssetDatabase.LoadAssetAtPath(path + "/Texture/pen_light.png", typeof(Texture)), GetText("KPlace")),
                new GUIContent((Texture)AssetDatabase.LoadAssetAtPath(path + "/Texture/rubber_light.png", typeof(Texture)), GetText("KDelete")),
                new GUIContent((Texture)AssetDatabase.LoadAssetAtPath(path + "/Texture/snap_light.png", typeof(Texture)), GetText("KSnap")),
                };
            }
        }

        private void LoadPrefab()
        {
            if (prefabLocation == null) return;
            prefab = (GameObject)AssetDatabase.LoadAssetAtPath(prefabLocation, typeof(GameObject));
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

        private void ValidatePrefab()
        {
            if (prefab == null) return;
            GameObject obj = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefab);   //get source object from the field
            bool isAlreadyPrefab = (obj == prefab);
            bool isOuterPrefab = PrefabUtility.IsOutermostPrefabInstanceRoot(prefab);
            if (obj != null)
            {
                if (isOuterPrefab || isAlreadyPrefab)
                {
                    prefab = obj;
                    prefabError = PrefabErrorMode.None;
                }
                else
                {
                    prefab = null;
                    prefabError = PrefabErrorMode.NotOuterMostPrefab;
                }
            }
            else
            {
                prefab = null;
                prefabError = PrefabErrorMode.NotAnPrefab;
            }
        }

        private void UpdatePrefabInfo()
        {
            if (prefab != null)
            {
                prefabInfo.originalPrefab = prefab;
                prefabInfo.hasCollider = (prefabInfo.originalPrefab.GetComponent<Collider>() != null);
                prefabLocation = AssetDatabase.GetAssetPath(prefab);
            }
            else
            {
                prefabInfo.originalPrefab = null;
                prefabLocation = null;
            }
            if (prefabInfo.requireList)
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
            if (prefabInfo.requireList && mode == Mode.Delete)
            {
                UpdateDeletionObjectsList();
            }
        }

        private void OnDeleteMode()
        {
            if (prefabInfo.requireList)
            {
                UpdateDeletionObjectsList();
            }
        }

        private void OnHierarchyChanged()
        {
            if (!on || mode != Mode.Delete || !prefabInfo.requireList) return;
            UpdateDeletionObjectsList();
        }

        private string GetPath()
        {
            string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
            string parentPath = Path.GetDirectoryName(path);
            return parentPath;
        }

        private string GetText(string key) => LanguageSetting.GetText(key);

    }
}