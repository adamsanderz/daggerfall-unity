﻿// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2018 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: TheLacus
// Contributors:    
// 
// Notes:
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using IniParser;
using IniParser.Model;

namespace DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings
{
    /// <summary>
    /// Read/Write settings files.
    /// </summary>
    public static class ModSettingsReader
    {
        #region Fields

        /// <summary>
        /// Section containing information used by the modding system.
        /// </summary>
        public const string internalSection = "Internal";

        /// <summary>
        /// Key with version of settings file.
        /// </summary>
        public const string settingsVersionKey = "SettingsVersion";

        /// <summary>
        /// Delimiter between First and Second value of a tuple.
        /// </summary>
        public const string tupleDelimiterChar = "<,>";

        static FileIniDataParser parser = new FileIniDataParser();

        #endregion

        #region Load/Save Settings

        /// <summary>
        /// Check if a mod support settings. If configuration file
        /// is missing it will be recreated with default values.
        /// </summary>
        public static bool HasSettings(Mod mod)
        {
            // Check file on disk
            if (File.Exists(SettingsPath(mod)))
                return true;

            // Recreate file on disk using default values
            ModSettingsConfiguration config;
            if (TryGetConfig(mod, out config))
            {
                ResetSettings(mod, config);
                return true;
            }

            return false;
        }

        public static void GetSettings(Mod mod, out IniData settings, out ModSettingsConfiguration config)
        {
            // Load config
            if (!TryGetConfig(mod, out config))
                throw new ArgumentException("Mod has no associated settings.");

            // Load serialized settings or recreate them
            string path = SettingsPath(mod);
            if (File.Exists(path))
            {
                settings = parser.ReadFile(path);

                var header = settings.Sections.GetSectionData(internalSection);
                if (header == null || header.Keys[settingsVersionKey] != config.version)
                {
                    ResetSettings(mod, ref settings, config);
                    Debug.LogFormat("Settings for {0} are incompatible with current version. " +
                        "New settings have been recreated with default values", mod.Title);
                } 
            }
            else
            {
                settings = null;
                ResetSettings(mod, ref settings, config);
                Debug.LogFormat("Missing settings for {0}. " +
                    "New settings have been recreated with default values.", mod.Title);
            }
        }

        public static bool TryGetConfig(Mod mod, out ModSettingsConfiguration config, bool legacySupport = true)
        {
            if (mod.AssetBundle.Contains("modsettings.asset"))
            {
                config = mod.GetAsset<ModSettingsConfiguration>("modsettings.asset");
                return true;
            }

            if (legacySupport)
            {
                // Support for old mods
                if (mod.AssetBundle.Contains(mod.Title + ".ini.txt"))
                {
                    var data = GetIniDataFromTextAsset(mod.GetAsset<TextAsset>(mod.Title + ".ini.txt"));
                    config = ParseIniToConfig(data);
                    return true;
                }
                else if (mod.AssetBundle.Contains("modsettings.ini.txt"))
                {
                    var data = GetIniDataFromTextAsset(mod.GetAsset<TextAsset>("modsettings.ini.txt"));
                    config = ParseIniToConfig(data);
                    return true;
                }

                // Eventually this will no longer be supported as file name can be changed by user.
                if (mod.AssetBundle.Contains(mod.FileName + ".ini.txt"))
                {
                    Debug.LogWarningFormat("{0} is using an obsolete modsettings filename!", mod.Title);
                    var data = GetIniDataFromTextAsset(mod.GetAsset<TextAsset>(mod.FileName + ".ini.txt"));
                    config = ParseIniToConfig(data);
                    return true;
                }
            }

            config = null;
            return false;
        }

        /// <summary>
        /// Save settings to disk.
        /// </summary>
        public static void SaveSettings(Mod mod, IniData settings)
        {
            parser.WriteFile(SettingsPath(mod), settings);
        }

        /// <summary>
        /// Save default settings to disk.
        /// </summary>
        public static void ResetSettings(Mod mod, ModSettingsConfiguration config)
        {
            parser.WriteFile(SettingsPath(mod), ParseConfigToIni(config));
        }

        /// <summary>
        /// Save default settings to disk and set them as current settings.
        /// </summary>
        public static void ResetSettings(Mod mod, ref IniData settings, ModSettingsConfiguration config)
        {
            settings = ParseConfigToIni(config);
            parser.WriteFile(SettingsPath(mod), settings);
        }

        /// <summary>
        /// Import presets from mod and local presets from disk.
        /// </summary>
        public static List<IniData> GetPresets (Mod mod)
        {
            List<IniData> presets = new List<IniData>();

            // Get presets from mod (TextAsset)
            int index = 0;
            while (mod.AssetBundle.Contains("settingspreset" + index + ".ini.txt"))
            {
                TextAsset presetFile = mod.GetAsset<TextAsset>("settingspreset" + index + ".ini.txt");
                presets.Add(GetIniDataFromTextAsset(presetFile));
                index++;
            }

            // Get preset from mod (Config)
            ModSettingsConfiguration config;
            if (TryGetConfig(mod, out config, false))
            {
                foreach (var presetName in config.presets)
                {
                    var presetConfig = mod.GetAsset<ModSettingsConfiguration>(presetName);
                    presets.Add(ParseConfigToIni(presetConfig));
                }
            }

            // Get presets from disk
            foreach (string path in Directory.GetFiles(mod.DirPath, mod.FileName + "preset*.ini"))
                presets.Add(parser.ReadFile(path));

            return presets;
        }

        public static void CreatePreset(Mod mod, IniData data, Preset preset)
        {
            IniData presetData = new IniData(data);
            var section = new SectionData(internalSection);
            section.Keys.AddKey("PresetName", preset.Title);
            section.Keys.AddKey("Description", preset.Description);
            section.Keys.AddKey("PresetAuthor", preset.Author);
            section.Keys.AddKey("SettingsVersion", preset.Version);
            presetData.Sections.Add(section);

            string name = string.Format("{0}preset{1}.ini", mod.FileName, preset.Title);
            foreach (char c in Path.GetInvalidPathChars())
                name = name.Replace(c, '_');
            parser.WriteFile(Path.Combine(mod.DirPath, name), presetData);      
        }

        #endregion

        #region Helper Methods

        public static IniData ParseConfigToIni(ModSettingsConfiguration config)
        {
            var iniData = new IniData();

            // Header
            var header = new SectionData(internalSection);
            header.Keys.AddKey(settingsVersionKey, config.version);

            if (config.isPreset)
            {
                header.Keys.AddKey("PresetName", config.presetSettings.name);
                header.Keys.AddKey("PresetAuthor", config.presetSettings.author);
                header.Keys.AddKey("Description", config.presetSettings.description);
            }

            iniData.Sections.Add(header);

            // Settings
            foreach (var section in config.sections)
            {
                var sectionData = new SectionData(section.name);

                foreach (var key in section.keys)
                {
                    KeyData keyData = new KeyData(key.name);

                    switch (key.type)
                    {
                        case ModSettingsKey.KeyType.Toggle:
                            keyData.Value = key.toggle.value.ToString();
                            break;

                        case ModSettingsKey.KeyType.MultipleChoice:
                            keyData.Value = key.multipleChoice.selected.ToString();
                            break;

                        case ModSettingsKey.KeyType.Slider:
                            keyData.Value = key.slider.value.ToString();
                            break;

                        case ModSettingsKey.KeyType.FloatSlider:
                            keyData.Value = key.floatSlider.value.ToString();
                            break;

                        case ModSettingsKey.KeyType.Tuple:
                            keyData.Value = key.tuple.first + tupleDelimiterChar + key.tuple.second;
                            break;

                        case ModSettingsKey.KeyType.FloatTuple:
                            keyData.Value = key.floatTuple.first + tupleDelimiterChar + key.floatTuple.second;
                            break;

                        case ModSettingsKey.KeyType.Text:
                            keyData.Value = key.text.text;
                            break;

                        case ModSettingsKey.KeyType.Color:
                            keyData.Value = key.color.HexColor;
                            break;
                    }

                    sectionData.Keys.AddKey(keyData);
                }

                if (section.name == internalSection)
                    iniData.Sections.GetSectionData(internalSection).Merge(sectionData);
                else
                    iniData.Sections.Add(sectionData);
            }

            return iniData;
        }

        public static ModSettingsConfiguration ParseIniToConfig(IniData iniData)
        {
            var config = ScriptableObject.CreateInstance(typeof(ModSettingsConfiguration)) as ModSettingsConfiguration;
            ParseIniToConfig(iniData, config);
            return config;
        }

        public static void ParseIniToConfig(IniData iniData, ModSettingsConfiguration config)
        {
            var configSections = new List<ModSettingsConfiguration.Section>();
            foreach (SectionData section in iniData.Sections)
            {
                var configSection = new ModSettingsConfiguration.Section();
                configSection.name = section.SectionName;

                List<ModSettingsKey> keys = new List<ModSettingsKey>();
                foreach (KeyData key in section.Keys)
                {
                    var configKey = new ModSettingsKey();
                    configKey.name = key.KeyName;

                    if (key.Value == "True" || key.Value == "False")
                    {
                        configKey.type = ModSettingsKey.KeyType.Toggle;
                        configKey.toggle = new ModSettingsKey.Toggle();
                        configKey.toggle.value = bool.Parse(key.Value);
                    }
                    else if (key.Value.Contains(tupleDelimiterChar))
                    {
                        configKey.type = ModSettingsKey.KeyType.FloatTuple;
                        configKey.floatTuple = new ModSettingsKey.FloatTuple();
                        int index = key.Value.IndexOf(tupleDelimiterChar);
                        float.TryParse(key.Value.Substring(0, index), out configKey.floatTuple.first);
                        float.TryParse(key.Value.Substring(index + tupleDelimiterChar.Length), out configKey.floatTuple.second);
                    }
                    else if (IsHexColor(key.Value))
                    {
                        configKey.type = ModSettingsKey.KeyType.Color;
                        configKey.color = new ModSettingsKey.Tint();
                        configKey.color.HexColor = key.Value;
                    }
                    else
                    {
                        configKey.type = ModSettingsKey.KeyType.Text;
                        configKey.text = new ModSettingsKey.Text();
                        configKey.text.text = key.Value;
                    }

                    keys.Add(configKey);
                }

                if (section.SectionName == internalSection)
                {
                    // Header
                    config.version = section.Keys[settingsVersionKey];

                    // Add section only if there are other hidden keys
                    if (keys.Count > 1)
                    {
                        configSection.keys = keys.Where(x => x.name != settingsVersionKey).ToArray();
                        configSections.Add(configSection);
                    }
                }
                else
                {
                    // Settings
                    configSection.keys = keys.ToArray();
                    configSections.Add(configSection);
                }
            }
            config.sections = configSections.ToArray();
        }

        public static bool IsHexColor(string stringColor)
        {
            int hexColor;
            return (stringColor.Length == 8 &&
                int.TryParse(stringColor, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hexColor));
        }

        /// <summary>
        /// Make a displayable name for a key or section.
        /// </summary>
        public static string FormattedName(string name)
        {
            return string.Concat((name.First().ToString().ToUpper() + name.Substring(1))
                .Select(x => Char.IsUpper(x) ? " " + x : x.ToString()).ToArray()).TrimStart(' ');
        }

        #endregion

        #region Private Methods

        private static string SettingsPath(Mod mod)
        {
            return Path.Combine(mod.DirPath, mod.FileName + ".ini");
        }

        private static IniData GetIniDataFromTextAsset (TextAsset textAsset)
        {
            MemoryStream stream = new MemoryStream(textAsset.bytes);
            StreamReader reader = new StreamReader(stream);
            IniData iniData = parser.ReadData(reader);
            reader.Close();
            return iniData;
        }

        #endregion
    }
}
