using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClearFrame {
    public partial class MainWindow {
        public async Task CheckV04(string report) {
            preferencesReady=false;int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};
            check(PlaylistCore.Canonical("https://www.youtube.com/watch?v=abcdefghijk&list=PL1234567890&t=3")=="https://www.youtube.com/playlist?list=PL1234567890","playlist URL normalization");
            foreach(var bad in new[]{"https://youtube.com.evil.test/playlist?list=PL1234567890","file:///playlist?list=PL1234567890","https://youtube.com:999/playlist?list=PL1234567890","https://youtube.com/playlist?list=bad%26id","https://youtube.com/watch?v=abcdefghijk"}){bool rejected=false;try{PlaylistCore.Canonical(bad);}catch{rejected=true;}check(rejected,"invalid playlist link");}
            string json="{\"title\":\"Sample\",\"entries\":[{\"id\":\"abcdefghijk\",\"title\":\"First\",\"duration\":61},{\"id\":\"abcdefghijk\",\"title\":\"Duplicate\"},{\"id\":\"lmnopqrstuv\",\"title\":\"[Private video]\"},{\"id\":\"12345678901\",\"live_status\":\"is_live\"},{\"id\":\"invalid\"}]}";
            var playlist=PlaylistCore.Parse(json);check(playlist.Items.Count==3&&playlist.Items.Count(i=>i.Available)==1,"unavailable entries and duplicate IDs");
            playlist.Items[1].Selected=true;check(!playlist.Items[1].Selected,"unavailable item cannot be selected");
            string entries=string.Join(",",Enumerable.Range(0,201).Select(i=>"{\"id\":\""+i.ToString("00000000000")+"\"}"));var limited=PlaylistCore.Parse("{\"entries\":["+entries+"]}");check(limited.Limited&&limited.Items.Count==200,"playlist cap exposed");
            AddPlaylist(playlist.Items);check(jobs.Count==1&&jobs[0].Title=="First"&&jobs[0].TargetResolution==1080,"enqueue available playlist picks with current settings");AddPlaylist(playlist.Items);check(jobs.Count==1,"playlist picks deduplicated against queue");jobs.Clear();
            var a=new Job{Id="a",Status="Queued"};var b=new Job{Id="b",Status="Queued"};jobs.Add(a);jobs.Add(b);var visited=new List<string>();
            await RunQueue(async(j,t)=>{j.Status="Downloading";visited.Add(j.Id);C<CheckBox>("PauseAfterBox").IsChecked=true;await Task.Delay(15);j.Status="Complete";},CancellationToken.None);
            check(a.Status=="Complete"&&b.Status=="Queued"&&visited.SequenceEqual(new[]{"a"}),"pause finishes current and leaves following item queued");
            C<CheckBox>("PauseAfterBox").IsChecked=false;await RunQueue((j,t)=>{visited.Add(j.Id);j.Status="Complete";return Task.FromResult(0);},CancellationToken.None);check(visited.SequenceEqual(new[]{"a","b"}),"resume only processes remaining queue");
            a.Status=b.Status="Queued";visited.Clear();using(var stop=new CancellationTokenSource()){await RunQueue((j,t)=>{visited.Add(j.Id);stop.Cancel();j.Status="Stopped";return Task.FromResult(0);},stop.Token);}check(a.Status=="Stopped"&&b.Status=="Queued"&&visited.Count==1,"stop does not start next item");
            a.Status=b.Status="Queued";C<CheckBox>("PauseAfterBox").IsChecked=true;await RunQueue((j,t)=>{j.Status="Failed";return Task.FromResult(0);},CancellationToken.None);check(a.Status=="Failed"&&b.Status=="Queued","pause also stops after failed attempt");
            C<CheckBox>("PauseAfterBox").IsChecked=false;a.Status="Queued";await RunQueue((j,t)=>{C<CheckBox>("PauseAfterBox").IsChecked=true;C<CheckBox>("PauseAfterBox").IsChecked=false;j.Status="Complete";return Task.FromResult(0);},CancellationToken.None);check(a.Status=="Complete"&&b.Status=="Complete","cleared pause continues queue");
            a.Status="Downloading";a.Progress=42;a.Duration=125;a.Resolution=2160;a.Container="mp4";a.EstimatedBytes=52428800;
            check(a.CardSummary.Contains("02:05")&&a.CardSummary.Contains("~50 MB")&&a.ProgressLabel=="42% of stream","card duration size and honest stream progress");
            a.Thumbnail=DemoThumbnail(false);string saved=Core.Json.Serialize(a);check(!saved.Contains("Thumbnail")&&!saved.Contains("ProgressLabel"),"runtime card data excluded from history");
            check(Job.TimeLabel(90061)=="25:01:01"&&Job.TimeLabel(double.NaN)=="Duration unknown","duration display handles long and unknown media");
            var picker=new PlaylistPicker(w,(link,token)=>Task.FromResult(playlist),p=>{});await picker.CheckOffline();count+=4;
            var cancelledPicker=new PlaylistPicker(w,async(link,token)=>{await Task.Delay(10000,token);return playlist;},p=>{});await cancelledPicker.CheckCancellation();count++;
            jobs.Clear();File.WriteAllText(report,count+" v0.4 playlist, queue and card checks passed. No desktop window or network request was opened.");
        }
        public async Task CheckLivePlaylist(string url,string report){var result=await FetchPlaylist(url,CancellationToken.None);if(result.Items.Count==0||result.Items.Count>PlaylistCore.Limit)throw new Exception("Live playlist returned no usable entries or exceeded the cap.");var first=result.Items.First(i=>i.Available);var image=await thumbnails.Get(first.Id);if(image==null)throw new Exception("Live thumbnail fetch failed.");File.WriteAllText(report,"PASS: live playlist metadata and application parsing: "+result.Title+" · "+result.Items.Count+" entries. Thumbnail decoded: "+image.PixelWidth+" × "+image.PixelHeight+". No video or audio downloaded.");thumbnails.Stop();}
        public void CheckRestart(string phase) {
            string id="0123456789abcdef0123456789abcdef";
            if(phase=="write") {
                if(jobs.Count!=0)throw new Exception("Restart fixture must be empty.");
                folder=Path.Combine(dataDir,"downloads");string stage=Path.Combine(folder,".clearframe",id);Directory.CreateDirectory(stage);File.WriteAllText(Path.Combine(stage,"source.part"),"partial fixture bytes");
                jobs.Add(new Job{Id=id,Status="Downloading",Url="https://www.youtube.com/watch?v=abcdefghijk",VideoId="abcdefghijk",Profile="mp4",Container="mp4",TargetResolution=1080,Folder=folder});jobs.Add(new Job{Id="1123456789abcdef0123456789abcdef",Status="Queued",Profile="mp4",Folder=folder});Save();if(persistenceError!=null)throw new Exception(persistenceError);
            } else if(phase=="read") {
                if(jobs.Count!=2||jobs[0].Status!="Stopped"||jobs[1].Status!="Queued"||running||active!=null)throw new Exception("Restart state mismatch.");
                if(File.ReadAllText(Path.Combine(jobs[0].Folder,".clearframe",jobs[0].Id,"source.part"))!="partial fixture bytes")throw new Exception("Partial staging changed across restart.");
                C<ListView>("QueueList").SelectedItem=jobs[0];C<Button>("RetryButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(jobs[0].Status!="Queued"||jobs[0].Id!=id)throw new Exception("Retry lost stable staging identity.");
                File.WriteAllText(Path.Combine(dataDir,"restart-checks.txt"),"PASS: separate app processes restore interrupted jobs stopped, retain queued jobs and partial bytes, and retry with the same staging ID. No desktop window or live download was used.");
            } else throw new Exception("Unknown restart check phase.");
        }
        static BitmapSource DemoThumbnail(bool warm) {
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(new LinearGradientBrush(warm?Color.FromRgb(105,61,70):Color.FromRgb(24,70,91),warm?Color.FromRgb(246,186,116):Color.FromRgb(87,167,171),90),null,new Rect(0,0,224,126));dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(244,224,164)),null,new Point(169,35),18,18);dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(29,67,61)),null,Geometry.Parse("M0,110 L62,43 L119,104 L175,64 L224,106 L224,126 L0,126 Z"));}var bitmap=new RenderTargetBitmap(224,126,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
        }
        public void DemoLibrary(){preferencesReady=false;jobs.Add(new Job{Title="North coast · Sample travel film",Profile="mp4",Container="mp4",Resolution=2160,Duration=372,EstimatedBytes=483183820,Status="Downloading",Progress=64,Detail="2160p · stream 64% · 5.2 MiB/s · 00:34 remaining",Thumbnail=DemoThumbnail(false)});jobs.Add(new Job{Title="Evening light · Sample short film",Profile="mp4",Container="mp4",Resolution=1080,Duration=126,EstimatedBytes=83886080,Status="Complete",Progress=100,Detail="Verified 1080p + audio · 80 MB",Thumbnail=DemoThumbnail(true)});UpdateCount();C<CheckBox>("PauseAfterBox").IsEnabled=true;Status("Demo library · Sample items shown for the interface preview.");}
    }
}
