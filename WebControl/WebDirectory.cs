using System;
using System.Collections.Generic;
using System.Text;

namespace WebControl {
    public class WebDirectory {
        private Dictionary<string, WebFile> files = new Dictionary<string, WebFile>();
        private Dictionary<string, WebDirectory> subdirs = new Dictionary<string, WebDirectory>();

        public WebFile GetFile( string filename ) {
            if( this.files.ContainsKey( filename ) ) {
                return this.files[filename];
            }
            return null;
        }

        public void PutFile( string filename, WebFile file ) {
            if( filename.StartsWith( "/" ) ) {
                filename = filename.Substring( 1 );
            }
            this.files[filename] = file;
        }

        public void RemoveFile( string filename ) {
            if( filename.StartsWith( "/" ) ) {
                filename = filename.Substring( 1 );
            }
            if( this.files.ContainsKey( filename ) ) {
                this.files.Remove( filename );
            }
        }

        public bool IsEmpty() {
            if( 0 < this.files.Count || 0 < this.subdirs.Count ) {
                return false;
            }
            return true;
        }

        public WebDirectory GetSubdirectory(string dirname) {
            if( dirname.StartsWith( "/" ) ) {
                dirname = dirname.Substring( 1 );
            }
            if( this.subdirs.ContainsKey( dirname ) ) {
                return this.subdirs[dirname];
            }
            
            // XXX
            return null;
        }

        public void CreateSubdirectory( string dirname ) {
            if( dirname.StartsWith( "/" ) ) {
                dirname = dirname.Substring( 1 );
            }
            if( !this.subdirs.ContainsKey( dirname ) ) {
                this.subdirs[dirname] = new WebDirectory();
            }
        }

        public void RemoveSubdirectory( string dirname ) {
            if( dirname.StartsWith( "/" ) ) {
                dirname = dirname.Substring( 1 );
            }
            if( this.subdirs.ContainsKey( dirname ) ) {
                if( this.subdirs[dirname].IsEmpty() ) {
                    this.subdirs.Remove( dirname );
                } else {
                    // XXX
                }
            }
        }
    }
}
