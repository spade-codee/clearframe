using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ClearFrame {
    public sealed class ClipDraft {
        public string Name, Start, End, X, Y, Frame;
        public int Size, EditingIndex;
    }
    public sealed class RecoveryData {
        public string Kind, SavedUtc;
        public int Version;
        public ClipProjectData Project;
        public ClipDraft Draft;
    }
    public static class ClipRecovery {
        public const string Extension=".cfrecovery.json";
        static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=20};}
        public static string DraftSnapshot(ClipDraft draft){return Serializer().Serialize(draft);}
        public static void Validate(RecoveryData data){
            if(data==null||data.Kind!="ClearFrame clip recovery"||data.Version!=1)throw new InvalidDataException("This is not a supported ClearFrame recovery snapshot.");ClipProject.Validate(data.Project);
            DateTime saved;if(!DateTime.TryParseExact(data.SavedUtc,"o",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out saved)||saved.Kind!=DateTimeKind.Utc)throw new InvalidDataException("Invalid recovery timestamp.");
            var d=data.Draft;if(d==null||d.Size<0||d.Size>1||d.EditingIndex< -1||d.EditingIndex>=data.Project.Clips.Count)throw new InvalidDataException("Invalid recovery editor state.");
            foreach(string value in new[]{d.Name,d.Start,d.End,d.X,d.Y,d.Frame})if(value==null||value.Length>4096)throw new InvalidDataException("Recovery editor fields exceed their size limit.");
            if(d.Name.Length>80)throw new InvalidDataException("Recovery clip name exceeds its size limit.");
        }
        public static RecoveryData Read(string path){using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(file.Length>1048576)throw new InvalidDataException("Recovery file exceeds 1 MB.");using(var reader=new StreamReader(file,Encoding.UTF8,true)){var data=Serializer().Deserialize<RecoveryData>(reader.ReadToEnd());Validate(data);return data;}}}
        public static void Save(string path,RecoveryData data){
            Validate(data);path=Path.GetFullPath(path);if(!path.EndsWith(Extension,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid recovery filename.");
            if(string.Equals(path,data.Project.SourcePath,StringComparison.OrdinalIgnoreCase)||string.Equals(path+".bak",data.Project.SourcePath,StringComparison.OrdinalIgnoreCase))throw new IOException("Recovery cannot replace the source video.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));bool exists=File.Exists(path);if(exists)Read(path);if(exists&&File.Exists(path+".bak"))Read(path+".bak");string temp=path+".tmp-"+Guid.NewGuid().ToString("N");
            try{byte[] bytes=Encoding.UTF8.GetBytes(Serializer().Serialize(data));using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}if(exists)File.Replace(temp,path,path+".bak");else File.Move(temp,path);}finally{if(File.Exists(temp))try{File.Delete(temp);}catch{}}
        }
        public static void Check(string directory,string report){
            Directory.CreateDirectory(directory);int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};Action<Action,string> reject=(action,name)=>{bool failed=false;try{action();}catch{failed=true;}check(failed,name);};
            string source=Path.Combine(directory,"source.bin");File.WriteAllText(source,"recovery fixture");var info=new VideoInfo{Width=320,Height=180,Duration=3};var project=ClipProject.Capture(source,info,new[]{new NamedClip{Name="Saved clip",Start=0,End=1,Width=720}},System.Threading.CancellationToken.None);
            var data=new RecoveryData{Kind="ClearFrame clip recovery",Version=1,SavedUtc=DateTime.UtcNow.ToString("o"),Project=project,Draft=new ClipDraft{Name="Uncommitted",Start="typing",End="",X="-",Y="10",Frame="1.25",Size=1,EditingIndex=0}};string path=Path.Combine(directory,"session"+Extension);Save(path,data);var loaded=Read(path);check(loaded.Draft.Start=="typing"&&loaded.Draft.End==""&&loaded.Draft.X=="-","unfinished raw inputs preserved");check(loaded.Project.Clips[0].Name=="Saved clip"&&loaded.Draft.Name=="Uncommitted"&&loaded.Draft.EditingIndex==0,"draft separate from saved clip list");
            string before=File.ReadAllText(path);data.Draft.Name="Later";Save(path,data);check(Read(path).Draft.Name=="Later"&&Read(path+".bak").Draft.Name=="Uncommitted","atomic update and prior snapshot");
            using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)){reject(()=>Save(path,data),"locked autosave refused");}check(Read(path).Draft.Name=="Later"&&Directory.GetFiles(directory,"*.tmp-*").Length==0,"failed write keeps latest snapshot and removes temporary files");
            data.Draft.EditingIndex=2;reject(()=>Validate(data),"invalid editing index");data.Draft.EditingIndex=0;data.Draft.Size=2;reject(()=>Validate(data),"invalid size");data.Draft.Size=1;data.Draft.Frame=new string('x',4097);reject(()=>Validate(data),"oversized draft");data.Draft.Frame="0";data.Version=2;reject(()=>Validate(data),"unknown version");data.Version=1;
            data.Project.Clips[0].Start=double.NaN;reject(()=>Validate(data),"invalid committed recipe");data.Project.Clips[0].Start=0;File.WriteAllText(path,"{broken");reject(()=>Save(path,data),"corrupt current snapshot preserved");check(File.ReadAllText(path)=="{broken"&&Read(path+".bak").Draft.Name=="Uncommitted","backup remains readable when current damaged");
            data.SavedUtc="yesterday";reject(()=>Validate(data),"invalid timestamp");File.WriteAllText(report,count+" recovery checks passed.");
        }
        public static void CheckRestart(string directory,string phase){
            Directory.CreateDirectory(directory);string source=Path.Combine(directory,"source.bin"),path=Path.Combine(directory,"session"+Extension);
            if(phase=="write"){File.WriteAllText(source,"restart source");var project=ClipProject.Capture(source,new VideoInfo{Width=320,Height=180,Duration=3},new[]{new NamedClip{Name="Stored",Start=0,End=1,Width=720,State="Complete"}},System.Threading.CancellationToken.None);Save(path,new RecoveryData{Kind="ClearFrame clip recovery",Version=1,SavedUtc=DateTime.UtcNow.ToString("o"),Project=project,Draft=new ClipDraft{Name="Unsaved edit",Start="1.",End="",X="100",Y="10",Frame="0.5",Size=1,EditingIndex=0}});}
            else{var data=Read(path);if(data.Draft.Name!="Unsaved edit"||data.Draft.Start!="1."||data.Project.Clips[0].ToClip().State!="Ready"||!ClipProject.Matches(data.Project,source,System.Threading.CancellationToken.None))throw new Exception("Recovery did not survive restart.");File.WriteAllText(Path.Combine(directory,"recovery-restart.txt"),"PASS: a separate application process restores committed clips and unfinished raw editor inputs; outputs stay Ready and no export starts.");}
        }
    }
}
