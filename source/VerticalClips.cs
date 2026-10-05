using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ClearFrame {
    public static class VerticalClips {
        public static int[] CropSize(VideoInfo source){
            if(Math.Abs(source.PixelAspect-1)>0.001)throw new Exception("Vertical clips require a square-pixel source in this version.");
            int unit=Math.Min(source.Width/18,source.Height/32);if(unit<1)throw new Exception("This source is too small for a vertical crop.");
            return new[]{18*unit,32*unit};
        }
        public static int Height(int width){if(width!=720&&width!=1080)throw new Exception("Choose 720×1280 or 1080×1920.");return width/9*16;}
        public static string Filter(VideoInfo source,int x,int y,int width){
            var crop=CropSize(source);if(x<0||y<0||x%2!=0||y%2!=0||(long)x+crop[0]>source.Width||(long)y+crop[1]>source.Height)throw new Exception("Keep the vertical crop inside the picture using even X/Y positions.");
            return string.Format(CultureInfo.InvariantCulture,"crop={0}:{1}:{2}:{3},scale={4}:{5}:flags=lanczos,setsar=1",crop[0],crop[1],x,y,width,Height(width));
        }
        public static List<string> ExportArgs(string input,string output,VideoInfo source,int x,int y,int width,double start,double end){
            DownloadOptions.ValidateClip(start,end,source.Duration);var args=CleanupCore.CompatibleArgs(input,output);args[args.IndexOf("-vf")+1]=Filter(source,x,y,width);
            args.InsertRange(args.IndexOf("-i"),new[]{"-ss",start.ToString(CultureInfo.InvariantCulture)});args.InsertRange(args.Count-1,new[]{"-t",(end-start).ToString(CultureInfo.InvariantCulture)});return args;
        }
        public static void Verify(string json,VideoInfo source,int width,double start,double end){
            var expected=new VideoInfo{Width=width,Height=Height(width),Duration=end-start,Audio=source.Audio};CleanupCore.VerifyCompatible(json,expected);
            var actual=CleanupCore.ReadInfo(json);if(Math.Abs(actual.Duration-expected.Duration)>0.35||Math.Abs(actual.PixelAspect-1)>0.001)throw new Exception("The vertical clip failed duration or pixel-aspect verification.");
        }
        public static void Check(string report){
            int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};Action<Action,string> reject=(action,name)=>{bool failed=false;try{action();}catch{failed=true;}check(failed,name);};
            var landscape=new VideoInfo{Width=1920,Height=1080,Duration=60,Audio=true};var crop=CropSize(landscape);check(crop[0]==594&&crop[1]==1056,"largest even 9:16 landscape crop");
            var portrait=new VideoInfo{Width=1080,Height=1920,Duration=60};crop=CropSize(portrait);check(crop[0]==1080&&crop[1]==1920,"portrait source retains whole frame");
            check(Height(720)==1280&&Height(1080)==1920,"platform-size presets");check(Filter(landscape,1326,24,720).StartsWith("crop=594:1056:1326:24"),"crop can reach far edge");
            reject(()=>Filter(landscape,-2,0,720),"negative crop");reject(()=>Filter(landscape,1,0,720),"odd crop");reject(()=>Filter(landscape,1328,0,720),"out-of-frame crop");reject(()=>Filter(landscape,0,0,800),"unsupported export size");reject(()=>CropSize(new VideoInfo{Width=16,Height=16}),"tiny source");reject(()=>CropSize(new VideoInfo{Width=320,Height=180,PixelAspect=1.2}),"non-square pixels");
            var args=ExportArgs("source.mkv","clip.mp4",landscape,0,0,1080,1.25,4.25);check(args[args.IndexOf("-ss")+1]=="1.25"&&args[args.IndexOf("-t")+1]=="3","accurate clip range");check(args.Contains("0:a:0?")&&args.Contains("-n")&&args.Contains("aac_low"),"silent source and no-overwrite compatibility options");
            reject(()=>ExportArgs("in","out",landscape,0,0,720,2,2.2),"sub-second range");reject(()=>ExportArgs("in","out",landscape,0,0,720,2,70),"range past source");File.WriteAllText(report,count+" vertical-clip checks passed.");
        }
    }
}
