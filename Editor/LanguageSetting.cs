using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dg
{
    internal static class LanguageSetting
    {
        public enum Language
        {
            ENG,
            JP
        }

        private static string csvPath;
        private static string settingPath;
        private static Dictionary<string, string> translations = new Dictionary<string, string>();
        public static Language language { get; private set; } 

        [InitializeOnLoadMethod]
        private static void Init()
        {
            csvPath = AssetDatabase.GUIDToAssetPath("9496dd598027c3047b8233228b4007cb");
            settingPath = AssetDatabase.GUIDToAssetPath("a14632402852e6c46b31eb8ae2607b0b"); 
            LoadSetting();
            LoadTexts();
        }

        private static void LoadTexts()
        {
            using (StreamReader reader = new StreamReader(csvPath))
            {
                string[] firstLine = reader.ReadLine().Split(',');
                int langIndex = Array.IndexOf(firstLine, language.ToString());
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

        public static void SwitchLanguage()
        {
            switch (language)
            {
                case Language.ENG:
                    language = Language.JP;
                    break;
                case Language.JP:
                    language = Language.ENG;
                    break;
                default:
                    break;
            }
            LoadTexts();
            SaveSetting();
        }

        private static void SaveSetting()
        {
            using (StreamWriter writer = new StreamWriter(settingPath, false))
            {
                writer.WriteLine(language.ToString());
            }
        }

        private static void LoadSetting()
        {
            string lang;
            using (StreamReader reader = new StreamReader(settingPath))
            {
                lang = reader.ReadLine();
            }
            if (lang == "null")
            {
                SetSystemLangauge();
            }
            else
            {
                language = Enum.Parse<Language>(lang);
            }
        }

        private static void SetSystemLangauge()
        {
            if (Application.systemLanguage == SystemLanguage.Japanese)
            {
                language = Language.JP;
            }
            else
            {
                language = Language.ENG;
            }
            SaveSetting();
        }
    }
}
