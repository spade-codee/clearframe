using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ClearFrame;

class CleanupMediaTests {
    static string ffmpeg, ffprobe;
    static string Run(string exe, params string[] arguments) {
        var result = Core.Run(exe, arguments, CancellationToken.None, 90, null).GetAwaiter().GetResult();
        if (result.Code != 0) throw new Exception(result.Error);
        return result.Output;
    }
    static string Probe(string path) { return Run(ffprobe, "-v", "error", "-show_streams", "-show_format", "-of", "json", path); }
    static void Preview(string source,string output,string filter,int width,int height) {
        Run(ffmpeg,CleanupCore.PreviewArgs(source,output,filter,0.5));
        var root=Core.Json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(Probe(output));
        var video=Core.Entries(root,"streams").First(s=>Core.S(s,"codec_type")=="video");
        if(Core.N(video,"width")!=width||Core.N(video,"height")!=height)throw new Exception("Preview dimensions differ: "+output);
    }
    static int Main(string[] args) {
        var log = new StringBuilder();
        try {
            ffmpeg = Path.Combine(args[0], "ffmpeg.exe"); ffprobe = Path.Combine(args[0], "ffprobe.exe");
            Directory.CreateDirectory(args[1]); string source = Path.Combine(args[1], "synthetic source.mp4");
            Run(ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=24:duration=1.5,drawbox=x=260:y=20:w=40:h=20:color=white:t=fill", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=1.5", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", source);
            byte[] original = File.ReadAllBytes(source); var info = CleanupCore.ReadInfo(Probe(source));
            BatchChecks(source,info,args[1],log);
            string projectFolder=Path.Combine(args[1],"project-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(projectFolder);string projectPath=Path.Combine(projectFolder,"clips.cfclips.json");
            ClipProject.Save(projectPath,ClipProject.Capture(source,info,new[]{new NamedClip{Name="Reopened clip",Start=0.25,End=1.25,X=100,Y=10,Width=720,State="Complete"}},CancellationToken.None));
            var reopened=ClipProject.Read(projectPath);string movedSource=Path.Combine(projectFolder,"renamed-source.mp4");File.Copy(source,movedSource);if(!ClipProject.Matches(reopened,movedSource,CancellationToken.None))throw new Exception("Relocated project source rejected.");var movedInfo=CleanupCore.ReadInfo(Probe(movedSource));ClipProject.VerifyMetadata(reopened,movedInfo);var reopenedClips=reopened.Clips.Select(c=>c.ToClip()).ToList();
            ClipBatch.Export(ffmpeg,ffprobe,movedSource,movedInfo,reopenedClips,projectFolder,CancellationToken.None,null).GetAwaiter().GetResult();VerticalClips.Verify(Probe(reopenedClips[0].Output),info,720,0.25,1.25);log.AppendLine("PASS: saved project reopens against renamed identical source and exports a verified clip with restored range, crop and size.");
            foreach(int width in new[]{720,1080}){
                string vertical=Path.Combine(args[1],"vertical-"+width+"-"+Guid.NewGuid().ToString("N")+".mp4");
                var verticalArgs=VerticalClips.ExportArgs(source,vertical,info,230,10,width,0.25,1.25);Run(ffmpeg,verticalArgs.ToArray());VerticalClips.Verify(Probe(vertical),info,width,0.25,1.25);
                log.AppendLine("PASS: vertical "+width+" export has exact 9:16 dimensions, selected duration, H.264 and AAC LC.");
                var deniedVertical=Core.Run(ffmpeg,verticalArgs,CancellationToken.None,15,null).GetAwaiter().GetResult();if(deniedVertical.Code==0)throw new Exception("Vertical output was overwritten.");
                log.AppendLine("PASS: vertical "+width+" existing output is protected.");
            }
            string av1=Path.Combine(args[1],"av1-opus-source.mkv");Run(ffmpeg,"-hide_banner","-loglevel","error","-y","-i",source,"-c:v","libaom-av1","-cpu-used","8","-crf","35","-c:a","libopus",av1);
            byte[] av1Bytes=File.ReadAllBytes(av1);string compatible=Path.Combine(args[1],"windows-compatible-"+Guid.NewGuid().ToString("N")+".mp4");Run(ffmpeg,CleanupCore.CompatibleArgs(av1,compatible).ToArray());string compatibilityJson=Probe(compatible);CleanupCore.VerifyCompatible(compatibilityJson,CleanupCore.ReadInfo(Probe(av1)));log.AppendLine("PASS: AV1/Opus source converts to full-size H.264/yuv420p and AAC LC with verified duration.");
            var denyCompatible=Core.Run(ffmpeg,CleanupCore.CompatibleArgs(av1,compatible),CancellationToken.None,15,null).GetAwaiter().GetResult();if(denyCompatible.Code==0||!av1Bytes.SequenceEqual(File.ReadAllBytes(av1)))throw new Exception("Compatibility export changed an existing file.");log.AppendLine("PASS: compatible export protects existing output and original source bytes.");
            bool rejected=false;try{CleanupCore.VerifyCompatible(compatibilityJson.Replace("\"codec_name\": \"h264\"","\"codec_name\": \"av1\""),info);}catch{rejected=true;}if(!rejected)throw new Exception("Verifier accepted the wrong codec.");log.AppendLine("PASS: compatibility verification rejects wrong video codec.");
            foreach (string mode in new[] { "blend", "blur", "crop" }) {
                string output = Path.Combine(args[1], mode + "-" + Guid.NewGuid().ToString("N") + ".mp4");
                string filter = mode == "crop" ? CleanupCore.Filter(mode, 0, 0, 240, 180, 320, 180) : CleanupCore.Filter(mode, 258, 18, 44, 24, 320, 180);
                Run(ffmpeg, CleanupCore.ExportArgs(source, output, filter, true).ToArray());
                CleanupCore.Verify(Probe(output), info, mode == "crop" ? 240 : 320, 180);
                log.AppendLine("PASS: " + mode + " export dimensions, audio and duration.");
                Preview(source,Path.Combine(args[1],mode+"-preview.png"),filter,mode=="crop"?720:960,540);
                log.AppendLine("PASS: " + mode + " preview generation and dimensions.");
                var denied = Core.Run(ffmpeg, CleanupCore.ExportArgs(source, output, filter, true), CancellationToken.None, 15, null).GetAwaiter().GetResult();
                if (denied.Code == 0) throw new Exception("Existing output was overwritten.");
                log.AppendLine("PASS: existing " + mode + " output is protected.");
            }
            if (!original.SequenceEqual(File.ReadAllBytes(source))) throw new Exception("Source file was changed.");
            log.AppendLine("PASS: source bytes unchanged.");
            string tiny = Path.Combine(args[1], "tiny-"+Guid.NewGuid().ToString("N")+".mp4");
            Run(ffmpeg, CleanupCore.ExportArgs(source,tiny,CleanupCore.Filter("blur",0,0,8,8,320,180),true).ToArray());
            CleanupCore.Verify(Probe(tiny),info,320,180);log.AppendLine("PASS: small edge blur region.");
            string rotated=Path.Combine(args[1],"rotated.mp4");Run(ffmpeg,"-hide_banner","-loglevel","error","-y","-display_rotation:v:0","90","-i",source,"-c","copy",rotated);
            var rotationInfo=CleanupCore.ReadInfo(Probe(rotated));string rotatedOut=Path.Combine(args[1],"rotated-clean-"+Guid.NewGuid().ToString("N")+".mp4");Run(ffmpeg,CleanupCore.ExportArgs(rotated,rotatedOut,CleanupCore.Filter("blur",10,10,40,20,rotationInfo.Width,rotationInfo.Height),true).ToArray());CleanupCore.Verify(Probe(rotatedOut),rotationInfo,180,320);log.AppendLine("PASS: rotation-aware export.");
            string silent=Path.Combine(args[1],"silent.mp4");Run(ffmpeg,"-hide_banner","-loglevel","error","-y","-i",source,"-an","-c:v","copy",silent);
            string silentOut=Path.Combine(args[1],"silent-clean-"+Guid.NewGuid().ToString("N")+".mp4");Run(ffmpeg,CleanupCore.ExportArgs(silent,silentOut,"crop=240:180:0:0",false).ToArray());var silentInfo=CleanupCore.ReadInfo(Probe(silentOut));if(silentInfo.Audio)throw new Exception("Silent export unexpectedly contains audio.");CleanupCore.Verify(Probe(silentOut),CleanupCore.ReadInfo(Probe(silent)),240,180);log.AppendLine("PASS: silent-video export.");
            string silentCompatible=Path.Combine(args[1],"silent-compatible-"+Guid.NewGuid().ToString("N")+".mp4");Run(ffmpeg,CleanupCore.CompatibleArgs(silent,silentCompatible).ToArray());CleanupCore.VerifyCompatible(Probe(silentCompatible),CleanupCore.ReadInfo(Probe(silent)));log.AppendLine("PASS: compatibility export supports silent video.");
            string silentVertical=Path.Combine(args[1],"silent-vertical-"+Guid.NewGuid().ToString("N")+".mp4");var silentSource=CleanupCore.ReadInfo(Probe(silent));Run(ffmpeg,VerticalClips.ExportArgs(silent,silentVertical,silentSource,0,0,720,0.25,1.25).ToArray());VerticalClips.Verify(Probe(silentVertical),silentSource,720,0.25,1.25);log.AppendLine("PASS: vertical export supports silent video.");
            string cover=Path.Combine(args[1],"cover.png");Run(ffmpeg,"-hide_banner","-loglevel","error","-y","-f","lavfi","-i","color=red:size=600x600","-frames:v","1",cover);
            string covered=Path.Combine(args[1],"covered.mp4");Run(ffmpeg,"-hide_banner","-loglevel","error","-y","-i",source,"-i",cover,"-map","1:v","-map","0:v","-map","0:a","-c","copy","-disposition:v:0","attached_pic",covered);
            string coveredJson=Probe(covered);if(!coveredJson.Contains("\"attached_pic\": 1"))throw new Exception("Fixture lacks attached cover art.");var coveredInfo=CleanupCore.ReadInfo(coveredJson);if(coveredInfo.Width!=320||coveredInfo.Height!=180)throw new Exception("Cover dimensions were selected.");
            string coveredOut=Path.Combine(args[1],"covered-clean-"+Guid.NewGuid().ToString("N")+".mp4");Run(ffmpeg,CleanupCore.ExportArgs(covered,coveredOut,"crop=240:180:0:0",true).ToArray());CleanupCore.Verify(Probe(coveredOut),coveredInfo,240,180);Preview(covered,Path.Combine(args[1],"covered-preview.png"),null,960,540);log.AppendLine("PASS: attached cover art excluded from probe, preview and export.");
            string multi=Path.Combine(args[1],"multiple-video.mkv");Run(ffmpeg,"-hide_banner","-loglevel","error","-y","-i",source,"-f","lavfi","-i","color=red:size=640x480:duration=1.5","-map","0:v","-map","1:v","-map","0:a","-c:v","libx264","-c:a","copy",multi);Preview(multi,Path.Combine(args[1],"multiple-preview.png"),null,960,540);log.AppendLine("PASS: preview uses first video rather than largest video stream.");
            string anamorphic=Path.Combine(args[1],"anamorphic.mp4");Run(ffmpeg,"-hide_banner","-loglevel","error","-y","-i",source,"-vf","setsar=9/8","-c:v","libx264","-c:a","copy",anamorphic);Preview(anamorphic,Path.Combine(args[1],"anamorphic-preview.png"),null,960,480);log.AppendLine("PASS: preview preserves non-square-pixel display proportions.");
            Run(ffmpeg,"-hide_banner","-loglevel","error","-y","-i",source,"-frames:v","1",Path.Combine(args[1],"preview.png"));
            File.WriteAllText(args[2], log.ToString()); return 0;
        } catch (Exception ex) { log.AppendLine(ex.ToString()); File.WriteAllText(args[2], log.ToString()); return 1; }
    }
    static void BatchChecks(string source,VideoInfo info,string root,StringBuilder log){
        string folder=Path.Combine(root,"batch-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var a=new NamedClip{Name="Opening",Start=0.25,End=1.25,Width=720};var b=new NamedClip{Name="Answer",Start=0,End=1,X=230,Y=20,Width=1080};var clips=new[]{a,b};
        using(var stop=new CancellationTokenSource()){
            bool cancelled=false;try{ClipBatch.Export(ffmpeg,ffprobe,source,info,clips,folder,stop.Token,(index,value)=>{if(index==0&&value==100)stop.Cancel();}).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
            if(!cancelled||a.State!="Complete"||b.State!="Ready"||File.Exists(Path.Combine(folder,"Answer.mp4")))throw new Exception("Batch cancellation did not preserve pending state.");
        }
        log.AppendLine("PASS: cancellation after first clip keeps its verified output and leaves remaining clip pending.");
        byte[] first=File.ReadAllBytes(a.Output);ClipBatch.Export(ffmpeg,ffprobe,source,info,clips,folder,CancellationToken.None,null).GetAwaiter().GetResult();
        if(!first.SequenceEqual(File.ReadAllBytes(a.Output))||b.State!="Complete")throw new Exception("Retry changed completed output.");
        VerticalClips.Verify(Probe(a.Output),info,720,0.25,1.25);VerticalClips.Verify(Probe(b.Output),info,1080,0,1);log.AppendLine("PASS: retry skips completed clip and exports independent crop, range and size for pending clip.");
        var c=new NamedClip{Name="New",Start=0,End=1,Width=720};var collision=new NamedClip{Name="Answer",Start=0,End=1,Width=720};bool failed=false;
        try{ClipBatch.Export(ffmpeg,ffprobe,source,info,new[]{c,collision},folder,CancellationToken.None,null).GetAwaiter().GetResult();}catch{failed=true;}
        if(!failed||File.Exists(Path.Combine(folder,"New.mp4")))throw new Exception("Batch preflight wrote a partial batch.");log.AppendLine("PASS: later filename collision prevents every pending export from starting.");
        using(var stop=new CancellationTokenSource()){
            bool cancelled=false;try{ClipBatch.Export(ffmpeg,ffprobe,source,info,new[]{c},folder,stop.Token,(index,value)=>stop.Cancel()).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
            if(!cancelled||c.State!="Cancelled"||Directory.GetFiles(folder,"*.partial-*").Length!=0||File.Exists(Path.Combine(folder,"New.mp4")))throw new Exception("Active clip cancellation left output.");
        }log.AppendLine("PASS: active clip cancellation leaves no final or temporary output.");
        failed=false;try{ClipBatch.Export(Path.Combine(folder,"missing-ffmpeg.exe"),ffprobe,source,info,new[]{c},folder,CancellationToken.None,null).GetAwaiter().GetResult();}catch{failed=true;}
        if(!failed||c.State!="Failed"||Directory.GetFiles(folder,"*.partial-*").Length!=0)throw new Exception("Failed process state or cleanup incorrect.");log.AppendLine("PASS: process failure marks clip failed and removes temporary output.");
        ClipBatch.Export(ffmpeg,ffprobe,source,info,new[]{c},folder,CancellationToken.None,null).GetAwaiter().GetResult();if(c.State!="Complete")throw new Exception("Failed clip could not retry.");log.AppendLine("PASS: failed clip retries successfully.");
        var race=new NamedClip{Name="Race",Start=0,End=1,Width=720};string raced=Path.Combine(folder,"Race.mp4");failed=false;
        try{ClipBatch.Export(ffmpeg,ffprobe,source,info,new[]{race},folder,CancellationToken.None,(index,value)=>{if(value==0&&!File.Exists(raced))File.WriteAllText(raced,"external file");}).GetAwaiter().GetResult();}catch{failed=true;}
        if(!failed||File.ReadAllText(raced)!="external file"||Directory.GetFiles(folder,"*.partial-*").Length!=0)throw new Exception("Concurrent output collision was not protected.");log.AppendLine("PASS: file created after preflight is never overwritten; temporary export is removed.");
        var changed=new VideoInfo{Width=640,Height=180,Duration=info.Duration,Audio=info.Audio};failed=false;try{ClipBatch.Export(ffmpeg,ffprobe,source,changed,new[]{race},folder,CancellationToken.None,null).GetAwaiter().GetResult();}catch(Exception ex){failed=ex.Message.Contains("source video changed");}if(!failed)throw new Exception("Changed source accepted.");log.AppendLine("PASS: changed source metadata stops batch before export.");
    }
}
