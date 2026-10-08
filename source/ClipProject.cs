using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace ClearFrame {
    public sealed class ClipRecipe {
        public string Name;
        public double Start, End;
        public int X, Y, Width;
        public NamedClip ToClip(){return new NamedClip{Name=Name,Start=Start,End=End,X=X,Y=Y,Width=Width};}
    }
    public sealed class ClipProjectData {
        public int Version;
        public string Kind, SourcePath, SourceSha256;
        public long SourceLength;
        public VideoInfo SourceInfo;
        public List<ClipRecipe> Clips;
    }
    public static class ClipProject {
        public const string Extension=".cfclips.json";
        static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=16};}
        public static List<ClipRecipe> Recipes(IEnumerable<NamedClip> clips){return clips.Select(c=>new ClipRecipe{Name=c.Name,Start=c.Start,End=c.End,X=c.X,Y=c.Y,Width=c.Width}).ToList();}
        public static string Snapshot(string source,IEnumerable<NamedClip> clips){return Serializer().Serialize(new{Source=source,Clips=Recipes(clips)});}
        public static void Validate(ClipProjectData data){
            if(data==null||data.Kind!="ClearFrame clip project"||data.Version!=1)throw new InvalidDataException("This is not a supported ClearFrame clip project (version 1).");
            if(string.IsNullOrWhiteSpace(data.SourcePath)||data.SourcePath.Length>32767||!Path.IsPathRooted(data.SourcePath)||data.SourceLength<=0||!System.Text.RegularExpressions.Regex.IsMatch(data.SourceSha256??"",@"^[a-f0-9]{64}$"))throw new InvalidDataException("The project source reference is invalid.");
            Path.GetFullPath(data.SourcePath);
            var info=data.SourceInfo;if(info==null||info.Width<18||info.Height<32||info.Width>65536||info.Height>65536||info.Width%2!=0||info.Height%2!=0||double.IsNaN(info.Duration)||double.IsInfinity(info.Duration)||info.Duration<=0||double.IsNaN(info.PixelAspect)||double.IsInfinity(info.PixelAspect)||Math.Abs(info.PixelAspect-1)>0.001)throw new InvalidDataException("The project source metadata is invalid.");
            if(data.Clips==null||data.Clips.Count>ClipBatch.Limit)throw new InvalidDataException("A clip project can contain up to 50 saved clips.");
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var recipe in data.Clips){if(recipe==null)throw new InvalidDataException("The project contains an empty clip.");ClipBatch.Validate(recipe.ToClip(),info);if(!names.Add(recipe.Name))throw new InvalidDataException("The project contains duplicate clip names.");}
        }
        public static string Fingerprint(string source,CancellationToken token){
            using(var stream=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create()){
                var buffer=new byte[1048576];int read;while((read=stream.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();sha.TransformBlock(buffer,0,read,buffer,0);}token.ThrowIfCancellationRequested();sha.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();
            }
        }
        public static ClipProjectData Capture(string source,VideoInfo info,IEnumerable<NamedClip> clips,CancellationToken token){
            var file=new FileInfo(source);long length=file.Length;DateTime modified=file.LastWriteTimeUtc;
            var data=new ClipProjectData{Kind="ClearFrame clip project",Version=1,SourcePath=Path.GetFullPath(source),SourceInfo=info,SourceLength=length,Clips=Recipes(clips),SourceSha256=Fingerprint(source,token)};
            file.Refresh();if(file.Length!=length||file.LastWriteTimeUtc!=modified)throw new IOException("The source changed while the project was being saved. Try again.");Validate(data);return data;
        }
        public static ClipProjectData Read(string path){
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
                if(stream.Length>1048576)throw new InvalidDataException("The project exceeds the 1 MB size limit.");
                using(var reader=new StreamReader(stream,Encoding.UTF8,true)){var data=Serializer().Deserialize<ClipProjectData>(reader.ReadToEnd());Validate(data);return data;}
            }
        }
        public static void Save(string path,ClipProjectData data){
            Validate(data);path=Path.GetFullPath(path);
            if(!path.EndsWith(Extension,StringComparison.OrdinalIgnoreCase))throw new IOException("Save projects with the "+Extension+" extension.");
            if(string.Equals(path,data.SourcePath,StringComparison.OrdinalIgnoreCase)||string.Equals(path+".bak",data.SourcePath,StringComparison.OrdinalIgnoreCase))throw new IOException("Choose a project filename separate from the source video.");
            bool exists=File.Exists(path);
            if(exists)Read(path); // Only replace a valid project, even when its extension looks correct.
            if(exists&&File.Exists(path+".bak"))Read(path+".bak");
            string temp=path+".tmp-"+Guid.NewGuid().ToString("N");
            try{
                byte[] bytes=Encoding.UTF8.GetBytes(Serializer().Serialize(data));using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
                if(exists)File.Replace(temp,path,path+".bak");else File.Move(temp,path);
            }finally{if(File.Exists(temp))try{File.Delete(temp);}catch{}}
        }
        public static bool Matches(ClipProjectData project,string source,CancellationToken token){return new FileInfo(source).Length==project.SourceLength&&Fingerprint(source,token)==project.SourceSha256;}
        public static void VerifyMetadata(ClipProjectData project,VideoInfo actual){
            var saved=project.SourceInfo;if(actual.Width!=saved.Width||actual.Height!=saved.Height||Math.Abs(actual.Duration-saved.Duration)>0.1||actual.Audio!=saved.Audio||Math.Abs(actual.PixelAspect-saved.PixelAspect)>0.001)throw new InvalidDataException("The source metadata does not match this project.");
            foreach(var clip in project.Clips)ClipBatch.Validate(clip.ToClip(),actual);
        }
        public static void Check(string directory,string report){
            Directory.CreateDirectory(directory);int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};Action<Action,string> reject=(action,name)=>{bool failed=false;try{action();}catch{failed=true;}check(failed,name);};
            string source=Path.Combine(directory,"source.bin"),path=Path.Combine(directory,"session"+Extension);File.WriteAllText(source,"synthetic video bytes");var info=new VideoInfo{Width=320,Height=180,Duration=3,Audio=true};
            var clips=new[]{new NamedClip{Name="开场",Start=0,End=1,Width=720,State="Complete",Output="must-not-persist.mp4"},new NamedClip{Name="Answer",Start=1,End=3,X=230,Y=20,Width=1080}};
            var data=Capture(source,info,clips,CancellationToken.None);Save(path,data);var restored=Read(path);check(restored.Clips.Count==2&&restored.Clips[0].Name=="开场"&&restored.Clips[1].X==230&&restored.Clips[1].End==3,"settings roundtrip");
            check(restored.Clips[0].ToClip().State=="Ready"&&restored.Clips[0].ToClip().Output==""&&!File.ReadAllText(path).Contains("must-not-persist"),"no persisted completion or output authority");check(Matches(restored,source,CancellationToken.None),"original fingerprint matches");
            string relocated=Path.Combine(directory,"relocated.bin");File.Copy(source,relocated);check(Matches(restored,relocated,CancellationToken.None),"relocated bytes match");File.WriteAllText(relocated,"different video byte");check(!Matches(restored,relocated,CancellationToken.None),"different source rejected");
            using(var stop=new CancellationTokenSource()){stop.Cancel();reject(()=>Fingerprint(source,stop.Token),"hash cancellation");}
            data.Clips[0].Name="Updated";Save(path,data);check(Read(path).Clips[0].Name=="Updated"&&Read(path+".bak").Clips[0].Name=="开场","atomic replacement retains previous project");
            string previous=File.ReadAllText(path);using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)){reject(()=>Save(path,data),"locked project save rejected");}check(File.ReadAllText(path)==previous&&Directory.GetFiles(directory,"*.tmp-*").Length==0,"failed save preserves project and clears temporary files");
            string bad=Path.Combine(directory,"invalid"+Extension);File.WriteAllText(bad,"{broken");reject(()=>Read(bad),"invalid JSON");reject(()=>Save(bad,data),"corrupt project never overwritten");check(File.ReadAllText(bad)=="{broken","corrupt input retained");
            data.Version=2;reject(()=>Validate(data),"future schema rejected");data.Version=1;data.Clips[1].Name="updated";reject(()=>Validate(data),"duplicate names");data.Clips[1].Name="Answer";data.Clips[1].Start=double.NaN;reject(()=>Validate(data),"non-finite range");data.Clips[1].Start=1;
            info.PixelAspect=double.NaN;reject(()=>Validate(data),"non-finite pixel aspect");info.PixelAspect=1;data.Clips[1].X=232;reject(()=>Validate(data),"out-of-frame crop");data.Clips[1].X=230;
            reject(()=>Save(Path.Combine(directory,"video.mp4"),data),"project extension required");data.SourcePath=path;reject(()=>Save(path,data),"source overwrite blocked");data.SourcePath=source;
            string huge=Path.Combine(directory,"huge"+Extension);File.WriteAllText(huge,new string('x',1048577));reject(()=>Read(huge),"oversized project rejected");
            reject(()=>VerifyMetadata(data,new VideoInfo{Width=640,Height=180,Duration=3,Audio=true}),"mismatched metadata rejected");
            string snapshot=Snapshot(source,clips);clips[0].State="Failed";check(snapshot==Snapshot(source,clips),"export state does not dirty recipe");clips[0].End=2;check(snapshot!=Snapshot(source,clips),"range changes dirty recipe");
            File.WriteAllText(path+".bak","keep damaged backup");reject(()=>Save(path,data),"damaged backup protected");check(File.ReadAllText(path)==previous&&File.ReadAllText(path+".bak")=="keep damaged backup","failed backup validation keeps both files");
            data.Clips.Clear();string empty=Path.Combine(directory,"empty"+Extension);Save(empty,data);check(Read(empty).Clips.Count==0,"empty project roundtrip");File.WriteAllText(report,count+" clip-project checks passed.");
        }
        public static void CheckRestart(string directory,string phase){
            string source=Path.Combine(directory,"restart-source.bin"),path=Path.Combine(directory,"restart"+Extension);Directory.CreateDirectory(directory);
            if(phase=="write"){
                File.WriteAllText(source,"project restart fixture");var info=new VideoInfo{Width=320,Height=180,Duration=4,Audio=true};Save(path,Capture(source,info,new[]{new NamedClip{Name="Restored clip",Start=1,End=3,X=100,Y=10,Width=720,State="Complete",Output="ignored.mp4"}},CancellationToken.None));
            }else{
                var data=Read(path);var clip=data.Clips.Single().ToClip();if(clip.Name!="Restored clip"||clip.Start!=1||clip.End!=3||clip.X!=100||clip.Y!=10||clip.Width!=720||clip.State!="Ready"||clip.Output!=""||!Matches(data,source,CancellationToken.None))throw new Exception("Project did not survive separate-process reopening.");
                File.WriteAllText(Path.Combine(directory,"project-restart-checks.txt"),"PASS: separate application processes save and reopen named clip settings and verify the source; completion state resets to Ready without starting an export.");
            }
        }
    }
}
