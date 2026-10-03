using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ClearFrame;

class ClipMediaTests {
    static string ffmpeg,ffprobe;
    static string Run(string exe,params string[] args){var result=Core.Run(exe,args,CancellationToken.None,120,null).GetAwaiter().GetResult();if(result.Code!=0)throw new Exception(result.Error);return result.Output;}
    static string Probe(string path){return Run(ffprobe,"-v","error","-show_streams","-show_format","-of","json",path);}
    static int Main(string[] args){var log=new StringBuilder();try{
        ffmpeg=Path.Combine(args[0],"ffmpeg.exe");ffprobe=Path.Combine(args[0],"ffprobe.exe");string dir=Path.Combine(args[1],Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);string source=Path.Combine(dir,"source.mp4");
        Run(ffmpeg,"-hide_banner","-loglevel","error","-y","-f","lavfi","-i","testsrc2=size=320x180:rate=24:duration=5","-f","lavfi","-i","sine=frequency=440:duration=5","-c:v","libx264","-g","240","-keyint_min","240","-sc_threshold","0","-c:a","aac","-shortest",source);
        byte[] sourceBytes=File.ReadAllBytes(source);
        foreach(string profile in new[]{"mp4","compatible","mkv","mov","webm","mp3","m4a"}){
            var job=new Job{Profile=profile,Container=Core.Container(profile),Resolution=Core.IsAudio(profile)?0:180,Duration=5,ClipStart=1.25,ClipEnd=3.25};string output=Path.Combine(dir,profile+"."+job.Container);
            Run(ffmpeg,DownloadOptions.ClipArgs(source,output,job).ToArray());DownloadOptions.VerifyClip(Probe(output),job);log.AppendLine("PASS: "+profile+" clip duration, resolution and audio.");
            var denied=Core.Run(ffmpeg,DownloadOptions.ClipArgs(source,output,job),CancellationToken.None,15,null).GetAwaiter().GetResult();if(denied.Code==0)throw new Exception("Existing clip overwritten.");
        }
        string expected=Path.Combine(dir,"expected.rgb"),actual=Path.Combine(dir,"actual.rgb");Run(ffmpeg,"-v","error","-ss","1.25","-i",source,"-frames:v","1","-pix_fmt","rgb24","-f","rawvideo",expected);Run(ffmpeg,"-v","error","-i",Path.Combine(dir,"mp4.mp4"),"-frames:v","1","-pix_fmt","rgb24","-f","rawvideo",actual);
        byte[] wanted=File.ReadAllBytes(expected),got=File.ReadAllBytes(actual);if(wanted.Length!=got.Length)throw new Exception("First clip frame dimensions changed.");double error=wanted.Select((value,index)=>Math.Abs(value-got[index])).Average();if(error>8)throw new Exception("First clip frame does not match requested non-keyframe start: "+error);log.AppendLine("PASS: first decoded frame matches requested non-keyframe start (mean pixel difference "+error.ToString("0.00")+").");
        if(!sourceBytes.SequenceEqual(File.ReadAllBytes(source)))throw new Exception("Source modified.");log.AppendLine("PASS: source bytes preserved and existing outputs protected for all seven profiles.");
        File.WriteAllText(args[2],log.ToString());return 0;
    }catch(Exception ex){log.AppendLine(ex.ToString());File.WriteAllText(args[2],log.ToString());return 1;}}
}
