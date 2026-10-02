using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ClearFrame {
 public partial class MainWindow {
  string libraryFilter="all";
  Dictionary<string,object> preferences;
  bool preferencesReady;
  static readonly string[] Rates={"","1M","2M","5M","10M"};
  Brush Brush(string hex){return (Brush)new BrushConverter().ConvertFromString(hex);}
  string Rate(){return Rates[Math.Max(0,C<ComboBox>("SpeedBox").SelectedIndex)];}
  void SetupFeatures(){
   if(preferences!=null){C<ComboBox>("QualityBox").SelectedIndex=Index("quality",8);C<ComboBox>("FormatBox").SelectedIndex=Index("format",7);C<ComboBox>("SpeedBox").SelectedIndex=Index("speed",5);C<CheckBox>("FallbackBox").IsChecked=Core.S(preferences,"fallback")=="True";C<CheckBox>("SubtitleBox").IsChecked=Core.S(preferences,"subtitles")=="True";}
   preferencesReady=true;
   foreach(string name in new[]{"QualityBox","FormatBox","SpeedBox"}) C<ComboBox>(name).SelectionChanged+=(s,e)=>{UpdateFormatControls();Save();};
   C<CheckBox>("FallbackBox").Checked+=(s,e)=>Save();C<CheckBox>("FallbackBox").Unchecked+=(s,e)=>Save();C<CheckBox>("SubtitleBox").Checked+=(s,e)=>Save();C<CheckBox>("SubtitleBox").Unchecked+=(s,e)=>Save();
   C<TextBox>("SearchBox").TextChanged+=(s,e)=>UpdateCount();
   foreach(var pair in new[]{new[]{"AllNav","all"},new[]{"ActiveNav","active"},new[]{"CompleteNav","complete"},new[]{"AudioNav","audio"},new[]{"FailedNav","failed"}}){string key=pair[1];C<Button>(pair[0]).Click+=(s,e)=>SetFilter(key);}
   C<Button>("CleanupButton").Click+=(s,e)=>{if(running||inspecting||updating){Status("Stop downloads and finish the current check before opening video cleanup.");return;}new VideoCleanup(w,Path.Combine(home,"tools"),dataDir).Show();};
   C<Button>("BatchButton").Click+=(s,e)=>BatchDialog();
   C<Button>("OpenFolderButton").Click+=(s,e)=>{try{Directory.CreateDirectory(folder);Process.Start("explorer.exe",Core.Quote(folder));}catch(Exception ex){Status(ex.Message);}};
   C<Button>("PlayButton").Click+=(s,e)=>{var j=Selected();if(j==null||j.Status!="Complete"||!File.Exists(j.FilePath)){Status("Select a completed file that still exists on disk.");return;}try{Process.Start(new ProcessStartInfo(j.FilePath){UseShellExecute=true});}catch(Exception ex){Status(ex.Message);}};
   C<Button>("UpButton").Click+=(s,e)=>MoveSelected(-1);C<Button>("DownButton").Click+=(s,e)=>MoveSelected(1);
   var menu=new ContextMenu{Background=Brush("#22281B"),Foreground=Brush("#F4F4EF")};
   var retry=new MenuItem{Header="Retry all failed / stopped"};retry.Click+=(s,e)=>RetryAll();menu.Items.Add(retry);
   var clear=new MenuItem{Header="Clear completed history"};clear.Click+=(s,e)=>{int count=0;foreach(var j in jobs.Where(x=>x.Status=="Complete").ToList()){jobs.Remove(j);count++;}Save();UpdateCount();Status(count+" completed items removed from history. Saved files were kept.");};menu.Items.Add(clear);menu.Items.Add(new Separator());
   var details=new MenuItem{Header="Download details"};details.Click+=(s,e)=>ShowDetails();menu.Items.Add(details);
   var remove=new MenuItem{Header="Remove selected from history"};remove.Click+=(s,e)=>RemoveSelected();menu.Items.Add(remove);
   C<Button>("MoreButton").ContextMenu=menu;C<Button>("MoreButton").Click+=(s,e)=>{menu.PlacementTarget=C<Button>("MoreButton");menu.IsOpen=true;};
   C<ListView>("QueueList").MouseDoubleClick+=(s,e)=>{if(Selected()!=null)C<Button>("PlayButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
   w.PreviewKeyDown+=(s,e)=>{if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.L){C<TextBox>("UrlBox").Focus();C<TextBox>("UrlBox").SelectAll();e.Handled=true;}else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.F){C<TextBox>("SearchBox").Focus();e.Handled=true;}else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.B){BatchDialog();e.Handled=true;}};
   UpdateFormatControls();SetFilter("all");
  }
  int Index(string key,int max){int i=(int)Core.N(preferences,key);return i>=0&&i<max?i:0;}
  object Preferences(){return new Dictionary<string,object>{{"quality",C<ComboBox>("QualityBox").SelectedIndex},{"format",C<ComboBox>("FormatBox").SelectedIndex},{"speed",C<ComboBox>("SpeedBox").SelectedIndex},{"fallback",C<CheckBox>("FallbackBox").IsChecked==true},{"subtitles",C<CheckBox>("SubtitleBox").IsChecked==true}};}
  void UpdateFormatControls(){bool audio=Core.IsAudio(Profile());C<ComboBox>("QualityBox").IsEnabled=!audio;C<CheckBox>("FallbackBox").IsEnabled=!audio;C<CheckBox>("SubtitleBox").IsEnabled=!audio;
   C<TextBlock>("FormatHint").Text=Profile()=="mp3"?"Audio only · Best-quality variable-bitrate MP3. Converting cannot add detail missing from the source.":Profile()=="m4a"?"Audio only · Prefers the original AAC stream. Other audio is converted to AAC if needed.":Profile()=="compatible"||Profile()=="mov"?"H.264 + AAC for broad compatibility. Higher resolutions may need modern MP4, MKV or WebM.":"Keeps the original picture and sound. Your player must support the source codec.";
  }
  void SetFilter(string filter){libraryFilter=filter;string[] names={"AllNav","ActiveNav","CompleteNav","AudioNav","FailedNav"};string[] keys={"all","active","complete","audio","failed"};string[] titles={"All downloads","In progress","Completed","Audio library","Needs attention"};for(int i=0;i<keys.Length;i++){C<Button>(names[i]).Background=Brush(keys[i]==filter?"#D6F64A":"#10130D");C<Button>(names[i]).Foreground=Brush(keys[i]==filter?"#111609":"#D4DDC6");if(keys[i]==filter)C<TextBlock>("LibraryTitle").Text=titles[i];}UpdateCount();}
  bool Matches(Job j){string query=C<TextBox>("SearchBox").Text.Trim();bool text=query.Length==0||(j.Title??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||(j.Url??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||(j.Container??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;return text&&(libraryFilter=="all"||libraryFilter=="complete"&&j.Status=="Complete"||libraryFilter=="audio"&&Core.IsAudio(j.Profile)||libraryFilter=="failed"&&(j.Status=="Failed"||j.Status=="Stopped")||libraryFilter=="active"&&j.Status!="Complete"&&j.Status!="Failed"&&j.Status!="Stopped");}
  void RefreshLibrary(){var view=CollectionViewSource.GetDefaultView(jobs);view.Filter=o=>Matches((Job)o);view.Refresh();int visible=jobs.Count(Matches);C<TextBlock>("QueueCount").Text=visible+" shown · "+jobs.Count(x=>x.Status=="Queued")+" queued · "+jobs.Count(x=>x.Status=="Complete")+" complete";C<TextBlock>("TotalCount").Text=jobs.Count.ToString("00");C<TextBlock>("DoneCount").Text=jobs.Count(x=>x.Status=="Complete").ToString("00");C<StackPanel>("EmptyPanel").Visibility=visible==0?Visibility.Visible:Visibility.Collapsed;C<TextBlock>("EmptyText").Text=jobs.Count==0?"A little space for your next obsession.":"No downloads match this view.";}
  void MoveSelected(int direction){var j=Selected();if(j==null||j.Status!="Queued"){Status("Select a queued item to change its order.");return;}int index=jobs.IndexOf(j),other=index+direction;while(other>=0&&other<jobs.Count&&jobs[other].Status!="Queued")other+=direction;if(other<0||other>=jobs.Count)return;jobs.Move(index,other);C<ListView>("QueueList").SelectedItem=j;Save();UpdateCount();Status("Queue order updated.");}
  void RemoveSelected(){var j=Selected();if(j==null)return;if(j==active){Status("Stop the queue before removing the active item.");return;}jobs.Remove(j);Save();UpdateCount();Status("Removed from history. Saved and partial files were kept.");}
  void BatchDialog(){
   var dialog=new Window{Title="ClearFrame · Add a batch",Width=650,Height=535,MinWidth=500,MinHeight=440,Owner=w,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brush("#11160D"),Foreground=Brush("#F4F4EF"),FontFamily=new FontFamily("Segoe UI"),Resources=w.Resources};
   var grid=new Grid{Margin=new Thickness(24)};foreach(var h in new[]{GridLength.Auto,GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto,GridLength.Auto})grid.RowDefinitions.Add(new RowDefinition{Height=h});
   grid.Children.Add(new TextBlock{Text="Build your next offline collection.",FontSize=23,FontWeight=FontWeights.SemiBold});
   var note=new TextBlock{Text="One YouTube video link per line · Up to 100 links\nUses your current format, resolution, speed limit and save folder. Titles and availability are checked when each download starts.",TextWrapping=TextWrapping.Wrap,Foreground=Brush("#B7C99F"),Margin=new Thickness(0,10,0,15)};Grid.SetRow(note,1);grid.Children.Add(note);
   var input=new TextBox{AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,TextWrapping=TextWrapping.NoWrap,FontSize=12,VerticalContentAlignment=VerticalAlignment.Top};Grid.SetRow(input,2);grid.Children.Add(input);
   var error=new TextBlock{Text="Duplicate links in the batch are removed automatically.",Foreground=Brush("#B7C99F"),Margin=new Thickness(0,12,0,12),TextWrapping=TextWrapping.Wrap};Grid.SetRow(error,3);grid.Children.Add(error);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetRow(actions,4);grid.Children.Add(actions);var paste=new Button{Content="Paste links",Margin=new Thickness(0,0,8,0)};paste.Click+=(s,e)=>{try{input.Text=Clipboard.GetText();}catch(Exception ex){error.Text=ex.Message;}};actions.Children.Add(paste);var add=new Button{Content="Add batch to queue",Style=(Style)w.FindResource("Primary")};actions.Children.Add(add);
   add.Click+=(s,e)=>{try{var urls=Core.BatchUrls(input.Text);int added=0;foreach(string url in urls){if(Duplicate(url,Profile(),folder,Core.IsAudio(Profile())?0:TargetResolution()))continue;string id=url.Substring(url.Length-11);jobs.Add(new Job{Id=Guid.NewGuid().ToString("N"),Url=url,VideoId=id,Title="YouTube · "+id,Container=Core.Container(Profile()),Profile=Profile(),TargetResolution=TargetResolution(),Strict=C<CheckBox>("FallbackBox").IsChecked!=true,Subtitles=!Core.IsAudio(Profile())&&C<CheckBox>("SubtitleBox").IsChecked==true,RateLimit=Rate(),Resolution=Core.IsAudio(Profile())?0:TargetResolution(),Folder=folder,Status="Queued",Detail="Batch import · Awaiting source check",Log="",FilePath=""});added++;}Save();SetFilter("all");Status(added+" items added. "+(urls.Count-added)+" duplicates skipped. Select Start queue.");dialog.Close();}catch(Exception ex){error.Foreground=Brush("#FF8B83");error.Text=ex.Message;}};
   dialog.Content=grid;dialog.ShowDialog();
  }
  int RetryAll(){int count=0;foreach(var j in jobs.Where(x=>x!=active&&(x.Status=="Failed"||x.Status=="Stopped")).ToList()){j.Status="Queued";j.Progress=0;j.Detail="Ready to retry. Partial files kept.";count++;}Save();UpdateCount();Status(count+" items queued for retry."+(running?" The current queue will pick them up unless stopped.":" Select Start queue."));return count;}
  bool Duplicate(string url,string profile,string destination,int resolution){bool audio=Core.IsAudio(profile),strict=C<CheckBox>("FallbackBox").IsChecked!=true,subtitles=!audio&&C<CheckBox>("SubtitleBox").IsChecked==true;return jobs.Any(x=>x.Url==url&&x.Profile==profile&&(audio||x.TargetResolution==resolution&&(resolution==0||x.Strict==strict))&&x.Subtitles==subtitles&&string.Equals(x.Folder,destination,StringComparison.OrdinalIgnoreCase)&&x.Status!="Failed"&&x.Status!="Stopped");}
  public void CheckOfflineInterface(string report){
   preferencesReady=false;int checks=0;
   Action<bool,string> assert=(ok,name)=>{if(!ok)throw new Exception("UI check failed: "+name);checks++;};
   var first=new Job{Id="a",Title="First film",Profile="mp4",Container="mp4",Resolution=1080,Status="Queued",Url="https://youtu.be/abcdefghijk"};var second=new Job{Id="b",Title="Second film",Profile="mp4",Container="mp4",Resolution=2160,Status="Queued"};var sound=new Job{Id="c",Title="Piano recording",Profile="mp3",Container="mp3",Status="Complete"};jobs.Add(first);jobs.Add(second);jobs.Add(sound);
   SetFilter("audio");assert(CollectionViewSource.GetDefaultView(jobs).Cast<Job>().Count()==1,"audio filter");
   SetFilter("complete");assert(CollectionViewSource.GetDefaultView(jobs).Cast<Job>().Single()==sound,"completed filter");
   SetFilter("active");assert(CollectionViewSource.GetDefaultView(jobs).Cast<Job>().Count()==2,"active filter");
   SetFilter("all");C<TextBox>("SearchBox").Text="PIANO";assert(CollectionViewSource.GetDefaultView(jobs).Cast<Job>().Single()==sound,"case insensitive search");
   C<TextBox>("SearchBox").Text="mP4";assert(CollectionViewSource.GetDefaultView(jobs).Cast<Job>().Count()==2,"format search");C<TextBox>("SearchBox").Clear();
   C<ListView>("QueueList").SelectedItem=second;MoveSelected(-1);assert(jobs[0]==second,"queue reorder");
   C<ComboBox>("FormatBox").SelectedIndex=5;assert(!C<ComboBox>("QualityBox").IsEnabled&&!C<CheckBox>("SubtitleBox").IsEnabled,"audio controls");
   C<ComboBox>("FormatBox").SelectedIndex=0;assert(C<ComboBox>("QualityBox").IsEnabled&&C<CheckBox>("SubtitleBox").IsEnabled,"video controls restored");
   C<ComboBox>("SpeedBox").SelectedIndex=2;assert(Rate()=="2M","bandwidth setting");
   var pref=Core.Json.Deserialize<Dictionary<string,object>>(Core.Json.Serialize(Preferences()));assert(Core.N(pref,"speed")==2&&Core.N(pref,"format")==0,"preference serialization");
   var recovery=new Job{Id="r",Title="Recovery",Url="https://www.youtube.com/watch?v=abcdefghijk",Profile="mp4",Container="mp4",Folder=folder,TargetResolution=0,Resolution=2160,Strict=true,Status="Complete"};jobs.Add(recovery);
   assert(Duplicate(recovery.Url,"mp4",folder,0),"best quality deduplicates by requested quality");
   recovery.TargetResolution=1080;recovery.Resolution=720;recovery.Strict=false;C<CheckBox>("FallbackBox").IsChecked=true;assert(Duplicate(recovery.Url,"mp4",folder,1080),"fallback deduplicates by request");
   C<CheckBox>("FallbackBox").IsChecked=false;assert(!Duplicate(recovery.Url,"mp4",folder,1080),"strict quality remains distinct");
   recovery.Strict=true;C<CheckBox>("SubtitleBox").IsChecked=true;assert(!Duplicate(recovery.Url,"mp4",folder,1080),"subtitle request remains distinct");C<CheckBox>("SubtitleBox").IsChecked=false;
   recovery.Profile="mp3";assert(Duplicate(recovery.Url,"mp3",folder,4320),"audio ignores video quality preference");
   recovery.Status="Stopped";assert(!Duplicate(recovery.Url,"mp3",folder,4320),"stopped item does not block new request");
   var stopped=new Job{Status="Stopped",Progress=70};jobs.Add(stopped);active=recovery;int retried=RetryAll();assert(retried==1&&stopped.Status=="Queued"&&stopped.Progress==0&&recovery.Status=="Stopped","retry all skips active stopping item");active=null;
   foreach(string state in new[]{"Checking","Downloading","Finishing","Verifying"}){var interrupted=new Job{Status=state};RestoreJob(interrupted);assert(interrupted.Status=="Stopped","interrupted "+state+" remains stopped on restart");}
   foreach(string state in new[]{"Queued","Complete","Failed","Stopped"}){var stable=new Job{Status=state};RestoreJob(stable);assert(stable.Status==state,"restore preserves "+state);}
   persistenceError="test write failure";Status("Added.");assert(C<TextBlock>("StatusText").Text.StartsWith("History not saved: test write failure")&&(string)C<TextBlock>("StatusText").ToolTip==C<TextBlock>("StatusText").Text,"save failure stays visible with full tooltip");persistenceError=null;
   active=recovery;recovery.Status="Downloading";ApplyDownloadLine(recovery,"CFP|42.5%|1MiB/s|00:12",System.Threading.CancellationToken.None);assert(recovery.Progress==42.5&&recovery.Detail.Contains("00:12"),"active progress updates");
   ApplyDownloadLine(recovery,"CFP|NaN|1MiB/s|00:12",System.Threading.CancellationToken.None);assert(recovery.Progress==42.5,"non-finite progress ignored");
   ApplyDownloadLine(recovery,"[Merger] merging",System.Threading.CancellationToken.None);assert(recovery.Status=="Finishing","active merge transition");
   foreach(string state in new[]{"Stopped","Failed","Complete","Verifying","Queued"}){recovery.Status=state;recovery.Progress=77;recovery.Detail="Keep this";ApplyDownloadLine(recovery,"[Merger] delayed",System.Threading.CancellationToken.None);ApplyDownloadLine(recovery,"CFP|10%|1MiB/s|00:12",System.Threading.CancellationToken.None);assert(recovery.Status==state&&recovery.Progress==77&&recovery.Detail=="Keep this","late output preserves "+state);}
   recovery.Status="Downloading";ApplyDownloadLine(recovery,"[Merger] delayed",new System.Threading.CancellationToken(true));assert(recovery.Status=="Downloading","cancelled attempt cannot update state");active=null;
   ApplyDownloadLine(recovery,"CFP|20%|1MiB/s|00:12",System.Threading.CancellationToken.None);assert(recovery.Progress==77,"inactive attempt cannot update progress");
   C<ComboBox>("SpeedBox").SelectedIndex=0;jobs.Clear();SetFilter("all");preferencesReady=true;Status("Ready. Exact quality, with no upscaling or silent downgrades.");File.WriteAllText(report,checks+" offline WPF control checks passed. No desktop window or network request was opened.");
  }
 }
}
