using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.DirectoryServices.Protocols;

namespace WebControl {

    public delegate void WebEventDelegate( WebComponent source, WebSubmission values );
    public delegate WebComponent WebOptionComponentDelegate( WebSubmission values );
    public delegate void WebOptionSubmitDelegate( WebSubmission values );

    public class WebServer : IDisposable {
        private readonly HttpListener _listener = new HttpListener();
        private Thread _listener_thread = null;
        private bool _listening = false;
        private List<string> _auth_exempt_paths = new List<string>();
        private List<Tuple<string, string>> _auth_pairs = new List<Tuple<string, string>>();
        private string _auth_action_url;
        private string _auth_page_url;
        private WebForm _auth_login_form;
        private WebFileSystem _vfilesystem = new WebFileSystem();
        private WebThreads _threads = new WebThreads( "webthreads", 10, ThreadPriority.AboveNormal );

        // URL: Component ID: Handler
        private Dictionary<string, Dictionary<string, WebEventDelegate>> _component_submit_handlers = 
            new Dictionary<string, Dictionary<string, WebEventDelegate>>();
        // URL: Render Handler
        private Dictionary<string, WebEventDelegate> _component_render_handlers = new Dictionary<string, WebEventDelegate>();

        private WebForm _options_form;
        private Dictionary<string, Tuple<WebOptionComponentDelegate, WebOptionSubmitDelegate>> _options_defaults = 
            new Dictionary<string, Tuple<WebOptionComponentDelegate, WebOptionSubmitDelegate>>();
        private string _options_page_url;
        private string _options_action_url;

        public bool OptionsEnabled { get; set; }
        public string OptionsPageURL {
            get { return this._options_page_url; }
            set {
                if( !String.IsNullOrEmpty( this._options_page_url ) ) {
                    this.VFileSystem.RemoveFileByPath( this._options_page_url );
                    this.RemoveRenderHandler( this._options_page_url );
                }
                this._options_page_url = value;
                this.AddRenderHandler( this._options_page_url, this.OnOptionsRender );
                this._options_form = this.SetupOptionsPage( this._options_page_url, this._options_action_url );
            }
        }
        public string OptionsActionURL {
            get { return this._options_action_url; }
            set {
                if( !String.IsNullOrEmpty( this._options_action_url ) ) {
                    this.VFileSystem.RemoveFileByPath( this._options_action_url );
                    this.RemoveSubmitHandler( this._options_action_url, "." );
                }
                this._options_action_url = value;
                this.AddSubmitHandler( this._options_action_url, ".", this.OnOptions );
                this.SetupOptionsPost( this._options_action_url );
                if( null != this._options_form ) {
                    this._options_form.Action = this._options_action_url;
                }
            }
        }

        public bool AuthRequired { get; set; }
        public string AuthPageURL {
            get { return this._auth_page_url; }
            set {
                if( !String.IsNullOrEmpty(this._auth_page_url) ) {
                    this.VFileSystem.RemoveFileByPath( this._auth_page_url );
                }
                this.RemoveAuthExemptPath( this._auth_page_url );
                this._auth_page_url = value;
                this.AddAuthExemptPath( this._auth_page_url );
                this._auth_login_form = this.AuthSetupLoginPage( this._auth_page_url, this._auth_action_url );
            }
        }
        public string AuthActionURL {
            get { return this._auth_action_url; }
            set {
                if( !String.IsNullOrEmpty( this._auth_action_url ) ) {
                    this.VFileSystem.RemoveFileByPath( this._auth_action_url );
                }
                this.RemoveAuthExemptPath( this._auth_action_url );
                this.RemoveSubmitHandler( this._auth_action_url, "." );
                this._auth_action_url = value;
                this.AddSubmitHandler( this._auth_action_url, ".", this.OnLogin );
                this.AddAuthExemptPath( this._auth_action_url );
                //this.VFileSystem.PutFileByPath( this._auth_action_url, auth_login_action );
                this.AuthSetupLoginPost( this._auth_action_url );
                if( null != this._auth_login_form ) {
                    this._auth_login_form.Action = this._auth_action_url;
                }
            }
        }
        public List<string> AuthExemptPaths { get { return this._auth_exempt_paths; } }
        public string AuthDomain { get; set; }
        public string AuthDomainController { get; set; }
        public string DefaultPage { get; set; }
        public WebFileSystem VFileSystem {
            get { return this._vfilesystem; }
        }

        public WebServer() :
            this( "http://*:25100/" ) {
        }

        public WebServer( string prefix ) {
            this.DefaultPage = "/index.html";
            this._auth_exempt_paths.Add( "/control.css" );
            this.AuthActionURL = "/login_p.html";
            this.AuthPageURL = "/login.html";
            this.OptionsPageURL = "/options.html";
            this.OptionsActionURL = "/options_p.html";

            this._listener.Prefixes.Add( prefix );
            this._listener.Start();
        }

        private WebForm AuthSetupLoginPage( string page_url, string action_url ) {
            WebPage loginpage = new WebPage( "Login" );
            this.VFileSystem.Root.PutFile( page_url, loginpage );
            loginpage.AddCSS( "control.css" );

            WebHeader loginheader = new WebHeader( 1, "Login" );
            loginpage.Children.Add( loginheader );

            WebForm login_form = new WebForm( "login", action_url );
            login_form.Children.Add( new WebDiv( "username_wrapper", "login_wrapper", new WebTextInput( "username", "User" ) ) );
            login_form.Children.Add( new WebDiv( "password_wrapper", "login_wrapper", new WebPasswordInput( "password", "Password" ) ) );
            login_form.Children.Add( new WebDiv( "submit_wrapper", "login_wrapper", new WebSubmit( "submit", "Submit", null ) ) );
            loginpage.Children.Add( login_form );

            return login_form;
        }

        private void AuthSetupLoginPost( string action_url ) {
            WebPage loginpost = new WebPage( "Login" );
            this.VFileSystem.Root.PutFile( action_url, loginpost );
        }

        private WebForm SetupOptionsPage( string page_url, string action_url ) {
            WebPage optionspage = new WebPage( "Options" );
            this.VFileSystem.Root.PutFile( page_url, optionspage );
            optionspage.AddCSS( "control.css" );

            WebHeader optionsheader = new WebHeader( 1, "Options" );
            optionspage.Children.Add( optionsheader );

            WebForm options_form = new WebForm( "options", action_url );
            optionspage.Children.Add( options_form );

            return options_form;
        }

        private void SetupOptionsPost( string action_url ) {
            WebPage optionspost = new WebPage( "Options" );
            this.VFileSystem.Root.PutFile( action_url, optionspost );
        }

        private void OnOptionsRender( WebComponent source, WebSubmission values ) {
            WebPage outpage = (WebPage)source;
            WebForm outform = (WebForm)outpage.Children.Find( o => !String.IsNullOrEmpty( o.ID ) && o.ID.Equals( "options" ) );
            
            foreach( KeyValuePair<string,Tuple<WebOptionComponentDelegate, WebOptionSubmitDelegate>> kv in this._options_defaults ) {
                WebComponent comp = kv.Value.Item1( values );
                outform.Children.Add( new WebDiv( "wrapper_" + kv.Key, "options_wrapper", comp ) );
            }

            outform.Children.Add( new WebDiv( "wrapper_submit", "options_wrapper", new WebSubmit( "Submit", "Submit", this.OnOptions ) ) );
        }

        private void OnOptions( WebComponent source, WebSubmission values ) {
            foreach( KeyValuePair<string, Tuple<WebOptionComponentDelegate, WebOptionSubmitDelegate>> kv in this._options_defaults ) {
                kv.Value.Item2( values );
            }
            throw new WebRedirectException( "/" );
        }

        private void OnLogin( WebComponent source, WebSubmission values ) {

            string username = values.PostData["username"][0];
            string password = values.PostData["password"][0];
            bool auth_valid = false;

            if( !this.AuthRequired ) {
                return;
            }

            using( LdapConnection ldc = new LdapConnection( this.AuthDomainController ) ) {
                try {
                    NetworkCredential cred = new NetworkCredential( username, password, this.AuthDomain );
                    ldc.Credential = cred;
                    ldc.Bind();
                    auth_valid = true;
                } catch( LdapException ex ) {
                    Trace.TraceError( ex.ServerErrorMessage );
                    Trace.TraceError( ex.Message );
                    auth_valid = false;
                }
            }

            if( !auth_valid ) {
                throw new WebHTTPException( WebHTTPResponseCode.WEB_HTTP_401_BAD_AUTH, values.RawUrl );
            } else {
                string auth_token = Guid.NewGuid().ToString();
                values.SetCookieOut( "auth_token", auth_token );
                this._auth_pairs.Add( new Tuple<string, string>( values.ClientHostname, auth_token ) );
                throw new WebRedirectException( "/" );
            }
        }

        public bool IsLoggedIn( string clienthostname, string auth_token ) {
            foreach( Tuple<string, string> iter in this._auth_pairs ) {
                // TODO: Fix hostname detection.
                if( /*iter.Item1.Equals( clienthostname ) &&*/ iter.Item2.Equals( auth_token ) ) {
                    return true;
                }
            }
            return false;
        }

        protected virtual byte[] OnGET( WebSubmission values ) {
            WebFile wfile = null;
            WebPage outpage = null;
            string retstring = "";

            if(
                this.AuthRequired &&
                !this._auth_exempt_paths.Contains( values.RawUrl ) &&
                (null == values.GetCookieIn( "auth_token" ) ||
                !this.IsLoggedIn( null, values.GetCookieIn( "auth_token" ).Value ))
            ) {
                throw new WebRedirectException( this.AuthPageURL );
            }

            wfile = this._vfilesystem.GetFileByPath( values.RawUrl );
            
            if( null == wfile ) {
                throw new WebHTTPException( WebHTTPResponseCode.WEB_HTTP_404_NOT_FOUND, values.RawUrl );
            } else if( null != wfile.GetRootWebPage() ) {
                // This is a WebPage derived from WebComponent.
                outpage = (WebPage)wfile.GetRootWebPage().ToCopy();
                if( this._component_render_handlers.ContainsKey( values.RawUrl ) ) {
                    this._component_render_handlers[values.RawUrl]( outpage, values );
                }
                retstring = outpage.Render( values );
            } else if( null != wfile ) {
                // This is just a WebFile.
                retstring = wfile.ToString();
            }

            // TODO: Return binary bytes if wfile is binary.
            return Encoding.UTF8.GetBytes( retstring );
        }

        protected virtual byte[] OnPOST( WebSubmission values ) {
            WebFile wfile = null;
            WebPage outpage = null;
            string retstring = "";

            if(
                this.AuthRequired &&
                !this._auth_exempt_paths.Contains( values.RawUrl ) &&
                (null == values.GetCookieIn( "auth_token" ) ||
                !this.IsLoggedIn( null, values.GetCookieIn( "auth_token" ).Value ))
            ) {
                throw new WebRedirectException( this.AuthPageURL );
            }

            wfile = this._vfilesystem.GetFileByPath( values.RawUrl );
            
            if( null == wfile ) {
                throw new WebHTTPException( WebHTTPResponseCode.WEB_HTTP_404_NOT_FOUND, values.RawUrl );
            } else if( null != wfile.GetRootWebPage() ) {
                // This is a WebPage derived from WebComponent.
                outpage = (WebPage)wfile.GetRootWebPage().ToCopy();
                if( this._component_render_handlers.ContainsKey( values.RawUrl ) ) {
                    this._component_render_handlers[values.RawUrl]( outpage, values );
                }
                // Adjust the fetched page with submission returns.
                outpage.SubmitContainer( this, null, values );
                retstring = outpage.Render( values );
            } else {
                // This is just a WebFile.
                retstring = wfile.ToString();
            }

            // TODO: Return binary bytes if wfile is binary.
            return Encoding.UTF8.GetBytes( retstring );
        }

        public void AddOption( string id, WebOptionComponentDelegate renderer, WebOptionSubmitDelegate submitter ) {
            this._options_defaults.Add( id, new Tuple<WebOptionComponentDelegate,WebOptionSubmitDelegate>( renderer, submitter ) );
        }

        public void AddSubmitHandler( string rawurl, string id, WebEventDelegate handler ) {
            if( !String.IsNullOrEmpty( rawurl ) && !rawurl.StartsWith( "/" ) ) {
                // hreq.rawurl always starts with /
                rawurl = String.Format( "/{0}", rawurl );
            }
            try {
                this._component_submit_handlers[rawurl].Add( id, handler );
            } catch( KeyNotFoundException ) {
                this._component_submit_handlers.Add( rawurl, new Dictionary<string, WebEventDelegate>() );
                this._component_submit_handlers[rawurl].Add( id, handler );
            }
        }

        public void RemoveSubmitHandler( string rawurl, string id ) {
            if( !String.IsNullOrEmpty( rawurl ) && !rawurl.StartsWith( "/" ) ) {
                // hreq.rawurl always starts with /
                rawurl = String.Format( "/{0}", rawurl );
            }
            try {
                this._component_submit_handlers[rawurl].Remove( id );
            } catch( ArgumentNullException ) {
                // XXX
            } catch( KeyNotFoundException ) {
                // XXX
            }
        }

        public WebEventDelegate GetSubmitHandler( string rawurl, string id ) {
            if( !String.IsNullOrEmpty( rawurl ) && !rawurl.StartsWith( "/" ) ) {
                // hreq.rawurl always starts with /
                rawurl = String.Format( "/{0}", rawurl );
            }
            try {
                return this._component_submit_handlers[rawurl][id];
            } catch( ArgumentNullException ) {
                return null;
            } catch( KeyNotFoundException ) {
                return null;
            }
        }

        public void AddRenderHandler( string rawurl, WebEventDelegate handler ) {
            if( !String.IsNullOrEmpty( rawurl ) && !rawurl.StartsWith( "/" ) ) {
                // hreq.rawurl always starts with /
                rawurl = String.Format( "/{0}", rawurl );
            }
            this._component_render_handlers.Add( rawurl, handler );
        }

        public void RemoveRenderHandler( string rawurl ) {
            try {
                this._component_render_handlers.Remove( rawurl );
            } catch( ArgumentNullException ) {
                // XXX
            } catch( KeyNotFoundException ) {
                // XXX
            }
        }

        public void AddAuthExemptPath( string rawurl ) {
            if( !String.IsNullOrEmpty( rawurl ) && !rawurl.StartsWith( "/" ) ) {
                // hreq.rawurl always starts with /
                rawurl = String.Format( "/{0}", rawurl );
            }
            this._auth_exempt_paths.Add( rawurl );
        }

        public void RemoveAuthExemptPath( string path ) {
            if( this._auth_exempt_paths.Contains( path ) ) {
                this._auth_exempt_paths.Remove( path );
            } else if(
                !String.IsNullOrEmpty( path ) &&
                !path.StartsWith( "/" ) &&
                this._auth_exempt_paths.Contains( String.Format( "/{0}", path ) )
            ) {
                this._auth_exempt_paths.Remove( String.Format( "/{0}", path ) );
            }
        }

        private void HandleConnection( object state ) {
            HttpListenerContext ctx = state as HttpListenerContext;
            HttpListenerRequest hreq = ctx.Request;
            HttpListenerResponse hresp = ctx.Response;
            byte[] retbytes = new byte[] { };
            string rawurl_real;

            if( hreq.RawUrl.Length < 2 ) {
                rawurl_real = this.DefaultPage;
            } else {
                rawurl_real = hreq.RawUrl;
            }

            WebSubmission values = new WebSubmission( rawurl_real, "XXXFIXME", hresp, hreq );

            try {
                switch( hreq.HttpMethod ) {
                case "POST":
                    retbytes = this.OnPOST( values );
                    break;
                case "GET":
                    retbytes = this.OnGET( values );
                    break;
                default:
                    // TODO: HTTP Bad Method Error
                    throw new WebHTTPException( WebHTTPResponseCode.WEB_HTTP_405_BAD_METHOD, hreq.RawUrl );
                }

                hresp.ContentLength64 = retbytes.Length;
                hresp.OutputStream.Write( retbytes, 0, retbytes.Length );
            } catch( HttpListenerException ex ) {
                Trace.TraceError( ex.Message );
                // XXX
            } catch( WebHTTPException ex ) {
                hresp.StatusCode = ex.GetResponseCodeNumber();
                retbytes = Encoding.UTF8.GetBytes( ex.ToString() );
                ctx.Response.ContentLength64 = retbytes.Length;
                ctx.Response.OutputStream.Write( retbytes, 0, retbytes.Length );
            } catch( WebRedirectException ex ) {
                hresp.Redirect( ex.RedirectURL );
            } finally {
                ctx.Response.OutputStream.Close();
            }
        }

        private void StartListening() {
            Trace.TraceInformation( "Listening for connections..." );
            try {
                while( this._listener.IsListening && this._listening ) {
                    //ThreadPool.QueueUserWorkItem( this.HandleConnection, this._listener.GetContext() );
                    this._threads.AddHandler( this.HandleConnection, this._listener.GetContext() );
                }
            } catch {
                // XXX
            }
        }

        public void Run() {
            //ThreadPool.QueueUserWorkItem( this.StartListening );
            this._listening = true;
            this._listener_thread = new Thread( this.StartListening );
            this._listener_thread.Name = "Listener Thread";
            this._listener_thread.Priority = ThreadPriority.Normal;
            this._listener_thread.Start();
        }

        public void Stop() {
            this._listening = false;
            this._listener.Stop();
            this._listener.Close();
        }

        public void Dispose() {
            //this._listener.Dispose();
        }
    }
}
