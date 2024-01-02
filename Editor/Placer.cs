using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static DG3.Placer.RandData;
using static DG3.SettingManager;
using Random = UnityEngine.Random;


namespace DG3
{
    internal class Placer : EditorWindow
    {
        [MenuItem("Tools/Placer")]
        public static void OpenWindow()
        {
            GetWindow<Placer>();
        }

        public int surfaceLayer = int.MaxValue;
        public bool on = true;
        public Mode mode = Mode.Scatter;
        public float spawnRadius = 3f;
        public float rotationOffset = 0f;
        public float spacing = 0.1f;
        public float deletionRadius = 1f;
        public int spawnCount = 5;
        public float randAngleMin = 0f;
        public float randAngleMax = 180f;
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
        public bool isShowAdvancedSetting = false;
        public bool isShowRandSetting = true;
        public bool showPreview = true;

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
        private SerializedProperty propRandAngleMin;
        private SerializedProperty propRandAngleMax;
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
        private SerializedProperty propShowPreview;

        [SerializeField] private string prefabLocation = null;
        private PrefabErrorMode prefabError = PrefabErrorMode.None;
        private Pose hitPoint;
        private List<Pose> poseList = new List<Pose>();
        private PrefabInfo prefabInfo;
        private bool shift = false;
        private bool ctrl = false;
        private bool alt = false;
        private bool isInPrefabMode = false;
        private bool hasObjectSpawn = false;
        private float objectSpawnTime;
        private int controlID;

        private readonly float GizmoWidth = 2f;
        private readonly int discSegment = 64;

        public static class RandData
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
                            if (retryCount > 20) return;
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

            public static float GetRandValue(int index, float valueMin, float valueMax)
            {
                float rand = randValues[index % randValues.Length];
                return Mathf.Lerp(valueMin, valueMax, rand);
            }
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

        private Dictionary<PrefabErrorMode, string> errorText = new Dictionary<PrefabErrorMode, string>()
        {
            { PrefabErrorMode.NotAnPrefab, "KNotPrefabError" },
            { PrefabErrorMode.NotOuterMostPrefab, "KNotOuterMostPrefabError" }
        };

        private string[] toolHelper = new string[]
        {
            "KScatterHelper", "KPlaceHelper", "KDeleteHelper", "KSnapHelper"
        };

        private void Awake()
        {
            if (Camera.main != null)
            {
                Camera.main.depthTextureMode |= DepthTextureMode.Depth;   //shader need depth texture
            }
            LoadSetting(this);
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
            SaveSetting(this);
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
            propRandAngleMin = so.FindProperty(nameof(randAngleMin));
            propRandAngleMax = so.FindProperty(nameof(randAngleMax));
            propRotationOffset = so.FindProperty(nameof(rotationOffset));
            propScatterHeightTolerance = so.FindProperty(nameof(scatterHeightTolerance));
            propAlignWithWorldAxis = so.FindProperty(nameof(alignWithWorldAxis));
            propRandHeight = so.FindProperty(nameof(randHeight));
            propShowPreview = so.FindProperty(nameof(showPreview));
        }

        private void OnGUI()
        {
            so.Update();
            DrawHeader();
            if (on)
            {
                DrawToolBar();
                DrawHelperBox();
                switch (mode)

                {
                    case Mode.Scatter:
                        DrawLayerMask();
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
                        DrawLayerMask();
                        DrawAlignWithWorldAxis();
                        DrawSpawnRadius();
                        DrawHeightOffset();
                        DrawRotationOffset();
                        DrawPrefab();
                        DrawRandom();
                        break;
                    case Mode.Delete:
                        DrawLayerMask();
                        DrawAlignWithWorldAxis();
                        DrawDeleteRadius();
                        DrawHeightOffset();
                        DrawPrefab();
                        break;
                    case Mode.Snap:
                        DrawLayerMask();
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
                    string currentLang = SettingManager.language.ToString();
                    GUIStyle langButton = new GUIStyle(GUI.skin.button);
                    langButton.fontStyle = FontStyle.Bold;
                    langButton.fontSize = (int)(langButton.fontSize * 0.9f);
                    if (GUILayout.Button(currentLang, langButton, GUILayout.MaxWidth(40), GUILayout.MaxHeight(20)))
                    {
                        SettingManager.SwitchLanguage();
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
            }

            void DrawSpawnRadius()
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(propRadius, new GUIContent(GetText("KRadius"), GetText("KRadiusTT")));
                if (EditorGUI.EndChangeCheck())
                {
                    RandPoints.ValidateRandPoints(spawnCount, propRadius.floatValue, spacing);
                }
            }

            void DrawDeleteRadius()
            {
                EditorGUILayout.PropertyField(propDeletionRadius, new GUIContent(GetText("KRadius"), GetText("KRadiusTT")));
            }

            void DrawSpawnCount()
            {
                EditorGUI.BeginChangeCheck();
                int newSpawnCount = EditorGUILayout.IntSlider(new GUIContent(GetText("KSpawnCount"), GetText("KSpawnCountTT")), propSpawnCount.intValue, 1, 50);
                if (EditorGUI.EndChangeCheck())
                {
                    propSpawnCount.intValue = newSpawnCount;
                    GenerateRandValues(newSpawnCount);
                    RandPoints.GenerateRandPoints(newSpawnCount, spawnRadius, spacing);
                }
            }

            void DrawSpacing()
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(propSpacing, new GUIContent(GetText("KMinSpacing"), GetText("KMinSpacingTT")));
                propSpacing.floatValue = Mathf.Max(0f, propSpacing.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    RandPoints.ValidateRandPoints(spawnCount, spawnRadius, propSpacing.floatValue);
                }
            }

            void DrawHeightOffset()
            {
                EditorGUILayout.PropertyField(propHeightOffset, new GUIContent(GetText("KHeightOffset"), GetText("KHeightOffsetTT")));

            }

            void DrawAlignWithWorldAxis()
            {
                EditorGUILayout.PropertyField(propAlignWithWorldAxis, new GUIContent(GetText("KAlignWithWorld"), GetText("KAlignWithWorldTT")));
            }

            void DrawPrefab()
            {
                EditorGUI.BeginChangeCheck();
                prefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent(GetText("KPrefab"), GetText("KPrefabTT")), prefab, typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                {
                    ValidatePrefab();
                    UpdatePrefabInfo();
                }
                if (prefabError != PrefabErrorMode.None)
                {
                    using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField(GetText(errorText[prefabError]), EditorStyles.wordWrappedLabel);
                    }
                }
            }

            void DrawRotationOffset()
            {
                EditorGUILayout.PropertyField(propRotationOffset, new GUIContent(GetText("KRotationOffset"), GetText("KRotationOffsetTT")));
                propRotationOffset.floatValue = Mathf.Clamp(propRotationOffset.floatValue, 0f, 360f);
            }

            void DrawRandom()
            {
                GUILayout.Space(30);
                isShowRandSetting = EditorGUILayout.Foldout(isShowRandSetting, GetText("KRandSetting"));
                if (isShowRandSetting)
                {
                    EditorGUILayout.PropertyField(propRandRotation, new GUIContent(GetText("KRandRotation")));
                    if (randRotation)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            EditorGUILayout.PropertyField(propRandAngleMin, new GUIContent(GetText("KEulerAngleMin")));
                            EditorGUILayout.PropertyField(propRandAngleMax, new GUIContent(GetText("KEulerAngleMax")));
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
            }

            void DrawAdvanced()
            {
                EditorGUILayout.Space(30);
                isShowAdvancedSetting = EditorGUILayout.Foldout(isShowAdvancedSetting, GetText("KAdvancedSetting"));
                if (isShowAdvancedSetting)
                {
                    EditorGUILayout.PropertyField(propKeepRootRotation, new GUIContent(GetText("KRootRotation"), GetText("KRootRotationTT")));
                    EditorGUI.BeginChangeCheck();
                    float newTolerance = EditorGUILayout.Slider(new GUIContent(GetText("KTolerance"), GetText("KToleranceTT")), propScatterHeightTolerance.floatValue, 0f, 10f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        propScatterHeightTolerance.floatValue = newTolerance;
                    }

                    GUILayout.Space(15);
                    EditorGUILayout.PropertyField(propColor, new GUIContent(GetText("KRadiusColor"), GetText("KRadiusColorTT")));
                    EditorGUILayout.PropertyField(propShowPreview, new GUIContent(GetText("KShowPreview"), GetText("KShowPreviewTT")));
                }
            }

            void DrawLayerMask()
            {
                surfaceLayer = LayerMaskField(new GUIContent(GetText("KSurfaceLayer"), GetText("KSurfaceLayerTT")), surfaceLayer);
            }

            void DrawHelperBox()
            {

                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    string text = GetText(toolHelper[(int)mode]);
                    EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);
                }
                GUILayout.Space(20);
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
            UpdateObjectSpawnTime();
        }

        private void RaycastToMousePosition(List<PointWithOrientation> pointList, Camera cam, bool isSnappedMode)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, surfaceLayer)) return;
            RaycastHit finalHit = hit;
            if (!isSnappedMode)
            {
                switch (mode)
                {
                    case Mode.Delete:
                        if (IsObjectFromPrefab(hit.collider.gameObject, prefabInfo.originalPrefab))
                        {
                            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, surfaceLayer);
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
            else
            {
                GameObject[] objs = Selection.gameObjects;
                bool isFromObject = objs.Any(o => IsFromObject(hit.collider.gameObject, o));  //exclude the selected object itself
                if (isFromObject)
                {
                    RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, surfaceLayer);
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
                            if (Physics.Raycast(pointRay, out RaycastHit scatterHit, raycastmaxDistance, surfaceLayer))
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
            float newValue = propHeightOffset.floatValue - scrollDir * 0.1f;
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
                    newValue = propRadius.floatValue * (1 - scrollDir * 0.04f);
                    propRadius.floatValue = newValue;
                    RandPoints.ValidateRandPoints(spawnCount, spawnRadius, spacing);
                    break;
                case Mode.Delete:
                    newValue = propDeletionRadius.floatValue * (1 - scrollDir * 0.04f);
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
            return 0f;
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
                    point.rotation = Quaternion.AngleAxis(GetRandValue(i, randAngleMin, randAngleMax) + rotationOffset, pointList[i].up) * rot;
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
            if (!showPreview || previewMaterial == null) return;
            previewMaterial.SetPass(0);
            if (isSnappedMode)
            {
                Matrix4x4 localToWorld = Matrix4x4.TRS(hitPoint.position, hitPoint.rotation, Vector3.one);
                DrawMesh(obj, localToWorld, true, false, false);
            }
            else
            {
                if (!hasObjectSpawn)
                {
                    for (int i = 0; i < poseList.Count; i++)
                    {
                        Matrix4x4 localToWorld = Matrix4x4.TRS(poseList[i].position, poseList[i].rotation, Vector3.one);
                        DrawMesh(obj, localToWorld, !keepRootRotation, randScale, randHeight, i);
                    }
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
                        objsList.Add(obj);
                    }
                }
            }
            else
            {
                foreach (GameObject obj in prefabInfo.cachedAllInstancedInScene)
                {
                    if (obj == null) continue;
                    float distance = Vector3.Distance(obj.transform.position, hitPoint.position);
                    if (distance < deletionRadius)
                    {
                        objsList.Add(obj);
                    }
                }
            }
            return objsList;
        }

        private IEnumerable<GameObject> FindAllInstancesOfPrefab(GameObject prefab)
        {
            Scene activeScene = SceneManager.GetActiveScene();
#if UNITY_2021_2_OR_NEWER
            return PrefabUtility.FindAllInstancesOfPrefab(prefab, activeScene);
#else
            List<GameObject> foundInstances = new List<GameObject>();
            GameObject[] allObjects = activeScene.GetRootGameObjects();
            foreach (GameObject obj in allObjects)
            {
                if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(obj) == prefab)
                {
                    foundInstances.Add(obj);
                }
            }
            return foundInstances;
#endif
        }

        private void DrawDeletionPreviews(IEnumerable<GameObject> objs)
        {
            if (deletionMaterial == null) return;
            deletionMaterial.SetPass(0);
            foreach (GameObject o in objs)
            {
                IEnumerable<Tuple<Mesh, Matrix4x4>> allMeshes = GetAllMeshes(o);
                foreach (Tuple<Mesh, Matrix4x4> mesh in allMeshes)
                {
                    Graphics.DrawMeshNow(mesh.Item1, mesh.Item2);
                }
            }
        }

        private void DrawMesh(GameObject o, Matrix4x4 localToWorld, bool ignoreParentRotation, bool randScale, bool randHeight, int randValueIndex = -1)
        {
            IEnumerable<Tuple<Mesh, Matrix4x4>> allMeshes = GetAllMeshes(o);
            float height;
            height = randHeight ? GetRandValue(randValueIndex, randHeightMin, randHeightMax) + heightOffset : heightOffset;
            Matrix4x4 yAxisOffsetMatrix = Matrix4x4.Translate(new Vector3(0f, height, 0f));
            Matrix4x4 ignoreParentPositionMatrix = Matrix4x4.Translate(-o.transform.position);
            Matrix4x4 ignoreParentMatrix = ignoreParentPositionMatrix;
            if (ignoreParentRotation)
            {
                Matrix4x4 ignoreParentRotationMatrix = Matrix4x4.Rotate(Quaternion.Inverse(o.transform.rotation));
                ignoreParentMatrix = ignoreParentRotationMatrix * ignoreParentMatrix;
            }
            foreach (Tuple<Mesh, Matrix4x4> mesh in allMeshes)
            {
                Matrix4x4 outputMatrix;
                Matrix4x4 childMatrix = mesh.Item2;
                if (randScale)
                {
                    float scale = GetRandValue(randValueIndex, randScaleMin, randScaleMax);
                    Matrix4x4 scaleMatrix = Matrix4x4.Scale(Vector3.one * scale);
                    outputMatrix = localToWorld * yAxisOffsetMatrix * scaleMatrix * ignoreParentMatrix * childMatrix;
                }
                else
                {
                    outputMatrix = localToWorld * yAxisOffsetMatrix * ignoreParentMatrix * childMatrix;
                }
                Graphics.DrawMeshNow(mesh.Item1, outputMatrix);
            }
        }

        private IEnumerable<Tuple<Mesh, Matrix4x4>> GetAllMeshes(GameObject o)
        {
            MeshFilter[] meshFilters = o.GetComponentsInChildren<MeshFilter>();
            SkinnedMeshRenderer[] skinRenderers = o.GetComponentsInChildren<SkinnedMeshRenderer>();
            return meshFilters
                  .Select(meshFilter => Tuple.Create(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix))
                  .Concat(skinRenderers.Select(skinnedMeshRenderer => Tuple.Create(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.transform.localToWorldMatrix)));
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
                float height = randHeight ? GetRandValue(i, randHeightMin, randHeightMax) + heightOffset : heightOffset;
                spawnObject.transform.position = poseList[i].position + poseList[i].rotation * new Vector3(0f, height, 0f);
                spawnObject.transform.rotation = keepRootRotation ? poseList[i].rotation * prefabInfo.originalPrefab.transform.rotation : poseList[i].rotation;
                if (randScale)
                {
                    float scale = GetRandValue(i, randScaleMin, randScaleMax);
                    spawnObject.transform.localScale *= scale;
                }
            }
            RandPoints.GenerateRandPoints(spawnCount, spawnRadius, spacing);
            GenerateRandValues(spawnCount);
            DelayDrawPreview();
        }

        private void DelayDrawPreview()
        {
            hasObjectSpawn = true;
            objectSpawnTime = Time.realtimeSinceStartup;
        }

        private void UpdateObjectSpawnTime()
        {
            if (!hasObjectSpawn) return;
            if (Time.realtimeSinceStartup - objectSpawnTime > 0.1f)
            {
                hasObjectSpawn = false;
            }
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
                if (Physics.Raycast(pointRay, out RaycastHit pointHit, raycastmaxDistance, surfaceLayer))
                {
                    surfaceHits[i] = pointHit.point;
                }
                else
                {
                    surfaceHits[i] = null;
                }
            }
            for (int i = 0; i < segment; i++)  //append the first point to array
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
                    DrawLine((Vector3)lastPoint, (Vector3)surfaceHits[i], GizmoWidth);
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

        private void DrawLine(Vector3 p1, Vector3 p2, float thickness)
        {
#if UNITY_2020_2_OR_NEWER
            Handles.DrawLine(p1, p2, GizmoWidth);
#else
            Handles.DrawAAPolyLine(GizmoWidth * 2f, p1, p2);
#endif
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
            previewMaterial = AssetManager.GetPreviewMaterial();
            deletionMaterial = AssetManager.GetDeletionMaterial();
            toolIcons = new GUIContent[]
            {
            new GUIContent(AssetManager.GetBrushTexture(), GetText("KScatter")),
            new GUIContent(AssetManager.GetPenTexture(), GetText("KPlace")),
            new GUIContent(AssetManager.GetEraserTexture(), GetText("KDelete")),
            new GUIContent(AssetManager.GetSnapTexture(), GetText("KSnap")),
            };
        }

        private void LoadPrefab()
        {
            if (prefabLocation == null) return;
            prefab = (GameObject)AssetDatabase.LoadAssetAtPath(prefabLocation, typeof(GameObject));
        }

        private void ValidatePrefab()
        {
            if (prefab == null) return;
            GameObject obj = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefab);   
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

        private int LayerMaskField(GUIContent content, int layerMask)
        {
            List<string> layers = new List<string>();
            List<int> layerNumbers = new List<int>();

            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layers.Add(layerName);
                    layerNumbers.Add(i);
                }
            }

            int maskWithoutEmpty = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if (((1 << layerNumbers[i]) & layerMask) != 0)
                {
                    maskWithoutEmpty |= 1 << i;
                }
            }

            maskWithoutEmpty = EditorGUILayout.MaskField(content, maskWithoutEmpty, layers.ToArray());
            int mask = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if ((maskWithoutEmpty & (1 << i)) != 0)
                {
                    mask |= 1 << layerNumbers[i];
                }    
            }
            return mask;
        }
    }
}