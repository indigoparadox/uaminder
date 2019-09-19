using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WebControl {

    public class WebSubmission {
        private Dictionary<string, List<string>> _post_data = new Dictionary<string, List<string>>();
        private HttpListenerRequest _request = null;
        private HttpListenerResponse _response = null;
        private string _request_rawurl;

        public NameValueCollection Headers { get { return this._response.Headers; } }
        public Dictionary<string, List<string>> PostData { get { return this._post_data; } }
        //public string Filename { get; set; }
        public string ClientHostname { get; set; }
        public string RawUrl { get { return this._request_rawurl; } }

        public Cookie GetCookieIn( string name ) {
            return this._request.Cookies[name];
        }

        public void SetCookieOut( string name, string value ) {
            this.SetCookieOut( name, value, null, null, new DateTime( 0 ) );
        }

        public void SetCookieOut( string name, string value, string path ) {
            this.SetCookieOut( name, value, path );
        }

        public void SetCookieOut( string name, string value, DateTime expires ) {
            this.SetCookieOut( name, value, null, null, expires );
        }

        public void SetCookieOut( string name, string value, string path, string domain, DateTime expires ) {
            Cookie new_cookie = new Cookie();

            if( !String.IsNullOrEmpty( path ) ) {
                if( !String.IsNullOrEmpty( domain ) ) {
                    new_cookie = new Cookie( name, value, path, domain );
                } else {
                    new_cookie = new Cookie( name, value, path );
                }
            } else {
                new_cookie = new Cookie( name, value );
            }

            if( 0 != expires.Ticks ) {
                new_cookie.Expires = expires;
            }

            this._response.SetCookie( new_cookie );
        }

        protected static Dictionary<string, List<string>> GetPostData( HttpListenerRequest request ) {
            Dictionary<string, List<string>> outvals = new Dictionary<string, List<string>>();

            string request_body = "";
            using( Stream body = request.InputStream ) {
                using( StreamReader reader = new StreamReader( body ) ) {
                    request_body = reader.ReadToEnd();
                }
            }

            if( String.IsNullOrEmpty( request_body ) ) {
                Trace.TraceInformation( "Request with empty body received." );
                return outvals;
            }

            foreach( string pair in request_body.Split( '&' ) ) {
                string[] split_pair = pair.Split( '=' );
                string pair_key = WebUtility.UrlDecode( split_pair[0] );

                if( !outvals.ContainsKey( pair_key ) ) {
                    outvals.Add( pair_key, new List<string>() );
                }
                outvals[pair_key].Add( WebUtility.UrlDecode( split_pair[1] ) );
            }

            return outvals;
        }

        public WebSubmission( string filename, string clienthostname, HttpListenerResponse resp, HttpListenerRequest request ) {
            this._post_data = GetPostData( request );
            this._request = request;
            this._response = resp;
            this._request_rawurl = filename;
            this.ClientHostname = clienthostname;
        }
    }
}
