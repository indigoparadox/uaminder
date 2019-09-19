using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebControl {
    [Serializable]
    public class WebException : Exception {

    }

    public enum WebHTTPResponseCode {
        WEB_HTTP_401_BAD_AUTH = 401,
        WEB_HTTP_403_ACCESS_DENIED = 403,
        WEB_HTTP_404_NOT_FOUND = 404,
        WEB_HTTP_405_BAD_METHOD = 405,
    }

    public class WebRedirectException : WebException {
        public string RedirectURL { get; set; }

        public WebRedirectException( string url ) {
            this.RedirectURL = url;
        }
    }

    public class WebHTTPException : WebException {

        private WebHTTPResponseCode _response;
        private string _path;

        public WebHTTPResponseCode Response { get { return this._response; } }
        public string Path { get { return this._path; } }

        public WebHTTPException( WebHTTPResponseCode response, string path ) {
            this._response = response;
            this._path = path;
        }

        public int GetResponseCodeNumber() {
            return (int)this._response;
        }

        public override string ToString() {
            string message = "";

            switch( this._response ) {
            case WebHTTPResponseCode.WEB_HTTP_401_BAD_AUTH:
                message = "Authentication failed.";
                break;
            case WebHTTPResponseCode.WEB_HTTP_403_ACCESS_DENIED:
                message = "Access denied or invalid CSRF token.";
                break;
            case WebHTTPResponseCode.WEB_HTTP_404_NOT_FOUND:
                message = "Not found.";
                break;
            case WebHTTPResponseCode.WEB_HTTP_405_BAD_METHOD:
                message = "Bad method.";
                break;
            }

            return String.Format( "{0} accessing {1}: {2}", this._response, this._path, message );
        }
    }
}
