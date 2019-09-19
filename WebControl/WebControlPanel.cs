using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.Protocols;
using System.Net;

namespace WebControl {
    public class WebControlPanel : WebServer {

        public string Title { get; set; }

        private void SetupFilesystem() {
            WebPage rootpage = new WebPage( this.Title );
            this.VFileSystem.Root.PutFile( "index.html", rootpage );
            rootpage.AddCSS( "control.css" );

            WebTextFile controlcss = new WebTextFile( WebControl.Properties.Resources.control );
            this.VFileSystem.Root.PutFile( "control.css", controlcss );
        }

        public WebControlPanel( string title/*, UpdateCallback ucb, object ucb_state*/ ) :
        base( "http://*:25100/" ) {

            this.Title = title;
            /*this._ucb = ucb;
            this._ucb_state = ucb_state;*/

            this.AuthRequired = true;
            this.AuthDomainController = "tbccpadc01.tbccpa.local";
            this.AuthDomain = "tbccpa.local";

            this.SetupFilesystem();

        }
    }
}
