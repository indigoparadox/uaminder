using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace UserAppMinderService {
    [RunInstaller( true )]
    public partial class UserAppMinderSvcInstaller : System.Configuration.Install.Installer {
        public UserAppMinderSvcInstaller() {
            InitializeComponent();
        }

        private void serviceProcessInstaller1_AfterInstall( object sender, InstallEventArgs e ) {

        }

        private void serviceInstaller1_AfterInstall( object sender, InstallEventArgs e ) {

        }

        private void serviceInstaller1_Committed( object sender, InstallEventArgs e ) {
            // Auto Start the Service Once Installation is Finished.
            var controller = new ServiceController( this.serviceInstaller1.ServiceName );
            controller.Start();
        }
    }
}
