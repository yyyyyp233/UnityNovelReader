using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace UnityNovelReader.Editor
{
    internal sealed class ReaderStateStore
    {
        internal const string HomeOverrideEnvironmentVariable = "UNITY_NOVEL_READER_HOME";

        private readonly string statePath;

        internal ReaderStateStore()
            : this(GetDefaultStatePath())
        {
        }

        internal ReaderStateStore(string customStatePath)
        {
            statePath = customStatePath;
        }

        internal string StatePath
        {
            get { return statePath; }
        }

        internal static string GetDefaultDataDirectory()
        {
            string overrideDirectory = Environment.GetEnvironmentVariable(HomeOverrideEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(overrideDirectory))
            {
                return Path.GetFullPath(overrideDirectory);
            }

            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".unity-novel-reader");
            }

            return Path.Combine(root, "UnityNovelReader");
        }

        internal static string GetDefaultStatePath()
        {
            return Path.Combine(GetDefaultDataDirectory(), "state.json");
        }

        internal ReaderStateData Load()
        {
            ReaderStateData loaded = TryLoad(statePath);
            if (loaded == null)
            {
                loaded = TryLoad(statePath + ".bak");
            }

            if (loaded == null)
            {
                loaded = new ReaderStateData();
            }

            loaded.EnsureDefaults();
            return loaded;
        }

        internal void Save(ReaderStateData state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            state.EnsureDefaults();
            string directory = Path.GetDirectoryName(statePath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("State path must include a directory.");
            }

            Directory.CreateDirectory(directory);
            string json = JsonUtility.ToJson(state, true);
            string temporaryPath = statePath + ".tmp";
            string backupPath = statePath + ".bak";

            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            try
            {
                if (File.Exists(statePath))
                {
                    File.Copy(statePath, backupPath, true);
                }

                File.Copy(temporaryPath, statePath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static ReaderStateData TryLoad(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                return JsonUtility.FromJson<ReaderStateData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Unity Novel Reader could not load state from '" + path + "': " + exception.Message);
                return null;
            }
        }
    }
}
