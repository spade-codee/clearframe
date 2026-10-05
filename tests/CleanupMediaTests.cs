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
}
