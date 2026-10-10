using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ClearFrame {
    public partial class VideoCleanup {
        readonly MediaElement player=new MediaElement{LoadedBehavior=MediaState.Manual,UnloadedBehavior=MediaState.Manual,ScrubbingEnabled=true,Stretch=System.Windows.Media.Stretch.Uniform,Visibility=Visibility.Collapsed};
        readonly Slider timeline=new Slider{Minimum=0,Maximum=1,IsMoveToPointEnabled=true,SmallChange=0.1,LargeChange=1,VerticalAlignment=VerticalAlignment.Center};
        readonly Button play=new Button{Content="Play"},markIn=new Button{Content="Set start"},markOut=new Button{Content="Set end"};
        readonly TextBlock positionLabel=new TextBlock{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,8,0),MinWidth=110};
        readonly Grid timelinePanel=new Grid{Margin=new Thickness(0,0,0,10)};
        readonly DispatcherTimer playbackTick=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)},seekTick=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};
        bool timelineSync,playing,playerOpened,timelineClosed;
        double requestedPlaybackTime;
        string playbackInput;
        static double FramePosition(double time,double duration){return Math.Max(0,Math.Min(Math.Max(0,duration-0.001),time));}
        static string TimeLabel(double time){var t=TimeSpan.FromSeconds(Math.Max(0,time));return ((int)t.TotalHours).ToString("00")+":"+t.Minutes.ToString("00")+":"+t.Seconds.ToString("00")+"."+t.Milliseconds.ToString("000");}
        void SetupTimeline(Grid frame,StackPanel rows){
            frame.Children.Insert(1,player);rows.Children.Insert(0,timelinePanel);
            foreach(var width in new[]{GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto,GridLength.Auto,GridLength.Auto})timelinePanel.ColumnDefinitions.Add(new ColumnDefinition{Width=width});
            play.Margin=new Thickness(0,0,8,0);markIn.Margin=new Thickness(0,0,8,0);
            UIElement[] controls={play,timeline,positionLabel,markIn,markOut};for(int i=0;i<controls.Length;i++){Grid.SetColumn(controls[i],i);timelinePanel.Children.Add(controls[i]);}
            timeline.ToolTip="Seek through the original video. Release to load a frame preview.";
            timeline.ValueChanged+=(s,e)=>{if(timelineSync||info==null)return;double target=timeline.Value;StopPlayback(false);seconds.Text=FramePosition(target,info.Duration).ToString("0.###",CultureInfo.InvariantCulture);timelineSync=true;timeline.Value=target;timelineSync=false;UpdatePositionLabel();seekTick.Stop();if(!busy)seekTick.Start();};
            seekTick.Tick+=async(s,e)=>{if(busy||timeline.IsMouseCaptureWithin)return;seekTick.Stop();if(info!=null&&Mode()=="vertical")await Frame(false);};
            play.Click+=async(s,e)=>{if(playing){StopPlayback(true);await Frame(false);return;}try{if(info==null||busy)return;requestedPlaybackTime=CleanupCore.FrameTime(seconds.Text,info.Duration);seekTick.Stop();ShowFrame(false);playing=true;play.Content="Pause";player.Visibility=Visibility.Visible;picture.Visibility=Visibility.Hidden;editedPreview=false;selection.Visibility=Visibility.Visible;DrawSelection();
                if(playbackInput!=input){player.Close();playerOpened=false;playbackInput=input;player.Source=new Uri(Path.GetFullPath(input));}else if(playerOpened)player.Position=TimeSpan.FromSeconds(requestedPlaybackTime);
                player.Play();playbackTick.Start();status.Text="Playing the original video. Pause to inspect a frame; Set start / Set end marks the current position.";
            }catch(Exception ex){PlaybackFailed(ex.Message);}};
            player.MediaOpened+=(s,e)=>{playerOpened=true;if(playing)player.Position=TimeSpan.FromSeconds(requestedPlaybackTime);};
            player.MediaFailed+=(s,e)=>PlaybackFailed(e.ErrorException==null?"Windows decoder unavailable":e.ErrorException.Message);
            player.MediaEnded+=async(s,e)=>{if(timelineClosed)return;StopPlayback(false);if(info!=null){seconds.Text=FramePosition(info.Duration,info.Duration).ToString("0.###",CultureInfo.InvariantCulture);UpdateTimeline();if(!busy)await Frame(false);}};
            playbackTick.Tick+=(s,e)=>{if(!playing||!playerOpened||info==null)return;timelineSync=true;timeline.Value=Math.Min(info.Duration,player.Position.TotalSeconds);timelineSync=false;UpdatePositionLabel();};
            markIn.Click+=(s,e)=>MarkTimeline(false);markOut.Click+=(s,e)=>MarkTimeline(true);
            window.Closed+=(s,e)=>{timelineClosed=true;seekTick.Stop();playbackTick.Stop();player.Close();};
        }
        void PlaybackFailed(string message){StopPlayback(false);player.Close();playbackInput=null;playerOpened=false;status.Text="Windows could not play this source. The timeline and Load frame still use FFmpeg for still previews. Convert a copy for Windows playback if needed. "+message;}
        void StopPlayback(bool remember){if(!playing)return;double time=playerOpened?player.Position.TotalSeconds:requestedPlaybackTime;playing=false;playbackTick.Stop();player.Pause();player.Visibility=Visibility.Collapsed;picture.Visibility=Visibility.Visible;play.Content="Play";if(remember&&info!=null)seconds.Text=FramePosition(time,info.Duration).ToString("0.###",CultureInfo.InvariantCulture);}
        void MarkTimeline(bool end){ClipEdit(()=>MarkTimelineCore(end));} void MarkTimelineCore(bool end){if(info==null||busy)return;double time=playing&&playerOpened?player.Position.TotalSeconds:timeline.Value;StopPlayback(false);string text=Math.Min(info.Duration,Math.Max(0,time)).ToString("0.###",CultureInfo.InvariantCulture);if(end)clipEnd.Text=text;else clipStart.Text=text;seconds.Text=FramePosition(time,info.Duration).ToString("0.###",CultureInfo.InvariantCulture);UpdateTimeline();status.Text=(end?"Clip end":"Clip start")+" set to "+text+" s. Save clip / Save changes to update the list.";}
        void UpdatePositionLabel(){positionLabel.Text=TimeLabel(timeline.Value)+" / "+TimeLabel(info==null?0:info.Duration);}
        void UpdateTimeline(){
            timelinePanel.Visibility=Mode()=="vertical"?Visibility.Visible:Visibility.Collapsed;if(Mode()!="vertical")StopPlayback(true);
            play.IsEnabled=timeline.IsEnabled=markIn.IsEnabled=markOut.IsEnabled=!busy&&info!=null;
            if(!playing){double value=0;if(info!=null)double.TryParse(seconds.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out value);if(double.IsNaN(value)||double.IsInfinity(value))value=0;timelineSync=true;timeline.Maximum=info==null?1:info.Duration;timeline.Value=info==null?0:Math.Max(0,Math.Min(info.Duration,value));timelineSync=false;}UpdatePositionLabel();
        }
    }
}
