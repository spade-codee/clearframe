using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ClearFrame;
class ProcessTests {
 static int Main(string[] args){try{
  string fake=args[0];var values=new[]{"space here","embedded\"quote","C:\\tail\\","& calc.exe","$(echo test)","Unicode é \u65e5\u672c\u8a9e"};
  var r=Core.Run(fake,new[]{"--echo"}.Concat(values),CancellationToken.None,10,null).GetAwaiter().GetResult();
  var actual=r.Output.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(s=>Encoding.UTF8.GetString(Convert.FromBase64String(s))).ToArray();if(!actual.SequenceEqual(values))throw new Exception("Argument roundtrip failed");
  using(var ct=new CancellationTokenSource()){ct.CancelAfter(300);bool cancelled=false;try{Core.Run(fake,new[]{"--sleep"},ct.Token,10,null).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}if(!cancelled)throw new Exception("Cancellation failed");}
  bool timedOut=false;try{Core.Run(fake,new[]{"--sleep"},CancellationToken.None,1,null).GetAwaiter().GetResult();}catch(Exception ex){timedOut=ex.Message.Contains("timed out");}if(!timedOut)throw new Exception("Timeout failed");
  File.WriteAllText(args[1],"Process checks passed: spaces, quotes, trailing backslashes, shell metacharacters and Unicode survive argument roundtrip; cancellation and timeout terminate workers.");return 0;
 }catch(Exception ex){File.WriteAllText(args[1],ex.ToString());return 1;}}
}
