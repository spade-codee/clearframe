using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClearFrame {
    public class SavedState {
        public string Folder;
        public List<Job> Jobs = new List<Job>();
        public Dictionary<string, object> Preferences;
    }

    public sealed class StateStore {
        readonly string path;
        bool preservePrimary, preserveBackup, keepBackup;
        public StateStore(string path) { this.path = path; }

        static SavedState Read(string file) {
            var data = Core.Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(file));
            object raw;
            if (data == null || !data.TryGetValue("jobs", out raw) || raw == null)
                throw new InvalidDataException("Missing download history.");
            var jobs = Core.Json.ConvertToType<List<Job>>(raw);
            if (jobs == null || jobs.Exists(j => j == null)) throw new InvalidDataException("Invalid download history.");
            object preferences;
            data.TryGetValue("preferences", out preferences);
            return new SavedState { Folder = Core.S(data, "folder"), Jobs = jobs, Preferences = preferences as Dictionary<string, object> };
        }

        public SavedState Load(out string notice) {
            notice = null;
            preservePrimary = preserveBackup = keepBackup = false;
            if (File.Exists(path)) {
                try { return Read(path); }
                catch { preservePrimary = true; }
            }
            if (File.Exists(path + ".bak")) {
                try {
                    var recovered = Read(path + ".bak");
                    keepBackup = true;
                    notice = "Download history recovered from its backup. Recent changes may be missing; interrupted downloads remain stopped.";
                    return recovered;
                } catch { preserveBackup = true; }
            }
            if (preservePrimary || preserveBackup) {
                keepBackup = true;
                notice = "Download history could not be read. Unreadable files will be preserved separately before new history is saved.";
            }
            return new SavedState();
        }

        public void Save(SavedState state) {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try {
                var data = new Dictionary<string, object> { { "folder", state.Folder }, { "jobs", state.Jobs }, { "preferences", state.Preferences } };
                File.WriteAllText(temp, Core.Json.Serialize(data), Encoding.UTF8);
                // Preserve unreadable input before replacing anything; a failed copy aborts saving.
                if (preservePrimary && File.Exists(path)) File.Copy(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                if (preserveBackup && File.Exists(path + ".bak")) File.Copy(path + ".bak", path + ".bak.corrupt-" + Guid.NewGuid().ToString("N"));
                if (File.Exists(path)) File.Replace(temp, path, keepBackup ? null : path + ".bak");
                else File.Move(temp, path);
                preservePrimary = preserveBackup = keepBackup = false;
            } finally {
                if (File.Exists(temp)) try { File.Delete(temp); } catch { }
            }
        }
    }
}
