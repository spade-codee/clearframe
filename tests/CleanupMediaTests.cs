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
    static int Main(string[] args) {
        var log = new StringBuilder();
        try {
            ffmpeg = Path.Combine(args[0], "ffmpeg.exe"); ffprobe = Path.Combine(args[0], "ffprobe.exe");
            Directory.CreateDirectory(args[1]); string source = Path.Combine(args[1], "synthetic source.mp4");
            Run(ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=24:duration=1.5,drawbox=x=260:y=20:w=40:h=20:color=white:t=fill", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=1.5", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", source);
            byte[] original = File.ReadAllBytes(source); var info = CleanupCore.ReadInfo(Probe(source));
            foreach (string mode in new[] { "blend", "blur", "crop" }) {
                string output = Path.Combine(args[1], mode + "-" + Guid.NewGuid().ToString("N") + ".mp4");
                string filter = mode == "crop" ? CleanupCore.Filter(mode, 0, 0, 240, 180, 320, 180) : CleanupCore.Filter(mode, 258, 18, 44, 24, 320, 180);
                Run(ffmpeg, CleanupCore.ExportArgs(source, output, filter, true).ToArray());
                CleanupCore.Verify(Probe(output), info, mode == "crop" ? 240 : 320, 180);
                log.AppendLine("PASS: " + mode + " export dimensions, audio and duration.");
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
            Run(ffmpeg,"-hide_banner","-loglevel","error","-y","-i",source,"-frames:v","1",Path.Combine(args[1],"preview.png"));
            File.WriteAllText(args[2], log.ToString()); return 0;
        } catch (Exception ex) { log.AppendLine(ex.ToString()); File.WriteAllText(args[2], log.ToString()); return 1; }
    }
}
