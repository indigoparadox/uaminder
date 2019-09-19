using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebControl;
using RestSharp;
using System.Security.Cryptography;
using System.Threading;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System.Security;

namespace AppMinder {
    public class ProcMinderApp : IDisposable {
        private ProcMinder _pm = null;
        private WebControlPanel _cp = null;
        private RestClient _rc = null;
        private string _rc_url;
        private string _api_key = null;
        private EventLog _logger = null;
        private static ProcMinderApp _pma_singleton = null;

        public bool Running {
            get;
            set;
        }

        public static ProcMinderApp GetSingleton() {
            if( null == _pma_singleton ) {
                _pma_singleton = new ProcMinderApp();
            }
            return _pma_singleton;
        }

        private void LogInfo( string message ) {
            if( null != this._logger ) {
                this._logger.WriteEntry( message, EventLogEntryType.Information );
            } else {
                Console.WriteLine( message );
            }
        }

        private void LogWarning( string message ) {
            if( null != this._logger ) {
                this._logger.WriteEntry( message, EventLogEntryType.Warning );
            } else {
                Console.WriteLine( message );
            }
        }

        private string RegistryGet( string value, string error_msg ) {
            try {
                using( RegistryKey key = Registry.LocalMachine.OpenSubKey( "Software\\UserAppMinder" ) ) {
                    Object o = key.GetValue( value );
                    if( null == o ) {
                        if( !String.IsNullOrEmpty( error_msg ) ) {
                            this.LogWarning( error_msg );
                        }
                        return null;
                    } else {
                        return (string)o;
                    }
                }
            } catch( NullReferenceException ) {
                if( !String.IsNullOrEmpty( error_msg ) ) {
                    this.LogWarning( error_msg );
                }
                return null;
            }
        }
        
        public ProcMinderApp() {
            this._rc_url = RegistryGet( "ReportServer", "No proc server found. Disabling telemetry." );
            if( null != this._rc_url ) {
                this._rc = new RestClient( this._rc_url );
            }
            this._api_key = RegistryGet( "APIKey", "No API key found. Disabling telemetry verification." );
        }

        private void ReportProcs( ProcMinder pm, object state ) {
            string report_time = DateTime.Now.ToString( "yyyy-MM-dd HH:mm:ss" );
            RestRequest req = new RestRequest( "post/proc", Method.POST );

            string verify_line = String.Format(
                "{0}:{1}",
                Environment.MachineName,
                report_time
            );

            Trace.TraceInformation( "Reporting process statistics..." );
            StringBuilder procs_list = new StringBuilder();
            foreach( ProcObject proc in pm.Processes ) {

                if(
                    null == this._rc ||
                    String.IsNullOrEmpty( proc.ProcName ) ||
                    String.IsNullOrEmpty( proc.ProcUser ) ||
                    0 >= proc.ProcId
                ) {
                    continue;
                }

                procs_list.Append( ", " );
                procs_list.Append( proc.ToJson() );
            }

            if( 0 < procs_list.Length ) {
                procs_list.Remove( 0, 2 ); // Remove first comma.
            }
            procs_list.Insert( 0, "[" );
            procs_list.Append( "]" );

            req.AddParameter( "procs", procs_list.ToString() );
            req.AddParameter( "time", report_time );
            req.AddParameter( "host", Environment.MachineName );

            // HMAC hashing to verify integrity.
            if( null != this._api_key ) {
                HMACSHA256 hmac = new HMACSHA256( Encoding.UTF8.GetBytes( this._api_key ) );
                byte[] hash = hmac.ComputeHash( Encoding.UTF8.GetBytes( verify_line ) );
                req.AddParameter( "hash", Convert.ToBase64String( hash ) );
            }

            IRestResponse res = this._rc.Execute( req );

        }

#if false
        private void ReportProc( ProcObject proc, object state ) {
            string report_time = (string)state;
            string verify_line = String.Format(
                "{0}:{1}:{2}:{3}:{4}",
                proc.ProcName,
                proc.ProcId.ToString(),
                proc.ProcUser,
                Environment.MachineName,
                report_time
            );

            //Console.WriteLine( verify_line );

            if(
                null == this._rc ||
                String.IsNullOrEmpty( proc.ProcName ) ||
                String.IsNullOrEmpty( proc.ProcUser ) ||
                0 >= proc.ProcId
            ) {
                return;
            }

            /* Console.WriteLine( "Reporting process statistics..." );
            foreach( KeyValuePair<int, ProcObject> kv in pm.Processes ) { */
            //ProcObject proc = kv.Value;
            RestRequest req = new RestRequest( "post/proc", Method.POST );

            req.AddParameter( "name", proc.ProcName );
            req.AddParameter( "id", proc.ProcId.ToString() );
            req.AddParameter( "user", proc.ProcUser );
            req.AddParameter( "host", Environment.MachineName );
            req.AddParameter( "cpu", proc.CPUPercent.ToString() );
            req.AddParameter( "iorbs", proc.ReadBytesSec.ToString() );
            req.AddParameter( "iowbs", proc.WriteBytesSec.ToString() );
            req.AddParameter( "workingset", proc.WorkingSet.ToString() );
            req.AddParameter( "timestamp", report_time );

            // HMAC hashing to verify integrity.
            if( null != this._api_key ) {
                HMACSHA256 hmac = new HMACSHA256( Encoding.UTF8.GetBytes( this._api_key ) );
                byte[] hash = hmac.ComputeHash( Encoding.UTF8.GetBytes( verify_line ) );
                req.AddParameter( "hash", Convert.ToBase64String( hash ) );
            }

            IRestResponse res = this._rc.Execute( req );

            //}
        }
#endif

        private void OnKillButton( WebComponent source, WebSubmission values ) {
            //Console.WriteLine( values.Headers.ToString() );
        }

        public void UpdatePage( WebComponent source, WebSubmission values ) {
            WebPage page = (WebPage)source;
            try {
                WebForm proc_form = (WebForm)page.Children.Where( o => o.ID.Equals( "proc_form" ) ).First();
                WebTable proc_table = (WebTable)proc_form.Children.Where( o => o.ID.Equals( "processes" ) ).First();
                proc_table.ClearRows();
                foreach( ProcObject proc in this._pm.Processes ) {
                    WebComponent icon_img = null;
                    if( null == proc.JpegB64 ) {
                        proc.JpegB64 = "";
                        try {
                            Bitmap pico = Icon.ExtractAssociatedIcon( proc.Filename ).ToBitmap();
                            icon_img = new WebImage( pico );
                            proc.JpegB64 = ((WebImage)icon_img).Src;
                        } catch( Exception ) {
                            // XXX
                        }
                    } else {
                        icon_img = new WebImage( proc.JpegB64 );
                    }

                    if( null != icon_img ) {
                        ((WebImage)icon_img).WidthPx = 16;
                        ((WebImage)icon_img).HeightPx = 16;
                    } else {
                        icon_img = new WebText( "" );
                    }

                    proc_table.AddBodyRow(
                        "proc-row-" + proc.ProcId,
                        icon_img,
                        new WebText( proc.ProcName ),
                        new WebText( proc.ProcId.ToString() ),
                        new WebText( proc.ProcUser ),
                        new WebText( proc.CPUPercent.ToString( "n2" ) ),
                        new WebText( WebComponent.FormatDataSize( proc.ReadBytesSec ) ),
                        new WebText( WebComponent.FormatDataSize( proc.WriteBytesSec ) ),
                        new WebText( WebComponent.FormatDataSize( proc.WorkingSet ) ),
                        new WebCheckbox( "Kill", "proc_guid", proc.GUID.ToString() )
                    );
                }
            } catch(InvalidOperationException ex ) {
                Trace.TraceError( ex.Message );
            } catch( NullReferenceException ex ) {
                Trace.TraceError( ex.Message );
            }
        }

        public WebComponent OptRenderAPIKey( WebSubmission values ) {
            return new WebTextInput( "APIKey", "API Key", RegistryGet( "APIKey", null ) );
        }

        public void OnOptAPIKey( WebSubmission values ) {
            // XXX: Save
            Console.WriteLine( values.PostData["APIKey"][0] );
        }

        public WebComponent OptRenderReportServer( WebSubmission values ) {
            return new WebTextInput( "ReportServer", "Reporting Server", RegistryGet( "ReportServer", null ) );
        }

        public void OnOptReportServer( WebSubmission values ) {
            // XXX: Save
            Console.WriteLine( values.PostData["ReportServer"][0] );
        }

        public void Run() {

            try {
                // Setup event log.
                if( !EventLog.SourceExists( "ProcMinder" ) ) {
                    EventLog.CreateEventSource( "ProcMinder", "Application" );
                    Console.WriteLine( "Please restart the application to finish initializing logger." );
                    //return;
                }

                // Create an EventLog instance and assign its source.
                this._logger = new EventLog();
                this._logger.Source = "ProcMinder";
            } catch( SecurityException ex ) {
                Console.WriteLine( String.Format( "Unable to setup logging: {0}", ex.Message ) );
            }

            if( null != this._rc_url ) {
                this._pm = new ProcMinder( this.ReportProcs, null, this._logger );
            } else {
                this._pm = new ProcMinder();
            }

            string interval_str = RegistryGet( "Interval", "No interval key found, using default: " + this._pm.Interval.ToString() );
            if( null != interval_str ) {
                this._pm.Interval = int.Parse( interval_str );
            }

            // Setup the base page.
            this._cp = new WebControlPanel( "Process Minder"/*, this.UpdatePage, this._pm*/ );
            this._cp.AddRenderHandler( "/index.html", this.UpdatePage );

            WebDiv nav = new WebDiv( "nav" );

            WebLink nav_options = new WebLink( "/options.html", new WebText( "Options" ) );
            nav.Children.Add( nav_options );

            WebTable proc_table = new WebTable();
            proc_table.SetHeadRow(
                null,
                new WebText( "" ),
                new WebText( "Process Name" ),
                new WebText( "Process ID" ),
                new WebText( "User" ),
                new WebText( "CPU Time" ),
                new WebText( "Read Bytes/s" ),
                new WebText( "Write Bytes/s" ),
                new WebText( "Working Set" ),
                new WebSubmit( "execute", "Execute", this.OnKillButton )
            );
            proc_table.ID = "processes";

            WebForm proc_form = new WebForm( "proc_form", "/" );
            proc_form.Children.Add( proc_table );

            WebPage page = (WebPage)this._cp.VFileSystem.Root.GetFile( "index.html" );
            page.Children.Add( nav );
            page.Children.Add( proc_form );

            this._cp.AddOption( "APIKey", this.OptRenderAPIKey, this.OnOptAPIKey );
            this._cp.AddOption( "ReportServer", this.OptRenderReportServer, this.OnOptReportServer );

            this.LogInfo( "Service started." );

            // Start polling and serving.
            this._pm.Run();
            this._cp.Run();

            this.Running = true;

            /* while( this.Running ) {
                Thread.Sleep( 10000 );
            } */
        }

        public void Stop() {
            try {
                this._pm.Stop();
                this._cp.Stop();
            } catch( NullReferenceException ex ) {
                this.LogWarning( ex.Message );
            }
        }

        public void Dispose() {
            this._logger.Dispose();
        }
    }
}
