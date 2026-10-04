using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;

namespace ClearFrame {
    public static class PlaybackDefaults {
        public static int FormatIndex(IDictionary<string,object> preferences){
            if(preferences==null||!preferences.ContainsKey("format"))return 1;
            double value=Core.N(preferences,"format");if(value<0||value>6||value!=Math.Floor(value))return 1;
            int index=(int)value;
            // Releases through 0.6.0 defaulted to modern MP4 (often AV1).
            // Move that old default once; retain later explicit advanced choices.
            return index==0&&Core.N(preferences,"playbackDefaultsVersion")<1?1:index;
        }
    }
    public partial class MainWindow {
        public void CheckPlaybackDefaults(string report){
            preferencesReady=false;int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};
            check(PlaybackDefaults.FormatIndex(null)==1,"fresh app prefers H.264");
            check(PlaybackDefaults.FormatIndex(new Dictionary<string,object>())==1,"missing format prefers H.264");
            check(PlaybackDefaults.FormatIndex(new Dictionary<string,object>{{"format",0}})==1,"old AV1-capable default migrates");
            foreach(int index in new[]{1,2,3,4,5,6})check(PlaybackDefaults.FormatIndex(new Dictionary<string,object>{{"format",index}})==index,"other saved format retained");
            check(PlaybackDefaults.FormatIndex(new Dictionary<string,object>{{"format",0},{"playbackDefaultsVersion",1}})==0,"explicit modern MP4 choice retained after migration");
            check(PlaybackDefaults.FormatIndex(new Dictionary<string,object>{{"format",99}})==1,"invalid format reset");
            C<ComboBox>("FormatBox").SelectedIndex=1;check(Profile()=="compatible"&&C<TextBlock>("FormatHint").Text.Contains("H.264"),"default profile and hint agree");
            var saved=Core.Json.Deserialize<Dictionary<string,object>>(Core.Json.Serialize(Preferences()));check(Core.N(saved,"playbackDefaultsVersion")==1,"migration marker persists");
            C<ComboBox>("FormatBox").SelectedIndex=0;check(C<TextBlock>("FormatHint").Text.Contains("Windows"),"advanced codec warning visible");
            File.WriteAllText(report,count+" playback-default checks passed. No media player was opened.");
        }
    }
}
