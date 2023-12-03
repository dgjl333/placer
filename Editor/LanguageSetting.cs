using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class LanguageSetting
{
    private static string path;
    public enum Lanugage
    {
        ENG,
        JP
    }

    private static Dictionary<string, string> translations = new Dictionary<string, string>();
    private static Lanugage lanugage = Lanugage.ENG; 

    [InitializeOnLoadMethod]
    private static void Init()
    {
        path = AssetDatabase.GUIDToAssetPath("9496dd598027c3047b8233228b4007cb");
        LoadTexts();
    }

    private static void LoadTexts()
    {
        using (StreamReader reader = new StreamReader(path, Encoding.UTF8))
        {
            string[] firstLine = reader.ReadLine().Split(',');
            int langIndex = Array.IndexOf(firstLine, lanugage.ToString());
            while (!reader.EndOfStream)
            {
                string[] text = reader.ReadLine().Split(',');
                string key = text[0];
                translations[key] = text[langIndex];
            }
        }
    }

    public static string GetText(string key)
    {
        if (translations.Count == 0)
        {
            LoadTexts();
        }
        return translations[key];
    }
}
