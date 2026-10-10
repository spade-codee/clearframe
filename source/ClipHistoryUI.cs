using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ClearFrame {
    public partial class VideoCleanup {
        sealed class ClipEditState {
            // Recipes are replaced, never edited in place. Keeping their identities lets
            // completed exports stay complete even when an older edit is restored.
            public NamedClip[] Clips;
            public ClipDraft Draft;
            public string Baseline, Signature;
        }
        const int ClipHistoryLimit=100;
        readonly List<ClipEditState> clipHistory=new List<ClipEditState>();
        readonly DispatcherTimer historyTick=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(600)};
        readonly Button undoClip=new Button{Content="Undo",ToolTip="Undo clip edit (Ctrl+Z)"},redoClip=new Button{Content="Redo",ToolTip="Redo clip edit (Ctrl+Y or Ctrl+Shift+Z)"};
        readonly WrapPanel historyRow=new WrapPanel{Margin=new Thickness(0,8,0,0)};
        int historyIndex=-1,historyActionDepth;
        bool historyApplying,historyReady;
        bool HistoryActive(){return historyReady&&!historyApplying&&historyActionDepth==0&&!busy&&info!=null&&Mode()=="vertical";}
        ClipEditState CaptureClipEdit(){
            var draft=CaptureDraft();string frame=draft.Frame;draft.Frame="";string signature=ClipRecovery.DraftSnapshot(draft);draft.Frame=frame;
            return new ClipEditState{Clips=savedClips.ToArray(),Draft=draft,Baseline=draftBaseline,Signature=signature};
        }
        bool SameClipEdit(ClipEditState a,ClipEditState b){return a.Signature==b.Signature&&a.Baseline==b.Baseline&&a.Clips.SequenceEqual(b.Clips);}
        void SetupClipHistory(StackPanel rows){
            undoClip.Margin=new Thickness(0,0,6,0);redoClip.Margin=new Thickness(0,0,12,0);historyRow.Children.Add(undoClip);historyRow.Children.Add(redoClip);
            historyRow.Children.Add(new TextBlock{Text="Clip edits · Ctrl+Z / Ctrl+Y · this session",VerticalAlignment=VerticalAlignment.Center,FontSize=11});rows.Children.Add(historyRow);
            foreach(var field in new[]{clipName,clipStart,clipEnd,fields[0],fields[1]}){field.TextChanged+=(s,e)=>ScheduleClipHistory();field.LostKeyboardFocus+=(s,e)=>CommitClipHistory();}
            verticalSize.SelectionChanged+=(s,e)=>ScheduleClipHistory();historyTick.Tick+=(s,e)=>{if(!selecting)CommitClipHistory();};
            undoClip.Click+=(s,e)=>TravelClipHistory(-1);redoClip.Click+=(s,e)=>TravelClipHistory(1);
            window.PreviewKeyDown+=(s,e)=>{if(ClipHistoryKey(e.Key,Keyboard.Modifiers))e.Handled=true;};
            window.Closed+=(s,e)=>{historyTick.Stop();clipHistory.Clear();historyReady=false;};
        }
        bool ClipHistoryKey(Key key,ModifierKeys modifiers){
            if(Mode()!="vertical")return false;
            bool undo=key==Key.Z&&modifiers==ModifierKeys.Control;
            bool redo=(key==Key.Y&&modifiers==ModifierKeys.Control)||(key==Key.Z&&modifiers==(ModifierKeys.Control|ModifierKeys.Shift));
            if(undo||redo)TravelClipHistory(undo?-1:1);return undo||redo;
        }
        void ResetClipHistory(){historyTick.Stop();clipHistory.Clear();historyIndex=-1;historyReady=false;UpdateClipHistory();}
        void InitializeClipHistory(){
            if(!historyReady&&!busy&&info!=null&&Mode()=="vertical"){clipHistory.Add(CaptureClipEdit());historyIndex=0;historyReady=true;}UpdateClipHistory();
        }
        void UpdateClipHistory(){
            historyRow.Visibility=Mode()=="vertical"?Visibility.Visible:Visibility.Collapsed;
            bool pending=HistoryActive()&&historyIndex>=0&&!SameClipEdit(clipHistory[historyIndex],CaptureClipEdit());
            undoClip.IsEnabled=HistoryActive()&&(historyIndex>0||pending);redoClip.IsEnabled=HistoryActive()&&!pending&&historyIndex+1<clipHistory.Count;
        }
        void ScheduleClipHistory(){if(!HistoryActive())return;historyTick.Stop();historyTick.Start();UpdateClipHistory();}
        void CommitClipHistory(){
            historyTick.Stop();if(!HistoryActive())return;var state=CaptureClipEdit();
            if(!SameClipEdit(clipHistory[historyIndex],state)){
                clipHistory.RemoveRange(historyIndex+1,clipHistory.Count-historyIndex-1);clipHistory.Add(state);
                if(clipHistory.Count>ClipHistoryLimit+1)clipHistory.RemoveAt(0);historyIndex=clipHistory.Count-1;
            }UpdateClipHistory();
        }
        void ClipEdit(Action action){
            bool record=HistoryActive();if(record)CommitClipHistory();historyActionDepth++;
            try{action();}finally{historyActionDepth--;if(record)CommitClipHistory();}
        }
        void TravelClipHistory(int direction){
            if(!HistoryActive())return;CommitClipHistory();int target=historyIndex+direction;if(target<0||target>=clipHistory.Count)return;
            StopPlayback(false);seekTick.Stop();var state=clipHistory[target];historyApplying=true;
            try{
                savedClips.Clear();savedClips.AddRange(state.Clips);var d=state.Draft;editingClip=d.EditingIndex<0?null:savedClips[d.EditingIndex];
                clipName.Text=d.Name;clipStart.Text=d.Start;clipEnd.Text=d.End;verticalSize.SelectedIndex=d.Size;fields[0].Text=d.X;fields[1].Text=d.Y;seconds.Text=d.Frame;draftBaseline=state.Baseline;
                ClearFrames();UpdateClipBatch();UpdateTimeline();historyIndex=target;
            }finally{historyApplying=false;}
            UpdateClipHistory();ScheduleRecovery();status.Text=(direction<0?"Undid":"Redid")+" clip edit. Load a frame to preview. Project files and exported videos are unchanged.";
        }
        void RemoveSavedClip(NamedClip clip){ClipEdit(()=>{savedClips.Remove(clip);if(editingClip==clip)NewClip();UpdateClipBatch();});}
        void MoveSavedClip(int index,int target){if(index<0||target<0||index>=savedClips.Count||target>=savedClips.Count)return;ClipEdit(()=>{var clip=savedClips[index];savedClips.RemoveAt(index);savedClips.Insert(target,clip);UpdateClipBatch();});}

        public void CheckClipHistory(string report){
            int checks=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("Undo check failed: "+name);checks++;};
            info=new VideoInfo{Width=320,Height=180,Duration=10};input="history-source.mp4";method.SelectedIndex=4;SetVerticalCrop();clipStart.Text="0";clipEnd.Text="2";clipName.Text="First";ResetDraftBaseline();ResetClipHistory();InitializeClipHistory();
            check(!undoClip.IsEnabled&&!redoClip.IsEnabled,"new session has no history");
            clipStart.Text="1.";clipEnd.Text="";fields[0].Text="-";check(undoClip.IsEnabled,"unfinished edits immediately enable undo");TravelClipHistory(-1);
            check(clipStart.Text=="0"&&clipEnd.Text=="2"&&fields[0].Text!="-"&&!EditorDraftDirty(),"undo pending raw inputs restores clean baseline");TravelClipHistory(1);
            check(clipStart.Text=="1."&&clipEnd.Text==""&&fields[0].Text=="-"&&EditorDraftDirty(),"redo preserves incomplete inputs");TravelClipHistory(-1);
            seconds.Text="1";CommitClipHistory();check(!undoClip.IsEnabled&&redoClip.IsEnabled,"frame navigation does not consume or branch history");
            clipName.Text="Branch";TravelClipHistory(1);check(!redoClip.IsEnabled&&clipName.Text=="Branch","new edit discards redo branch");
            SaveClip();check(savedClips.Count==1,"save records a clip");TravelClipHistory(-1);check(savedClips.Count==0&&clipName.Text=="Branch"&&EditorDraftDirty(),"undo add restores unsaved draft");TravelClipHistory(1);check(savedClips.Count==1&&!EditorDraftDirty()&&editingClip==savedClips[0],"redo add restores selection and baseline");
            var first=savedClips[0];first.State="Complete";first.Output="kept.mp4";clipEnd.Text="3";SaveClip();var revision=savedClips[0];check(revision!=first&&revision.State=="Ready","changed recipe is a pending revision");TravelClipHistory(-1);
            check(savedClips[0]==first&&first.State=="Complete"&&first.Output=="kept.mp4"&&clipEnd.Text=="3"&&EditorDraftDirty(),"undo save preserves finished export and unfinished draft");TravelClipHistory(1);check(savedClips[0]==revision&&revision.State=="Ready","redo revision stays pending");
            NewClip();clipName.Text="Second";SaveClip();var second=savedClips[1];MoveSavedClip(1,0);check(!EditorDraftDirty(),"reordering does not dirty unchanged editor fields");TravelClipHistory(-1);check(savedClips[1]==second&&editingClip==second,"undo reorder keeps editing identity");TravelClipHistory(1);check(savedClips[0]==second,"redo reorder");
            RemoveSavedClip(second);TravelClipHistory(-1);check(savedClips.Count==2&&editingClip==second&&clipName.Text=="Second","undo removal restores clip and editor");TravelClipHistory(1);check(savedClips.Count==1&&editingClip==null,"redo removal");
            projectSnapshot=ClipProject.Snapshot(input,savedClips);TravelClipHistory(-1);check(ProjectDirty(),"undo after project save marks changed recipes dirty");TravelClipHistory(1);check(!ProjectDirty(),"redo returns to manually saved recipes");
            SetBusy(true);int index=historyIndex;TravelClipHistory(-1);check(historyIndex==index&&!undoClip.IsEnabled&&!redoClip.IsEnabled,"busy operation blocks undo");SetBusy(false);
            revision.State="Complete";revision.Output="new-export.mp4";TravelClipHistory(-1);TravelClipHistory(1);check(savedClips[0].State=="Complete"&&savedClips[0].Output=="new-export.mp4","later export status survives older snapshots");
            clipName.Text="Keyboard";CommitClipHistory();check(ClipHistoryKey(Key.Z,ModifierKeys.Control)&&clipName.Text!="Keyboard","Ctrl+Z uses editor history");check(ClipHistoryKey(Key.Z,ModifierKeys.Control|ModifierKeys.Shift)&&clipName.Text=="Keyboard","Ctrl+Shift+Z redoes");ClipHistoryKey(Key.Z,ModifierKeys.Control);check(ClipHistoryKey(Key.Y,ModifierKeys.Control)&&clipName.Text=="Keyboard","Ctrl+Y redoes");check(!ClipHistoryKey(Key.Z,ModifierKeys.Control|ModifierKeys.Alt),"unrelated shortcut untouched");
            ListBox list;TextBlock note;var dialog=BatchWindow(out list,out note);var buttons=(WrapPanel)((Grid)dialog.Content).Children[3];list.SelectedIndex=0;((Button)buttons.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(list.Items.Count==0,"batch remove refreshes list");((Button)buttons.Children[5]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(list.Items.Count==1&&savedClips[0]==revision,"batch undo restores visible list");((Button)buttons.Children[6]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(list.Items.Count==0,"batch redo refreshes visible list");dialog.Close();
            int oldX=int.Parse(fields[0].Text),oldY=int.Parse(fields[1].Text),oldSize=verticalSize.SelectedIndex;MoveVerticalCrop(new Point(300,170));verticalSize.SelectedIndex=oldSize==0?1:0;CommitClipHistory();TravelClipHistory(-1);check(fields[0].Text==oldX.ToString()&&fields[1].Text==oldY.ToString()&&verticalSize.SelectedIndex==oldSize,"undo restores crop and export size");TravelClipHistory(1);check(fields[0].Text=="230"&&fields[1].Text=="20"&&verticalSize.SelectedIndex!=oldSize,"redo restores crop and export size");
            string oldEnd=clipEnd.Text;timeline.Value=8;seekTick.Stop();MarkTimeline(true);TravelClipHistory(-1);check(clipEnd.Text==oldEnd,"timeline mark is one undoable action");TravelClipHistory(1);check(clipEnd.Text=="8","redo timeline mark");
            for(int i=0;i<110;i++){clipName.Text="Edit "+i;CommitClipHistory();}check(clipHistory.Count==101&&historyIndex==100,"history bounded to 100 undo steps");
            ResetClipBatch();InitializeClipHistory();check(!undoClip.IsEnabled&&!redoClip.IsEnabled&&clipHistory.Count==1,"source reset clears history");
            method.SelectedIndex=0;check(historyRow.Visibility==Visibility.Collapsed&&!undoClip.IsEnabled,"history unavailable outside vertical mode");
            historyTick.Stop();recoveryTick.Stop();File.WriteAllText(report,checks+" clip undo/redo control checks passed. No desktop window or media playback was opened.");
        }
    }
}
