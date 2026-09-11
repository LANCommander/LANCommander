#ifndef LAUNCHER_APP_WORKER_H
#define LAUNCHER_APP_WORKER_H

// The background-work seam.
//
// Three services run one long job off the UI thread: the download queue, the
// media prefetcher and the game art fetcher. All three had the same twenty
// lines of _beginthreadex / WaitForSingleObject / CRITICAL_SECTION inline,
// which is what made them the only reason those files included <windows.h>.
//
// DOS has no threads at all, so the seam has to be shaped around the one
// thing both platforms can honestly do: start a job, be told when it is
// finished, and take a lock while touching shared state. On DOS the job runs
// to completion inside worker_start() and the lock is a no-op, which is the
// truthful implementation for a single-tasking OS rather than a stub that
// pretends to be asynchronous.
//
// Callers must therefore not assume worker_start() returns before the work
// is done. All three already tolerate that: each checks its own done flag on
// the next tick, which on DOS is simply already set.

namespace launcher
{

    // The job. Runs on a background thread where there is one; runs inline
    // otherwise. Must set whatever done flag its owner polls before it
    // returns, exactly as the thread bodies already did.
    typedef void (*WorkerFn)(void *arg);

    // Starts `fn(arg)`. Returns an opaque handle, or NULL if the job could
    // not be started — never NULL merely because it finished quickly, so
    // `handle != NULL` keeps meaning "a job is outstanding" to callers that
    // clear it themselves.
    void *worker_start(WorkerFn fn, void *arg);

    // Waits for the job to finish and releases the handle. Safe with NULL.
    void worker_join(void *handle);

    // True where a started job runs concurrently with the caller. False on
    // DOS. Callers use it for what they tell the user, not for control flow.
    bool worker_concurrent();

    // Drops the CALLING thread below the UI thread's priority. Called by a
    // job on itself, at the top of its WorkerFn.
    //
    // For the download worker, which spends an install in a tight loop of
    // socket reads and file writes. On Win9x both of those thunk down to
    // 16-bit code behind the Win16Mutex, which is the same lock every GDI
    // call needs, so a worker that never yields it is a launcher that stops
    // redrawing. Priority decides who gets it next.
    //
    // A no-op on DOS, where the job IS the UI thread.
    void worker_set_background_priority();

    // A recursive-safe mutex guarding a service's shared state. Uncontended
    // and free on DOS, where there is no second thread to contend with.
    void *worker_mutex_create();
    void worker_mutex_destroy(void *mutex);
    void worker_mutex_lock(void *mutex);
    void worker_mutex_unlock(void *mutex);

    // Yields the calling thread for roughly `ms`. For a worker waiting on
    // something the UI thread will set -- the script debugger's "still paused"
    // spin is the only such wait -- where burning a core is the alternative.
    //
    // A no-op on DOS: nothing else can run to change the condition, so a
    // sleep there would only be a slower infinite loop.
    void worker_sleep_ms(unsigned int ms);

} // namespace launcher

#endif // LAUNCHER_APP_WORKER_H
