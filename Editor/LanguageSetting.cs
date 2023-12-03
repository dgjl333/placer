using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class LanguageSetting
{
    public static string path;
    public enum Lanugage
    {
        ENG,
        JP
    }
    private readonly static Dictionary<string, string> translations = new Dictionary<string, string>();
    private static Lanugage lanugage = Lanugage.JP; 

    public static void LoadTexts()
    {
        string csvPath = path + "/Others/Language.csv";
        using (StreamReader reader = new StreamReader(csvPath, Encoding.UTF8))
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
