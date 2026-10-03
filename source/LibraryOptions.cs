using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClearFrame {
    public partial class MainWindow {
        double clipStart,clipEnd;
        string SubtitleLanguage(){return DownloadOptions.LanguageCodes[Math.Max(0,C<ComboBox>("SubtitleLanguageBox").SelectedIndex)];}
        void SetupLibraryOptions(){
            var languages=C<ComboBox>("SubtitleLanguageBox");foreach(var name in DownloadOptions.LanguageNames)languages.Items.Add(name);languages.SelectedIndex=0;
            if(preferences!=null){int language=Array.IndexOf(DownloadOptions.LanguageCodes,Core.S(preferences,"subtitleLanguage"));languages.SelectedIndex=language<0?0:language;C<ComboBox>("SortBox").SelectedIndex=Index("sort",6);}
            languages.SelectionChanged+=(s,e)=>Save();C<ComboBox>("SortBox").SelectionChanged+=(s,e)=>{UpdateCount();Save();};
            C<Button>("ClipButton").Click+=(s,e)=>ClipDialog();
        }
        void ResetClip(){clipStart=clipEnd=0;C<Button>("ClipButton").Content="Clip range…";UpdateFormatControls();}
        void ClipDialog(){
            if(preview==null||selection==null){Status("Check a video link before choosing a clip range.");return;}
            double duration=Core.N(preview,"duration");if(duration<=0||double.IsNaN(duration)||double.IsInfinity(duration)){Status("Clipping requires a known source duration.");return;}
            BuildClipDialog(duration).ShowDialog();
        }
        Window BuildClipDialog(double duration){
            var dialog=new Window{Title="ClearFrame · Clip range",Width=590,Height=385,ResizeMode=ResizeMode.NoResize,Owner=w.IsVisible?w:null,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=w.Background,Foreground=w.Foreground,Resources=w.Resources,FontFamily=w.FontFamily};
            var panel=new StackPanel{Margin=new Thickness(24)};panel.Children.Add(new TextBlock{Text="Save just the part you want.",FontSize=23,FontWeight=FontWeights.SemiBold});
            panel.Children.Add(new TextBlock{Text="Source: "+Job.TimeLabel(duration)+". Enter seconds, mm:ss or hh:mm:ss. A clip must be at least one second long.",Margin=new Thickness(0,12,0,14)});
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition());var from=new TextBox{Text=clipStart.ToString("0.###",CultureInfo.InvariantCulture),Margin=new Thickness(0,0,8,0),ToolTip="Clip start"};var to=new TextBox{Text=(clipEnd>0?clipEnd:duration).ToString("0.###",CultureInfo.InvariantCulture),ToolTip="Clip end"};var first=new StackPanel();first.Children.Add(new TextBlock{Text="START"});first.Children.Add(from);var last=new StackPanel();last.Children.Add(new TextBlock{Text="END"});last.Children.Add(to);Grid.SetColumn(last,1);row.Children.Add(first);row.Children.Add(last);panel.Children.Add(row);
            var notice=new TextBlock{Text="Downloads the full source first, then re-encodes the selected range. Needs extra time and disk space. Video clips support SDR only. Applies to this checked link; batches and playlist picks remain full-length.",Margin=new Thickness(0,14,0,14)};panel.Children.Add(notice);
            var buttons=new StackPanel{Orientation=Orientation.Horizontal};var full=new Button{Content="Use full video",Margin=new Thickness(0,0,10,0)};var apply=new Button{Content="Use clip",Style=(Style)w.FindResource("Primary")};buttons.Children.Add(full);buttons.Children.Add(apply);panel.Children.Add(buttons);dialog.Content=panel;
            full.Click+=(s,e)=>{ResetClip();RefreshPreview();dialog.Close();};apply.Click+=(s,e)=>{try{double start=DownloadOptions.ParseTime(from.Text),end=DownloadOptions.ParseTime(to.Text);DownloadOptions.ValidateClip(start,end,duration);clipStart=start;clipEnd=end;C<Button>("ClipButton").Content="Clip "+start.ToString("0.###",CultureInfo.InvariantCulture)+"–"+end.ToString("0.###",CultureInfo.InvariantCulture)+"s";RefreshPreview();dialog.Close();}catch(Exception ex){notice.Text=ex.Message;}};return dialog;
        }
        public void RenderClipDialog(string path){clipStart=62;clipEnd=90;var dialog=BuildClipDialog(372);var root=(StackPanel)dialog.Content;root.Margin=new Thickness(0);root.Width=542;root.Height=322;root.Measure(new Size(542,322));root.Arrange(new Rect(0,0,542,322));root.UpdateLayout();var bitmap=new RenderTargetBitmap(590,370,96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(dialog.Background,null,new Rect(0,0,590,370));dc.DrawRectangle(new VisualBrush(root){AutoLayoutContent=false,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,542,322)},null,new Rect(24,24,542,322));}bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);}
        void ApplyLibrarySort(ICollectionView view){
            var list=view as ListCollectionView;if(list==null)return;int mode=C<ComboBox>("SortBox").SelectedIndex;
            list.CustomSort=mode<=0?null:new JobComparer(mode,jobs.ToList());C<Button>("UpButton").IsEnabled=C<Button>("DownButton").IsEnabled=mode<=0;
        }
        string SaveCaptions(Job job,string stage,string source,string target){
            if(!job.Subtitles)return "";int saved=0,found=0;bool warning=false;
            try{string stem=Path.GetFileNameWithoutExtension(source);foreach(string sub in Directory.GetFiles(stage,"*.srt")){
                string name=Path.GetFileName(sub);if(!name.StartsWith(stem+".",StringComparison.OrdinalIgnoreCase))continue;found++;
                try{string text=File.ReadAllText(sub);if(job.IsClip)text=DownloadOptions.ClipSubtitles(text,job.ClipStart,job.ClipEnd);if(!string.IsNullOrWhiteSpace(text)){string destination=Path.Combine(job.Folder,Path.GetFileNameWithoutExtension(target)+name.Substring(stem.Length));using(var writer=new StreamWriter(new FileStream(destination,FileMode.CreateNew,FileAccess.Write),new UTF8Encoding(false)))writer.Write(text);saved++;}File.Delete(sub);}catch(Exception ex){warning=true;Log(job,"Caption warning: "+ex.Message);}
            }}catch(Exception ex){warning=true;Log(job,"Caption warning: "+ex.Message);}
            if(warning)return " · Caption save warning; see Details";if(saved==0)return job.IsClip&&found>0?" · No captions in the selected range":" · Subtitles unavailable ("+DownloadOptions.Language(job.SubtitleLanguage)+")";return " · "+saved+" caption file(s)";
        }
        sealed class JobComparer:IComparer {
            readonly int mode;readonly Dictionary<Job,int> position;
            public JobComparer(int mode,List<Job> jobs){this.mode=mode;position=jobs.Select((job,index)=>new{job,index}).ToDictionary(x=>x.job,x=>x.index);}
            public int Compare(object x,object y){var a=(Job)x;var b=(Job)y;int result=0;
                if(mode==1)result=b.AddedUtcTicks.CompareTo(a.AddedUtcTicks);else if(mode==2)result=a.AddedUtcTicks.CompareTo(b.AddedUtcTicks);else if(mode==3)result=StringComparer.OrdinalIgnoreCase.Compare(a.Title,b.Title);else if(mode==4)result=SizeForSort(b).CompareTo(SizeForSort(a));else if(mode==5)result=b.OutputDuration.CompareTo(a.OutputDuration);
                int ai,bi;return result!=0?result:(position.TryGetValue(a,out ai)?ai:int.MaxValue).CompareTo(position.TryGetValue(b,out bi)?bi:int.MaxValue);
            }
            static long SizeForSort(Job job){return job.SavedBytes>0?job.SavedBytes:job.EstimatedBytes;}
        }
    }
}
