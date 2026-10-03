using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClearFrame {
    public class PlaylistItem : INotifyPropertyChanged {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public double Duration { get; set; }
        public bool Available { get; set; }
        bool selected;
        public bool Selected { get { return selected; } set { selected = value && Available; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); } }
        public event PropertyChangedEventHandler PropertyChanged;
        public string Url { get { return "https://www.youtube.com/watch?v=" + Id; } }
    }
    public class PlaylistResult {
        public string Title;
        public bool Limited;
        public List<PlaylistItem> Items = new List<PlaylistItem>();
    }
    public static class PlaylistCore {
        public const int Limit = 200;
        public static string Canonical(string input) {
            Uri uri;
            if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !uri.IsDefaultPort || uri.UserInfo != "" ||
                !new[] { "youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com", "youtu.be" }.Contains(uri.Host.ToLowerInvariant()))
                throw new Exception("Paste a YouTube playlist link containing a list ID.");
            string id = null;
            foreach (var part in uri.Query.TrimStart('?').Split('&')) { var bits = part.Split(new[] { '=' }, 2); if (bits.Length == 2 && bits[0] == "list") id = Uri.UnescapeDataString(bits[1]); }
            if (id == null || !Regex.IsMatch(id, @"^[A-Za-z0-9_-]{10,150}$")) throw new Exception("Paste a public YouTube playlist link. Channel pages and account-only lists are not supported.");
            return "https://www.youtube.com/playlist?list=" + id;
        }
        public static PlaylistResult Parse(string json) {
            var root = Core.Json.Deserialize<Dictionary<string, object>>(json);
            if (root == null || !root.ContainsKey("entries")) throw new Exception("The source did not return a playlist.");
            var entries = Core.Entries(root, "entries").Take(Limit + 1).ToList();
            var result = new PlaylistResult { Title = Core.S(root, "title"), Limited = entries.Count > Limit || Core.N(root, "playlist_count") > Limit };
            var seen = new HashSet<string>();
            foreach (var entry in entries.Take(Limit)) {
                string id = Core.S(entry, "id"), title = Core.S(entry, "title"), availability = Core.S(entry, "availability");
                if (!Regex.IsMatch(id, @"^[A-Za-z0-9_-]{11}$") || !seen.Add(id)) continue;
                bool available = (availability == "" || availability == "public" || availability == "unlisted") && title != "[Deleted video]" && title != "[Private video]" && Core.S(entry, "live_status") != "is_live" && Core.S(entry, "live_status") != "is_upcoming";
                double duration = Core.N(entry, "duration"); if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0) duration = 0;
                result.Items.Add(new PlaylistItem { Id = id, Title = string.IsNullOrWhiteSpace(title) ? "YouTube · " + id : title, Available = available, Duration = duration, Detail = available ? Job.TimeLabel(duration) + " · " + Core.S(entry, "uploader") : "Unavailable or requires account access / live" });
            }
            return result;
        }
    }
    public sealed class PlaylistPicker {
        readonly Window window;
        readonly TextBox url = new TextBox();
        readonly TextBlock notice = new TextBlock { Text = "Preview up to 200 entries. Select the videos you want; availability and quality are rechecked when downloading.", Margin = new Thickness(0,12,0,12) };
        readonly Button load = new Button { Content = "Load playlist", Margin = new Thickness(8,0,0,0) };
        readonly Button add = new Button { Content = "Add selected", IsEnabled = false };
        readonly Button all = new Button { Content = "Select available", Margin = new Thickness(0,0,8,0) };
        readonly Button none = new Button { Content = "Clear selection", Margin = new Thickness(0,0,8,0) };
        readonly ObservableCollection<PlaylistItem> items = new ObservableCollection<PlaylistItem>();
        readonly Func<string,CancellationToken,Task<PlaylistResult>> fetch;
        readonly Action<List<PlaylistItem>> enqueue;
        CancellationTokenSource cancellation;
        bool busy;
        public PlaylistPicker(Window owner,Func<string,CancellationToken,Task<PlaylistResult>> fetch,Action<List<PlaylistItem>> enqueue) {
            this.fetch=fetch;this.enqueue=enqueue;
            window=new Window { Title="ClearFrame · Choose playlist videos",Width=820,Height=690,MinWidth=650,MinHeight=500,Owner=owner.IsVisible?owner:null,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=owner.Background,Foreground=owner.Foreground,FontFamily=owner.FontFamily,Resources=owner.Resources };
            var root=new Grid { Margin=new Thickness(24) }; foreach(var height in new[]{GridLength.Auto,GridLength.Auto,GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto})root.RowDefinitions.Add(new RowDefinition { Height=height });
            root.Children.Add(new TextBlock { Text="Your playlist. Your picks.",FontSize=26,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,18) });
            var entry=new DockPanel();DockPanel.SetDock(load,Dock.Right);entry.Children.Add(load);entry.Children.Add(url);url.ToolTip="Paste a YouTube playlist URL";Grid.SetRow(entry,1);root.Children.Add(entry);Grid.SetRow(notice,2);root.Children.Add(notice);
            var list=new ListView { ItemsSource=items };Grid.SetRow(list,3);root.Children.Add(list);
            var template=new DataTemplate(typeof(PlaylistItem));var panel=new FrameworkElementFactory(typeof(DockPanel));
            var check=new FrameworkElementFactory(typeof(CheckBox));check.SetBinding(CheckBox.IsCheckedProperty,new Binding("Selected") { Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged });check.SetBinding(CheckBox.IsEnabledProperty,new Binding("Available"));check.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,14,0));check.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);panel.AppendChild(check);
            var words=new FrameworkElementFactory(typeof(StackPanel));var title=new FrameworkElementFactory(typeof(TextBlock));title.SetBinding(TextBlock.TextProperty,new Binding("Title"));title.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);words.AppendChild(title);var detail=new FrameworkElementFactory(typeof(TextBlock));detail.SetBinding(TextBlock.TextProperty,new Binding("Detail"));detail.SetValue(TextBlock.ForegroundProperty,new SolidColorBrush(Color.FromRgb(162,180,140)));detail.SetValue(TextBlock.FontSizeProperty,11.0);words.AppendChild(detail);panel.AppendChild(words);template.VisualTree=panel;list.ItemTemplate=template;
            var actions=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,14,0,0) };actions.Children.Add(all);actions.Children.Add(none);add.Style=(Style)owner.FindResource("Primary");actions.Children.Add(add);Grid.SetRow(actions,4);root.Children.Add(actions);window.Content=root;
            load.Click+=async(s,e)=>{if(busy){cancellation.Cancel();return;}await Load();};all.Click+=(s,e)=>Select(true);none.Click+=(s,e)=>Select(false);
            url.TextChanged+=(s,e)=>{if(!busy&&items.Count>0){items.Clear();Count();notice.Text="Load this playlist to choose its videos.";}};
            add.Click+=(s,e)=>{var selected=items.Where(i=>i.Selected&&i.Available).ToList();if(selected.Count==0)return;enqueue(selected);window.Close();};
            window.Closing+=(s,e)=>{if(busy){e.Cancel=true;cancellation.Cancel();notice.Text="Cancelling playlist check; close again when it finishes.";}};
        }
        void Select(bool selected){foreach(var item in items)item.Selected=selected;}
        void Count(){int count=items.Count(i=>i.Selected);add.Content="Add selected ("+count+")";add.IsEnabled=!busy&&count>0;}
        async Task Load(){
            string link;try{link=PlaylistCore.Canonical(url.Text);}catch(Exception ex){notice.Text=ex.Message;return;}
            busy=true;cancellation=new CancellationTokenSource();url.IsReadOnly=true;all.IsEnabled=none.IsEnabled=false;items.Clear();Count();load.Content="Cancel check";notice.Text="Reading playlist entries…";
            try{var result=await fetch(link,cancellation.Token);foreach(var item in result.Items){item.PropertyChanged+=(s,e)=>Count();items.Add(item);}notice.Text=result.Title+" · "+items.Count+" videos · "+items.Count(i=>!i.Available)+" unavailable"+(result.Limited?" · First 200 entries only.":"");}
            catch(OperationCanceledException){notice.Text="Playlist check cancelled. Nothing was added.";}catch(Exception ex){notice.Text=ex.Message;}
            finally{busy=false;cancellation.Dispose();cancellation=null;url.IsReadOnly=false;all.IsEnabled=none.IsEnabled=true;load.Content="Load playlist";Count();}
        }
        public void Show(){window.ShowDialog();}
        public async Task CheckOffline(){url.Text="https://www.youtube.com/playlist?list=PL1234567890";await Load();if(items.Count!=3||add.IsEnabled)throw new Exception("Playlist load/empty selection failed.");Select(true);if(items.Count(i=>i.Selected)!=1||!add.IsEnabled)throw new Exception("Playlist select available failed.");Select(false);if(add.IsEnabled)throw new Exception("Playlist clear selection failed.");url.Text="invalid";await Load();if(items.Count!=0||!notice.Text.Contains("playlist link"))throw new Exception("Playlist changed URL invalidation failed.");}
        public async Task CheckCancellation(){url.Text="https://www.youtube.com/playlist?list=PL1234567890";var task=Load();if(!busy)throw new Exception("Playlist check did not start.");cancellation.Cancel();await task;if(busy||add.IsEnabled||!notice.Text.Contains("cancelled"))throw new Exception("Playlist cancellation failed.");}
        public void RenderDemo(string path){
            url.Text="https://www.youtube.com/playlist?list=PL1234567890";notice.Text="Sample film collection · Demo entries · Choose the videos to add using your current download settings.";
            foreach(var item in new[]{new PlaylistItem{Title="North coast · Sample travel film",Detail="06:12 · Sample creator",Available=true},new PlaylistItem{Title="Evening light · Sample short film",Detail="02:06 · Sample creator",Available=true},new PlaylistItem{Title="Unavailable entry",Detail="Private video · Cannot be selected",Available=false}}){item.PropertyChanged+=(s,e)=>Count();items.Add(item);}items[0].Selected=true;
            var root=(Grid)window.Content;root.Margin=new Thickness(0);root.Width=772;root.Height=592;root.Measure(new Size(772,592));root.Arrange(new Rect(0,0,772,592));root.UpdateLayout();var bitmap=new RenderTargetBitmap(820,640,96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(window.Background,null,new Rect(0,0,820,640));dc.DrawRectangle(new VisualBrush(root){AutoLayoutContent=false,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,772,592)},null,new Rect(24,24,772,592));}bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);
        }
    }
}
