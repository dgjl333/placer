using UnityEditor;
using UnityEngine;


namespace DG3
{
    internal static class AssetManager
    {
        private static bool isDarkTheme;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            isDarkTheme = EditorGUIUtility.isProSkin;
        }

        public static Texture GetBrushTexture()
        {
            if (isDarkTheme) return GetTexture("054cbe4d84ee48e4bbb80b87766d7735");
            return GetTexture("d6f12724b5b136d4793034f60cde2853");
        }
        public static Texture GetPenTexture()
        {
            if (isDarkTheme) return GetTexture("5608d9cad87ca0a4486baf3dfcbf1c4d");
            return GetTexture("fc2ac71cbcf871649a1ce2d26ede97d1");
        }
        public static Texture GetEraserTexture()
        {
            if (isDarkTheme) return GetTexture("b5650f05fd407ad4fa6fe099865a7f5a");
            return GetTexture("1a2cfd1bbfc25ed458abeb3f4df55e69");
        }
        public static Texture GetSnapTexture()
        {
            if (isDarkTheme) return GetTexture("73778a4ca7701eb4aa7617665b230d7e");
            return GetTexture("003b31fd880f01b40a7bd7921e973cbc");
        }

        public static Material GetPreviewMaterial()
        {
            return GetMaterial("9082455ad724e3241958c754778516c7");
        }

        public static Material GetDeletionMaterial()
        {
            return GetMaterial("1b572307fd5a9c740b2854df42cecb0c");
        }

        public static string GetLangCSVPath()
        {
            return AssetDatabase.GUIDToAssetPath("9496dd598027c3047b8233228b4007cb");
        }

        public static string GetLangSettingPath()
        {
            return AssetDatabase.GUIDToAssetPath("a14632402852e6c46b31eb8ae2607b0b");
        }

        public static string GetSettingPath()
        {
            return AssetDatabase.GUIDToAssetPath("8fc226f1a60493a409d64b567aa5c8fe");
        }

        private static Texture GetTexture(string guid)
        {
            return (Texture)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), typeof(Texture));
        }

        private static Material GetMaterial(string guid)
        {
            return (Material)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), typeof(Material));
        }
    }
}