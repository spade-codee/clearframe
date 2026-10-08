using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ClearFrame {
    public partial class VideoCleanup {
        readonly Button recoverClips=new Button{Content="Recover…"};
        readonly TextBlock recoveryNote=new TextBlock{Text="Recovery starts after opening a video.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0),FontSize=11};
        readonly DispatcherTimer recoveryTick=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
        string recoveryDirectory,recoveryPath,recoverySignature,draftBaseline;
        ClipProjectData recoveryIdentity;
        DateTime recoverySourceWrite;
        CancellationTokenSource recoveryCancellation;
        bool recoveryWriting,recoveryClosed;
        int recoveryGeneration;
        ClipDraft CaptureDraft(){return new ClipDraft{Name=clipName.Text,Start=clipStart.Text,End=clipEnd.Text,X=fields[0].Text,Y=fields[1].Text,Frame=seconds.Text,Size=verticalSize.SelectedIndex,EditingIndex=editingClip==null?-1:savedClips.IndexOf(editingClip)};}
        string DraftSignature(){var draft=CaptureDraft();draft.Frame="";return ClipRecovery.DraftSnapshot(draft);}
        void ResetDraftBaseline(){draftBaseline=DraftSignature();ScheduleRecovery();}
        bool EditorDraftDirty(){return info!=null&&draftBaseline!=null&&DraftSignature()!=draftBaseline;}
        void SetupRecovery(DockPanel heading,StackPanel rows,string directory){
            recoveryDirectory=string.IsNullOrEmpty(directory)?null:Path.Combine(directory,"clip-recovery");DockPanel.SetDock(recoverClips,Dock.Right);recoverClips.Margin=new Thickness(0,0,8,0);heading.Children.Insert(0,recoverClips);rows.Children.Add(recoveryNote);
            foreach(var field in new[]{clipName,clipStart,clipEnd,seconds,fields[0],fields[1]})field.TextChanged+=(s,e)=>ScheduleRecovery();verticalSize.SelectionChanged+=(s,e)=>ScheduleRecovery();
            recoverClips.Click+=async(s,e)=>await RecoverClips();recoveryTick.Tick+=async(s,e)=>{recoveryTick.Stop();if(busy||recoveryWriting){ScheduleRecovery();return;}await AutosaveRecovery();};
            window.Closed+=(s,e)=>{recoveryClosed=true;recoveryTick.Stop();if(recoveryCancellation!=null)recoveryCancellation.Cancel();};
        }
        void ScheduleRecovery(){
            recoverClips.Visibility=recoveryNote.Visibility=Mode()=="vertical"?Visibility.Visible:Visibility.Collapsed;recoverClips.IsEnabled=!busy;
            if(recoveryDirectory==null||recoveryClosed||info==null||string.IsNullOrEmpty(input)||Mode()!="vertical")return;recoveryTick.Stop();recoveryTick.Start();
        }
        void ResetRecovery(){
            recoveryGeneration++;if(recoveryCancellation!=null)recoveryCancellation.Cancel();recoveryIdentity=null;recoverySignature=null;recoveryPath=null;draftBaseline=null;recoveryTick.Stop();recoveryNote.Text="Recovery will keep this session locally after a short idle period.";
        }
        async Task AutosaveRecovery(){
            if(recoveryDirectory==null||recoveryClosed||busy||recoveryWriting||info==null||string.IsNullOrEmpty(input)||Mode()!="vertical")return;
            var draft=CaptureDraft();string signature=ClipProject.Snapshot(input,savedClips)+"|"+ClipRecovery.DraftSnapshot(draft);if(signature==recoverySignature)return;
            int generation=recoveryGeneration;string source=input;var sourceInfo=info;var recipes=ClipProject.Recipes(savedClips);var cached=recoveryIdentity;DateTime cachedWrite=recoverySourceWrite,checkedWrite=DateTime.MinValue;
            recoveryWriting=true;var tokenSource=new CancellationTokenSource();recoveryCancellation=tokenSource;recoveryNote.Text="Preparing local recovery snapshot…";
            try{
                var identity=await Task.Run(()=>{
                    var file=new FileInfo(source);checkedWrite=file.LastWriteTimeUtc;
                    if(cached!=null){if(file.Length!=cached.SourceLength||file.LastWriteTimeUtc!=cachedWrite)throw new IOException("The source changed. Reopen the original video before recovery can continue.");return cached;}
                    return ClipProject.Capture(source,sourceInfo,recipes.Select(c=>c.ToClip()),tokenSource.Token);
                });
                if(cached==null){var actual=CleanupCore.ReadInfo(await Probe(source,tokenSource.Token));ClipProject.VerifyMetadata(identity,actual);}
                tokenSource.Token.ThrowIfCancellationRequested();if(recoveryClosed||generation!=recoveryGeneration)return;
                var checkedFile=new FileInfo(source);if(checkedFile.Length!=identity.SourceLength||checkedFile.LastWriteTimeUtc!=checkedWrite)throw new IOException("The source changed during recovery preparation. Reopen it before continuing.");
                var data=new RecoveryData{Kind="ClearFrame clip recovery",Version=1,SavedUtc=DateTime.UtcNow.ToString("o"),Project=new ClipProjectData{Kind=identity.Kind,Version=identity.Version,SourcePath=identity.SourcePath,SourceSha256=identity.SourceSha256,SourceLength=identity.SourceLength,SourceInfo=identity.SourceInfo,Clips=recipes},Draft=draft};
                if(recoveryPath==null)recoveryPath=Path.Combine(recoveryDirectory,"session-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+ClipRecovery.Extension);
                ClipRecovery.Save(recoveryPath,data);recoveryIdentity=identity;recoverySourceWrite=checkedWrite;recoverySignature=signature;recoveryNote.Text="Recovery saved locally at "+DateTime.Now.ToString("HH:mm:ss")+" · includes unfinished editor fields. Save project for a portable copy.";
            }catch(OperationCanceledException){}catch(Exception ex){if(!recoveryClosed&&generation==recoveryGeneration)recoveryNote.Text="Recovery could not be saved: "+ex.Message+" Use Save project to keep a manual copy.";}
            finally{recoveryWriting=false;if(recoveryCancellation==tokenSource)recoveryCancellation=null;tokenSource.Dispose();}
        }
        async Task<string> LocateProjectSource(ClipProjectData project,string document,CancellationToken token){
            string source=project.SourcePath;if(!File.Exists(source)){string adjacent=Path.Combine(Path.GetDirectoryName(document),Path.GetFileName(source));source=File.Exists(adjacent)?adjacent:null;}
            bool matches=source!=null&&await Task.Run(()=>ClipProject.Matches(project,source,token));
            if(!matches){var locate=new OpenFileDialog{Title="Locate the unchanged original: "+Path.GetFileName(project.SourcePath),Filter="Video files|*.mp4;*.mkv;*.mov;*.webm;*.avi;*.m4v|All files|*.*"};if(locate.ShowDialog(window)!=true)return null;source=locate.FileName;if(!await Task.Run(()=>ClipProject.Matches(project,source,token)))throw new IOException("That file does not match the original video.");}return source;
        }
        void ApplyRecovery(RecoveryData data,string source,VideoInfo metadata){
            ApplyClipProject(data.Project,source,metadata,"recovered"+ClipProject.Extension);projectPath=null;projectSnapshot=null;
            var d=data.Draft;if(d.EditingIndex<0)NewClip();else LoadClip(savedClips[d.EditingIndex]);
            // Establish the committed baseline before restoring raw, possibly unfinished inputs.
            ResetDraftBaseline();clipName.Text=d.Name;clipStart.Text=d.Start;clipEnd.Text=d.End;verticalSize.SelectedIndex=d.Size;fields[0].Text=d.X;fields[1].Text=d.Y;seconds.Text=d.Frame;ClearFrames();UpdateClipBatch();
            status.Text="Recovery restored, including unfinished fields. Review them, Save clip / Save changes, then Save project. No exports have started.";
        }
        async Task RecoverClips(){
            if(busy)return;if(recoveryDirectory==null||!Directory.Exists(recoveryDirectory)){status.Text="No local recovery snapshots yet. Open a video and edit a clip to create one.";return;}
            var dialog=new Window{Title="ClearFrame · Recover clip work",Width=760,Height=430,Owner=window,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=window.Background,Foreground=window.Foreground,Resources=window.Resources,FontFamily=window.FontFamily};
            var grid=new Grid{Margin=new Thickness(24)};grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});dialog.Content=grid;
            grid.Children.Add(new TextBlock{Text="Choose a local snapshot. Source verification happens before restoring.\nYour manual project files and exported videos are kept.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)});
            var list=new ListBox{Background=window.Background,Foreground=window.Foreground};Grid.SetRow(list,1);grid.Children.Add(list);
            foreach(string path in Directory.GetFiles(recoveryDirectory,"*"+ClipRecovery.Extension+"*").Where(p=>p.EndsWith(ClipRecovery.Extension,StringComparison.OrdinalIgnoreCase)||p.EndsWith(ClipRecovery.Extension+".bak",StringComparison.OrdinalIgnoreCase)).OrderByDescending(File.GetLastWriteTimeUtc).Take(100)){
                try{var data=ClipRecovery.Read(path);list.Items.Add(new ListBoxItem{Content=DateTime.Parse(data.SavedUtc).ToLocalTime().ToString("g")+" · "+Path.GetFileName(data.Project.SourcePath)+" · "+data.Project.Clips.Count+" clips"+(path.EndsWith(".bak")?" · previous snapshot":""),Tag=path,ToolTip=path});}catch{list.Items.Add(new ListBoxItem{Content="Unreadable snapshot: "+Path.GetFileName(path),IsEnabled=false});}
            }
            string selected=null;var buttons=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0)};var restore=new Button{Content="Restore selected",Style=(Style)window.FindResource("Primary")};var folder=new Button{Content="Open recovery folder",Margin=new Thickness(8,0,0,0)};buttons.Children.Add(restore);buttons.Children.Add(folder);Grid.SetRow(buttons,2);grid.Children.Add(buttons);
            restore.Click+=(s,e)=>{var item=list.SelectedItem as ListBoxItem;if(item==null||item.Tag==null)return;selected=(string)item.Tag;dialog.Close();};folder.Click+=(s,e)=>{try{System.Diagnostics.Process.Start("explorer.exe",Core.Quote(recoveryDirectory));}catch(Exception ex){status.Text=ex.Message;}};dialog.ShowDialog();if(selected==null||!CanDiscardClips())return;
            SetBusy(true);cancellation=new CancellationTokenSource();try{var data=ClipRecovery.Read(selected);status.Text="Checking the recovery source…";string source=await LocateProjectSource(data.Project,selected,cancellation.Token);if(source==null){status.Text="Recovery cancelled. Current work kept.";return;}var metadata=CleanupCore.ReadInfo(await Probe(source,cancellation.Token));ClipProject.VerifyMetadata(data.Project,metadata);cancellation.Token.ThrowIfCancellationRequested();ApplyRecovery(data,source,metadata);}
            catch(OperationCanceledException){status.Text="Recovery cancelled. Current work kept.";}catch(Exception ex){status.Text="Recovery could not be opened: "+ex.Message+" Current work kept.";}finally{cancellation.Dispose();cancellation=null;SetBusy(false);}
        }
        public async Task CheckAutosaveFlow(string source,string report){
            Directory.CreateDirectory(Path.GetDirectoryName(recoveryDirectory));
            int checks=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);checks++;};input=source;info=CleanupCore.ReadInfo(await Probe(source,CancellationToken.None));method.SelectedIndex=4;SetVerticalCrop();clipStart.Text="0";clipEnd.Text="1";clipName.Text="Committed";seconds.Text="0";SetBusy(false);SaveClip();recoveryTick.Stop();
            string manual=Path.Combine(Path.GetDirectoryName(recoveryDirectory),"manual.cfclips.json");ClipProject.Save(manual,ClipProject.Capture(source,info,savedClips,CancellationToken.None));string manualBytes=File.ReadAllText(manual);
            await AutosaveRecovery();check(recoveryPath!=null&&File.Exists(recoveryPath)&&ClipRecovery.Read(recoveryPath).Project.Clips.Count==1,"automatic snapshot persists clip list");string firstPath=recoveryPath;
            clipStart.Text="1.";clipEnd.Text="";fields[0].Text="-";recoveryTick.Stop();await AutosaveRecovery();check(ClipRecovery.Read(firstPath).Draft.Start=="1."&&ClipRecovery.Read(firstPath).Draft.X=="-"&&ClipRecovery.Read(firstPath+".bak").Draft.Start=="0","automatic snapshot preserves incomplete fields and prior backup");
            clipName.Text="Changed draft";recoveryTick.Stop();using(var locked=new FileStream(firstPath,FileMode.Open,FileAccess.Read,FileShare.None)){await AutosaveRecovery();}check(recoveryNote.Text.Contains("could not be saved")&&ClipRecovery.Read(firstPath).Draft.Name=="Committed","write failure visible and prior snapshot preserved");
            check(File.ReadAllText(manual)==manualBytes,"autosave never writes manual project");
            var recovered=ClipRecovery.Read(firstPath);ApplyRecovery(recovered,source,info);recoveryTick.Stop();check(clipStart.Text=="1."&&clipEnd.Text==""&&fields[0].Text=="-"&&savedClips[0].Start==0&&savedClips[0].State=="Ready","recovery applies unfinished inputs without changing valid recipe");
            int files=Directory.GetFiles(recoveryDirectory).Length;var pending=AutosaveRecovery();ResetRecovery();recoveryTick.Stop();await pending;check(Directory.GetFiles(recoveryDirectory).Length==files,"source reset cancels pending recovery generation");
            timeline.Value=0.75;seekTick.Stop();await Frame(false);check(originalFrame!=null&&Math.Abs(double.Parse(seconds.Text,System.Globalization.CultureInfo.InvariantCulture)-0.75)<0.001,"timeline seeking generates a real FFmpeg still frame");
            timeline.Value=info.Duration;seekTick.Stop();await Frame(false);check(originalFrame!=null,"timeline end seek retains the final available video frame");
            recoveryTick.Stop();seekTick.Stop();File.WriteAllText(report,checks+" asynchronous autosave/timeline checks passed using synthetic media. No desktop window or Windows media playback was opened.");
        }
    }
}
