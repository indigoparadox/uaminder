using AppMinder;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace UserAppMinderService {
    public partial class UserAppMinderService : ServiceBase {

        public UserAppMinderService() {
            InitializeComponent();
        }

        protected override void OnStart( string[] args ) {
            ProcMinderApp.GetSingleton().Run();
        }

        protected override void OnStop() {
            ProcMinderApp.GetSingleton().Stop();
        }
    }
}
