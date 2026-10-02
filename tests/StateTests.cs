using System;
using System.IO;
using System.Linq;
using System.Text;
using ClearFrame;

class StateTests {
    static readonly StringBuilder log = new StringBuilder();
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); log.AppendLine("PASS: " + name); }
    static SavedState Snapshot(string title) {
        var state = new SavedState { Folder = "C:\\Videos\\ClearFrame" };
        state.Jobs.Add(new Job { Id = "0123456789abcdef0123456789abcdef", Title = title, Status = "Queued", Profile = "mp4", TargetResolution = 1080 });
        return state;
    }
    static int Main(string[] args) {
        try {
            string directory = Path.Combine(args[0], "state-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "state.json"), notice;
            var store = new StateStore(path);
            Check(store.Load(out notice).Jobs.Count == 0 && notice == null, "first launch has empty history");
            store.Save(Snapshot("first"));
            Check(store.Load(out notice).Jobs.Single().Title == "first" && notice == null, "history roundtrip");
            store.Save(Snapshot("second"));
            string backup = File.ReadAllText(path + ".bak");
            Check(new StateStore(path + ".bak").Load(out notice).Jobs.Single().Title == "first", "atomic save retains previous history");
            File.WriteAllText(path, "broken primary");
            store = new StateStore(path); var recovered = store.Load(out notice);
            Check(recovered.Jobs.Single().Title == "first" && notice.Contains("recovered"), "corrupt primary recovers from backup");
            store.Save(recovered);
            Check(File.ReadAllText(path + ".bak") == backup && File.ReadAllText(Directory.GetFiles(directory, "state.json.corrupt-*").Single()) == "broken primary", "recovery save preserves good backup and damaged primary bytes");
            store.Save(Snapshot("third"));
            Check(new StateStore(path + ".bak").Load(out notice).Jobs.Single().Title == "first", "normal backup rotation resumes after recovery");
            File.Delete(path);
            store = new StateStore(path); recovered = store.Load(out notice);
            Check(recovered.Jobs.Single().Title == "first" && notice.Contains("recovered"), "missing primary recovers from backup");store.Save(recovered);
            File.WriteAllText(path, "broken primary again");File.WriteAllText(path + ".bak", "broken backup");
            store = new StateStore(path); recovered = store.Load(out notice);
            Check(recovered.Jobs.Count == 0 && notice.Contains("could not be read"), "both damaged histories produce visible recovery notice");
            store.Save(Snapshot("fresh"));
            Check(Directory.GetFiles(directory, "state.json.corrupt-*").Any(p => File.ReadAllText(p) == "broken primary again") && File.ReadAllText(Directory.GetFiles(directory, "state.json.bak.corrupt-*").Single()) == "broken backup", "both damaged files preserved before fresh save");
            store.Save(Snapshot("newer"));File.WriteAllText(path, "{}");store = new StateStore(path);
            Check(store.Load(out notice).Jobs.Single().Title == "fresh", "invalid JSON structure also recovers from backup");store.Save(Snapshot("restored"));
            string before = File.ReadAllText(path); bool failed = false;
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) {
                try { store.Save(Snapshot("must not replace")); } catch (IOException) { failed = true; }
            }
            Check(failed && File.ReadAllText(path) == before, "failed atomic replacement preserves history");
            Check(!Directory.GetFiles(directory, "*.tmp-*").Any(), "failed and successful saves leave no temporary files");
            File.WriteAllText(args[1], log.ToString()); return 0;
        } catch (Exception ex) { log.AppendLine(ex.ToString()); File.WriteAllText(args[1], log.ToString()); return 1; }
    }
}
