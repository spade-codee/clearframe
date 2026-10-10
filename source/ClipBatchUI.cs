using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClearFrame {
    public partial class VideoCleanup {
        readonly List<NamedClip> savedClips=new List<NamedClip>();
        readonly TextBox clipName=new TextBox{Width=140,Text="Clip 1",MaxLength=80};
        readonly Button saveClip=new Button{Content="Save clip"},newClip=new Button{Content="New"},reviewClips=new Button{Content="Clips (0)…"};
        NamedClip editingClip;
        void SetupClipBatch(){
            clipOptions.Children.Add(Field("CLIP NAME",clipName,8));
            foreach(var button in new[]{saveClip,newClip,reviewClips}){button.Margin=new Thickness(0,17,6,0);clipOptions.Children.Add(button);}
            saveClip.Click+=(s,e)=>SaveClip();newClip.Click+=(s,e)=>{if(CanReplaceDraft())NewClip();};reviewClips.Click+=(s,e)=>ReviewClips();
        }
        void UpdateClipBatch(){clipName.IsEnabled=saveClip.IsEnabled=newClip.IsEnabled=!busy&&info!=null;reviewClips.IsEnabled=!busy&&savedClips.Count>0;reviewClips.Content="Clips ("+savedClips.Count+")…";saveClip.Content=editingClip==null?"Save clip":"Save changes";UpdateClipProject();}
        void NewClip(){ClipEdit(NewClipCore);} void NewClipCore(){editingClip=null;int n=1;while(savedClips.Any(c=>string.Equals(c.Name,"Clip "+n,StringComparison.OrdinalIgnoreCase)))n++;clipName.Text="Clip "+n;UpdateClipBatch();ResetDraftBaseline();}
        bool CanDiscardClips(){return (!ProjectDirty()&&!EditorDraftDirty())||MessageBox.Show(window,"Your clip list or editor fields have unsaved changes. Continue without saving them? Choose No, use Save clip / Save changes, then Save project. Local recovery keeps only its last completed snapshot. Exported videos are kept.","ClearFrame · Unsaved project",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;}
        bool CanReplaceDraft(){return !EditorDraftDirty()||MessageBox.Show(window,"These editor fields have not been added to the clip list. Replace them? Choose No, then Save clip / Save changes to keep this edit.","ClearFrame · Unsaved clip edit",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;}
        void ResetClipBatch(){ResetClipHistory();StopPlayback(false);player.Close();playbackInput=null;playerOpened=false;ResetRecovery();savedClips.Clear();projectPath=null;projectSnapshot=null;NewClip();}
        void SaveClip(){ClipEdit(SaveClipCore);} void SaveClipCore(){
            if(busy||info==null||Mode()!="vertical")return;
            try{
                var r=Region();var clip=new NamedClip{Name=clipName.Text,Start=DownloadOptions.ParseTime(clipStart.Text),End=DownloadOptions.ParseTime(clipEnd.Text),X=r[0],Y=r[1],Width=VerticalWidth()};
                ClipBatch.Validate(clip,info);
                if(savedClips.Any(c=>c!=editingClip&&string.Equals(c.Name,clip.Name,StringComparison.OrdinalIgnoreCase)))throw new Exception("Another clip already uses this name. Choose a unique name.");
                int index=editingClip==null?-1:savedClips.IndexOf(editingClip);
                if(index<0){if(savedClips.Count>=ClipBatch.Limit)throw new Exception("This batch already has 50 clips. Remove one before adding more.");savedClips.Add(clip);}else savedClips[index]=clip;
                editingClip=clip;UpdateClipBatch();ResetDraftBaseline();status.Text="Added “"+clip.Name+"” to the clip list. New adds another; Clips reviews/exports; Save project keeps the list for later.";
            }catch(Exception ex){status.Text=ex.Message;}
        }
        void LoadClip(NamedClip clip){ClipEdit(()=>LoadClipCore(clip));} void LoadClipCore(NamedClip clip){editingClip=clip;clipName.Text=clip.Name;clipStart.Text=clip.Start.ToString("0.###",CultureInfo.InvariantCulture);clipEnd.Text=clip.End.ToString("0.###",CultureInfo.InvariantCulture);verticalSize.SelectedIndex=clip.Width==720?1:0;SetRectangle(clip.X,clip.Y,0,0);ClearFrames();UpdateClipBatch();ResetDraftBaseline();status.Text="Editing “"+clip.Name+"”. Load a frame to preview, then Save changes to update the list.";}
        Window BatchWindow(out ListBox list,out TextBlock note){
            var dialog=new Window{Title="ClearFrame · Clip batch",Width=850,Height=490,MinWidth=760,MinHeight=420,Owner=window.IsVisible?window:null,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=window.Background,Foreground=window.Foreground,Resources=window.Resources,FontFamily=window.FontFamily};
            var grid=new Grid{Margin=new Thickness(24)};foreach(var height in new[]{GridLength.Auto,GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto})grid.RowDefinitions.Add(new RowDefinition{Height=height});dialog.Content=grid;
            grid.Children.Add(new TextBlock{Text="One source. Several moments.",FontSize=24,FontWeight=FontWeights.SemiBold});
            note=new TextBlock{Text="Exports run in this order. Complete clips are skipped on retry. Use Save project in the editor to keep this list for later. Save clip / Save changes before exporting or saving a project.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,16)};Grid.SetRow(note,1);grid.Children.Add(note);
            list=new ListBox{ItemsSource=savedClips,DisplayMemberPath="Summary",Background=new SolidColorBrush(Color.FromRgb(18,22,15)),Foreground=window.Foreground,BorderThickness=new Thickness(0),SelectedIndex=savedClips.Count>0?0:-1};Grid.SetRow(list,2);grid.Children.Add(list);
            var row=new WrapPanel{Margin=new Thickness(0,16,0,0)};Grid.SetRow(row,3);grid.Children.Add(row);
            var edit=new Button{Content="Edit selected"};var remove=new Button{Content="Remove"};var up=new Button{Content="Move up"};var down=new Button{Content="Move down"};var run=new Button{Content="Export pending clips…",Style=(Style)window.FindResource("Primary")};
            foreach(var button in new[]{edit,remove,up,down,run}){button.Margin=new Thickness(0,0,8,0);row.Children.Add(button);}
            var items=list;var message=note;
            var undo=new Button{Content="Undo",ToolTip="Ctrl+Z",Margin=new Thickness(0,0,8,0)};var redo=new Button{Content="Redo",ToolTip="Ctrl+Y"};row.Children.Add(undo);row.Children.Add(redo);
            Action refresh=()=>{var selected=items.SelectedItem;items.Items.Refresh();if(selected!=null&&savedClips.Contains(selected as NamedClip))items.SelectedItem=selected;else items.SelectedIndex=savedClips.Count>0?0:-1;UpdateClipHistory();undo.IsEnabled=undoClip.IsEnabled;redo.IsEnabled=redoClip.IsEnabled;run.IsEnabled=savedClips.Any(c=>c.State!="Complete");};
            undo.Click+=(s,e)=>{TravelClipHistory(-1);refresh();message.Text=status.Text;};redo.Click+=(s,e)=>{TravelClipHistory(1);refresh();message.Text=status.Text;};
            dialog.PreviewKeyDown+=(s,e)=>{if(ClipHistoryKey(e.Key,System.Windows.Input.Keyboard.Modifiers)){e.Handled=true;refresh();message.Text=status.Text;}};
            edit.Click+=(s,e)=>{var clip=items.SelectedItem as NamedClip;if(clip==null||!CanReplaceDraft())return;LoadClip(clip);dialog.Close();};
            remove.Click+=(s,e)=>{var clip=items.SelectedItem as NamedClip;if(clip==null)return;RemoveSavedClip(clip);refresh();message.Text="Removed from this clip list. Undo restores it. Any exported video is kept.";};
            Action<int> move=offset=>{int index=items.SelectedIndex,target=index+offset;if(index<0||target<0||target>=savedClips.Count)return;var clip=savedClips[index];MoveSavedClip(index,target);refresh();items.SelectedItem=clip;};up.Click+=(s,e)=>move(-1);down.Click+=(s,e)=>move(1);
            refresh();run.Click+=async(s,e)=>{dialog.Close();await ExportClipBatch();};return dialog;
        }
        void ReviewClips(){if(busy)return;ListBox list;TextBlock note;BatchWindow(out list,out note).ShowDialog();}
        async Task ExportClipBatch(){
            string folder;using(var dialog=new System.Windows.Forms.FolderBrowserDialog{Description="Choose a folder for your named MP4 clips",SelectedPath=Path.GetDirectoryName(input)}){if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;folder=dialog.SelectedPath;}
            SetBusy(true);progress.Value=0;cancellation=new CancellationTokenSource();int total=savedClips.Count(c=>c.State!="Complete");bool activeBatch=true;
            try{
                status.Text="Checking all pending clips and filenames before export…";
                await ClipBatch.Export(Tool("ffmpeg"),Tool("ffprobe"),input,info,savedClips,folder,cancellation.Token,(index,value)=>window.Dispatcher.BeginInvoke(new Action(()=>{if(!activeBatch)return;progress.Value=(index+value/100)/total*100;status.Text="Exporting clip "+(index+1)+" of "+total+" · "+value.ToString("0")+"%. Cancel keeps completed clips.";})));
                progress.Value=100;status.Text="Batch complete: "+total+" new clips exported and verified. Original video kept.";
            }catch(OperationCanceledException){status.Text="Batch cancelled. Completed clips are kept; choose Clips to review and retry the remaining clips.";}
            catch(Exception ex){status.Text="Batch stopped: "+ex.Message+" Completed clips are kept.";}
            finally{activeBatch=false;var last=savedClips.LastOrDefault(c=>c.State=="Complete");if(last!=null)outputFile=last.Output;cancellation.Dispose();cancellation=null;SetBusy(false);}
        }
        public void RenderClipBatch(string file){
            savedClips.Add(new NamedClip{Name="Opening hook",Start=12,End=28,X=662,Y=12,Width=1080});savedClips.Add(new NamedClip{Name="The best answer",Start=75,End=112,X=900,Y=12,Width=720});
            ListBox list;TextBlock note;var dialog=BatchWindow(out list,out note);var root=(Grid)dialog.Content;root.Margin=new Thickness(0);root.Width=802;root.Height=390;root.Measure(new Size(802,390));root.Arrange(new Rect(0,0,802,390));root.UpdateLayout();
            var image=new RenderTargetBitmap(850,438,96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(dialog.Background,null,new Rect(0,0,850,438));dc.DrawRectangle(new VisualBrush(root){AutoLayoutContent=false,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,802,390)},null,new Rect(24,24,802,390));}image.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(file))encoder.Save(stream);
        }
    }
}
