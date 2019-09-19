using AppMinder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UserAppMinder {
    class Program {
        static void Main( string[] args ) {
            ProcMinderApp app = ProcMinderApp.GetSingleton();
            app.Run();
            Console.WriteLine( "Running..." );
            Console.ReadKey();
            app.Stop();
        }
    }
}
