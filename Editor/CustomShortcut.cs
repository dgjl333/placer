using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class CustomShortcut
{
    public enum ClearMode
    {
        Rotation,
        Position,
        Scale
    }
    static CustomShortcut()
    {
        SceneView.duringSceneGui += DuringSceneGUI;
    }
    private static void DuringSceneGUI(SceneView scene)
    {
        bool shift = (Event.current.modifiers & EventModifiers.Shift) != 0;
        if (shift && Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.R)
            {
                Event.current.Use();
                Clear(ClearMode.Rotation);
            }
            if (Event.current.keyCode == KeyCode.G)
            {
                Event.current.Use();
                Clear(ClearMode.Position);
            }
            if (Event.current.keyCode == KeyCode.S)
            {
                Event.current.Use();
                Clear(ClearMode.Scale);
            }
        }
    }

    private static void Clear(ClearMode mode)
    {
        if (Selection.gameObjects == null) return;
        foreach (GameObject go in Selection.gameObjects)
        {
            Undo.RecordObject(go.transform, string.Empty);
            switch (mode)
            {
                case ClearMode.Position:
                    go.transform.position = Vector3.zero;
                    break;
                case ClearMode.Rotation:
                    go.transform.rotation = Quaternion.identity;
                    break;
                case ClearMode.Scale:
                    go.transform.localScale = Vector3.one;
                    break;
                default:
                    break;

            }
        }
    }

}
