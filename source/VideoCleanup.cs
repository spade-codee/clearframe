using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ClearFrame {
    public class VideoInfo {
        public int Width, Height;
        public double Duration;
        public bool Audio;
        public string AudioCodec;
        public double PixelAspect=1;
    }
    public static class CleanupCore {
        public static VideoInfo ReadInfo(string json) {
            var root = Core.Json.Deserialize<Dictionary<string, object>>(json);
            var streams = Core.Entries(root, "streams").ToList();
            var video = streams.FirstOrDefault(s => Core.S(s, "codec_type") == "video" && Core.N(s, "width") > 0 && !IsCoverArt(s));
            if (video == null) throw new Exception("This file does not contain a readable video stream.");
            string transfer = Core.S(video, "color_transfer");
            if (transfer == "smpte2084" || transfer == "arib-std-b67") throw new Exception("HDR editing is not supported in this version. Use an SDR source to avoid unexpected colors.");
            int width = (int)Core.N(video, "width"), height = (int)Core.N(video, "height");
            double rotation = 0;
            foreach (var side in Core.Entries(video, "side_data_list")) if (side.ContainsKey("rotation")) rotation = Core.N(side, "rotation");
            if (Math.Abs(rotation % 90) > 1) throw new Exception("This editor supports rotation in 90-degree steps. Normalize the video's rotation before editing.");
            if (Math.Abs(rotation % 180) > 1) { int swap = width; width = height; height = swap; }
            object raw;
            var format = root.TryGetValue("format", out raw) ? raw as Dictionary<string, object> : null;
            double duration = format == null ? 0 : Core.N(format, "duration");
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0 || duration > TimeSpan.MaxValue.TotalSeconds) throw new Exception("Cannot determine this video's duration.");
            if (width <= 0 || height <= 0) throw new Exception("Cannot determine this video's dimensions.");
            if (width % 2 != 0 || height % 2 != 0) throw new Exception("This editor currently requires even video dimensions for H.264 export.");
            var audio = streams.FirstOrDefault(s => Core.S(s, "codec_type") == "audio");
            double pixelAspect=1;string[] ratio=Core.S(video,"sample_aspect_ratio").Split(':');double numerator,denominator;if(ratio.Length==2&&double.TryParse(ratio[0],out numerator)&&double.TryParse(ratio[1],out denominator)&&numerator>0&&denominator>0)pixelAspect=numerator/denominator;
            return new VideoInfo { Width = width, Height = height, Duration = duration, Audio = audio != null, AudioCodec = audio == null ? "" : Core.S(audio, "codec_name"),PixelAspect=pixelAspect };
        }
        static bool IsCoverArt(Dictionary<string, object> stream) {
            object value;
            var disposition = stream.TryGetValue("disposition", out value) ? value as Dictionary<string, object> : null;
            return disposition != null && Core.N(disposition, "attached_pic") != 0;
        }
        public static double FrameTime(string text, double duration) {
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value >= duration)
                throw new Exception("Enter a frame time from 0 up to the video's duration, in seconds.");
            return value;
        }
        public static string Filter(string mode, int x, int y, int width, int height, int videoWidth, int videoHeight) {
            if (x < 0 || y < 0 || width < 8 || height < 8 || (long)x + width > videoWidth || (long)y + height > videoHeight)
                throw new Exception("Select an area at least 8 × 8 pixels, entirely inside the frame.");
            if ((x | y | width | height) % 2 != 0) throw new Exception("Use even coordinates and dimensions for accurate H.264 cropping.");
            if (mode == "blend") {
                if (x < 2 || y < 2 || x + width >= videoWidth - 1 || y + height >= videoHeight - 1)
                    throw new Exception("Blending needs a border around the logo. For a logo touching an edge, use blur or crop.");
                return string.Format(CultureInfo.InvariantCulture, "delogo=x={0}:y={1}:w={2}:h={3}", x, y, width, height);
            }
            if (mode == "crop") return string.Format(CultureInfo.InvariantCulture, "crop={0}:{1}:{2}:{3}", width, height, x, y);
            if (mode == "blur") return string.Format(CultureInfo.InvariantCulture, "split[base][region];[region]crop={0}:{1}:{2}:{3},boxblur={4}:2[blurred];[base][blurred]overlay={2}:{3}", width, height, x, y, Math.Min(12, Math.Max(1, Math.Min(width, height) / 4 - 1)));
            throw new Exception("Select a supported cleanup method.");
        }
        public static void Verify(string json, VideoInfo original, int expectedWidth, int expectedHeight) {
            var saved = ReadInfo(json);
            if (saved.Width != expectedWidth || saved.Height != expectedHeight || (original.Audio && !saved.Audio) || Math.Abs(saved.Duration - original.Duration) > Math.Max(3, original.Duration * 0.02))
                throw new Exception("Export verification failed: dimensions, audio or duration did not match.");
        }
        public static List<string> ExportArgs(string input, string output, string filter, bool copyAudio) {
            return new List<string> { "-hide_banner", "-loglevel", "error", "-nostdin", "-n", "-i", input, "-map", "0:V:0", "-map", "0:a:0?", "-vf", filter, "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p", "-c:a", copyAudio ? "copy" : "aac", "-map_metadata", "-1", "-movflags", "+faststart", "-progress", "pipe:1", "-nostats", output };
        }
        public static List<string> CompatibleArgs(string input,string output) {
            var args=ExportArgs(input,output,"null",false);
            args.InsertRange(args.IndexOf("-i"),new[]{"-xerror","-err_detect","explode"});
            args.InsertRange(args.Count-1,new[]{"-profile:v","high","-profile:a","aac_low","-b:a","192k","-map_chapters","-1"});
            return args;
        }
        public static void VerifyCompatible(string json,VideoInfo original) {
            Verify(json,original,original.Width,original.Height);
            var root=Core.Json.Deserialize<Dictionary<string,object>>(json);var streams=Core.Entries(root,"streams").ToList();
            var video=streams.First(s=>Core.S(s,"codec_type")=="video"&&!IsCoverArt(s));
            var audio=streams.FirstOrDefault(s=>Core.S(s,"codec_type")=="audio");
            if(Core.S(video,"codec_name")!="h264"||Core.S(video,"pix_fmt")!="yuv420p"||(audio!=null&&(Core.S(audio,"codec_name")!="aac"||Core.S(audio,"profile")!="LC")))throw new Exception("The export did not meet the H.264/AAC compatibility profile.");
        }
        public static string[] PreviewArgs(string input, string output, string filter, double seconds) {
            return new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-ss", seconds.ToString(CultureInfo.InvariantCulture), "-i", input, "-map", "0:V:0", "-frames:v", "1", "-vf", (string.IsNullOrEmpty(filter) ? "" : filter + ",") + "scale=960:540:force_original_aspect_ratio=decrease:reset_sar=1", "-update", "1", "-y", output };
        }
    }
    public class VideoCleanup {
        readonly Window window;
        readonly string tools, cache;
        readonly Image picture = new Image { Stretch = Stretch.Uniform };
        readonly Canvas canvas = new Canvas { Background = Brushes.Transparent, ClipToBounds = true };
        readonly System.Windows.Shapes.Rectangle selection = new System.Windows.Shapes.Rectangle { Stroke = new SolidColorBrush(Color.FromRgb(214,246,74)), StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(45,214,246,74)), IsHitTestVisible = false };
        readonly TextBlock fileLabel = new TextBlock { Text = "Choose a local video to begin.", TextWrapping = TextWrapping.Wrap };
        readonly TextBlock status = new TextBlock { Text = "The original file is kept. Edits are exported to a new MP4.", TextWrapping = TextWrapping.Wrap };
        readonly TextBlock help = new TextBlock { TextWrapping = TextWrapping.Wrap };
        readonly ComboBox method = new ComboBox { MinWidth = 190, SelectedIndex = 0 };
        readonly TextBox[] fields = new TextBox[4];
        readonly TextBox seconds = new TextBox { Text = "0", Width = 70 };
        readonly TextBox clipStart=new TextBox{Text="0",Width=100},clipEnd=new TextBox{Text="",Width=100};
        readonly ComboBox verticalSize=new ComboBox{Width=170,SelectedIndex=0};
        readonly WrapPanel clipOptions=new WrapPanel();
        readonly TextBlock editorHeading=new TextBlock{FontSize=26,FontWeight=FontWeights.SemiBold};
        readonly Button open = new Button { Content = "Open video" };
        readonly Button refresh = new Button { Content = "Load frame" };
        readonly Button previewEdit = new Button { Content = "Preview edit" };
        readonly Button compare = new Button { Content = "Show original", IsEnabled = false };
        readonly Button export = new Button { Content = "Export new MP4" };
        readonly Button cancel = new Button { Content = "Cancel", IsEnabled = false };
        readonly ProgressBar progress = new ProgressBar { Height = 4, Minimum = 0, Maximum = 100 };
        VideoInfo info;
        string input, outputFile;
        BitmapSource originalFrame, processedFrame;
        CancellationTokenSource cancellation;
        bool busy, selecting, updatingFields, editedPreview;
        Point start;
        public VideoCleanup(Window owner, string toolDirectory, string cacheDirectory, bool compatibilityMode=false,bool verticalMode=false) {
            tools = toolDirectory; cache = Path.Combine(cacheDirectory, "editor");
            window = new Window { Title = compatibilityMode?"ClearFrame · Convert for Windows":"ClearFrame · Video cleanup", Width = 1040, Height = 840, MinWidth = 880, MinHeight = 740, Owner = owner.IsVisible ? owner : null, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(9,11,8)), Foreground = new SolidColorBrush(Color.FromRgb(244,244,239)), FontFamily = new FontFamily("Segoe UI"), FontSize = 13, Resources = owner.Resources };
            var layout = new Grid { Margin = new Thickness(24) };
            foreach (var height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1,GridUnitType.Star), GridLength.Auto, GridLength.Auto, GridLength.Auto }) layout.RowDefinitions.Add(new RowDefinition { Height = height });
            var heading = new DockPanel { Margin = new Thickness(0,0,0,15) };
            DockPanel.SetDock(open, Dock.Right); heading.Children.Add(open); heading.Children.Add(editorHeading); layout.Children.Add(heading);
            fileLabel.Margin = new Thickness(0,0,0,12); Grid.SetRow(fileLabel,1); layout.Children.Add(fileLabel);
            var frame = new Grid { Background = Brushes.Black, MinHeight = 200, ClipToBounds = true }; frame.Children.Add(picture); frame.Children.Add(canvas); canvas.Children.Add(selection); Grid.SetRow(frame,2); layout.Children.Add(frame);
            var options = new WrapPanel { Margin = new Thickness(0,14,0,10) }; Grid.SetRow(options,3); layout.Children.Add(options);
            method.Items.Add("Blend fixed logo area"); method.Items.Add("Blur selected area"); method.Items.Add("Crop to selected area"); method.Items.Add("Windows-compatible MP4");method.Items.Add("Vertical clip (9:16)"); method.SelectedIndex = verticalMode?4:compatibilityMode?3:0; options.Children.Add(Field("METHOD",method,16));
            string[] names = { "X", "Y", "WIDTH", "HEIGHT" };
            for(int i=0;i<4;i++) { fields[i]=new TextBox { Text="0", Width=65 }; options.Children.Add(Field(names[i],fields[i],8)); fields[i].TextChanged+=(s,e)=> { if(!updatingFields){InvalidateEdit();DrawSelection();} }; }
            options.Children.Add(Field("FRAME (SECONDS)",seconds,8)); refresh.Margin=new Thickness(0,17,8,0);options.Children.Add(refresh);previewEdit.Margin=new Thickness(0,17,0,0);options.Children.Add(previewEdit);
            verticalSize.Items.Add("1080 × 1920");verticalSize.Items.Add("720 × 1280");clipOptions.Width=900;clipOptions.Margin=new Thickness(0,10,0,0);clipOptions.Children.Add(Field("CLIP START",clipStart,12));clipOptions.Children.Add(Field("CLIP END",clipEnd,12));clipOptions.Children.Add(Field("EXPORT SIZE",verticalSize,12));options.Children.Add(clipOptions);
            verticalSize.SelectionChanged+=(s,e)=>InvalidateEdit();clipStart.TextChanged+=(s,e)=>{try{seconds.Text=DownloadOptions.ParseTime(clipStart.Text).ToString("0.###",CultureInfo.InvariantCulture);}catch{}};
            help.Foreground = new SolidColorBrush(Color.FromRgb(177,191,153)); help.Margin = new Thickness(0,0,0,14); Grid.SetRow(help,4); layout.Children.Add(help);
            var footer = new Grid(); footer.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});footer.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});footer.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetRow(footer,5);layout.Children.Add(footer);
            var buttons=new StackPanel{Orientation=Orientation.Horizontal};export.Style=(Style)owner.FindResource("Primary");export.Margin=new Thickness(0,0,8,0);buttons.Children.Add(export);buttons.Children.Add(cancel);compare.Margin=new Thickness(8,0,0,0);buttons.Children.Add(compare);var show=new Button{Content="Show export",Margin=new Thickness(8,0,0,0)};show.Click+=(s,e)=>{try{if(!string.IsNullOrEmpty(outputFile)&&File.Exists(outputFile))System.Diagnostics.Process.Start("explorer.exe","/select,"+Core.Quote(outputFile));else status.Text="Export a video first to show its location.";}catch(Exception ex){status.Text="Could not open the export location: "+ex.Message;}};buttons.Children.Add(show);footer.Children.Add(buttons);
            progress.Margin=new Thickness(0,12,0,10);Grid.SetRow(progress,1);footer.Children.Add(progress);Grid.SetRow(status,2);footer.Children.Add(status);
            window.Content=layout;
            open.Click+=async(s,e)=>await Open();refresh.Click+=async(s,e)=>await Frame(false);previewEdit.Click+=async(s,e)=>await Frame(true);export.Click+=async(s,e)=>await Export();cancel.Click+=(s,e)=>{if(cancellation!=null)cancellation.Cancel();};
            method.SelectionChanged+=(s,e)=>{Hint();InvalidateEdit();if(info!=null&&Mode()=="vertical")try{SetVerticalCrop();}catch(Exception ex){status.Text=ex.Message;}SetBusy(busy);selection.Visibility=Mode()=="compatible"||editedPreview||picture.Source==null?Visibility.Hidden:Visibility.Visible;};seconds.TextChanged+=(s,e)=>ClearFrames();compare.Click+=(s,e)=>ShowFrame(!editedPreview); Hint();SetBusy(false);
            canvas.MouseLeftButtonDown+=(s,e)=>{if(info==null||busy||picture.Source==null||Mode()=="compatible")return;if(editedPreview){status.Text="Use Show original to select an area on the original picture.";return;}start=ToVideo(e.GetPosition(canvas));if(Mode()=="vertical")MoveVerticalCrop(start);selecting=true;canvas.CaptureMouse();e.Handled=true;};
            canvas.MouseMove+=(s,e)=>{if(!selecting)return;Point end=ToVideo(e.GetPosition(canvas));if(Mode()=="vertical"){MoveVerticalCrop(end);return;}SetRectangle((int)Math.Min(start.X,end.X),(int)Math.Min(start.Y,end.Y),(int)Math.Abs(start.X-end.X),(int)Math.Abs(start.Y-end.Y));};
            canvas.MouseLeftButtonUp+=(s,e)=>{selecting=false;canvas.ReleaseMouseCapture();};canvas.SizeChanged+=(s,e)=>DrawSelection();
            window.Closing+=(s,e)=>{if(busy){e.Cancel=true;status.Text="Cancel the current operation and wait before closing.";}};
        }
        StackPanel Field(string label,FrameworkElement control,int margin){var panel=new StackPanel{Margin=new Thickness(0,0,margin,0)};panel.Children.Add(new TextBlock{Text=label,FontSize=9,Margin=new Thickness(0,0,0,6)});panel.Children.Add(control);return panel;}
        string Mode(){return new[]{"blend","blur","crop","compatible","vertical"}[method.SelectedIndex];}
        void Hint(){clipOptions.Visibility=Mode()=="vertical"?Visibility.Visible:Visibility.Collapsed;editorHeading.Text=Mode()=="vertical"?"Frame it for Shorts.":Mode()=="compatible"?"Make it play on Windows.":"Clean up the frame.";window.Title="ClearFrame · "+(Mode()=="vertical"?"Vertical clips":Mode()=="compatible"?"Convert for Windows":"Video cleanup");if(Mode()=="vertical"){help.Text="Drag to position the fixed 9:16 crop. Enter start/end as seconds or mm:ss. Exports a new H.264/AAC clip. Smaller crops are enlarged to your export size; resizing cannot add detail. SDR and square pixels only.";return;}if(Mode()=="compatible"){help.Text="Convert the full picture to H.264 with AAC audio in a new MP4. No rectangle or watermark edit is applied. SDR only; re-encoding can change quality and file size. The original is kept.";return;}help.Text=Mode()=="crop"?"Drag the rectangle around the picture you want to KEEP. Everything outside it is removed.":Mode()=="blend"?"Drag around a fixed logo. Blending estimates pixels from its border; hidden detail is not recovered and artifacts may remain.":"Drag around the area to obscure. Blur hides detail; it does not reconstruct the original picture.";help.Text+=" Edits apply to this area for the entire video. Export re-encodes video to H.264; the first audio track is retained.";}
        void SetBusy(bool value){busy=value;open.IsEnabled=!value;refresh.IsEnabled=!value&&info!=null;previewEdit.IsEnabled=!value&&info!=null;export.IsEnabled=!value&&info!=null;method.IsEnabled=!value;seconds.IsEnabled=!value;for(int i=0;i<fields.Length;i++)fields[i].IsEnabled=!value&&Mode()!="compatible"&&(Mode()!="vertical"||i<2);clipStart.IsEnabled=clipEnd.IsEnabled=verticalSize.IsEnabled=!value&&info!=null;cancel.IsEnabled=value;compare.IsEnabled=!value&&originalFrame!=null&&processedFrame!=null;}
        string Tool(string name){string file=Path.Combine(tools,name+".exe");if(!File.Exists(file))throw new Exception("Missing "+name+". Run the ClearFrame installer again to repair the tools. Portable users can run Get-Tools.ps1.");return file;}
        async Task<string> Probe(string path,CancellationToken token){var r=await Core.Run(Tool("ffprobe"),new[]{"-v","error","-show_streams","-show_format","-of","json",path},token,60,null);if(r.Code!=0)throw new Exception(Core.Friendly(r.Error));return r.Output;}
        async Task Open(){var dialog=new OpenFileDialog{Filter="Video files|*.mp4;*.mkv;*.mov;*.webm;*.avi;*.m4v|All files|*.*"};if(dialog.ShowDialog(window)!=true)return;bool loaded=false;SetBusy(true);cancellation=new CancellationTokenSource();try{var details=CleanupCore.ReadInfo(await Probe(dialog.FileName,cancellation.Token));if(Mode()=="vertical")VerticalClips.CropSize(details);input=dialog.FileName;info=details;loaded=true;ClearFrames();fileLabel.Text=Path.GetFileName(input)+" · "+info.Width+" × "+info.Height+" · "+TimeSpan.FromSeconds(info.Duration).ToString(@"hh\:mm\:ss");SetRectangle(Math.Max(2,info.Width/10),Math.Max(2,info.Height/10),Math.Max(8,info.Width/5),Math.Max(8,info.Height/5));seconds.Text="0";clipStart.Text="0";clipEnd.Text=info.Duration.ToString("0.###",CultureInfo.InvariantCulture);if(Mode()=="vertical")SetVerticalCrop();status.Text=Mode()=="vertical"?"Video loaded. Position the crop and choose a clip range.":Mode()=="compatible"?"Video loaded. Export a new Windows-compatible MP4 when ready.":"Video loaded. Drag on the frame to select the area.";}catch(Exception ex){status.Text=ex.Message;}finally{cancellation.Dispose();cancellation=null;SetBusy(false);}if(loaded)await Frame(false);}
        int[] Region(){int[] r=new int[4];for(int i=0;i<4;i++)if(!int.TryParse(fields[i].Text,out r[i]))throw new Exception("Rectangle values must be whole pixels.");return r;}
        int VerticalWidth(){return verticalSize.SelectedIndex==1?720:1080;}
        void SetVerticalCrop(){var crop=VerticalClips.CropSize(info);SetRectangle((info.Width-crop[0])/2,(info.Height-crop[1])/2,crop[0],crop[1]);}
        void MoveVerticalCrop(Point point){try{var crop=VerticalClips.CropSize(info);SetRectangle((int)point.X-crop[0]/2,(int)point.Y-crop[1]/2,crop[0],crop[1]);}catch(Exception ex){status.Text=ex.Message;}}
        string Filter(){if(Mode()=="compatible")return "null";var r=Region();if(Mode()=="vertical")return VerticalClips.Filter(info,r[0],r[1],VerticalWidth());return CleanupCore.Filter(Mode(),r[0],r[1],r[2],r[3],info.Width,info.Height);}
        void ClearFrames(){originalFrame=null;processedFrame=null;picture.Source=null;editedPreview=false;selection.Visibility=Visibility.Hidden;compare.IsEnabled=false;compare.Content="Show original";if(info!=null)status.Text="Frame time changed. Load frame or Preview edit to view it.";}
        void InvalidateEdit(){processedFrame=null;compare.IsEnabled=false;if(editedPreview)ShowFrame(false);}
        void ShowFrame(bool edited){var frame=edited?processedFrame:originalFrame;if(frame==null)return;picture.Source=frame;editedPreview=edited;selection.Visibility=edited||Mode()=="compatible"?Visibility.Hidden:Visibility.Visible;compare.Content=edited?"Show original":"Show edit";compare.IsEnabled=!busy&&originalFrame!=null&&processedFrame!=null;status.Text=edited?"Edited frame. Show original compares the same frame before editing.":Mode()=="compatible"?"Full original picture. Export creates a separate H.264/AAC copy.":Mode()=="vertical"?"Original frame. Drag to position the 9:16 crop, then preview the edit.":"Original frame. Drag to select an area, then preview the edit.";DrawSelection();}
        async Task<BitmapSource> ReadFrame(string filter,double time,CancellationToken token){
            Directory.CreateDirectory(cache);string path=Path.Combine(cache,Guid.NewGuid().ToString("N")+".png");
            try{var result=await Core.Run(Tool("ffmpeg"),CleanupCore.PreviewArgs(input,path,filter,time),token,90,null);if(result.Code!=0)throw new Exception(Core.Friendly(result.Error));token.ThrowIfCancellationRequested();var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(path);bitmap.EndInit();bitmap.Freeze();return bitmap;}
            finally{if(File.Exists(path))try{File.Delete(path);}catch{}}
        }
        async Task Frame(bool edited){
            if(info==null||busy)return;SetBusy(true);cancellation=new CancellationTokenSource();
            try{double time=CleanupCore.FrameTime(seconds.Text,info.Duration);string filter=edited?Filter():null;if(originalFrame==null||!edited){originalFrame=await ReadFrame(null,time,cancellation.Token);processedFrame=null;ShowFrame(false);}if(edited){processedFrame=await ReadFrame(filter,time,cancellation.Token);ShowFrame(true);}}
            catch(OperationCanceledException){status.Text="Preview cancelled.";}catch(Exception ex){status.Text=ex.Message;}finally{cancellation.Dispose();cancellation=null;SetBusy(false);}
        }
        Rect DisplayRect(){double width=originalFrame==null?info.Width:originalFrame.PixelWidth,height=originalFrame==null?info.Height:originalFrame.PixelHeight;double scale=Math.Min(canvas.ActualWidth/width,canvas.ActualHeight/height);return new Rect((canvas.ActualWidth-width*scale)/2,(canvas.ActualHeight-height*scale)/2,width*scale,height*scale);}
        Point ToVideo(Point p){var rect=DisplayRect();return new Point(Math.Max(0,Math.Min(info.Width,(p.X-rect.X)/rect.Width*info.Width)),Math.Max(0,Math.Min(info.Height,(p.Y-rect.Y)/rect.Height*info.Height)));}
        void SetRectangle(int x,int y,int width,int height){if(Mode()=="vertical"&&info!=null){var fixedCrop=VerticalClips.CropSize(info);width=fixedCrop[0];height=fixedCrop[1];x=Math.Max(0,Math.Min(info.Width-width,x));y=Math.Max(0,Math.Min(info.Height-height,y));}updatingFields=true;int[] r={x/2*2,y/2*2,width/2*2,height/2*2};for(int i=0;i<4;i++)fields[i].Text=r[i].ToString();updatingFields=false;InvalidateEdit();DrawSelection();}
        void DrawSelection(){if(info==null||canvas.ActualWidth<=0)return;try{var r=Region();var rect=DisplayRect();double scaleX=rect.Width/info.Width,scaleY=rect.Height/info.Height;Canvas.SetLeft(selection,rect.X+r[0]*scaleX);Canvas.SetTop(selection,rect.Y+r[1]*scaleY);selection.Width=Math.Max(0,r[2]*scaleX);selection.Height=Math.Max(0,r[3]*scaleY);}catch{}}
        async Task Export(){if(info==null||busy)return;string filter;int[] region;double from=0,to=0;try{if(Mode()=="vertical"){from=DownloadOptions.ParseTime(clipStart.Text);to=DownloadOptions.ParseTime(clipEnd.Text);DownloadOptions.ValidateClip(from,to,info.Duration);}filter=Filter();region=Mode()=="compatible"?new[]{0,0,info.Width,info.Height}:Region();}catch(Exception ex){status.Text=ex.Message;return;}var dialog=new SaveFileDialog{Filter="MP4 video|*.mp4",DefaultExt=".mp4",AddExtension=true,FileName=Path.GetFileNameWithoutExtension(input)+(Mode()=="vertical"?"-vertical-clip.mp4":Mode()=="compatible"?"-windows-compatible.mp4":"-clean.mp4"),InitialDirectory=Path.GetDirectoryName(input),OverwritePrompt=false};if(dialog.ShowDialog(window)!=true)return;string target=Path.GetFullPath(dialog.FileName);if(File.Exists(target)){status.Text="Choose a new filename. Existing files, including the original, are never overwritten.";return;}if(!string.Equals(Path.GetExtension(target),".mp4",StringComparison.OrdinalIgnoreCase)){status.Text="This version exports MP4 files. Use the .mp4 extension.";return;}
            string temporary=target+".partial-"+Guid.NewGuid().ToString("N")+".mp4";SetBusy(true);progress.Value=0;cancellation=new CancellationTokenSource();try{var drive=new DriveInfo(Path.GetPathRoot(target));if(drive.IsReady&&drive.AvailableFreeSpace<Math.Max(new FileInfo(input).Length,536870912L))throw new Exception("Free additional disk space before exporting. Final output size depends on the source.");status.Text="Exporting a new MP4. You can cancel; the original is kept.";
                var result=await Core.Run(Tool("ffmpeg"),Mode()=="vertical"?VerticalClips.ExportArgs(input,temporary,info,region[0],region[1],VerticalWidth(),from,to):Mode()=="compatible"?CleanupCore.CompatibleArgs(input,temporary):CleanupCore.ExportArgs(input,temporary,filter,info.AudioCodec=="aac"),cancellation.Token,86400,line=>{if(line.StartsWith("out_time_us=")){double time;if(double.TryParse(line.Substring(12),NumberStyles.Float,CultureInfo.InvariantCulture,out time))window.Dispatcher.BeginInvoke(new Action(()=>progress.Value=Math.Min(99,time/1000000/(Mode()=="vertical"?to-from:info.Duration)*100)));}});
                if(result.Code!=0)throw new Exception(Core.Friendly(result.Error));cancellation.Token.ThrowIfCancellationRequested();string verified=await Probe(temporary,cancellation.Token);if(Mode()=="vertical")VerticalClips.Verify(verified,info,VerticalWidth(),from,to);else if(Mode()=="compatible")CleanupCore.VerifyCompatible(verified,info);else CleanupCore.Verify(verified,info,Mode()=="crop"?region[2]:info.Width,Mode()=="crop"?region[3]:info.Height);cancellation.Token.ThrowIfCancellationRequested();File.Move(temporary,target);outputFile=target;progress.Value=100;status.Text="Export verified: "+Path.GetFileName(target)+". Original file kept.";
            }catch(OperationCanceledException){status.Text="Export cancelled. Original file kept.";}catch(Exception ex){status.Text=ex.Message;}finally{if(File.Exists(temporary))try{File.Delete(temporary);}catch{} cancellation.Dispose();cancellation=null;SetBusy(false);}
        }
        public void Show(){window.ShowDialog();}
        public void CheckOfflineInterface(string report){
            int checks=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("Editor check failed: "+name);checks++;};
            check(!compare.IsEnabled&&!export.IsEnabled,"empty editor actions");
            info=new VideoInfo{Width=320,Height=180,Duration=2};
            var before=BitmapSource.Create(320,180,96,96,PixelFormats.Bgr32,null,new byte[320*180*4],320*4);before.Freeze();
            var after=BitmapSource.Create(240,180,96,96,PixelFormats.Bgr32,null,new byte[240*180*4],240*4);after.Freeze();
            originalFrame=before;processedFrame=after;SetBusy(false);ShowFrame(true);
            check(picture.Source==after&&compare.IsEnabled&&selection.Visibility==Visibility.Hidden,"edited preview");
            compare.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(picture.Source==before&&selection.Visibility==Visibility.Visible&&(string)compare.Content=="Show edit","original comparison");
            compare.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(picture.Source==after,"cached edit comparison");
            fields[0].Text="12";check(picture.Source==before&&processedFrame==null&&!compare.IsEnabled,"numeric edit invalidates preview");
            processedFrame=after;ShowFrame(true);method.SelectedIndex=1;check(picture.Source==before&&processedFrame==null,"method invalidates preview");
            processedFrame=after;ShowFrame(true);SetRectangle(10,10,40,20);check(picture.Source==before&&processedFrame==null,"drag selection invalidates preview");
            processedFrame=after;ShowFrame(true);SetBusy(true);check(!compare.IsEnabled&&!previewEdit.IsEnabled&&!export.IsEnabled,"busy actions");SetBusy(false);
            seconds.Text="1";check(picture.Source==null&&originalFrame==null&&processedFrame==null&&!compare.IsEnabled&&selection.Visibility==Visibility.Hidden,"time invalidates both previews");
            originalFrame=BitmapSource.Create(640,320,96,96,PixelFormats.Bgr32,null,new byte[640*320*4],640*4);canvas.Measure(new Size(640,360));canvas.Arrange(new Rect(0,0,640,360));ShowFrame(false);SetRectangle(10,10,40,20);var point=ToVideo(new Point(320,100));
            check(Math.Abs(point.X-160)<0.01&&Math.Abs(point.Y-45)<0.01&&Math.Abs(Canvas.GetTop(selection)-(20+10*320.0/180))<0.01,"non-square-pixel selection mapping");
            method.SelectedIndex=3;fields[0].Text="invalid";check(Filter()=="null"&&!fields[0].IsEnabled&&selection.Visibility==Visibility.Hidden,"compatible export ignores region and disables selection");
            SetBusy(true);check(!export.IsEnabled&&!open.IsEnabled,"compatible export busy guard");SetBusy(false);check(export.IsEnabled&&!fields[0].IsEnabled,"compatible mode stays ready without region controls");
            method.SelectedIndex=0;check(fields[0].IsEnabled,"returning to cleanup restores region controls");
            method.SelectedIndex=4;check(clipOptions.Visibility==Visibility.Visible&&fields[0].IsEnabled&&!fields[2].IsEnabled&&Region()[2]==90&&Region()[3]==160,"vertical crop and controls");
            MoveVerticalCrop(new Point(1000,1000));check(Region()[0]==230&&Region()[1]==20,"vertical crop clamps to far edge");
            MoveVerticalCrop(new Point(-100,-100));check(Region()[0]==0&&Region()[1]==0,"vertical crop clamps to near edge");
            verticalSize.SelectedIndex=1;check(Filter().Contains("scale=720:1280"),"vertical export size follows selection");
            method.SelectedIndex=0;check(clipOptions.Visibility==Visibility.Collapsed&&fields[2].IsEnabled,"leaving vertical restores cleanup controls");
            File.WriteAllText(report,checks+" offline editor control checks passed.");
        }
        public void Render(string file,string framePath){
            if(!string.IsNullOrEmpty(framePath)){
                var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(Path.GetFullPath(framePath));bitmap.EndInit();bitmap.Freeze();picture.Source=bitmap;originalFrame=bitmap;
                info=new VideoInfo{Width=bitmap.PixelWidth,Height=bitmap.PixelHeight,Duration=1.5,Audio=true};fileLabel.Text="Synthetic test video · demonstration preview";SetRectangle(info.Width*4/5,info.Height/10,info.Width/7,info.Height/7);clipEnd.Text="1.5";if(Mode()=="vertical")SetVerticalCrop();selection.Visibility=Mode()=="compatible"?Visibility.Hidden:Visibility.Visible;SetBusy(false);
            }
            var root=(Grid)window.Content;root.Margin=new Thickness(0);root.Width=992;root.Height=752;root.Measure(new Size(992,752));root.Arrange(new Rect(0,0,992,752));root.UpdateLayout();DrawSelection();
            var image=new RenderTargetBitmap(1040,800,96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(window.Background,null,new Rect(0,0,1040,800));dc.DrawRectangle(new VisualBrush(root){AutoLayoutContent=false,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,992,752)},null,new Rect(24,24,992,752));}image.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(file))encoder.Save(stream);
        }
    }
}
