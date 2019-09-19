using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace WebControl {
    class WebThreads {
        public delegate void WebThreadHandler( object state );

        private class WebThreadQueueItem {
            public WebThreadHandler Callback { get; set; }
            public object State { get; set; }

            public WebThreadQueueItem(WebThreadHandler cb, object s ) {
                this.Callback = cb;
                this.State = s;
            }
        }

        private object _thread_lock = new object();
        private Thread[] _threads;
        private Stack<int> _available_thread_indexes = new Stack<int>();
        private Queue<WebThreadQueueItem> _waiting_queue = new Queue<WebThreadQueueItem>();
        private ThreadPriority _priority;
        private string _name;

        public WebThreads(string name, int count, ThreadPriority priority) {
            //this._threads = //new List<Thread>( count );
            /* for(int i = 0 ;count > i ; i++ ) {
                this._threads = new Thread( name + "-" + i.ToString() );
                this._threads[i].Priority = priority;
            } */
            //this._threads.AddRange()

            this._threads = new Thread[count];
            for(int i = 0 ; count > i ;i++ ) {
                this._available_thread_indexes.Push( i );
            }
            this._name = name;
            this._priority = priority;

            /* this._monitor_thread = new Thread( this.MonitorQueue );
            this._monitor_thread.Name = "Monitor Thread";
            this._monitor_thread.Priority = ThreadPriority.AboveNormal; */
        }

        private void AddHandler( WebThreadQueueItem item ) {
            this.AddHandler( item.Callback, item.State );
        }
        
        public void AddHandler( WebThreadHandler handler, object state ) {
            lock( this._thread_lock ) {
                if( 0 >= this._available_thread_indexes.Count ) {
                    Trace.TraceInformation( "Enqueuing worker until thread available..." );
                    this._waiting_queue.Enqueue( new WebThreadQueueItem( handler, state ) );
                } else {
                    int first_available = this._available_thread_indexes.Pop();
                    this._threads[first_available] = new Thread( () => {
                        // Run the handler and put back this index when we're done.
                        handler( state );
                        this._available_thread_indexes.Push( first_available );
                        if( 0 < this._waiting_queue.Count && 0 < this._available_thread_indexes.Count ) {
                            // Try to re-add the latest item in the queue, letting it take our current spot.
                            this.AddHandler( this._waiting_queue.Dequeue() );
                        }
                    } );
                    this._threads[first_available].Name = this._name + "-" + first_available;
                    this._threads[first_available].Priority = this._priority;
                    Trace.TraceInformation( "Running worker in thread #" + first_available.ToString() + "..." );
                    this._threads[first_available].Start();
                }
            }
        }
    }
}
