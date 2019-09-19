using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.ComponentModel;

namespace AppMinder {
    public class ProcObject : IDisposable {
        private PerformanceCounter _proc_wset_c;
        /*private PerformanceCounter _rea_op_s_c;
        private PerformanceCounter _write_op_s_c;
        private PerformanceCounter _data_op_s_c;*/
        private PerformanceCounter _read_bytes_s_c;
        private PerformanceCounter _write_bytes_s_c;
        //private PerformanceCounter _data_bytes_s_c;

        private string _proc_name;
        private string _proc_user;
        private float _workingset = 0;
        private int _proc_id;
        private Process _proc;
        private DateTime _last_sample_time;
        private TimeSpan _last_proc_cpu;
        private double _cpu_pct;
        /*private float _read_op_s_n;
        private float _write_op_s_n;
        private float _data_op_s_n;*/
        private float _read_b_s_n;
        private float _write_b_s_n;
        //private float _data_b_s_n;

        private Guid _guid;

        public string ProcName {
            get { return this._proc_name; }
        }

        public int ProcId {
            get { return this._proc_id; }
        }

        public string ProcUser {
            get { return this._proc_user; }
        }

        public double CPUPercent {
            get { return this._cpu_pct; }
        }

        public float WorkingSet {
            get { return this._workingset; }
        }

        /*public float ReadOpSec {
            get { return this._read_op_s_n; }
        }

        public float WriteOpSec {
            get { return this._write_op_s_n; }
        }

        public float DataOpSec {
            get { return this._data_op_s_n; }
        }*/

        public float ReadBytesSec {
            get { return this._read_b_s_n; }
        }

        public float WriteBytesSec {
            get { return this._write_b_s_n; }
        }

        /*public float DataBytesSec {
            get { return this._data_b_s_n; }
        }*/

        public string Filename {
            get { return this._proc.MainModule.FileName; }
        }

        public string JpegB64 {
            get; set;
        }

        public string GUID {
            get { return this._guid.ToString(); }
        }

        public ProcObject( Process proc ) {
            this._proc_name = proc.ProcessName;
            this._proc_user = null;
            this._proc_id = proc.Id;
            this._proc = proc;
            this.JpegB64 = null;
            this._guid = Guid.NewGuid();
            this._proc_wset_c = new PerformanceCounter( "Process", "Working Set", proc.ProcessName );
            //this._rea_op_s_c = new PerformanceCounter( "Process", "IO Read Operations/sec", proc.ProcessName );
            //this._write_op_s_c = new PerformanceCounter( "Process", "IO Write Operations/sec", proc.ProcessName );
            //this._data_op_s_c = new PerformanceCounter( "Process", "IO Data Operations/sec", proc.ProcessName );
            this._read_bytes_s_c = new PerformanceCounter( "Process", "IO Read Bytes/sec", proc.ProcessName );
            this._write_bytes_s_c = new PerformanceCounter( "Process", "IO Write Bytes/sec", proc.ProcessName );
            //this._data_bytes_s_c = new PerformanceCounter( "Process", "IO Data Bytes/sec", proc.ProcessName );
        }

        public bool IsComplete() {
            return !String.IsNullOrEmpty( this._proc_name ) &&
                !String.IsNullOrEmpty( this._proc_user ) &&
                0 < this.ProcId;
        }

        private static string JsonProp( string name, string value, bool comma ) {
            value.Replace( "'", "&apos;" );
            value.Replace( "\"", "&quot;" );
            name.Replace( "'", "&apos;" );
            name.Replace( "\"", "&quot;" );
            return "\"" + name + "\": \"" + value + "\"" + (comma ? ", " : "");
        }

        private static string JsonProp( string name, float value, bool comma ) {
            return "\"" + name + "\": \"" + value.ToString( "0.0000" ) + "\"" + (comma ? ", " : "");
        }

        private static string JsonProp( string name, double value, bool comma ) {
            return "\"" + name + "\": \"" + value.ToString( "0.0000" ) + "\"" + (comma ? ", " : "");
        }

        private static string JsonProp( string name, int value, bool comma ) {
            return "\"" + name + "\": " + value.ToString() + (comma ? ", " : "");
        }

        public string ToJson() {
            StringBuilder out_json = new StringBuilder();

            out_json.Append( "{" );

            out_json.Append( JsonProp( "user", this._proc_user, true ) );
            out_json.Append( JsonProp( "pid", this._proc.Id, true ) );
            out_json.Append( JsonProp( "pname", this._proc_name, true ) );
            out_json.Append( JsonProp( "cpu_pct", this._cpu_pct, true ) );
            out_json.Append( JsonProp( "working_set", this._workingset, true ) );
            out_json.Append( JsonProp( "net_down", this._read_b_s_n, true ) );
            out_json.Append( JsonProp( "net_up", this._write_b_s_n, true ) );
            out_json.Append( JsonProp( "guid", this._guid.ToString(), false ) );

            out_json.Append( "}" );

            return out_json.ToString();
        }

        public bool Poll() {

            //if( !this._proc.)

            try {
                // Don't bother if it's empty, only null. Empty means we don't have access.
                if( null == this._proc_user ) {
#if DEBUG
                    Trace.TraceInformation( "Getting user for process #" + this._proc_id.ToString() + "..." );
#endif // DEBUG
                    this._proc_user = ProcMinder.GetProcessOwner( this._proc.Id );
                }

                if( this._last_sample_time == null || this._last_sample_time == new DateTime() ) {
                    // Take first sample.
                    this._last_sample_time = DateTime.Now;
                    this._last_proc_cpu = this._proc.TotalProcessorTime;
                } else {
                    DateTime cur_sample_time = DateTime.Now;
                    TimeSpan cur_proc_cpu = this._proc.TotalProcessorTime;

                    this._cpu_pct = (cur_proc_cpu.TotalMilliseconds - this._last_proc_cpu.TotalMilliseconds) /
                        cur_sample_time.Subtract( this._last_sample_time ).TotalMilliseconds / Convert.ToDouble( Environment.ProcessorCount );

                    this._last_sample_time = cur_sample_time;
                    this._last_proc_cpu = cur_proc_cpu;
                }

                this._workingset = this._proc_wset_c.NextValue();
                /*this._read_op_s_n = this._rea_op_s_c.NextValue();
                this._write_op_s_n = this._write_op_s_c.NextValue();
                this._data_op_s_n = this._data_op_s_c.NextValue();*/
                this._read_b_s_n = this._read_bytes_s_c.NextValue();
                this._write_b_s_n = this._write_bytes_s_c.NextValue();
                //this._data_b_s_n = this._data_bytes_s_c.NextValue();
                return true;
            } catch( Win32Exception ex ) {
                if( -2147467259 == ex.ErrorCode ) {
                    // XXX
                } else {
                    //Console.WriteLine( ex.ErrorCode );
                }
                return true;
            } catch( Exception ) {
                // Process probably exited.
                return false;
            }
        }

        public void Dispose() {
            this._proc_wset_c.Dispose();
            /*this._rea_op_s_c.Dispose();
            this._write_op_s_c.Dispose();
            this._data_op_s_c.Dispose();*/
            this._read_bytes_s_c.Dispose();
            this._write_bytes_s_c.Dispose();
            //this._data_bytes_s_c.Dispose();
        }
    }
}
