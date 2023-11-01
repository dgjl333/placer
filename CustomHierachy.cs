using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class CustomHierarchy : MonoBehaviour
{
    private static float size = 14f;
    private static float startPos = 32f;

    static CustomHierarchy()
    {
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindow;
    }
    private static void OnHierarchyWindow(int instanceID, Rect selectionRect)
    {
        ActiveEditorTracker tracker = ActiveEditorTracker.sharedTracker;
        GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (obj == null)
            return;

        Rect pos = selectionRect;
        pos.x = startPos;
        pos.width = size;
        GUIStyle style = new GUIStyle("IN LockButton");
        bool isCurrentLocked = tracker.isLocked && IsLockedItem(instanceID);
        bool state = GUI.Toggle(pos, isCurrentLocked, string.Empty, style);
        if (state && tracker.isLocked && !IsLockedItem(instanceID))  //when selecting non locked object when there is already locked one 
        {
            tracker.isLocked = false;
            Selection.activeInstanceID = instanceID;
            tracker.isLocked = true;
            tracker.ForceRebuild();

        }
        if (state == isCurrentLocked)
        {
            return;
        }
        Selection.activeInstanceID = instanceID;
        tracker.isLocked = state;
        tracker.ForceRebuild();

    }
    private static bool IsLockedItem(int id)
    {
        if (ActiveEditorTracker.sharedTracker.activeEditors.Length > 0)
        {
            return ActiveEditorTracker.sharedTracker.activeEditors[0].target.GetInstanceID() == id;
        }
        else
        {
            return false;
        }
    }
}
