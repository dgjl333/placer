using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DG3
{
    internal static class SettingManager
    {
        public enum Language
        {
            ENG,
            JP
        }

        private static string settingPath;
        private static string langCSVPath;
        private static string langSettingPath;
        private static Dictionary<string, string> translations = new Dictionary<string, string>();
        public static Language language { get; private set; }

        [InitializeOnLoadMethod]
        private static void Init()
        {
            settingPath = AssetManager.GetSettingPath();
            langCSVPath = AssetManager.GetLangCSVPath();
            langSettingPath = AssetManager.GetLangSettingPath();
            LoadLangSetting();
            LoadLangTexts();
        }

        private static void LoadLangTexts()
        {
            using (StreamReader reader = new StreamReader(langCSVPath))
            {
                string[] firstLine = reader.ReadLine().Split(',');
                int langIndex = Array.IndexOf(firstLine, language.ToString());
                while (!reader.EndOfStream)
                {
                    string[] text = reader.ReadLine().Split(',');
                    string key = text[0];
                    translations[key] = text[langIndex].Replace("\\n", "\n");
                }
            }
        }

        public static string GetText(string key)
        {
            if (translations.Count == 0)
            {
                LoadLangTexts();
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
            LoadLangTexts();
            SaveLangSetting();
        }

        public static void LoadSetting(System.Object o)
        {
            string data = File.ReadAllText(settingPath);
            JsonUtility.FromJsonOverwrite(data, o);
        }

        public static void SaveSetting(System.Object o)
        {
            string data = JsonUtility.ToJson(o, false);
            File.WriteAllText(settingPath, data);
        }

        private static void SaveLangSetting()
        {
            using (StreamWriter writer = new StreamWriter(langSettingPath, false))
            {
                writer.WriteLine(language.ToString());
            }
        }

        private static void LoadLangSetting()
        {
            string lang;
            using (StreamReader reader = new StreamReader(langSettingPath))
            {
                lang = reader.ReadLine();
            }
            if (lang == "null")
            {
                SetSystemLangauge();
            }
            else
            {
                language = (Language)Enum.Parse(typeof(Language), lang);
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
        }
    }
}
