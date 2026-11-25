using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Serialization;

namespace RetocExtractor.RetocExtractor.Codes
{
    [Serializable]
    public class AppSettings
    {
        public string ActiveProfileName { get; set; }
        public List<ProfileSettings> Profiles { get; set; }

        public AppSettings()
        {
            ActiveProfileName = ProfileSettings.DefaultProfileName;
            Profiles = [];
        }
    }

    [Serializable]
    public class ProfileSettings
    {
        public const string DefaultProfileName = "Default";
        public string Name { get; set; }
        public string PaksPath { get; set; }
        public string ProtectedAesKey { get; set; }
        public string UnrealVersion { get; set; }

        public ProfileSettings()
        {
            Name = ProfileSettings.DefaultProfileName;
            PaksPath = string.Empty;
            ProtectedAesKey = string.Empty;
            UnrealVersion = MainForm.DefaultRetocVersionToken;
        }
    }

    internal static class SettingsStore
    {

        private static readonly string SettingsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "settings.xml");
        private static readonly string PreviousSettingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UE5Export", "settings.xml");
        private static readonly string OldRetocSettingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RetocAssetPicker", "settings.xml");

        public static AppSettings Load()
        {
            AppSettings settings = null;
            string sourceFile = SettingsFile;
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                if (!File.Exists(sourceFile))
                {
                    if (File.Exists(PreviousSettingsFile)) sourceFile = PreviousSettingsFile;
                    else if (File.Exists(OldRetocSettingsFile)) sourceFile = OldRetocSettingsFile;
                }
                if (File.Exists(sourceFile))
                {
                    var serializer = new XmlSerializer(typeof(AppSettings));
                    using var stream = File.OpenRead(sourceFile);
                    settings = serializer.Deserialize(stream) as AppSettings;
                }
            }
            catch
            {
                // Preserve corrupt settings for manual recovery before using defaults.
                try
                {
                    if (File.Exists(sourceFile))
                    {
                        string backup = sourceFile + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
                        File.Copy(sourceFile, backup, false);
                    }
                }
                catch
                {
                    // Startup must continue if the recovery copy cannot be created.
                }
                settings = null;
            }

            settings ??= new AppSettings();
            settings.Profiles ??= [];
            settings.Profiles.RemoveAll(p => p == null);
            if (settings.Profiles.Count == 0)
                settings.Profiles.Add(new ProfileSettings { Name = ProfileSettings.DefaultProfileName });

            foreach (ProfileSettings profile in settings.Profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Name)) profile.Name = ProfileSettings.DefaultProfileName;
                profile.PaksPath ??= string.Empty;
                profile.ProtectedAesKey ??= string.Empty;
                if (string.IsNullOrWhiteSpace(profile.UnrealVersion)) profile.UnrealVersion = MainForm.DefaultRetocVersionToken;
            }

            ProfileSettings active = FindProfile(settings, settings.ActiveProfileName);
            if (active == null)
            {
                settings.ActiveProfileName = settings.Profiles[0].Name;
            }

            if (!string.Equals(sourceFile, SettingsFile, StringComparison.OrdinalIgnoreCase))
            {
                try { Save(settings); }
                catch
                {
                    // Startup continues; the next settings change will attempt another save.
                }
            }
            return settings;
        }

        public static void Save(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            Directory.CreateDirectory(SettingsDirectory);
            string temporaryFile = SettingsFile + ".tmp";
            var serializer = new XmlSerializer(typeof(AppSettings));
            using (var stream = new FileStream(temporaryFile, FileMode.Create, FileAccess.Write, FileShare.None))
                serializer.Serialize(stream, settings);

            if (File.Exists(SettingsFile))
            {
                try { File.Replace(temporaryFile, SettingsFile, null); }
                catch
                {
                    // Use the copy fallback on file systems that do not support File.Replace.
                    File.Copy(temporaryFile, SettingsFile, true);
                    File.Delete(temporaryFile);
                }
            }
            else
            {
                File.Move(temporaryFile, SettingsFile);
            }
        }

        public static string ProtectAesKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            byte[] clearBytes = Encoding.UTF8.GetBytes(value.Trim());
            try
            {
                return Convert.ToBase64String(NativeDpapi.Protect(clearBytes));
            }
            finally
            {
                Array.Clear(clearBytes, 0, clearBytes.Length);
            }
        }

        public static string UnprotectAesKey(string protectedValue)
        {
            if (string.IsNullOrWhiteSpace(protectedValue)) return string.Empty;
            byte[] protectedBytes;
            try
            {
                protectedBytes = Convert.FromBase64String(protectedValue);
            }
            catch (FormatException)
            {
                return string.Empty;
            }

            byte[] clearBytes = null;
            try
            {
                clearBytes = NativeDpapi.Unprotect(protectedBytes);
                return Encoding.UTF8.GetString(clearBytes);
            }
            catch (Win32Exception)
            {
                // DPAPI data may belong to another Windows user or profile.
                return string.Empty;
            }
            catch (DllNotFoundException)
            {
                return string.Empty;
            }
            catch (EntryPointNotFoundException)
            {
                return string.Empty;
            }
            finally
            {
                if (clearBytes != null) Array.Clear(clearBytes, 0, clearBytes.Length);
            }
        }

        private static class NativeDpapi
        {
            private const int CryptProtectUiForbidden = 0x1;

            [StructLayout(LayoutKind.Sequential)]
            private struct DataBlob
            {
                public int Size;
                public IntPtr Data;
            }

            [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
            private static extern bool CryptProtectData(
                ref DataBlob input, string description, IntPtr optionalEntropy, IntPtr reserved,
                IntPtr prompt, int flags, out DataBlob output);

            [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
            private static extern bool CryptUnprotectData(
                ref DataBlob input, out IntPtr description, IntPtr optionalEntropy, IntPtr reserved,
                IntPtr prompt, int flags, out DataBlob output);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr LocalFree(IntPtr memory);

            public static byte[] Protect(byte[] clearBytes)
            {
                return Transform(clearBytes, true);
            }

            public static byte[] Unprotect(byte[] protectedBytes)
            {
                return Transform(protectedBytes, false);
            }

            private static byte[] Transform(byte[] inputBytes, bool protect)
            {
                if (inputBytes == null) throw new ArgumentNullException(nameof(inputBytes));
                if (inputBytes.Length == 0) return [];

                DataBlob input = new() { Size = inputBytes.Length, Data = Marshal.AllocHGlobal(inputBytes.Length) };
                DataBlob output = new();
                IntPtr description = IntPtr.Zero;
                try
                {
                    Marshal.Copy(inputBytes, 0, input.Data, inputBytes.Length);
                    bool succeeded = protect
                        ? CryptProtectData(ref input, "Retoc Extractor AES key", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output)
                        : CryptUnprotectData(ref input, out description, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output);

                    if (!succeeded)
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    byte[] result = new byte[output.Size];
                    if (output.Size > 0) Marshal.Copy(output.Data, result, 0, output.Size);
                    return result;
                }
                finally
                {
                    if (output.Data != IntPtr.Zero)
                    {
                        ZeroMemory(output.Data, output.Size);
                        LocalFree(output.Data);
                    }
                    if (description != IntPtr.Zero) LocalFree(description);
                    if (input.Data != IntPtr.Zero)
                    {
                        ZeroMemory(input.Data, input.Size);
                        Marshal.FreeHGlobal(input.Data);
                    }
                }
            }

            private static void ZeroMemory(IntPtr memory, int length)
            {
                if (memory == IntPtr.Zero || length <= 0) return;
                for (int i = 0; i < length; i++) Marshal.WriteByte(memory, i, 0);
            }
        }

        public static ProfileSettings FindProfile(AppSettings settings, string name)
        {
            if (settings == null || settings.Profiles == null || string.IsNullOrWhiteSpace(name)) return null;
            foreach (ProfileSettings profile in settings.Profiles)
                if (string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)) return profile;
            return null;
        }
    }
}
