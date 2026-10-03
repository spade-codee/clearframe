using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ClearFrame {
    public static class DownloadOptions {
        public static readonly string[] LanguageCodes={"en","es","fr","de","pt","ar","hi","ja","ko","zh","it","ru","tr","id","yo"};
        public static readonly string[] LanguageNames={"English","Spanish","French","German","Portuguese","Arabic","Hindi","Japanese","Korean","Chinese","Italian","Russian","Turkish","Indonesian","Yoruba"};
        public static string Language(string code){code=string.IsNullOrEmpty(code)?"en":code;if(!LanguageCodes.Contains(code))throw new Exception("Choose a supported subtitle language.");return code;}
        public static string LanguagePattern(string code){return Language(code)+"(?:-.*)?,-live_chat";}
        public static double ParseTime(string value){
            value=(value??"").Trim();var parts=value.Split(':');double result=0;
            if(parts.Length>3||parts.Length==0)throw new Exception("Enter seconds, mm:ss or hh:mm:ss.");
            for(int i=0;i<parts.Length;i++){double part;if(!Regex.IsMatch(parts[i],i==parts.Length-1?@"^\d+(\.\d{1,3})?$":@"^\d+$")||!double.TryParse(parts[i],NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out part)||(i>0&&part>=60))throw new Exception("Enter seconds, mm:ss or hh:mm:ss with seconds/minutes below 60.");result=result*60+part;}
            if(double.IsNaN(result)||double.IsInfinity(result)||result>604800)throw new Exception("Time must be within seven days.");return result;
        }
        public static void ValidateClip(double start,double end,double duration){
            if(double.IsNaN(start)||double.IsNaN(end)||double.IsInfinity(start)||double.IsInfinity(end)||start<0||end-start<1||end>604800)throw new Exception("A clip must start at or after zero and be at least one second long.");
            if(double.IsNaN(duration)||double.IsInfinity(duration)||duration<=0||end>duration+0.05)throw new Exception("The clip must end within the video's known duration.");
        }
        public static List<string> ClipArgs(string input,string output,Job job){
            ValidateClip(job.ClipStart,job.ClipEnd,job.Duration);
            var args=new List<string>{"-hide_banner","-loglevel","error","-nostdin","-n","-ss",job.ClipStart.ToString(CultureInfo.InvariantCulture),"-i",input,"-t",(job.ClipEnd-job.ClipStart).ToString(CultureInfo.InvariantCulture)};
            if(Core.IsAudio(job.Profile)){
                args.AddRange(new[]{"-map","0:a:0","-vn","-c:a",job.Profile=="mp3"?"libmp3lame":"aac"});
                args.AddRange(job.Profile=="mp3"?new[]{"-q:a","0"}:new[]{"-b:a","192k"});
            }else{
                if(!new[]{"mp4","compatible","mkv","mov","webm"}.Contains(job.Profile))throw new Exception("Unsupported clip format.");
                args.AddRange(new[]{"-map","0:V:0","-map","0:a:0","-c:v",job.Profile=="webm"?"libvpx-vp9":"libx264"});
                args.AddRange(job.Profile=="webm"?new[]{"-crf","30","-b:v","0"}:new[]{"-preset","medium","-crf","18"});
                args.AddRange(new[]{"-pix_fmt","yuv420p","-c:a",job.Profile=="webm"?"libopus":"aac","-b:a","192k"});
            }
            args.AddRange(new[]{"-map_metadata","-1","-map_chapters","-1"});if(new[]{"mp4","mov","m4a"}.Contains(job.Container))args.AddRange(new[]{"-movflags","+faststart"});args.Add(output);return args;
        }
        public static void VerifyClip(string json,Job job){
            var data=Core.Json.Deserialize<Dictionary<string,object>>(json);object raw;var format=data.TryGetValue("format",out raw)?raw as Dictionary<string,object>:null;double duration=format==null?0:Core.N(format,"duration");
            if(double.IsNaN(duration)||double.IsInfinity(duration)||duration<=0||Math.Abs(duration-(job.ClipEnd-job.ClipStart))>0.35)throw new Exception("Clip duration verification failed.");
            Core.ValidateMedia(json,job.Resolution,job.ClipEnd-job.ClipStart);
        }
        public static string ClipSubtitles(string srt,double start,double end){
            var output=new StringBuilder();int number=0;
            foreach(string block in Regex.Split(srt.Replace("\r\n","\n").Trim(),@"\n\s*\n")){
                if(string.IsNullOrWhiteSpace(block))continue;var lines=block.Split('\n');int timeLine=Array.FindIndex(lines,l=>l.Contains("-->"));
                if(timeLine<0)throw new Exception("Unsupported subtitle timing.");var match=Regex.Match(lines[timeLine],@"^(\d+:\d{2}:\d{2},\d{3})\s+-->\s+(\d+:\d{2}:\d{2},\d{3})(?:\s.*)?$");if(!match.Success)throw new Exception("Unsupported subtitle timing.");
                double from=ParseTime(match.Groups[1].Value.Replace(',','.')),to=ParseTime(match.Groups[2].Value.Replace(',','.'));from=Math.Max(start,from);to=Math.Min(end,to);if(to<=from)continue;
                output.AppendLine((++number).ToString(CultureInfo.InvariantCulture));output.AppendLine(SrtTime(from-start)+" --> "+SrtTime(to-start));foreach(var line in lines.Skip(timeLine+1))output.AppendLine(line);output.AppendLine();
            }return output.ToString();
        }
        static string SrtTime(double seconds){long milliseconds=(long)Math.Round(seconds*1000);return string.Format(CultureInfo.InvariantCulture,"{0:00}:{1:00}:{2:00},{3:000}",milliseconds/3600000,milliseconds/60000%60,milliseconds/1000%60,milliseconds%1000);}
    }
}
