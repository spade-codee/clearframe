using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClearFrame {
    public static class AudioTracks {
        public static string Normalize(string code){code=(code??"").Trim().ToLowerInvariant();if(code.Length>40||code.Length>0&&!Regex.IsMatch(code,@"^[a-z]{2,3}(?:-[a-z0-9]{2,8})*$"))throw new Exception("Unsupported audio-language code.");return code;}
        public static List<string> Available(IDictionary<string,object> metadata){
            var codes=new List<string>();foreach(var format in Core.Entries(metadata,"formats")){
                if(Core.S(format,"has_drm")=="True"||Core.S(format,"acodec")==""||Core.S(format,"acodec")=="none")continue;
                try{string code=Normalize(Core.S(format,"language"));if(code.Length>0&&!codes.Contains(code))codes.Add(code);}catch{}
            }return codes.OrderBy(Label,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        public static string Label(string code){if(string.IsNullOrEmpty(code))return "Automatic (source preference)";string name=code;bool descriptive=code.EndsWith("-desc",StringComparison.OrdinalIgnoreCase);try{name=CultureInfo.GetCultureInfo(descriptive?code.Substring(0,code.Length-5):code).EnglishName;}catch(CultureNotFoundException){}return name+(descriptive?" · audio description":"")+" ["+code+"]";}
    }
    public partial class MainWindow {
        string chosenAudioLanguage="";
        void SetupAudioTracks(){C<Button>("AudioLanguageButton").Click+=(s,e)=>AudioLanguageDialog();}
        void ResetAudioLanguage(){chosenAudioLanguage="";C<Button>("AudioLanguageButton").Content="Audio language…";}
        void AudioLanguageDialog(){if(preview==null){Status("Check a video link to see its available audio languages.");return;}BuildAudioLanguageDialog().ShowDialog();}
        Window BuildAudioLanguageDialog(){
            var dialog=new Window{Title="ClearFrame · Audio language",Width=550,Height=330,ResizeMode=ResizeMode.NoResize,Owner=w.IsVisible?w:null,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=w.Background,Foreground=w.Foreground,Resources=w.Resources,FontFamily=w.FontFamily};
            var panel=new StackPanel{Margin=new Thickness(24)};panel.Children.Add(new TextBlock{Text="Choose this video's spoken language.",FontSize=21,FontWeight=FontWeights.SemiBold});
            var codes=AudioTracks.Available(preview);codes.Insert(0,"");var choice=new ComboBox{Margin=new Thickness(0,18,0,16)};foreach(string code in codes)choice.Items.Add(AudioTracks.Label(code));choice.SelectedIndex=Math.Max(0,codes.IndexOf(chosenAudioLanguage));panel.Children.Add(choice);
            var note=new TextBlock{Text=(codes.Count==1?"This video has no labeled audio languages in its available metadata. Automatic selection is available.\n\n":"")+"Uses an existing audio track; does not translate or dub. Your format must support a track in that language. Applies to this checked link, including audio-only downloads; batches and playlists use Automatic.",Margin=new Thickness(0,0,0,16)};panel.Children.Add(note);
            var apply=new Button{Content="Use audio language",Style=(Style)w.FindResource("Primary"),HorizontalAlignment=HorizontalAlignment.Left};panel.Children.Add(apply);apply.Click+=(s,e)=>{chosenAudioLanguage=codes[choice.SelectedIndex];C<Button>("AudioLanguageButton").Content=chosenAudioLanguage.Length==0?"Audio language…":"Audio: "+chosenAudioLanguage;RefreshPreview();dialog.Close();};dialog.Content=panel;return dialog;
        }
        public void RenderAudioLanguages(string path){
            preview=new Dictionary<string,object>{{"formats",new[]{new Dictionary<string,object>{{"acodec","aac"},{"language","en"}},new Dictionary<string,object>{{"acodec","aac"},{"language","fr"}}}}};chosenAudioLanguage="fr";
            var dialog=BuildAudioLanguageDialog();var root=(StackPanel)dialog.Content;root.Margin=new Thickness(0);root.Width=502;root.Height=262;root.Measure(new Size(502,262));root.Arrange(new Rect(0,0,502,262));root.UpdateLayout();var bitmap=new RenderTargetBitmap(550,310,96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(dialog.Background,null,new Rect(0,0,550,310));dc.DrawRectangle(new VisualBrush(root){AutoLayoutContent=false,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,502,262)},null,new Rect(24,24,502,262));}bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
        }
        public void CheckAudioTracks(string report){
            preferencesReady=false;int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};Action<Action,string> reject=(action,name)=>{bool failed=false;try{action();}catch{failed=true;}check(failed,name);};
            Func<string,string,string,string,Dictionary<string,object>> format=(id,videoCodec,audioCodec,language)=>new Dictionary<string,object>{{"format_id",id},{"vcodec",videoCodec},{"acodec",audioCodec},{"language",language},{"width",1920},{"height",1080},{"fps",30}};
            var video=format("v","avc1","none","");var en=format("en","none","mp4a","en");var fr=format("fr","none","mp4a","fr");var es=format("es","none","opus","es");var mux=format("mux","avc1","mp4a","en");var metadata=new Dictionary<string,object>{{"formats",new[]{mux,video,en,fr,es}}};
            check(AudioTracks.Available(metadata).SequenceEqual(new[]{"en","fr","es"}),"only actual labeled audio languages offered");
            check(Core.Select(metadata,1080,true,"mp4","fr").Selector=="v+fr","French separate audio paired with silent video");
            check(Core.Select(metadata,1080,true,"mp4","en").Selector=="v+en","English selection");
            check(Core.Select(metadata,0,true,"mp3","es").Selector=="es","MP3 chooses requested language");
            check(Core.Select(metadata,0,true,"m4a","es").Selector=="es","M4A conversion retains requested language");
            reject(()=>Core.Select(metadata,1080,true,"mp4","es"),"incompatible codec never falls back to another language");
            reject(()=>Core.Select(metadata,1080,true,"mp4","de"),"missing language fails explicitly");
            reject(()=>AudioTracks.Normalize("en,--exec"),"unsafe code rejected");check(AudioTracks.Normalize("PT-BR")=="pt-br","regional codes normalized");
            var progressive=new Dictionary<string,object>{{"formats",new[]{mux}}};check(Core.Select(progressive,1080,true,"mp4","en").Selector=="mux","matching progressive audio allowed");reject(()=>Core.Select(progressive,1080,true,"mp4","fr"),"wrong-language progressive audio rejected");
            var unknown=new Dictionary<string,object>{{"formats",new[]{format("unknown","none","opus","")}}};check(AudioTracks.Available(unknown).Count==0&&Core.Select(unknown,0,true,"mp3").Selector=="unknown","unlabeled legacy audio uses automatic");
            var drm=format("drm","none","opus","de");drm["has_drm"]=true;var malformed=format("malformed","none","opus","bad/code");check(AudioTracks.Available(new Dictionary<string,object>{{"formats",new[]{drm,malformed,fr}}}).SequenceEqual(new[]{"fr"}),"DRM and malformed languages excluded");
            var regional=new Dictionary<string,object>{{"formats",new[]{format("br","none","opus","pt-BR"),format("pt","none","opus","pt")}}};check(Core.Select(regional,0,true,"mp3","PT-BR").Selector=="br","exact regional audio selected");check(AudioTracks.Label("en-desc").Contains("audio description"),"descriptive track labeled");
            var job=new Job{Url="https://www.youtube.com/watch?v=abcdefghijk",Profile="mp3",Container="mp3",Folder=folder,Status="Queued",AudioLanguage="fr"};check(Core.Json.Deserialize<Job>(Core.Json.Serialize(job)).AudioLanguage=="fr","job language survives serialization");check(Core.Json.Deserialize<Job>("{}").AudioLanguage==null,"legacy jobs default to automatic");jobs.Add(job);check(Duplicate(job.Url,"mp3",folder,0,0,0,"fr")&&!Duplicate(job.Url,"mp3",folder,0),"duplicate detection distinguishes language from automatic");chosenAudioLanguage="fr";ResetAudioLanguage();check(chosenAudioLanguage=="","changing link resets language");
            File.WriteAllText(report,count+" audio-language checks passed using metadata fixtures. No live multi-language download was used.");
        }
    }
}
