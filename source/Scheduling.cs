using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ClearFrame {
    public sealed class QueueSchedule {
        public DateTime? DueUtc { get; private set; }
        public static DateTime ParseLocal(string text,TimeZoneInfo zone,DateTime nowUtc){
            DateTime value;
            if(!DateTime.TryParseExact(text,"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out value))throw new Exception("Use the date and time format YYYY-MM-DD HH:mm, for example 2026-10-04 21:30.");
            value=DateTime.SpecifyKind(value,DateTimeKind.Unspecified);
            if(zone.IsInvalidTime(value)||zone.IsAmbiguousTime(value))throw new Exception("That time falls in a daylight-saving clock change. Choose another time.");
            DateTime utc=TimeZoneInfo.ConvertTimeToUtc(value,zone);
            if(utc<=nowUtc||utc>nowUtc.AddDays(7))throw new Exception("Choose a future time within the next seven days.");
            return utc;
        }
        public void Set(DateTime utc){DueUtc=utc;}
        public void Cancel(){DueUtc=null;}
        public bool TakeDue(DateTime nowUtc,bool busy){if(busy||!DueUtc.HasValue||nowUtc<DueUtc.Value)return false;DueUtc=null;return true;}
    }
    public partial class MainWindow {
        readonly QueueSchedule schedule=new QueueSchedule();
        DispatcherTimer scheduleTimer;
        void SetupScheduling(){
            C<Button>("ScheduleButton").Click+=(s,e)=>ScheduleDialog();
            scheduleTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
            scheduleTimer.Tick+=async(s,e)=>{if(schedule.TakeDue(DateTime.UtcNow,running||updating||inspecting)){UpdateScheduleLabel();await Start();}};
            scheduleTimer.Start();w.Closed+=(s,e)=>{scheduleTimer.Stop();schedule.Cancel();};
        }
        void UpdateScheduleLabel(){var button=C<Button>("ScheduleButton");button.Content=schedule.DueUtc.HasValue?"Scheduled "+schedule.DueUtc.Value.ToLocalTime().ToString("HH:mm"):"Schedule…";button.ToolTip=schedule.DueUtc.HasValue?"Starts "+schedule.DueUtc.Value.ToLocalTime().ToString("f")+". Click to change or cancel.":"Start the queue later while ClearFrame stays open";}
        void CancelSchedule(){schedule.Cancel();UpdateScheduleLabel();}
        void ScheduleDialog(){
            if(running){Status("Wait for the current queue to finish before scheduling another start.");return;}
            var dialog=new Window{Title="ClearFrame · Schedule downloads",Width=550,Height=350,ResizeMode=ResizeMode.NoResize,Owner=w,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=w.Background,Foreground=w.Foreground,Resources=w.Resources,FontFamily=w.FontFamily};
            var panel=new StackPanel{Margin=new Thickness(24)};
            panel.Children.Add(new TextBlock{Text="Choose when your queue starts.",FontSize=22,FontWeight=FontWeights.SemiBold});
            panel.Children.Add(new TextBlock{Text="Local time · "+TimeZoneInfo.Local.DisplayName+"\nYYYY-MM-DD HH:mm (24-hour clock)",Margin=new Thickness(0,12,0,8)});
            var input=new TextBox{Text=(schedule.DueUtc.HasValue?schedule.DueUtc.Value.ToLocalTime():DateTime.Now.AddHours(1)).ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture)};panel.Children.Add(input);
            var note=new TextBlock{Text="Keep ClearFrame open and your PC awake. Starts all queued items at that time. Closing the app cancels the schedule; waking the PC late starts it when the app is ready.",Margin=new Thickness(0,12,0,16)};panel.Children.Add(note);
            var row=new StackPanel{Orientation=Orientation.Horizontal};var cancel=new Button{Content="Cancel schedule",Margin=new Thickness(0,0,10,0)};var apply=new Button{Content="Schedule queue",Style=(Style)w.FindResource("Primary")};row.Children.Add(cancel);row.Children.Add(apply);panel.Children.Add(row);dialog.Content=panel;
            cancel.Click+=(s,e)=>{CancelSchedule();Status("Schedule cancelled. Your queued downloads are kept.");dialog.Close();};
            apply.Click+=(s,e)=>{try{if(!jobs.Any(x=>x.Status=="Queued"))throw new Exception("Add some downloads to the queue first.");CheckTools();schedule.Set(QueueSchedule.ParseLocal(input.Text,TimeZoneInfo.Local,DateTime.UtcNow));UpdateScheduleLabel();Status("Queue scheduled for "+schedule.DueUtc.Value.ToLocalTime().ToString("f")+". Keep ClearFrame open.");dialog.Close();}catch(Exception ex){note.Text=ex.Message;}};
            dialog.ShowDialog();
        }
        public void CheckScheduling(string report){
            preferencesReady=false;int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};var now=new DateTime(2026,10,4,12,0,0,DateTimeKind.Utc);
            var west=TimeZoneInfo.CreateCustomTimeZone("Test +1",TimeSpan.FromHours(1),"Test +1","Test +1");
            check(QueueSchedule.ParseLocal("2026-10-04 14:00",west,now)==now.AddHours(1),"local time converts to UTC");
            foreach(var input in new[]{"today","2026-10-04 12:00","2026-10-20 14:00","2026-10-04 25:00"}){bool failed=false;try{QueueSchedule.ParseLocal(input,west,now);}catch{failed=true;}check(failed,"invalid schedule rejected");}
            var eastern=TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");foreach(var input in new[]{"2026-03-08 02:30","2026-11-01 01:30"}){bool failed=false;try{QueueSchedule.ParseLocal(input,eastern,new DateTime(2026,1,1));}catch(Exception ex){failed=ex.Message.Contains("clock change");}check(failed,"DST clock changes rejected");}
            schedule.Set(now.AddMinutes(1));check(!schedule.TakeDue(now,false),"not early");check(!schedule.TakeDue(now.AddMinutes(2),true)&&schedule.DueUtc.HasValue,"busy retains pending start");check(schedule.TakeDue(now.AddMinutes(3),false),"late idle start fires");check(!schedule.TakeDue(now.AddMinutes(4),false),"fires only once");schedule.Set(now);CancelSchedule();check(!schedule.TakeDue(now,false)&&C<Button>("ScheduleButton").Content.ToString()=="Schedule…","cancel clears schedule and label");schedule.Set(now);UpdateScheduleLabel();check(C<Button>("ScheduleButton").Content.ToString().StartsWith("Scheduled "),"visible scheduled state");CancelSchedule();check(!new QueueSchedule().DueUtc.HasValue,"new session never restores an automatic start");
            File.WriteAllText(report,count+" scheduling checks passed. No network downloads or wall-clock waits were used.");
        }
    }
}
