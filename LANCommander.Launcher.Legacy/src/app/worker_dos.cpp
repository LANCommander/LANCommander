// worker_dos.cpp — background jobs on a single-tasking OS.
//
// DOS runs one thing at a time, so worker_start() runs the job to completion
// and returns. The launcher does not stop responding for any longer than the
// job takes either way; what it loses is the ability to repaint while the job
// runs, which no amount of scaffolding here can give back.
//
// The alternative — a timer-interrupt scheduler switching stacks under the UI
// — would have to make every allocation, every FILE* and every Watt-32 socket
// re-entrant across a hardware interrupt. Neither libc nor Watt-32 is, so it
// would trade a visible pause for intermittent corruption.
//
// The mutexes are genuinely unnecessary rather than merely unimplemented:
// there is no second context to contend with, so an uncontended lock is the
// correct implementation and not a stub.

#include "app/worker.h"

#include <cstddef>

namespace launcher
{

    namespace
    {
        // Any non-null value would do; a distinct address makes a stray
        // handle recognisable in a debugger.
        int s_handle_token = 0;
    } // namespace

    void *worker_start(WorkerFn fn, void *arg)
    {
        if (!fn)
            return NULL;

        fn(arg);

        // Non-null even though the job is already finished: callers use the
        // handle to mean "a job was started and has not been reaped yet",
        // and reap it on their next tick when their own done flag is set —
        // which fn() will have set before returning.
        return &s_handle_token;
    }

    void worker_join(void *handle) { (void)handle; }

    bool worker_concurrent() { return false; }

    // Nothing to deprioritise: the job runs on the only thread there is.
    void worker_set_background_priority() {}

    void *worker_mutex_create() { return &s_handle_token; }

    void worker_mutex_destroy(void *mutex) { (void)mutex; }

    void worker_mutex_lock(void *mutex) { (void)mutex; }

    void worker_mutex_unlock(void *mutex) { (void)mutex; }

    // Nothing else runs here, so sleeping could only delay the caller without
    // changing anything it might be waiting for.
    void worker_sleep_ms(unsigned int ms) { (void)ms; }

} // namespace launcher
