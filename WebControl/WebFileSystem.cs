using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WebControl {
    public class WebFileSystem {

        public static string[] PathSeparators = { "/", @"\" };
        WebDirectory _root = new WebDirectory();

        public WebDirectory Root {
            get { return this._root; }
        }

        public WebDirectory GetDirectoryByPath( string path ) {
            string[] elements = path.Split( WebFileSystem.PathSeparators, StringSplitOptions.RemoveEmptyEntries );

            foreach( string element in elements ) {
                Trace.TraceInformation( "Requested file: " + element );
            }

            return this.GetDirectoryByPath( this._root, elements );
        }

        private WebDirectory GetDirectoryByPath( WebDirectory curdir, string[] next ) {
            if( 1 < next.Length ) {
                // Still more to go!
                WebDirectory outdir = curdir.GetSubdirectory( next[0] );
                if( null != outdir ) {
                    return this.GetDirectoryByPath( outdir, new ArraySegment<string>( next, 1, next.Length - 1 ).Array );
                }
            } else if( 1 == next.Length ) {
                // Found it?
                return curdir.GetSubdirectory( next[0] );
            }
            return null;
        }

        public WebFile GetFileByPath( string path ) {
            string[] elements = path.Split( WebFileSystem.PathSeparators, StringSplitOptions.RemoveEmptyEntries );

            foreach( string element in elements ) {
                Trace.TraceInformation( "Requested file: " + element );
            }

            WebDirectory basedir = this.GetDirectoryByPath(
                this._root, new ArraySegment<string>( elements, 0, elements.Length - 1 ).Array );
            if( null == basedir ) {
                basedir = this._root;
            }

            string filename = elements[elements.Length - 1];
            WebFile outfile = basedir.GetFile( filename );
            if( null != outfile ) {
                return outfile;
            } else {
                Trace.TraceError( "Attempted to open missing file: " + path );
            }

            return null;
        }

        public void PutFileByPath( string path, WebFile file_in ) {
            string[] elements = path.Split( WebFileSystem.PathSeparators, StringSplitOptions.RemoveEmptyEntries );

            foreach( string element in elements ) {
                Trace.TraceInformation( "Requested file: " + element );
            }

            WebDirectory basedir = this.GetDirectoryByPath(
                this._root, new ArraySegment<string>( elements, 0, elements.Length - 1 ).Array );
            if( null == basedir ) {
                basedir = this._root;
            }

            string filename = elements[elements.Length - 1];
            WebFile outfile = basedir.GetFile( filename );
            if( null != outfile ) {
                Trace.TraceError( "Attempted to put existing file: " + path );
            } else {
                basedir.PutFile( filename, file_in );
            }
        }

        public void RemoveFileByPath( string path ) {
            string[] elements = path.Split( WebFileSystem.PathSeparators, StringSplitOptions.RemoveEmptyEntries );

            foreach( string element in elements ) {
                Trace.TraceInformation( "Requested file: " + element );
            }

            WebDirectory basedir = this.GetDirectoryByPath(
                this._root, new ArraySegment<string>( elements, 0, elements.Length - 1 ).Array );
            if( null == basedir ) {
                basedir = this._root;
            }

            string filename = elements[elements.Length - 1];
            WebFile outfile = basedir.GetFile( filename );
            if( null != outfile ) {
                basedir.RemoveFile( filename );
            } else {
                Trace.TraceError( "Attempted to remove missing file: " + path );
            }
        }
    }
}
