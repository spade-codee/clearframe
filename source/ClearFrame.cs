using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ClearFrame {
 public class Job : INotifyPropertyChanged {
  public string Id { get; set; }
  public string Url { get; set; }
  public string Folder { get; set; }
  public string VideoId { get; set; }
  public string Selector { get; set; }
  public string Container { get; set; }
  public string Profile { get; set; }
  public int TargetResolution { get; set; }
  public bool Strict { get; set; }
  public bool Subtitles { get; set; }
  public string SubtitleLanguage { get; set; }
  public double ClipStart { get; set; }
  public double ClipEnd { get; set; }
  public long AddedUtcTicks { get; set; }
  public long SavedBytes { get; set; }
  [ScriptIgnore] public bool IsClip { get{return ClipEnd>0;} }
  [ScriptIgnore] public double OutputDuration { get{return IsClip?ClipEnd-ClipStart:Duration;} }
  [ScriptIgnore] public string ClipLabel { get{return IsClip?"Clip "+ClipStart.ToString("0.###",CultureInfo.InvariantCulture)+"–"+ClipEnd.ToString("0.###",CultureInfo.InvariantCulture)+"s":"Full video";} }
  int resolution;
  public int Resolution { get{return resolution;} set{resolution=value;Changed("Resolution");Changed("Badge");Changed("CardSummary");} }
  double duration;long estimatedBytes;
  public double Duration { get{return duration;} set{duration=value;Changed("CardSummary");} }
  public long EstimatedBytes { get{return estimatedBytes;} set{estimatedBytes=value;Changed("CardSummary");} }
  BitmapSource thumbnail;
  [ScriptIgnore] public BitmapSource Thumbnail { get{return thumbnail;} set{thumbnail=value;Changed("Thumbnail");} }
  [ScriptIgnore] public string CardSummary { get{return (Core.IsAudio(Profile)?"Audio":Badge)+" · "+(Container??"").ToUpperInvariant()+" · "+TimeLabel(OutputDuration)+(IsClip?" · "+ClipLabel:"")+(SavedBytes>0?" · "+Core.Size(SavedBytes):EstimatedBytes>0?" · ~"+Core.Size(EstimatedBytes)+(IsClip?" source":""):"");} }
  [ScriptIgnore] public string ProgressLabel { get{return Status=="Downloading"?Progress.ToString("0",CultureInfo.InvariantCulture)+"% of stream":Status=="Complete"?"Verified download":Status=="Trimming"?"Encoding clip":Status=="Finishing"?"Merging / converting":Status=="Verifying"?"Checking saved file":"";} }
  public static string TimeLabel(double seconds){if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<=0||seconds>TimeSpan.MaxValue.TotalSeconds)return "Duration unknown";var time=TimeSpan.FromSeconds(seconds);return time.TotalHours>=1?((long)time.TotalHours)+time.ToString(@"\:mm\:ss"):time.ToString(@"mm\:ss");}
  public string FilePath { get; set; }
  public string Log { get; set; }
  public string RateLimit { get; set; }
  public string Kind {get{return Core.IsAudio(Profile)?"AUDIO":"VIDEO";}}
  public string Badge {get{return Core.IsAudio(Profile)?(Container??"").ToUpperInvariant():Resolution>0?Resolution+"p":"VIDEO";}}
  string title, detail, status; double progress;
  public string Title { get { return title; } set { title=value; Changed("Title"); } }
  public string Detail { get { return detail; } set { detail=value; Changed("Detail"); } }
  public string Status { get { return status; } set { status=value; Changed("Status");Changed("ProgressLabel"); } }
  public double Progress { get { return progress; } set { progress=value; Changed("Progress");Changed("ProgressLabel"); } }
  public event PropertyChangedEventHandler PropertyChanged;
  void Changed(string name) { if(PropertyChanged!=null) PropertyChanged(this,new PropertyChangedEventArgs(name)); }
 }
 public class Result { public int Code; public string Output; public string Error; }
 public class Selection { public string Selector; public int Resolution; public double Fps; public long Bytes; public bool Approximate; public string VideoCodec; }
 public static class Core {
  public static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength=50000000, RecursionLimit=200 };
  public static string S(IDictionary<string,object> d,string key) { object v; return d.TryGetValue(key,out v)&&v!=null?Convert.ToString(v,CultureInfo.InvariantCulture):""; }
  public static double N(IDictionary<string,object> d,string key) { double v; return double.TryParse(S(d,key),NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:0; }
  public static string Canonical(string input) {
   Uri u; if(!Uri.TryCreate(input.Trim(),UriKind.Absolute,out u)||(u.Scheme!="https"&&u.Scheme!="http")||!string.IsNullOrEmpty(u.UserInfo)||!u.IsDefaultPort) throw new Exception("Paste a valid YouTube video link.");
   string h=u.Host.ToLowerInvariant(), id=null;
   if(h=="youtu.be") id=u.AbsolutePath.Trim('/');
   else if(new[]{"youtube.com","www.youtube.com","m.youtube.com","music.youtube.com"}.Contains(h)) {
    if(u.AbsolutePath=="/watch") {
     foreach(var p in u.Query.TrimStart('?').Split('&')) { var bits=p.Split(new[]{'='},2); if(bits.Length==2&&bits[0]=="v") id=Uri.UnescapeDataString(bits[1]); }
    } else { var m=Regex.Match(u.AbsolutePath,@"^/(shorts|embed|live)/([A-Za-z0-9_-]{11})/?$"); if(m.Success) id=m.Groups[2].Value; }
   }
   if(id==null||!Regex.IsMatch(id,@"^[A-Za-z0-9_-]{11}$")) throw new Exception("Use a single YouTube video or Shorts link. Playlist and channel links are not supported in this version.");
   return "https://www.youtube.com/watch?v="+id;
  }
  public static IEnumerable<Dictionary<string,object>> Entries(IDictionary<string,object> d,string key) {
   object o; if(!d.TryGetValue(key,out o)||!(o is IEnumerable)) yield break;
   foreach(object x in (IEnumerable)o) { var item=x as Dictionary<string,object>; if(item!=null) yield return item; }
  }
  public static int Resolution(IDictionary<string,object> f) { var w=N(f,"width"); var h=N(f,"height"); return w>0&&h>0?(int)Math.Min(w,h):0; }
  public static Selection Select(IDictionary<string,object> info,bool strict,bool mp4) { return Select(info,1080,strict,mp4?"compatible":"mkv"); }
  public static bool Codec(string codec,params string[] prefixes) {return prefixes.Any(p=>codec.StartsWith(p,StringComparison.OrdinalIgnoreCase));}
  public static string Container(string profile) {return profile=="compatible"||profile=="mp4"?"mp4":profile;}
  public static bool IsAudio(string profile) {return profile=="mp3"||profile=="m4a";}
  public static List<string> BatchUrls(string input) {
   var urls=new List<string>();var errors=new List<string>();var lines=Regex.Split(input,@"\r\n|\n|\r");
   if(lines.Count(x=>!string.IsNullOrWhiteSpace(x))>100)throw new Exception("Add up to 100 links per batch.");
   for(int i=0;i<lines.Length;i++){if(string.IsNullOrWhiteSpace(lines[i]))continue;try{string url=Canonical(lines[i]);if(!urls.Contains(url))urls.Add(url);}catch{errors.Add((i+1).ToString());}}
   if(errors.Count>0)throw new Exception("Fix the video links on lines "+string.Join(", ",errors)+". Nothing was added.");
   if(urls.Count==0)throw new Exception("Paste at least one video link, one per line.");return urls;
  }
  public static Selection Select(IDictionary<string,object> info,int target,bool strict,string profile) {
   if(S(info,"is_live")=="True"||S(info,"live_status")=="is_live"||S(info,"live_status")=="is_upcoming") throw new Exception("Live and upcoming streams are not supported. Try again after the recording is published.");
   var formats=Entries(info,"formats").Where(f=>S(f,"has_drm")!="True").ToList();
   if(IsAudio(profile)) {
    var audioFormats=formats.Where(f=>S(f,"vcodec")=="none"&&S(f,"acodec")!="none"&&S(f,"acodec")!="").ToList();
    var af=profile=="m4a"?audioFormats.LastOrDefault(f=>Codec(S(f,"acodec"),"mp4a","aac")):audioFormats.LastOrDefault();
    if(af==null)af=audioFormats.LastOrDefault();if(af==null)throw new Exception("No supported audio stream is available.");
    string id=S(af,"format_id");if(!Regex.IsMatch(id,@"^[A-Za-z0-9_.-]+$"))throw new Exception("Unsupported audio format identifier.");
    double audioBytes=N(af,"filesize");bool audioApproximate=audioBytes==0;if(audioBytes==0)audioBytes=N(af,"filesize_approx");return new Selection{Selector=id,Resolution=0,Fps=0,Bytes=(long)audioBytes,Approximate=audioApproximate,VideoCodec=S(af,"acodec")};
   }
   if(!new[]{"mp4","compatible","mkv","webm","mov"}.Contains(profile))throw new Exception("Unsupported file format.");
   Func<string,bool> audioAllowed=codec=>profile=="mkv"?codec!="none"&&codec!="":profile=="webm"?Codec(codec,"opus","vorbis"):Codec(codec,"mp4a","aac");
   Func<string,bool> videoAllowed=codec=>profile=="mkv"?codec!="none"&&codec!="":profile=="webm"?Codec(codec,"vp9","vp09","vp8","vp08","av01"):profile=="mp4"?Codec(codec,"avc1","h264","av01","vp9","vp09","hvc1","hev1","hevc"):Codec(codec,"avc1","h264");
   var audio=formats.Where(f=>S(f,"vcodec")=="none"&&audioAllowed(S(f,"acodec"))).LastOrDefault();
   var candidates=formats.Where(f=>videoAllowed(S(f,"vcodec"))&&Resolution(f)>0&&(target==0||(strict?Resolution(f)==target:Resolution(f)<=target)))
    .Where(f=>S(f,"acodec")=="none"?audio!=null:audioAllowed(S(f,"acodec")))
    .OrderBy(f=>Resolution(f)).ThenBy(f=>N(f,"fps")).ToList();
   if(candidates.Count==0) throw new Exception(strict&&target>0?target+"p is unavailable in this format. Try modern MP4 or MKV, choose a different quality, or enable lower-resolution fallback. No downgrade was started.":"No supported video + audio pair is available. Try MKV, or check another video.");
   var video=candidates.Last();
   bool needsAudio=S(video,"acodec")=="none";
   string vid=S(video,"format_id"), aid=needsAudio?S(audio,"format_id"):"";
   if(!Regex.IsMatch(vid,@"^[A-Za-z0-9_.-]+$")||(needsAudio&&!Regex.IsMatch(aid,@"^[A-Za-z0-9_.-]+$"))) throw new Exception("The video reported an unsupported format identifier.");
   double vb=N(video,"filesize"), ab=needsAudio?N(audio,"filesize"):0;
   bool approximate=vb==0||(needsAudio&&ab==0);
   if(vb==0) vb=N(video,"filesize_approx");
   if(needsAudio&&ab==0) ab=N(audio,"filesize_approx");
   long bytes=(long)(vb+ab);
   if(vb==0||(needsAudio&&ab==0)) bytes=0;
   return new Selection {Selector=vid+(needsAudio?"+"+aid:""),Resolution=Resolution(video),Fps=N(video,"fps"),Bytes=bytes,Approximate=approximate,VideoCodec=S(video,"vcodec")};
  }
  // Windows CreateProcess quoting; never invoke a shell with user-provided text.
  public static string Quote(string value) {
   var b=new StringBuilder("\""); int n=0;
   foreach(char c in value) { if(c=='\\') {n++;continue;} if(c=='"') {b.Append('\\',n*2+1);b.Append(c);} else {b.Append('\\',n);b.Append(c);} n=0; }
   b.Append('\\',n*2); return b.Append('"').ToString();
  }
  public static Task<Result> Run(string exe,IEnumerable<string> args,CancellationToken token,int seconds,Action<string> onLine) {
   return Task.Run(()=> {
    token.ThrowIfCancellationRequested();
    var stdout=new StringBuilder(); var stderr=new StringBuilder();
    using(var p=new Process()) {
     p.StartInfo=new ProcessStartInfo(exe,string.Join(" ",args.Select(Quote))) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
     p.OutputDataReceived+=(s,e)=>{if(e.Data!=null){lock(stdout) stdout.AppendLine(e.Data); if(onLine!=null) onLine(e.Data);}};
     p.ErrorDataReceived+=(s,e)=>{if(e.Data!=null){lock(stderr) stderr.AppendLine(e.Data); if(onLine!=null) onLine(e.Data);}};
     p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();
     var sw=Stopwatch.StartNew();
     while(!p.WaitForExit(200)) {
      if(token.IsCancellationRequested||sw.Elapsed.TotalSeconds>seconds) {
       try { using(var kill=Process.Start(new ProcessStartInfo("taskkill.exe","/PID "+p.Id+" /T /F") {UseShellExecute=false,CreateNoWindow=true})) {kill.WaitForExit(5000);} } catch { try{p.Kill();}catch{} }
       p.WaitForExit(5000); token.ThrowIfCancellationRequested(); throw new Exception("The operation timed out. Check your connection and retry.");
      }
     }
     p.WaitForExit();token.ThrowIfCancellationRequested();
     return new Result {Code=p.ExitCode,Output=stdout.ToString(),Error=stderr.ToString()};
    }
   });
  }
  public static string Friendly(string raw) {
   string l=(raw??"").ToLowerInvariant();
   if(l.Contains("sign in")||l.Contains("confirm you")||l.Contains("bot")) return "YouTube requires verification for this request. Try later or update the engine. This version does not import browser cookies.";
   if(l.Contains("private")||l.Contains("members-only")||l.Contains("age-restricted")) return "This video requires account access and cannot be downloaded by this version.";
   if(l.Contains("429")||l.Contains("too many requests")) return "YouTube is limiting requests. Wait before retrying.";
   if(l.Contains("403")) return "YouTube rejected the media request. Update the engine and check the video again.";
   if(l.Contains("unavailable")||l.Contains("not available")) return "The video or requested format is unavailable. Check the link or try MKV.";
   if(l.Contains("no space")||l.Contains("disk full")) return "There is not enough free disk space. Choose another folder.";
   var lines=(raw??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries); var line=lines.LastOrDefault(x=>x.StartsWith("ERROR:"))??lines.LastOrDefault()??"The operation failed. Select Details for diagnostics.";
   return line.Length>320?line.Substring(0,320):line;
  }
  public static string Size(long bytes) {return bytes>0?(bytes/1048576d).ToString("0",CultureInfo.InvariantCulture)+" MB":"Size unavailable";}
  public static void ValidateMedia(string json,int resolution,double expectedDuration) {
   var d=Json.Deserialize<Dictionary<string,object>>(json); var streams=Entries(d,"streams").ToList();
   var video=streams.FirstOrDefault(x=>S(x,"codec_type")=="video"&&Resolution(x)>0); var audio=streams.FirstOrDefault(x=>S(x,"codec_type")=="audio");
   if(audio==null||(resolution>0&&video==null)) throw new Exception("The saved file failed verification: a required media stream is missing.");
   if(resolution>0&&Resolution(video)!=resolution) throw new Exception("The saved file has an unexpected resolution. It has not been marked complete.");
   object obj; var format=d.TryGetValue("format",out obj)?obj as Dictionary<string,object>:null;
   double duration=format!=null?N(format,"duration"):0;
   if(duration<=0||(expectedDuration>0&&Math.Abs(duration-expectedDuration)>Math.Max(3,expectedDuration*0.02))) throw new Exception("The saved file may be incomplete: its duration does not match the source.");
  }
 }
 public partial class MainWindow {
  readonly Window w; readonly ObservableCollection<Job> jobs=new ObservableCollection<Job>();
  readonly string home=AppDomain.CurrentDomain.BaseDirectory; string dataDir; string folder; bool running,inspecting,updating,closing;
  Dictionary<string,object> preview; Selection selection; string previewUrl;
  CancellationTokenSource cancellation; CancellationTokenSource inspectionCancellation; Job active;
  readonly StateStore stateStore;
  string persistenceError;
  bool pauseAfterCurrent;
  readonly bool offline;
  readonly Thumbnails thumbnails=new Thumbnails();
  public MainWindow(bool testMode,string testStateDirectory=null) {
   offline=testMode;
   using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Main.xaml")) w=(Window)XamlReader.Load(s);
   C<TextBlock>("EngineStatus").Text="v"+Assembly.GetExecutingAssembly().GetName().Version.ToString(3)+"  /  WINDOWS x64";
   dataDir=testMode?(testStateDirectory??Path.Combine(home,"test-state")):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClearFrame");
   stateStore=new StateStore(Path.Combine(dataDir,"state.json"));
   folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),"ClearFrame");
   if(!testMode||testStateDirectory!=null) Load();
   C<TextBlock>("FolderText").Text=folder; C<ListView>("QueueList").ItemsSource=jobs;
   C<Button>("PasteButton").Click+=(s,e)=>{try{C<TextBox>("UrlBox").Text=Clipboard.GetText().Trim();}catch(Exception ex){Status(ex.Message);}};
   C<Button>("InspectButton").Click+=async(s,e)=>{if(inspecting){if(inspectionCancellation!=null)inspectionCancellation.Cancel();return;}await Inspect();};
   C<TextBox>("UrlBox").KeyDown+=async(s,e)=>{if(e.Key==System.Windows.Input.Key.Enter&&!inspecting) await Inspect();};
   C<TextBox>("UrlBox").TextChanged+=(s,e)=>{ResetClip();if(preview!=null){preview=null;selection=null;C<Button>("AddButton").IsEnabled=false;C<TextBlock>("PreviewTitle").Text="Check this link before adding it.";C<TextBlock>("PreviewDetail").Text="Your download options are kept for the next video.";}};
   C<ComboBox>("QualityBox").SelectionChanged+=(s,e)=>RefreshPreview(); C<ComboBox>("FormatBox").SelectionChanged+=(s,e)=>RefreshPreview();
   C<CheckBox>("FallbackBox").Checked+=(s,e)=>RefreshPreview();C<CheckBox>("FallbackBox").Unchecked+=(s,e)=>RefreshPreview();
   C<Button>("AddButton").Click+=(s,e)=>Add();
   C<Button>("FolderButton").Click+=(s,e)=>{using(var d=new Forms.FolderBrowserDialog()){d.Description="Choose where ClearFrame saves videos";d.SelectedPath=folder;if(d.ShowDialog()==Forms.DialogResult.OK){folder=d.SelectedPath;C<TextBlock>("FolderText").Text=folder;Save();}}};
   C<Button>("StartButton").Click+=async(s,e)=>await Start();
   C<Button>("StopButton").Click+=(s,e)=>{if(cancellation!=null){cancellation.Cancel();Status("Stopping. Partial files will be kept for retry.");}};
   C<CheckBox>("PauseAfterBox").Checked+=(s,e)=>{pauseAfterCurrent=true;Status("The current download will finish, then the queue will pause.");};
   C<CheckBox>("PauseAfterBox").Unchecked+=(s,e)=>{pauseAfterCurrent=false;if(running)Status("The queue will continue after the current download.");};
   C<Button>("RetryButton").Click+=(s,e)=>{var j=Selected();if(j!=null&&j!=active&&(j.Status=="Failed"||j.Status=="Stopped")){j.Status="Queued";j.Detail="Will recheck availability and resume supported partial downloads.";j.Progress=0;Save();UpdateCount();}else Status("Select a stopped or failed download to retry.");};
   C<Button>("OpenButton").Click+=(s,e)=>{var j=Selected();if(j==null)return;try{if(!string.IsNullOrEmpty(j.FilePath)&&File.Exists(j.FilePath)) Process.Start("explorer.exe","/select,"+Core.Quote(j.FilePath));else if(Directory.Exists(j.Folder)) Process.Start("explorer.exe",Core.Quote(j.Folder));else Status("The destination folder has not been created yet.");}catch(Exception ex){Status(ex.Message);}};
   C<Button>("UpdateButton").Click+=async(s,e)=>await UpdateEngine();
   w.Closing+=(s,e)=> {if(closing)return;if(running||inspecting||updating){e.Cancel=true;Status("Stop the queue and wait for the current check or update before closing.");return;}Save();closing=true;};
   SetupFeatures();
   jobs.CollectionChanged+=(s,e)=>{if(e.NewItems!=null)foreach(Job job in e.NewItems){if(job.AddedUtcTicks==0)job.AddedUtcTicks=DateTime.UtcNow.Ticks;LoadThumbnail(job);}};
   foreach(var job in jobs.Take(200))LoadThumbnail(job);
   w.Closed+=(s,e)=>thumbnails.Stop();
   UpdateCount();
   if(!testMode) {try{CheckTools();}catch(Exception ex){Status(ex.Message);}}
  }
  async void LoadThumbnail(Job job){if(offline)return;job.Thumbnail=await thumbnails.Get(job.VideoId);}
  T C<T>(string name) where T:class {return (T)w.FindName(name);}
  Job Selected(){return C<ListView>("QueueList").SelectedItem as Job;}
  void Status(string value){var text=C<TextBlock>("StatusText");text.Text=string.IsNullOrEmpty(persistenceError)?value:"History not saved: "+persistenceError+" · "+value;text.ToolTip=text.Text;}
  void UI(Action action){if(!w.Dispatcher.HasShutdownStarted) w.Dispatcher.BeginInvoke(action);}
  string Tool(string name){return Path.Combine(home,"tools",name+".exe");}
  int TargetResolution(){return new[]{1080,1440,2160,4320,0,720,480,360}[C<ComboBox>("QualityBox").SelectedIndex];}
  string Profile(){return new[]{"mp4","compatible","mkv","webm","mov","mp3","m4a"}[C<ComboBox>("FormatBox").SelectedIndex];}
  void CheckTools(){foreach(string tool in new[]{"yt-dlp","ffmpeg","ffprobe","deno"}) if(!File.Exists(Tool(tool))) throw new Exception("Missing "+tool+". Run Get-Tools.ps1 from the extracted release folder, or scripts/Get-Tools.ps1 from the repository.");}
  List<string> Common(){return new List<string>{"--ignore-config","--no-plugin-dirs","--cache-dir",Path.Combine(dataDir,"cache"),"--no-playlist","--no-colors","--encoding","utf-8","--socket-timeout","25","--retries","5","--extractor-retries","3","--ffmpeg-location",Path.Combine(home,"tools"),"--js-runtimes","deno:"+Tool("deno")};}
  async Task<Dictionary<string,object>> Metadata(string url,CancellationToken ct){var args=Common();args.AddRange(new[]{"--skip-download","--dump-single-json","--",url});var r=await Core.Run(Tool("yt-dlp"),args,ct,180,null);if(r.Code!=0)throw new Exception(Core.Friendly(r.Error));return Core.Json.Deserialize<Dictionary<string,object>>(r.Output);}
  async Task Inspect(){
   if(inspecting||updating)return;
   string url;try{CheckTools();url=Core.Canonical(C<TextBox>("UrlBox").Text);}catch(Exception ex){Status(ex.Message);return;}
   inspecting=true;inspectionCancellation=new CancellationTokenSource();C<Button>("InspectButton").Content="Cancel check";C<Button>("UpdateButton").IsEnabled=false;C<Button>("AddButton").IsEnabled=false;C<TextBox>("UrlBox").IsReadOnly=true;C<Button>("PasteButton").IsEnabled=false;
   preview=null;selection=null; C<TextBlock>("PreviewTitle").Text="Checking video and available formats…";C<TextBlock>("PreviewDetail").Text="This can take a moment while YouTube responds.";Status("Checking source quality, audio and file size…");
   try{preview=await Metadata(url,inspectionCancellation.Token);previewUrl=url;RefreshPreview();}
   catch(OperationCanceledException){C<TextBlock>("PreviewTitle").Text="Video check cancelled.";C<TextBlock>("PreviewDetail").Text="Paste another link or check this one again.";Status("Check cancelled.");}
   catch(Exception ex){C<TextBlock>("PreviewTitle").Text="This video could not be checked.";C<TextBlock>("PreviewDetail").Text=ex.Message;Status(ex.Message);}
   finally{inspecting=false;inspectionCancellation.Dispose();inspectionCancellation=null;C<Button>("InspectButton").Content="Check link  ↗";C<TextBox>("UrlBox").IsReadOnly=false;C<Button>("PasteButton").IsEnabled=true;C<Button>("UpdateButton").IsEnabled=!running;}
  }
  void RefreshPreview(){if(preview==null)return;UpdateFormatControls();C<TextBlock>("PreviewTitle").Text=Core.S(preview,"title");
   try{selection=Core.Select(preview,TargetResolution(),C<CheckBox>("FallbackBox").IsChecked!=true,Profile());double duration=Core.N(preview,"duration");string time=duration>0?TimeSpan.FromSeconds(duration).ToString(@"hh\:mm\:ss"):"Duration unknown"; C<TextBlock>("PreviewDetail").Text=Core.S(preview,"uploader")+"  ·  "+time+"  ·  "+(Core.IsAudio(Profile())?Profile().ToUpperInvariant()+" audio":selection.Resolution+"p / "+selection.Fps+" fps")+"  ·  "+selection.VideoCodec+"  ·  "+(selection.Approximate&&selection.Bytes>0?"~":"")+Core.Size(selection.Bytes)+"  ·  Sound included";C<Button>("AddButton").IsEnabled=true;Status(Core.IsAudio(Profile())?"Audio stream available. Ready to add.":(TargetResolution()==0||selection.Resolution==TargetResolution()?"Source quality available: ":"Lower-resolution fallback: ")+selection.Resolution+"p. Ready to add.");}
   catch(Exception ex){selection=null;C<Button>("AddButton").IsEnabled=false;C<TextBlock>("PreviewDetail").Text=ex.Message;Status(ex.Message);}
  }
  void Add(){if(preview==null||selection==null)return;string id=Core.S(preview,"id");string container=Core.Container(Profile());
   if(clipEnd>0)try{DownloadOptions.ValidateClip(clipStart,clipEnd,Core.N(preview,"duration"));}catch(Exception ex){Status(ex.Message);return;}
   if(Duplicate(previewUrl,Profile(),folder,TargetResolution(),clipStart,clipEnd)){Status("This video is already in your queue or history with these download settings.");return;}
   jobs.Add(new Job{Id=Guid.NewGuid().ToString("N"),Url=previewUrl,VideoId=id,Title=Core.S(preview,"title"),Selector=selection.Selector,Container=container,Profile=Profile(),TargetResolution=TargetResolution(),Strict=C<CheckBox>("FallbackBox").IsChecked!=true,Subtitles=!Core.IsAudio(Profile())&&C<CheckBox>("SubtitleBox").IsChecked==true,SubtitleLanguage=SubtitleLanguage(),ClipStart=clipStart,ClipEnd=clipEnd,RateLimit=Rate(),Resolution=selection.Resolution,Duration=Core.N(preview,"duration"),EstimatedBytes=selection.Bytes,Folder=folder,Status="Queued",Detail=(clipEnd>0?"Clip · Full source downloaded before trimming · ":"")+(Core.IsAudio(Profile())?"Audio":selection.Resolution+"p")+" · "+container.ToUpperInvariant()+" · "+Core.Size(selection.Bytes),Log="",FilePath=""});Save();UpdateCount();Status("Added. Select Start queue when you're ready.");
  }
  void UpdateCount(){RefreshLibrary();}
  void Log(Job j,string line){if(line.Length>4000)line=line.Substring(0,4000);j.Log=(j.Log??"")+line+Environment.NewLine;if(j.Log.Length>50000)j.Log=j.Log.Substring(j.Log.Length-50000);}
  void ApplyDownloadLine(Job j,string line,CancellationToken ct){
   Log(j,line);
   if(j!=active||ct.IsCancellationRequested||(j.Status!="Downloading"&&j.Status!="Finishing"))return;
   if(line.StartsWith("CFP|")){var parts=line.Split('|');double p;if(parts.Length>=4&&double.TryParse(parts[1].Trim().TrimEnd('%'),NumberStyles.Float,CultureInfo.InvariantCulture,out p)&&!double.IsNaN(p)&&!double.IsInfinity(p)){j.Progress=Math.Max(0,Math.Min(99,p));j.Detail=(Core.IsAudio(j.Profile)?"Audio":j.Resolution+"p")+" · stream "+p.ToString("0")+"% · "+parts[2].Trim()+" · "+parts[3].Trim()+" remaining";}}
   else if(line.StartsWith("[Merger]")||line.StartsWith("[VideoRemuxer]")||line.StartsWith("[Metadata]")||line.StartsWith("[ExtractAudio]")){j.Status="Finishing";j.Detail=Core.IsAudio(j.Profile)?"Preparing your audio file…":"Combining video and audio without quality loss…";}
  }
  async Task Start(){
   if(running||updating)return;if(!jobs.Any(x=>x.Status=="Queued")){Status("Add a checked video, or retry a stopped download first.");return;}
   try{CheckTools();}catch(Exception ex){Status(ex.Message);return;}
   running=true;pauseAfterCurrent=false;C<CheckBox>("PauseAfterBox").IsChecked=false;C<CheckBox>("PauseAfterBox").IsEnabled=true;cancellation=new CancellationTokenSource();C<Button>("StartButton").IsEnabled=false;C<Button>("StartButton").Content="↓  Start queue";C<Button>("StopButton").IsEnabled=true;C<Button>("UpdateButton").IsEnabled=false;
   try{await RunQueue(Download,cancellation.Token);}
   finally{bool paused=pauseAfterCurrent&&!cancellation.IsCancellationRequested&&jobs.Any(x=>x.Status=="Queued");active=null;running=false;cancellation.Dispose();cancellation=null;C<CheckBox>("PauseAfterBox").IsEnabled=false;C<CheckBox>("PauseAfterBox").IsChecked=false;C<Button>("StartButton").IsEnabled=true;C<Button>("StartButton").Content=paused?"↓  Resume queue":"↓  Start queue";C<Button>("StopButton").IsEnabled=false;C<Button>("UpdateButton").IsEnabled=!inspecting;Save();Status(paused?"Queue paused after the current item. Select Resume queue to continue.":jobs.Any(x=>x.Status=="Failed")?"Queue finished with errors. Select a failed item and open Details.":jobs.Any(x=>x.Status=="Stopped"||x.Status=="Queued")?"Queue stopped. Retry the stopped item to resume it.":"Queue complete. Saved files passed their media checks.");}
  }
  async Task RunQueue(Func<Job,CancellationToken,Task> download,CancellationToken token){while(!token.IsCancellationRequested){var job=jobs.FirstOrDefault(x=>x.Status=="Queued");if(job==null)break;active=job;await download(job,token);active=null;Save();UpdateCount();if(pauseAfterCurrent)break;}}
  async Task Download(Job j,CancellationToken ct){
   string clipTemporary=null;
   try{
    j.Status="Checking";UpdateCount();j.Progress=0;j.Detail="Refreshing source links…";Log(j,"--- "+DateTime.Now.ToString("s")+" ---");
    Directory.CreateDirectory(j.Folder);string testPath=Path.Combine(j.Folder,".clearframe-write-"+Guid.NewGuid().ToString("N"));File.WriteAllText(testPath,"");File.Delete(testPath);
    var info=await Metadata(Core.Canonical(j.Url),ct);var sel=Core.Select(info,j.TargetResolution,j.Strict,j.Profile??(j.Container=="mp4"?"compatible":"mkv"));
    j.Title=Core.S(info,"title");j.Selector=sel.Selector;j.Resolution=sel.Resolution;j.Duration=Core.N(info,"duration");j.EstimatedBytes=sel.Bytes;
    if(j.IsClip)DownloadOptions.ValidateClip(j.ClipStart,j.ClipEnd,j.Duration);if(j.Subtitles)DownloadOptions.Language(j.SubtitleLanguage);
    var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(j.Folder)));long required=sel.Bytes>0?(long)(sel.Bytes*(j.IsClip||Core.IsAudio(j.Profile)?4:2.2))+104857600:536870912;
    if(drive.IsReady&&drive.AvailableFreeSpace<required)throw new Exception("Not enough free disk space. Allow room for separate streams and the merged video.");
    // Each job has stable private staging to resume safely and avoid partial-file collisions.
    string stage=Path.Combine(j.Folder,".clearframe",j.Id);Directory.CreateDirectory(stage);
    // Never let an interrupted merge masquerade as an already downloaded file on retry.
    foreach(string old in Directory.GetFiles(stage)) if(Regex.IsMatch(Path.GetFileName(old),@" (\d+p|audio)\.(mp4|mkv|webm|mov|mp3|m4a)$")) File.Move(old,old+".unverified-"+DateTime.UtcNow.Ticks);
    var args=Common();args.AddRange(new[]{"--newline","--progress","--progress-delta","0.5","--progress-template","download:CFP|%(progress._percent_str)s|%(progress._speed_str)s|%(progress._eta_str)s","--print","after_move:CFPATH|%(filepath)s","--no-simulate","--continue","--part","--no-overwrites","--abort-on-unavailable-fragments","--fragment-retries","5","--concurrent-fragments",string.IsNullOrEmpty(j.RateLimit)?"3":"1","--windows-filenames","--trim-filenames","150","--embed-metadata","--format",j.Selector,"--paths",stage,"--output","%(title).100B [%(id)s] "+(Core.IsAudio(j.Profile)?"audio":j.Resolution+"p")+".%(ext)s"});
    if(Core.IsAudio(j.Profile)){if(!j.IsClip)args.AddRange(new[]{"--extract-audio","--audio-format",j.Container,"--audio-quality","0"});}else args.AddRange(new[]{"--merge-output-format",j.Container,"--remux-video",j.Container});
    if(!string.IsNullOrEmpty(j.RateLimit)){if(!new[]{"1M","2M","5M","10M"}.Contains(j.RateLimit))throw new Exception("Invalid saved speed limit.");args.AddRange(new[]{"--limit-rate",j.RateLimit});}
    if(j.Subtitles)args.AddRange(new[]{"--write-subs","--write-auto-subs","--sub-langs",DownloadOptions.LanguagePattern(j.SubtitleLanguage),"--sub-format","srt/vtt/best","--convert-subs","srt"});
    args.Add("--");args.Add(j.Url);j.Status="Downloading";j.Detail=(Core.IsAudio(j.Profile)?"Audio":j.Resolution+"p")+" · "+j.Container.ToUpperInvariant();string completedPath=null;
    var r=await Core.Run(Tool("yt-dlp"),args,ct,86400,line=>{
     if(line.StartsWith("CFPATH|")) completedPath=line.Substring(7).Trim();
     UI(()=>ApplyDownloadLine(j,line,ct));
    });
    ct.ThrowIfCancellationRequested();if(r.Code!=0)throw new Exception(Core.Friendly(r.Error));
    if(string.IsNullOrWhiteSpace(completedPath)||!File.Exists(completedPath))throw new Exception("The engine did not report a saved video. Open Details to inspect the output.");
    string full=Path.GetFullPath(completedPath);if(!full.StartsWith(Path.GetFullPath(stage)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("The engine returned an unexpected output location.");
    if(!j.IsClip&&!string.Equals(Path.GetExtension(full),"."+j.Container,StringComparison.OrdinalIgnoreCase))throw new Exception("The saved file has an unexpected extension. It has not been marked complete.");
    j.Status="Verifying";j.Progress=99;j.Detail=Core.IsAudio(j.Profile)?"Checking audio and duration…":"Checking resolution, audio and duration…";
    var probe=await Core.Run(Tool("ffprobe"),new[]{"-v","error","-show_streams","-show_format","-of","json",full},ct,60,null);if(probe.Code!=0)throw new Exception("The downloaded file could not be read by the verifier.");Core.ValidateMedia(probe.Output,j.Resolution,j.Duration);
    string source=full,outputName=Path.GetFileName(full);
    if(j.IsClip){if(!Core.IsAudio(j.Profile))CleanupCore.ReadInfo(probe.Output);j.Status="Trimming";j.Detail="Encoding the selected range. The full source stays in staging until verification.";clipTemporary=Path.Combine(stage,"clip-"+Guid.NewGuid().ToString("N")+"."+j.Container);var trim=await Core.Run(Tool("ffmpeg"),DownloadOptions.ClipArgs(full,clipTemporary,j),ct,86400,null);if(trim.Code!=0)throw new Exception(Core.Friendly(trim.Error));var clipProbe=await Core.Run(Tool("ffprobe"),new[]{"-v","error","-show_streams","-show_format","-of","json",clipTemporary},ct,60,null);if(clipProbe.Code!=0)throw new Exception("The clip could not be read by the verifier.");DownloadOptions.VerifyClip(clipProbe.Output,j);full=clipTemporary;outputName=Path.GetFileNameWithoutExtension(source)+" [clip "+j.ClipStart.ToString("0.###",CultureInfo.InvariantCulture)+"-"+j.ClipEnd.ToString("0.###",CultureInfo.InvariantCulture)+"s]."+j.Container;}
    ct.ThrowIfCancellationRequested();string target=Path.Combine(j.Folder,outputName);if(File.Exists(target)){string stem=Path.GetFileNameWithoutExtension(outputName),ext=Path.GetExtension(outputName);int i=2;do{target=Path.Combine(j.Folder,stem+" ("+i+++ ")"+ext);}while(File.Exists(target));}
    File.Move(full,target);j.FilePath=target;
    string captionNotice=SaveCaptions(j,stage,source,target);j.SavedBytes=new FileInfo(target).Length;
    j.Status="Complete";j.Progress=100;j.Detail="Verified "+(j.IsClip?"clip · ":"")+(Core.IsAudio(j.Profile)?j.Container.ToUpperInvariant()+" audio":j.Resolution+"p + audio")+" · "+Core.Size(j.SavedBytes)+captionNotice;Log(j,"Verified saved file: "+target);
    try{if(j.IsClip)File.Delete(source);if(!Directory.EnumerateFileSystemEntries(stage).Any())Directory.Delete(stage);}catch(Exception ex){Log(j,"Staging cleanup: "+ex.Message);}
   }catch(OperationCanceledException){j.Status="Stopped";j.Detail="Partial files kept. Select Retry, then Start queue.";Log(j,"Stopped by user.");}
   catch(Exception ex){j.Status="Failed";j.Detail=ex.Message;Log(j,"ERROR: "+ex.Message);}
   finally{if(clipTemporary!=null&&File.Exists(clipTemporary))try{File.Delete(clipTemporary);}catch{}}
  }
  async Task UpdateEngine(){if(running||inspecting||updating)return;updating=true;C<Button>("UpdateButton").IsEnabled=false;C<Button>("InspectButton").IsEnabled=false;C<Button>("StartButton").IsEnabled=false;Status("Checking the official yt-dlp release for an engine update…");try{CheckTools();var r=await Core.Run(Tool("yt-dlp"),new[]{"--ignore-config","--no-plugin-dirs","--update"},CancellationToken.None,180,null);Status(r.Code==0?"Engine check finished. "+r.Output.Trim():Core.Friendly(r.Error));}catch(Exception ex){Status(ex.Message);}finally{updating=false;C<Button>("UpdateButton").IsEnabled=true;C<Button>("InspectButton").IsEnabled=true;C<Button>("StartButton").IsEnabled=true;}}
  void ShowDetails(){var j=Selected();if(j==null){Status("Select a download to see its details.");return;}var box=new TextBox{Text=j.Title+Environment.NewLine+j.Url+Environment.NewLine+j.Folder+Environment.NewLine+Environment.NewLine+(j.Log??j.Detail),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new FontFamily("Consolas"),FontSize=12,Margin=new Thickness(16)};new Window{Title="ClearFrame · Download details",Width=800,Height=560,Owner=w,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=box}.ShowDialog();}
  void Save(){if(!preferencesReady)return;try{stateStore.Save(new SavedState{Folder=folder,Jobs=jobs.ToList(),Preferences=(Dictionary<string,object>)Preferences()});bool recovered=persistenceError!=null;persistenceError=null;if(recovered)Status("Download history saved.");}catch(Exception ex){persistenceError=ex.Message;Status("Your current queue is still in memory.");}}
  void Load(){string notice;var state=stateStore.Load(out notice);preferences=state.Preferences;if(!string.IsNullOrWhiteSpace(state.Folder))folder=state.Folder;foreach(var j in state.Jobs){if(!Regex.IsMatch(j.Id??"",@"^[a-f0-9]{32}$"))continue;RestoreJob(j);jobs.Add(j);}if(notice!=null)Status(notice);}
  static void RestoreJob(Job j){if(j.Status!="Complete"&&j.Status!="Failed"&&j.Status!="Stopped"&&j.Status!="Queued"){j.Status="Stopped";j.Detail="Interrupted earlier. Retry to resume.";}}
  public Window Window {get{return w;}}
  public void Render(string file,int width=1280,int height=880){C<TextBlock>("FolderText").Text="Videos\\ClearFrame";var root=C<Grid>("Root");root.Margin=new Thickness(0);root.Width=width;root.Height=height;root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(9,11,8)),null,new Rect(0,0,width,height));var brush=new VisualBrush(root){AutoLayoutContent=false,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,width,height),Stretch=Stretch.Fill};dc.DrawRectangle(brush,null,new Rect(0,0,width,height));}image.Render(visual);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(file))enc.Save(stream);}
 }
 public static class Program {
  [STAThread] public static int Main(string[] args){
   try{if(args.Length>0&&args[0]=="--self-test"){Tests.Run(args.Length>1?args[1]:"self-test.txt");return 0;}
    if(args.Length>1&&args[0]=="--verify"){Core.ValidateMedia(File.ReadAllText(args[1]),int.Parse(args[2]),double.Parse(args[3],CultureInfo.InvariantCulture));return 0;}
    var app=new Application();app.DispatcherUnhandledException+=(s,e)=>{MessageBox.Show(e.Exception.Message,"ClearFrame");e.Handled=true;};
    if(args.Length>1&&args[0]=="--render-editor"){var owner=new MainWindow(true);new VideoCleanup(owner.Window,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools"),Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-state")).Render(args[1],args.Length>2?args[2]:null);return 0;}
    if(args.Length>1&&args[0]=="--check-editor"){var owner=new MainWindow(true);new VideoCleanup(owner.Window,"","").CheckOfflineInterface(args[1]);return 0;}
    if(args.Length>1&&args[0]=="--check-v04"){var main=new MainWindow(true);Exception failure=null;app.Dispatcher.BeginInvoke(new Action(async()=>{try{await main.CheckV04(args[1]);}catch(Exception ex){failure=ex;}finally{app.Dispatcher.InvokeShutdown();}}));Dispatcher.Run();if(failure!=null)throw failure;return 0;}
    if(args.Length>1&&args[0]=="--check-v05"){new MainWindow(true).CheckV05(args[1]);return 0;}
    if(args.Length>1&&args[0]=="--render-clip"){new MainWindow(true).RenderClipDialog(args[1]);return 0;}
    if(args.Length>2&&args[0]=="--check-live-playlist"){var main=new MainWindow(true);Exception failure=null;app.Dispatcher.BeginInvoke(new Action(async()=>{try{await main.CheckLivePlaylist(args[1],args[2]);}catch(Exception ex){failure=ex;}finally{app.Dispatcher.InvokeShutdown();}}));Dispatcher.Run();if(failure!=null)throw failure;return 0;}
    if(args.Length>1&&args[0]=="--render-playlist"){var owner=new MainWindow(true);new PlaylistPicker(owner.Window,null,null).RenderDemo(args[1]);return 0;}
    if(args.Length>2&&args[0]=="--check-restart"){new MainWindow(true,args[1]).CheckRestart(args[2]);return 0;}
    if(args.Length>1&&args[0]=="--render-library"){var main=new MainWindow(true);main.DemoLibrary();main.Render(args[1],args.Length>3?int.Parse(args[2]):1280,args.Length>3?int.Parse(args[3]):880);return 0;}
    if(args.Length>1&&args[0]=="--render"){var render=new MainWindow(true);if(args.Length>2&&args[2]=="--check-ui")render.CheckOfflineInterface(args[1]+".checks.txt");int width=args.Length>3?int.Parse(args[2]):1280;int height=args.Length>3?int.Parse(args[3]):880;render.Render(args[1],width,height);return 0;}
    bool created;using(var mutex=new Mutex(true,"Local\\ClearFrame.Desktop",out created)){if(!created){MessageBox.Show("ClearFrame is already running.","ClearFrame");return 0;}app.Run(new MainWindow(false).Window);}return 0;
   }catch(Exception ex){if(args.Length>0){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-error.txt"),ex.ToString());return 1;}MessageBox.Show(ex.Message,"ClearFrame could not start");return 1;}
  }
 }
 public static class Tests {
  static int count;static void Assert(bool value,string title){if(!value)throw new Exception("FAILED: "+title);count++;}
  static void Reject(Action action,string title){try{action();}catch{count++;return;}throw new Exception("FAILED: "+title);}
  static Dictionary<string,object> F(string id,int w,int h,string vc,string ac,string ext,int fps){return new Dictionary<string,object>{{"format_id",id},{"width",w},{"height",h},{"vcodec",vc},{"acodec",ac},{"ext",ext},{"fps",fps}};}
  public static void Run(string report){
   Assert(Core.Canonical("https://youtu.be/abcdefghijk?t=4")=="https://www.youtube.com/watch?v=abcdefghijk","short URL");
   Assert(Core.Canonical("https://www.youtube.com/watch?v=abcdefghijk&list=test").EndsWith("abcdefghijk"),"playlist parameters stripped");
   Assert(Core.Canonical("https://youtube.com/shorts/abcdefghijk").EndsWith("abcdefghijk"),"Shorts");
   foreach(string bad in new[]{"file:///C:/Windows/test","https://youtube.com.evil.com/watch?v=abcdefghijk","https://youtube.com@evil.com/watch?v=abcdefghijk","https://youtube.com/playlist?list=hello","https://youtube.com/watch?v=abc&x=1","--exec calc.exe","https://youtube.com:999/watch?v=abcdefghijk"})Reject(()=>Core.Canonical(bad),"invalid URL");
   var f=new[]{F("18",1280,720,"avc1","mp4a","mp4",30),F("137",1920,1080,"avc1","none","mp4",30),F("399",1920,1080,"av01","none","mp4",60),F("140",0,0,"none","mp4a.40.2","m4a",0),F("251",0,0,"none","opus","webm",0)};
   var info=new Dictionary<string,object>{{"formats",f}};Assert(Core.Select(info,true,true).Selector=="137+140","compatible video plus AAC");Assert(Core.Select(info,true,false).Selector=="399+251","best codec and fps");
   var low=new Dictionary<string,object>{{"formats",new[]{f[0]}}};Reject(()=>Core.Select(low,true,true),"no silent downgrade");Assert(Core.Select(low,false,true).Resolution==720,"explicit fallback");
   var portrait=new Dictionary<string,object>{{"formats",new[]{F("p",1080,1920,"avc1","mp4a","mp4",30)}}};Assert(Core.Select(portrait,true,true).Resolution==1080,"portrait short edge");
   var ultra=new Dictionary<string,object>{{"formats",new[]{F("u",3840,2160,"avc1","mp4a","mp4",30)}}};Reject(()=>Core.Select(ultra,false,true),"do not exceed cap");
   var silent=new Dictionary<string,object>{{"formats",new[]{f[1]}}};Reject(()=>Core.Select(silent,true,true),"never save silent video");
   info["is_live"]=true;Reject(()=>Core.Select(info,true,true),"reject live");info.Remove("is_live");
   var higher=new Dictionary<string,object>{{"formats",new[]{f[1],F("qhd",2560,1440,"vp9","none","webm",60),F("fourk",3840,2160,"av01","none","mp4",60),F("eightk",7680,4320,"av01","none","mp4",30),f[3],f[4]}}};
   Assert(Core.Select(higher,1440,true,"webm").Selector=="qhd+251","1440p WebM video and Opus");
   Assert(Core.Select(higher,2160,true,"mp4").Selector=="fourk+140","4K modern MP4 and AAC");
   Assert(Core.Select(higher,4320,true,"mkv").Selector=="eightk+251","8K MKV");
   Assert(Core.Select(higher,0,true,"mkv").Resolution==4320,"uncapped source resolution");
   Assert(Core.Select(higher,2160,false,"compatible").Resolution==1080,"explicit codec fallback");
   Reject(()=>Core.Select(higher,2160,true,"compatible"),"no silent 4K codec downgrade");
   Assert(Core.Select(higher,1080,true,"mov").Selector=="137+140","MOV H264 and AAC");
   Reject(()=>Core.Select(higher,2160,true,"unknown"),"invalid container profile");
   Assert(Core.Select(info,1080,true,"mp3").Selector=="251","MP3 best audio source");
   Assert(Core.Select(info,4320,true,"m4a").Selector=="140","M4A prefers AAC regardless of video resolution");
   var opusOnly=new Dictionary<string,object>{{"formats",new[]{f[4]}}};Assert(Core.Select(opusOnly,0,true,"m4a").Selector=="251","M4A conversion fallback");
   Reject(()=>Core.Select(silent,1080,true,"mp3"),"reject missing audio source");
   Assert(Core.BatchUrls("https://youtu.be/abcdefghijk\n\nhttps://youtube.com/watch?v=abcdefghijk&list=x").Count==1,"batch canonical deduplication");
   Reject(()=>Core.BatchUrls("https://youtu.be/abcdefghijk\ninvalid"),"reject whole batch with invalid input");
   Reject(()=>Core.BatchUrls(string.Join("\n",Enumerable.Repeat("https://youtu.be/abcdefghijk",101))),"batch size limit");
   Reject(()=>Core.BatchUrls(" \n "),"empty batch");
   var serialized=Core.Json.Serialize(new Job{Id="a",Profile="mp3",Container="mp3",RateLimit="2M",Status="Queued"});var restored=Core.Json.Deserialize<Job>(serialized);Assert(restored.Profile=="mp3"&&restored.RateLimit=="2M"&&restored.Kind=="AUDIO","saved audio job roundtrip");
   Core.ValidateMedia("{\"streams\":[{\"codec_type\":\"audio\"}],\"format\":{\"duration\":\"2\"}}",0,2);count++;
   Reject(()=>Core.ValidateMedia("{\"streams\":[{\"codec_type\":\"video\",\"width\":1920,\"height\":1080}],\"format\":{\"duration\":\"2\"}}",0,2),"audio verification requires audio");
   Assert(CleanupCore.Filter("blend",10,10,40,20,320,180)=="delogo=x=10:y=10:w=40:h=20","blend filter");
   Assert(CleanupCore.Filter("blur",0,0,40,20,320,180).Contains("overlay=0:0"),"blur edge region");
   Assert(CleanupCore.Filter("crop",0,0,240,180,320,180)=="crop=240:180:0:0","crop kept region");
   Reject(()=>CleanupCore.Filter("blend",0,0,40,20,320,180),"blend requires surrounding pixels");
   Reject(()=>CleanupCore.Filter("crop",10,10,400,200,320,180),"out of bounds region");
   Reject(()=>CleanupCore.Filter("blur",11,10,40,20,320,180),"odd coordinates");
   Reject(()=>CleanupCore.Filter("crop",0,0,4,4,320,180),"tiny region");
   Reject(()=>CleanupCore.Filter("unknown",10,10,40,20,320,180),"unsupported cleanup method");
   var silentInfo=CleanupCore.ReadInfo("{\"streams\":[{\"codec_type\":\"video\",\"width\":320,\"height\":180}],\"format\":{\"duration\":\"2\"}}");Assert(!silentInfo.Audio&&silentInfo.Width==320,"silent video editor support");
   var rotated=CleanupCore.ReadInfo("{\"streams\":[{\"codec_type\":\"video\",\"width\":320,\"height\":180,\"side_data_list\":[{\"rotation\":90}]}],\"format\":{\"duration\":\"2\"}}");Assert(rotated.Width==180&&rotated.Height==320,"rotation dimensions");
   Reject(()=>CleanupCore.ReadInfo("{\"streams\":[{\"codec_type\":\"video\",\"width\":320,\"height\":180,\"color_transfer\":\"smpte2084\"}],\"format\":{\"duration\":\"2\"}}"),"HDR rejected before lossy SDR edit");
   Assert(CleanupCore.ExportArgs("input.mp4","output.mp4","crop=240:180:0:0",true).Contains("-n"),"export never overwrites");
   Assert(CleanupCore.FrameTime("0.5",2)==0.5,"decimal preview time");
   foreach(string time in new[]{"NaN","Infinity","-Infinity","-1","2","bad"})Reject(()=>CleanupCore.FrameTime(time,2),"invalid preview time");
   foreach(string duration in new[]{"NaN","Infinity","-1","0"})Reject(()=>CleanupCore.ReadInfo("{\"streams\":[{\"codec_type\":\"video\",\"width\":320,\"height\":180}],\"format\":{\"duration\":\""+duration+"\"}}"),"invalid editor duration");
   string cover="{\"codec_type\":\"video\",\"width\":600,\"height\":600,\"disposition\":{\"attached_pic\":1}}";
   var covered=CleanupCore.ReadInfo("{\"streams\":["+cover+",{\"codec_type\":\"video\",\"width\":320,\"height\":180}],\"format\":{\"duration\":\"2\"}}");Assert(covered.Width==320&&covered.Height==180,"ignore cover art when reading dimensions");
   Reject(()=>CleanupCore.ReadInfo("{\"streams\":["+cover+",{\"codec_type\":\"audio\"}],\"format\":{\"duration\":\"2\"}}"),"cover art alone is not video");
   string valid="{\"streams\":[{\"codec_type\":\"video\",\"width\":1920,\"height\":1080},{\"codec_type\":\"audio\"}],\"format\":{\"duration\":\"10\"}}";
   Core.ValidateMedia(valid,1080,10);count++;Reject(()=>Core.ValidateMedia(valid,720,10),"resolution mismatch");Reject(()=>Core.ValidateMedia(valid,1080,100),"truncation");
   Assert(Core.Quote("a b")=="\"a b\"","quote spaces");Assert(Core.Quote("C:\\folder\\")=="\"C:\\folder\\\\\"","quote trailing slash");Assert(Core.Quote("a\"b")=="\"a\\\"b\"","quote embedded quote");
   File.WriteAllText(report,count+" core checks passed."+Environment.NewLine+"Covers URL validation, quality selection, audio pairing, no silent downgrade, portrait resolution, duration checks and process argument escaping.");
  }
 }
}
