using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClearFrame {
    public sealed class NamedClip {
        public string Name, State = "Ready", Output = "";
        public double Start, End;
        public int X, Y, Width;
        public string Summary { get { return Name + "  ·  " + Start.ToString("0.###",CultureInfo.InvariantCulture) + "–" + End.ToString("0.###",CultureInfo.InvariantCulture) + " s  ·  " + Width + "×" + VerticalClips.Height(Width) + "  ·  crop " + X + "," + Y + "  ·  " + State; } }
    }
    public static class ClipBatch {
        public const int Limit = 50;
        public static string ValidateName(string name) {
            if(string.IsNullOrWhiteSpace(name)||name.Length>80||name!=name.Trim()||name.EndsWith(".")||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
                throw new Exception("Use a clip name of 1–80 characters, without filename symbols, leading/trailing spaces or a final dot.");
            string stem=name.Split('.')[0].TrimEnd().ToUpperInvariant();
            if(new[]{"CON","PRN","AUX","NUL","CONIN$","CONOUT$"}.Contains(stem)||System.Text.RegularExpressions.Regex.IsMatch(stem,@"^(COM|LPT)[1-9¹²³]$"))
                throw new Exception("That clip name is reserved by Windows. Choose another name.");
            return name;
        }
        public static void Validate(NamedClip clip,VideoInfo source) {
            ValidateName(clip.Name);DownloadOptions.ValidateClip(clip.Start,clip.End,source.Duration);VerticalClips.Filter(source,clip.X,clip.Y,clip.Width);
        }
        public static List<string> Plan(IList<NamedClip> clips,VideoInfo source,string folder) {
            if(clips.Count==0||clips.Count>Limit)throw new Exception("Add between 1 and 50 clips.");
            if(!Directory.Exists(folder))throw new Exception("Choose an existing output folder.");
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var paths=new List<string>();
            foreach(var clip in clips){Validate(clip,source);if(!names.Add(clip.Name))throw new Exception("Clip names must be unique, ignoring letter case.");
                string path=Path.Combine(Path.GetFullPath(folder),clip.Name+".mp4");
                if(path.Length>200)throw new Exception("Use a shorter output folder or clip name (maximum output path: 200 characters).");
                if(File.Exists(path)||Directory.Exists(path))throw new Exception("Already exists: "+Path.GetFileName(path)+". Rename the clip or choose another folder. No files were exported.");
                paths.Add(path);
            }return paths;
        }
        static async Task<string> Probe(string ffprobe,string file,CancellationToken token){var r=await Core.Run(ffprobe,new[]{"-v","error","-show_streams","-show_format","-of","json",file},token,60,null);if(r.Code!=0)throw new Exception(Core.Friendly(r.Error));return r.Output;}
        public static async Task Export(string ffmpeg,string ffprobe,string input,VideoInfo expected,IList<NamedClip> clips,string folder,CancellationToken token,Action<int,double> progress){
            token.ThrowIfCancellationRequested();
            var source=CleanupCore.ReadInfo(await Probe(ffprobe,input,token));
            if(source.Width!=expected.Width||source.Height!=expected.Height||Math.Abs(source.Duration-expected.Duration)>0.1||Math.Abs(source.PixelAspect-expected.PixelAspect)>0.001||source.Audio!=expected.Audio)
                throw new Exception("The source video changed. Open it again before exporting these clips.");
            var pending=clips.Where(c=>c.State!="Complete").ToList();if(pending.Count==0)throw new Exception("All saved clips are already complete. Save a new clip to export more.");
            var paths=Plan(pending,source,folder);
            for(int i=0;i<pending.Count;i++){
                token.ThrowIfCancellationRequested();var clip=pending[i];string temporary=paths[i]+".partial-"+Guid.NewGuid().ToString("N")+".mp4";
                try{
                    var drive=new DriveInfo(Path.GetPathRoot(paths[i]));if(drive.IsReady&&drive.AvailableFreeSpace<Math.Max(new FileInfo(input).Length,536870912L))throw new Exception("Free additional disk space before exporting. Output size depends on the source.");
                    clip.State="Exporting";if(progress!=null)progress(i,0);
                    int index=i;var result=await Core.Run(ffmpeg,VerticalClips.ExportArgs(input,temporary,source,clip.X,clip.Y,clip.Width,clip.Start,clip.End),token,86400,line=>{
                        double time;if(progress!=null&&line.StartsWith("out_time_us=")&&double.TryParse(line.Substring(12),NumberStyles.Float,CultureInfo.InvariantCulture,out time))progress(index,Math.Min(99,Math.Max(0,time/1000000/(clip.End-clip.Start)*100)));
                    });
                    if(result.Code!=0)throw new Exception(Core.Friendly(result.Error));token.ThrowIfCancellationRequested();
                    VerticalClips.Verify(await Probe(ffprobe,temporary,token),source,clip.Width,clip.Start,clip.End);token.ThrowIfCancellationRequested();
                    File.Move(temporary,paths[i]);clip.Output=paths[i];clip.State="Complete";if(progress!=null)progress(i,100);
                }catch(OperationCanceledException){clip.State="Cancelled";throw;}catch{clip.State="Failed";throw;}
                finally{if(File.Exists(temporary))try{File.Delete(temporary);}catch{}}
            }
        }
        public static void Check(string report){
            int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};Action<Action,string> reject=(action,name)=>{bool failed=false;try{action();}catch{failed=true;}check(failed,name);};
            foreach(string name in new[]{"", "../escape", "a/b", "clip.", " trailing", "CON", "com1.txt", "LPT²", "a:b", new string('x',81)})reject(()=>ValidateName(name),"unsafe name rejected");
            check(ValidateName("Interview – 开场")=="Interview – 开场","Unicode name retained");
            string folder=Path.Combine(Path.GetTempPath(),"ClearFrame-batch-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try{
                var source=new VideoInfo{Width=320,Height=180,Duration=5};var a=new NamedClip{Name="Intro",Start=0,End=1,Width=720};var b=new NamedClip{Name="Answer",Start=1,End=3,X=230,Y=20,Width=1080};
                var list=new[]{a,b};var paths=Plan(list,source,folder);check(paths.Count==2&&Path.GetFileName(paths[1])=="Answer.mp4","order and names preserved");
                b.Name="INTRO";reject(()=>Plan(list,source,folder),"case-insensitive duplicates");b.Name="Answer";
                File.WriteAllText(paths[1],"keep");reject(()=>Plan(list,source,folder),"preflight collision");check(File.ReadAllText(paths[1])=="keep"&&!File.Exists(paths[0]),"collision preserves files before batch");
                b.End=10;reject(()=>Validate(b,source),"range outside source");b.End=3;b.X=232;reject(()=>Validate(b,source),"crop outside source");
                reject(()=>Plan(new NamedClip[0],source,folder),"empty batch");reject(()=>Plan(Enumerable.Repeat(a,51).ToArray(),source,folder),"batch cap");
                File.Delete(paths[1]);Directory.CreateDirectory(paths[1]);b.X=230;reject(()=>Plan(list,source,folder),"directory collision");Directory.Delete(paths[1]);
            }finally{foreach(var path in Directory.GetFiles(folder))File.Delete(path);Directory.Delete(folder);}
            File.WriteAllText(report,count+" batch-clip checks passed.");
        }
    }
}
