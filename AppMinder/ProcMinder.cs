using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppMinder {
    class ProcMinder {
        public delegate void PollAllCallback( ProcMinder pm, object state );
        //public delegate void PollCallback( ProcObject proc, object state );
        
        private List<ProcObject> _procs = new List<ProcObject>();
        //private Timer _mtimer;
        private Object _proc_lock = new Object();
        private PollAllCallback _pcbs = null;
        //private PollCallback _pcb = null;
        private object _pcb_state = null;
        private Thread _poll_thread = null;
        private EventLog _logger = null;

        public int Interval {
            get;
            set;
        }

        public bool Running {
            get;
            set;
        }

        public List<ProcObject> Processes {
            get {
                lock( this._proc_lock ) {
                    return this._procs;
                }
            }
        }

        protected void LogError( string message ) {
            if( null != this._logger ) {
                this._logger.WriteEntry( message, EventLogEntryType.Error );
            } else {
                Console.WriteLine( message );
            }
        }

        public static string GetProcessOwner( int processId ) {
            string query = "Select * From Win32_Process Where ProcessID = " + processId;
            ManagementObjectSearcher searcher = new ManagementObjectSearcher( query );
            ManagementObjectCollection processList = searcher.Get();

            foreach( ManagementObject obj in processList ) {
                string[] argList = new string[] { string.Empty, string.Empty };
                int returnVal = Convert.ToInt32( obj.InvokeMethod( "GetOwner", argList ) );
                if( returnVal == 0 ) {
                    // return DOMAIN\user
                    return argList[1] + "\\\\" + argList[0];
                }
            }

            return "";
        }

        public ProcMinder( PollAllCallback pcbs, object state, EventLog logger ) : this() {
            this._pcbs = pcbs;
            this._pcb_state = state;
            this._logger = logger;
        }

        public ProcMinder() {
            this.Interval = 5000;
        }

        public void AddProc( Process proc ) {
            lock( this._proc_lock ) {
                this._procs.Add( new ProcObject( proc ) );
            }
        }

        protected void PollAllProcs( Object stateinfo ) {
#if old_style
            lock( this._proc_lock ) {
                Process[] fresh_procs = Process.GetProcesses();
                foreach( Process proc in fresh_procs ) {
                    if( !this._procs.Keys.Contains( proc.Id ) ) {
                        this._procs.Add( proc.Id, new ProcObject( proc ) );
                    }
                }
            }
#endif

            // Add new/missing processes.
            //ThreadPool.QueueUserWorkItem( x => {
            Trace.TraceInformation( "Adding new processes..." );
            this._procs.AddRange( Process.GetProcesses()
                .Where( o => !this._procs.Any( i => o.Id == i.ProcId ) )
                .Select( o => new ProcObject( o ) ) );
                
            Trace.Assert( this._procs.Count > 0, "Process list empty after building." );
            //} );

            //ThreadPool.QueueUserWorkItem( x => {
            Trace.TraceInformation( "Removing dead processes..." );
            lock( this._proc_lock ) {
                this._procs = this._procs.Where( ( ProcObject p ) => Process.GetProcesses().Select( i => p.ProcId == i.Id ).Any() ).ToList();
            }
            Trace.Assert( this._procs.Count > 0, "Process list empty after pruning." );
            //} );

            //Thread.Sleep( 500 );

            //} );

#if old_style
            /* lock( this._proc_lock ) {
                List<int> removal_pids = new List<int>();
                string report_time = DateTime.Now.ToString( "yyyy-MM-dd HH:mm:ss" );
                foreach( KeyValuePair<int, ProcObject> kv in this._procs ) {
                    ProcObject procobj = kv.Value;
                    if( !procobj.Poll() ) {
                        removal_pids.Add( procobj.ProcId );
                        //}
                        if( null != this._pcb ) {
                            this._pcb( procobj, report_time );
                        }
                    }
                }
                foreach( int pid in removal_pids ) {
                    this._procs.Remove( pid );
                }
            } */
#endif

            /*} catch( InvalidOperationException ex ) {
                // XXX
                this.LogError( String.Format( "Error: {0}: {1}", ex.TargetSite, ex.Message ) );
                this.LogError( ex.StackTrace );
            }*/

            //ThreadPool.QueueUserWorkItem( x => {
            Trace.TraceInformation( "Polling all processes..." );
            Parallel.ForEach(
                this._procs,
                ( ProcObject p ) => {
                    p.Poll();
                    //Thread.Sleep( 100 );
                }
            );

            if( null != this._pcbs ) {
                Thread.Sleep( 500 );
                Trace.TraceInformation( "Reporting processes..." );
                lock( this._proc_lock ) {
                    this._pcbs( this, this._pcb_state );
                }
            }
        }

        private void PollThread() {
            while( this.Running ) {
                this.PollAllProcs( null );
                Thread.Sleep( this.Interval );
            }
        }

        public void Run() {
            //this._mtimer = new Timer( this.PollAllProcs, null, 0, this.Interval );
            this.Running = true;
            this._poll_thread = new Thread( this.PollThread );
            this._poll_thread.Name = "Polling Thread";
            this._poll_thread.Priority = ThreadPriority.BelowNormal;
            this._poll_thread.Start();
        }

        public void Stop() {
            //this._mtimer.Dispose();
            this.Running = false;
        }
    }
}
