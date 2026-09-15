using System;
using System.IO;
using UnityEngine;

namespace GarageTycoon.Unity.Platform
{
    /// <summary>
    /// Reads and writes the save file on the device.
    ///
    /// Two details matter on mobile:
    ///  1. Saves are written to a temporary file and then moved into place. If the app is killed
    ///     mid-write (a phone call, the OS reclaiming memory) the old save survives intact rather
    ///     than being left half-written.
    ///  2. The time the game was closed is stored so offline progress can be worked out, using UTC
    ///     so travelling across time zones cannot rewind or fast-forward anyone's garage.
    /// </summary>
    public static class SaveFile
    {
        private const string FileName = "garage_tycoon_save.json";
        private const string TempFileName = "garage_tycoon_save.tmp";

        /// <summary>PlayerPrefs key used as a fallback when the file system is unavailable (e.g. WebGL).</summary>
        private const string PrefsKey = "GarageTycoon.Save";

        private static string SavePath { get { return Path.Combine(Application.persistentDataPath, FileName); } }
        private static string TempPath { get { return Path.Combine(Application.persistentDataPath, TempFileName); } }

        /// <summary>True when a save exists on this device.</summary>
        public static bool Exists()
        {
            try
            {
                if (File.Exists(SavePath)) return true;
            }
            catch (Exception)
            {
                // Fall through to the PlayerPrefs check below.
            }

            return PlayerPrefs.HasKey(PrefsKey);
        }

        /// <summary>Writes the save. Returns false if it could not be written for any reason.</summary>
        public static bool Write(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;

            try
            {
                File.WriteAllText(TempPath, json);

                // Replace the old save only once the new one is safely on disk.
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(TempPath, SavePath);

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[GarageTycoon] Could not write the save file, falling back to PlayerPrefs: " + exception.Message);

                try
                {
                    PlayerPrefs.SetString(PrefsKey, json);
                    PlayerPrefs.Save();
                    return true;
                }
                catch (Exception prefsException)
                {
                    Debug.LogError("[GarageTycoon] Saving failed entirely: " + prefsException.Message);
                    return false;
                }
            }
        }

        /// <summary>Reads the save, or returns null when there is nothing to read.</summary>
        public static string Read()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    if (!string.IsNullOrEmpty(json)) return json;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[GarageTycoon] Could not read the save file: " + exception.Message);
            }

            if (PlayerPrefs.HasKey(PrefsKey))
            {
                string json = PlayerPrefs.GetString(PrefsKey);
                if (!string.IsNullOrEmpty(json)) return json;
            }

            return null;
        }

        /// <summary>Deletes the save. Used by the "start again" option and by the editor tools.</summary>
        public static void Delete()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                if (File.Exists(TempPath)) File.Delete(TempPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[GarageTycoon] Could not delete the save file: " + exception.Message);
            }

            if (PlayerPrefs.HasKey(PrefsKey))
            {
                PlayerPrefs.DeleteKey(PrefsKey);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Seconds since the Unix epoch, in UTC. Stored in the save to measure time away.</summary>
        public static double NowUnixSeconds()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        /// <summary>Where the save lives, for the "where is my progress?" question.</summary>
        public static string DebugPath { get { return SavePath; } }
    }
}
