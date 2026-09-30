using System;
using System.Linq;
using System.Text;
using System.Threading;

class ProcessFixture {
    static int Main(string[] args) {
        if (args.Contains("--echo")) {
            foreach (string arg in args.Skip(1))
                Console.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(arg)));
            return 0;
        }
        if (args.Contains("--sleep")) { Thread.Sleep(30000); return 0; }
        return 2;
    }
}
