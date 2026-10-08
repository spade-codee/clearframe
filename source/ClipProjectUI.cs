using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ClearFrame {
    public partial class VideoCleanup {
        readonly Button openProject=new Button{Content="Open project…"},saveProject=new Button{Content="Save project…"};
        string projectPath,projectSnapshot;
        bool ProjectDirty(){return (savedClips.Count>0||projectSnapshot!=null)&&ClipProject.Snapshot(input,savedClips)!=projectSnapshot;}
        void SetupClipProject(DockPanel heading){
            foreach(var button in new[]{openProject,saveProject}){button.Margin=new Thickness(0,0,8,0);DockPanel.SetDock(button,Dock.Right);heading.Children.Insert(0,button);}
            openProject.Click+=async(s,e)=>await OpenClipProject();saveProject.Click+=async(s,e)=>await SaveClipProject();
        }
        void UpdateClipProject(){
            openProject.Visibility=saveProject.Visibility=Mode()=="vertical"?Visibility.Visible:Visibility.Collapsed;
            openProject.IsEnabled=!busy;saveProject.IsEnabled=!busy&&info!=null;
            saveProject.Content=ProjectDirty()?"Save project *":"Save project…";saveProject.ToolTip=projectPath??"Save your clip list to reopen later. Save clip first to include editor changes.";
        }
        async Task SaveClipProject(){
            if(busy||info==null)return;
            var dialog=new SaveFileDialog{Filter="ClearFrame clip project|*"+ClipProject.Extension,DefaultExt=ClipProject.Extension,AddExtension=true,OverwritePrompt=true,FileName=projectPath==null?Path.GetFileNameWithoutExtension(input)+ClipProject.Extension:Path.GetFileName(projectPath),InitialDirectory=Path.GetDirectoryName(projectPath??input)};
            if(dialog.ShowDialog(window)!=true)return;SetBusy(true);cancellation=new CancellationTokenSource();
            try{
                status.Text="Checking the source video and saving the project. Large videos may take a moment; Cancel stops the check.";
                var metadata=CleanupCore.ReadInfo(await Probe(input,cancellation.Token));
                var clips=savedClips.ToArray();var data=await Task.Run(()=>ClipProject.Capture(input,info,clips,cancellation.Token));ClipProject.VerifyMetadata(data,metadata);cancellation.Token.ThrowIfCancellationRequested();
                ClipProject.Save(dialog.FileName,data);projectPath=Path.GetFullPath(dialog.FileName);projectSnapshot=ClipProject.Snapshot(input,savedClips);status.Text="Project saved: "+Path.GetFileName(projectPath)+". Keep the source video; it is not embedded. Reopened clips start Ready.";
            }catch(OperationCanceledException){status.Text="Project save cancelled. Existing project and clip list kept.";}catch(Exception ex){status.Text="Project was not saved: "+ex.Message;}
            finally{cancellation.Dispose();cancellation=null;SetBusy(false);}
        }
        void ApplyClipProject(ClipProjectData project,string source,VideoInfo metadata,string path){
            input=source;info=metadata;ResetClipBatch();savedClips.AddRange(project.Clips.Select(c=>c.ToClip()));outputFile=null;progress.Value=0;
            fileLabel.Text=Path.GetFileName(source)+" · "+info.Width+" × "+info.Height+" · "+TimeSpan.FromSeconds(info.Duration).ToString(@"hh\:mm\:ss");
            if(savedClips.Count>0)LoadClip(savedClips[0]);else{ClearFrames();seconds.Text="0";clipStart.Text="0";clipEnd.Text=info.Duration.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture);SetVerticalCrop();NewClip();}
            projectPath=path.EndsWith(".bak",StringComparison.OrdinalIgnoreCase)?path.Substring(0,path.Length-4):path;projectSnapshot=ClipProject.Snapshot(input,savedClips);UpdateClipBatch();
            status.Text="Opened "+Path.GetFileName(path)+" · "+savedClips.Count+" clips ready. Load frame to preview. Existing exports stay protected; choose a new folder or names to export again.";
        }
        async Task OpenClipProject(){
            if(busy)return;var dialog=new OpenFileDialog{Filter="ClearFrame clip projects|*"+ClipProject.Extension+";*"+ClipProject.Extension+".bak|All files|*.*"};if(dialog.ShowDialog(window)!=true||!CanDiscardClips())return;
            SetBusy(true);cancellation=new CancellationTokenSource();
            try{
                var project=ClipProject.Read(dialog.FileName);string source=project.SourcePath;
                if(!File.Exists(source)){string adjacent=Path.Combine(Path.GetDirectoryName(dialog.FileName),Path.GetFileName(source));source=File.Exists(adjacent)?adjacent:null;}
                status.Text="Checking the project's source video. Large videos may take a moment; Cancel stops the check.";
                bool matches=source!=null&&await Task.Run(()=>ClipProject.Matches(project,source,cancellation.Token));
                if(!matches){
                    var locate=new OpenFileDialog{Title="Locate the original video for "+Path.GetFileName(project.SourcePath),Filter="Video files|*.mp4;*.mkv;*.mov;*.webm;*.avi;*.m4v|All files|*.*"};
                    if(locate.ShowDialog(window)!=true){status.Text="Project open cancelled. Your current clip list is kept.";return;}source=locate.FileName;
                    status.Text="Verifying the selected source matches the saved project…";
                    if(!await Task.Run(()=>ClipProject.Matches(project,source,cancellation.Token)))throw new Exception("That file does not match the original video. Choose the unchanged original, even if it has been renamed or moved.");
                }
                var metadata=CleanupCore.ReadInfo(await Probe(source,cancellation.Token));ClipProject.VerifyMetadata(project,metadata);cancellation.Token.ThrowIfCancellationRequested();ApplyClipProject(project,source,metadata,Path.GetFullPath(dialog.FileName));
            }catch(OperationCanceledException){status.Text="Project open cancelled. Your current clip list is kept.";}catch(Exception ex){status.Text="Could not open project: "+ex.Message+" Your current clip list is kept.";}
            finally{cancellation.Dispose();cancellation=null;SetBusy(false);}
        }
    }
}
