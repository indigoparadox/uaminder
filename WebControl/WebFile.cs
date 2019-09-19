using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WebControl {
    public interface WebFile {
        MemoryStream GetStream();
        WebContainer GetRootContainer();
        WebPage GetRootWebPage();
    }

    public class WebTextFile : WebFile {
        private string _contents;

        public WebTextFile( string contents ) {
            this._contents = contents;
        }

        public WebContainer GetRootContainer() {
            return null;
        }

        public WebPage GetRootWebPage() {
            return null;
        }

        public override string ToString() {
            return this._contents;
        }

        public MemoryStream GetStream() {
            return new MemoryStream( Encoding.UTF8.GetBytes( this._contents ) );
        }
    }
}
