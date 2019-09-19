using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Specialized;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;

namespace WebControl {

    [Serializable]
    public abstract class WebComponent {

        protected static string[] _data_sizes = { "B", "KB", "MB", "GB", "TB", "PB" };
        private Guid _guid = Guid.NewGuid();

        private List<string> _classes = new List<string>();
        protected string _tagname = null;

        public List<string> Classes { get { return this._classes; } }
        public string ID { get; set; }
        public Guid GUID { get { return this._guid; } }

        public WebComponent ToCopy() {
            IFormatter formatter = new BinaryFormatter();
            Stream stream = new MemoryStream();
            using( stream ) {
                formatter.Serialize( stream, this );
                stream.Seek( 0, SeekOrigin.Begin );
                return (WebComponent)formatter.Deserialize( stream );
            }
        }

        public static string RenderPair( string name, string val ) {
            if( !String.IsNullOrEmpty( val ) ) {
                return String.Format( "{0}=\"{1}\" ", name, val );
            } else {
                return "";
            }
        }

        public static string FormatDataSize( float bytes ) {
            int size_index = 0;

            while( 1024 < bytes && WebComponent._data_sizes.Length > size_index + 1 ) {
                bytes /= 1024;
                size_index++;
            }

            return String.Format( "{0:n2} {1}", bytes, WebComponent._data_sizes[size_index] );
        }

        public abstract string Render( WebSubmission values );

        protected virtual string RenderOpenTag() {
            StringBuilder outstr = new StringBuilder();

            outstr.Append( String.Format( "<{0} ", this._tagname ) );

            if( 0 < this.Classes.Count ) {
                outstr.Append( RenderPair( "class", String.Join( " ", this.Classes ) ) );
            }
            if( !String.IsNullOrEmpty( this.ID ) ) {
                outstr.Append( RenderPair( "id", this.ID ) );
            }

            return outstr.ToString();
        }

        public virtual void SubmitChild( WebServer server, WebComponent source, WebSubmission values ) {
            if( !string.IsNullOrEmpty( this.ID ) && null != server.GetSubmitHandler( values.RawUrl, this.ID ) ) {
                server.GetSubmitHandler( values.RawUrl, this.ID )( source, values );
            }
        }
    }

    [Serializable]
    public abstract class WebContainer : WebComponent {

        protected List<WebComponent> _children = new List<WebComponent>();

        public List<WebComponent> Children {
            get { return this._children; }
        }

        protected void SubmitChildren( WebServer server, WebComponent source, WebSubmission values ) {

            foreach( WebComponent child in this._children ) {
                if( child.GetType().IsSubclassOf( typeof( WebContainer ) ) ) {
                    ((WebContainer)child).SubmitContainer( server, source, values );
                }
                child.SubmitChild( server, source, values );
            }
        }

        public virtual void SubmitContainer( WebServer server, WebComponent source, WebSubmission values ) {
            this.SubmitChildren( server, source, values );
        }

        public override string Render( WebSubmission values ) {
            StringBuilder outstr = new StringBuilder();
            if( null != this._tagname ) {
                outstr.Append( String.Format( "{0}>", base.RenderOpenTag() ) );
            }
            foreach( WebComponent child in this._children ) {
                outstr.Append( child.Render( values ) );
            }
            if( null != this._tagname ) {
                outstr.Append( String.Format( "</{0}>", this._tagname ) );
            }
            return outstr.ToString();
        }
    }

    [Serializable]
    public class WebDiv : WebContainer {
        public WebDiv( string id ) : this( id, "", null ) {
        }

        public WebDiv( string id, params WebComponent[] children ) : this( id, "", children ) {
        }

        public WebDiv( string id, string classin, params WebComponent[] children ) {
            this.ID = id;
            if( !String.IsNullOrEmpty( classin ) ) {
                this.Classes.Add( classin );
            }
            if( null != children ) {
                this.Children.AddRange( children );
            }
            this._tagname = "div";
        }
    }

    [Serializable]
    public class WebLink : WebContainer {

        public string Href { get; set; }
        public string Title { get; set; }
        public string Name { get; set; }

        public WebLink( string name ) : this( null, null ) {
            this.Name = name;
        }

        public WebLink( string href, params WebComponent[] children ) {
            this.Href = href;
            if( null != children ) {
                this.Children.AddRange( children );
            }
            this._tagname = "a";
        }

        public override string Render( WebSubmission values ) {
            StringBuilder outstr = new StringBuilder();

            outstr.Append( base.RenderOpenTag() );
            outstr.Append( RenderPair( "name", this.Name ) );
            outstr.Append( RenderPair( "href", this.Href ) );
            outstr.Append( RenderPair( "title", this.Title ) );

            if( 0 < this.Children.Count ) {
                outstr.Append( ">" );
                foreach( WebComponent child in this._children ) {
                    outstr.Append( child.Render( values ) );
                }
                if( null != this._tagname ) {
                    outstr.Append( String.Format( "</{0}>", this._tagname ) );
                }
            } else {
                outstr.Append( " />" );
            }

            return outstr.ToString();
        }
    }

    [Serializable]
    public class WebForm : WebContainer {
        public string Action = "";

        public WebForm( string id, string action ) {
            this._tagname = "form";
            this.Action = action;
            this.ID = id;
        }

        public override string Render( WebSubmission values ) {
            StringBuilder outstr = new StringBuilder();
            string xsrf_token = Guid.NewGuid().ToString();

            outstr.Append( String.Format( "{0}", base.RenderOpenTag() ) );
            outstr.Append( RenderPair( "method", "post" ) );
            outstr.Append( RenderPair( "action", this.Action ) );
            outstr.Append( ">" );

            // Free XSRF protection.
            outstr.Append( String.Format( "<input type=\"hidden\" name=\"proc_xsrf_{0}\" value=\"{1}\" />", this.ID, xsrf_token ) );
            values.SetCookieOut( String.Format( "proc_xsrf_{0}", this.ID ), xsrf_token );

            foreach( WebComponent child in this._children ) {
                outstr.Append( child.Render( values ) );
            }
            if( null != this._tagname ) {
                outstr.Append( String.Format( "</{0}>", this._tagname ) );
            }
            return outstr.ToString();
        }

        public override void SubmitContainer( WebServer server, WebComponent source, WebSubmission values ) {
            string proc_xsrf_key = String.Format( "proc_xsrf_{0}", this.ID );
            string proc_xsrf_cookie = values.GetCookieIn( proc_xsrf_key ).Value;
            string proc_xsrf_form = values.PostData[proc_xsrf_key][0];

            if( !proc_xsrf_form.Equals( proc_xsrf_cookie ) ) {
                throw new WebHTTPException( WebHTTPResponseCode.WEB_HTTP_403_ACCESS_DENIED, values.RawUrl );
            } else {
                this.SubmitChildren( server, source, values );
            }
        }
    }

    [Serializable]
    public class WebHeader : WebComponent {
        private int _level = 1;

        public string Text { get; set; }
        public int Level {
            get {
                return this._level;
            }
            set {
                this._tagname = String.Format( "h{0}", value );
                this._level = value;
            }
        }

        public WebHeader( int level, string text ) {
            this.Level = level;
            this.Text = text;
        }

        public override string Render( WebSubmission values ) {
            StringBuilder outstr = new StringBuilder();

            outstr.Append( String.Format( "{0}>", this.RenderOpenTag() ) );
            outstr.Append( this.Text );
            outstr.Append( String.Format( "</{0}>", this._tagname ) );

            return outstr.ToString();
        }
    }

    [Serializable]
    public abstract class WebInput : WebComponent {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Size { get; set; }
        public string Label { get; set; }

        protected string _type = "";

        public WebInput( string name, string label ) {
            this._tagname = "input";
            this.Name = name;
            this.Label = label;
        }

        public override string Render( WebSubmission values ) {
            StringBuilder outstr = new StringBuilder();

            if( !String.IsNullOrEmpty( this.Label ) ) {
                outstr.Append( String.Format( "<label for=\"{0}\">{1}</label>", this.Name, this.Label ) );
            }

            outstr.Append( base.RenderOpenTag() );

            outstr.Append( RenderPair( "type", this._type ) );
            outstr.Append( RenderPair( "name", this.Name ) );
            outstr.Append( RenderPair( "size", this.Size ) );
            outstr.Append( RenderPair( "value", this.Value ) );

            outstr.Append( "/>" );

            return outstr.ToString();
        }
    }

    [Serializable]
    public class WebSubmit : WebInput {
        public WebSubmit( string name, string value, WebEventDelegate on_submit ) : base( name, null ) {
            this.Value = value;
            //this.OnSubmit = on_submit;
            this._type = "submit";
        }
    }

    [Serializable]
    public class WebCheckbox : WebInput {
        public bool Checked { get; set; }
        public WebCheckbox( string name, string label, string value ) : base( name, label ) {
            this.Value = value;
            this._type = "checkbox";
            this.Checked = false;
        }
    }

    [Serializable]
    public class WebTextInput : WebInput {
        public WebTextInput( string name, string label ) : this( name, label, "" ) {
        }

        public WebTextInput( string name, string label, string value ) : base( name, label ) {
            this.Value = value;
            this._type = "text";
        }
    }

    [Serializable]
    public class WebPasswordInput : WebInput {
        public WebPasswordInput( string name, string label ) : this( name, label, "" ) {
        }

        public WebPasswordInput( string name, string label, string value ) : base( name, label ) {
            this.Value = value;
            this._type = "password";
        }
    }

    [Serializable]
    public class WebPage : WebContainer, WebFile {

        private string _title = "";
        private List<string> _css = new List<string>();

        public WebPage( string title ) {
            this._title = title;
        }

        public override string ToString() {
            return this.Render( null );
        }

        public MemoryStream GetStream() {
            return new MemoryStream( Encoding.UTF8.GetBytes( this.Render( null ) ) );
        }

        public WebContainer GetRootContainer() {
            return this;
        }

        public void AddCSS( string src ) {
            this._css.Add( src );
        }

        public new string Render( WebSubmission values ) {
            StringBuilder output = new StringBuilder();

            output.Append( "<!DOCTYPE HTML>\n<html>\n<head>\n<title>" + this._title + "</title>\n" );

            foreach( string css_src in this._css ) {
                output.Append( "<link rel=\"stylesheet\" type=\"text/css\" href=\"" + css_src + "\" />\n" );
            }

            output.Append( "</head>\n<body>" );

            output.Append( base.Render( values ) );

            output.Append( "</body>\n</html>" );

            return output.ToString();
        }

        public override void SubmitContainer( WebServer server, WebComponent source, WebSubmission values ) {
            if( null != server.GetSubmitHandler( values.RawUrl, "." ) ) {
                server.GetSubmitHandler( values.RawUrl, "." )( source, values );
            }
            this.SubmitChildren( server, source, values );
        }

        public WebPage GetRootWebPage() {
            return this;
        }
    }

    [Serializable]
    public class WebText : WebComponent {
        public string Text { get; set; }

        public WebText( string text ) {
            this.Text = text;
        }

        public override string Render( WebSubmission values ) {
            return Text;
        }
    }

    [Serializable]
    public class WebImage : WebComponent {

        public string Src {
            get; set;
        }

        public string Alt {
            get; set;
        }

        public string Title {
            get; set;
        }

        public int WidthPx {
            get; set;
        }

        public int HeightPx {
            get; set;
        }

        public WebImage( Bitmap inline ) {
            using( MemoryStream ms = new MemoryStream() ) {
                inline.Save( ms, ImageFormat.Png );
                this.Src = "data:image / png; base64," + Convert.ToBase64String( ms.ToArray() );
            }
            this.WidthPx = 0;
            this.HeightPx = 0;
            this._tagname = "img";
        }

        public WebImage( string src ) {
            this.Src = src;
            this.WidthPx = 0;
            this.HeightPx = 0;
            this._tagname = "img";
        }

        public override string Render( WebSubmission values ) {
            StringBuilder outstr = new StringBuilder();

            outstr.Append( base.RenderOpenTag() );

            outstr.Append( RenderPair( "src", this.Src ) );
            outstr.Append( RenderPair( "alt", this.Alt ) );
            outstr.Append( RenderPair( "title", this.Title ) );

            if( 0 < this.WidthPx ) {
                outstr.Append( String.Format( "width=\"{0}px\" ", this.WidthPx ) );
            }

            if( 0 < this.HeightPx ) {
                outstr.Append( String.Format( "height=\"{0}px\" ", this.HeightPx ) );
            }

            outstr.Append( "/>" );

            return outstr.ToString();
        }
    }

    [Serializable]
    public class WebTable : WebContainer {
        [Serializable]
        private class WebCell : WebContainer {
            public WebCell( WebComponent c ) {
                this._tagname = "td";
                this.Children.Add( c );
            }
        }

        [Serializable]
        private class WebTableHeadCell : WebCell {
            public WebTableHeadCell( WebComponent c ) : base( c ) {
                this._tagname = "th";
            }
        }

        [Serializable]
        private class WebRow : WebContainer {
            public WebRow( WebCell[] cells ) {
                this._tagname = "tr";
                this._children.AddRange( cells );
            }
        }

        [Serializable]
        private class WebTableHead : WebContainer {
            public WebTableHead( WebRow r ) {
                this._tagname = "thead";
                this._children.Add( r );
            }
        }

        [Serializable]
        private class WebTableBody : WebContainer {
            public WebTableBody() {
                this._tagname = "tbody";
            }
        }

        //private List<WebRow> _rows = new List<WebRow>();
        //private WebRow _headers = null;

        private WebTableHead _thead = null;
        private WebTableBody _tbody = null;

        public WebTable() {
            this._tagname = "table";
        }

        /* public void SetHeaders( params WebComponent[] cells ) {
            WebRow row = new WebRow();
            row.Children.AddRange( cells );
            this._headers = row;
        } */

        public void SetHeadRow( string id, params WebComponent[] cells ) {
            WebRow row = new WebRow( cells.Select( o => new WebTableHeadCell( o ) ).ToArray() );
            row.ID = id;
            if( null == this._thead ) {
                this._thead = new WebTableHead( row );
                this._children.Add( this._thead );
            } else {
                this._thead.Children.Clear();
                this._thead.Children.Add( new WebTableHead( row ) );
            }
        }

        public void AddBodyRow( string id, params WebComponent[] cells ) {
            WebRow row = new WebRow( cells.Select( o => new WebCell( o ) ).ToArray() );
            row.ID = id;
            if( null == this._tbody ) {
                this._tbody = new WebTableBody();
                this._children.Add( this._tbody );
            }
            this._tbody.Children.Add( row );
        }

        /* public void DelRow( string id ) {
            this._rows.Remove( id );
        } */

        public void ClearRows() {
            if( null != this._tbody ) {
                this._tbody.Children.Clear();
            }
        }

            /*
        public override string Render() {
            StringBuilder outstr = new StringBuilder();

            outstr.Append( String.Format( "{0}>", base.Render() ) );

            if( null != this._headers ) {
                outstr.Append( "<thead>" );
                outstr.Append( "<tr>" );
                foreach( WebComponent cell in this._headers.Cells ) {
                    outstr.Append( "<th>" + cell.Render() + "</th>" );
                }
                outstr.Append( "</tr>\n" );
                outstr.Append( "</thead>" );
            }

            outstr.Append( "<tbody>" );

            int row_index = 1;
            foreach( WebRow row in this._rows ) {
                if( 0 == row_index % 2 ) {
                    outstr.Append( "<tr class=\"even\">" );
                } else {
                    outstr.Append( "<tr class=\"odd\">" );
                }
                foreach( WebComponent cell in kv.Value.Cells ) {
                    outstr.Append( "<td>" + cell.Render() + "</td>" );
                }
                outstr.Append( "</tr>\n" );
                row_index++;
            }

            outstr.Append( "</tbody></table>" );

            return outstr.ToString();
        }
        */
    }

    [Serializable]
    public class WebSelect : WebContainer {

        public WebSelect( bool dropdown ) {
            this._tagname = "select";
        }

        /*
        public override string Render() {
            StringBuilder outstr = new StringBuilder();

            outstr.Append( String.Format( "{0}>", base.Render() ) );

            foreach(KeyValuePair<string,string> kv in this._options ) {
                outstr.Append( "<option value=\"" + kv.Value + "\">" + kv.Key + "</option>" );
            }

            outstr.Append( "</select>" );

            return outstr.ToString();
        }
        */

        /* public override void Submit( WebSubmission values ) {
            this.OnSubmitted( values );
        } */

        /* public EventDelegate OnSelected {
            get;
            set;
        } */
    }
}
