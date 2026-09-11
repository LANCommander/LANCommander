#ifndef LAUNCHER_DOWNLOAD_QUEUE_H
#define LAUNCHER_DOWNLOAD_QUEUE_H

#include <stdint.h>

#include <string>
#include <vector>

namespace lancommander
{
    class GameClient;
    class LibraryClient;
}

namespace launcher
{

    class ScriptHost;

    enum class DownloadStatus
    {
        Queued,
        Downloading,
        Extracting,
        // The archive is unpacked and the game's Install.ps1 is running. A
        // separate state because it is the one an install can sit in for a
        // while with the progress bar full, which otherwise reads as a hang.
        RunningScripts,
        Complete,
        Failed
    };

    // What an install script needs that the queue has no way to know: which
    // server the game came from, and where the launcher puts games by
    // default. Both live in App, which the queue deliberately cannot see.
    //
    // `host` may be null, in which case no scripts are written or run and the
    // install behaves exactly as it did before scripts were wired up.
    struct ScriptEnvironment
    {
        ScriptHost *host;
        std::string server_address;
        std::string default_install_dir;

        ScriptEnvironment() : host(NULL) {}
    };

    struct DownloadItem
    {
        std::string game_id;
        std::string title;
        std::string dest_path; // temp zip path
        std::string install_dir;
        bool add_to_library;      // add game to user's library before downloading
        DownloadStatus status;
        float progress;           // 0.0 - 1.0

        // 64-bit because a game archive is allowed to be bigger than 4 GB.
        // These are only ever touched on the UI thread -- tick() copies them
        // out of the worker's own state -- so a 32-bit target reading them in
        // two halves is not a problem here.
        uint64_t received;        // bytes
        uint64_t total;           // bytes

        // Transfer rate, smoothed over a few seconds. 0 until there is enough
        // history to say anything, and while extracting.
        unsigned long speed_bps;

        // Seconds remaining at the current rate; 0 when unknown.
        unsigned long eta_seconds;

        std::string error;

        DownloadItem()
            : add_to_library(false),
              status(DownloadStatus::Queued), progress(0.0f),
              received(0), total(0), speed_bps(0), eta_seconds(0) {}
    };

    // The worker's half of the active job. Defined in the .cpp: nothing
    // outside the queue may touch it, because every field in it is shared
    // with a background thread.
    struct DownloadJob;

    class DownloadQueue
    {
    public:
        DownloadQueue();
        ~DownloadQueue();

        // Add a game to the download queue.
        // If add_to_library is true, the background thread will call
        // library.add() before starting the download.
        void enqueue(const std::string &game_id, const std::string &title,
                     const std::string &install_dir, bool add_to_library = false);

        // Call once per frame to check thread state and advance the queue.
        void tick(lancommander::GameClient &games, lancommander::LibraryClient &library,
                  const ScriptEnvironment &scripts);

        // Current state accessors.
        bool has_active() const;
        const DownloadItem *current_item() const;
        const std::vector<DownloadItem> &items() const;
        int pending_count() const;

        // Remove completed/failed items.
        void clear_finished();

    private:
        std::vector<DownloadItem> m_items;
        int m_active_idx;

        // Thread state (opaque HANDLE).
        void *m_thread;
        volatile bool m_thread_done;

        // The running job, or null. Heap-allocated and owned here rather than
        // pointed at from the worker, because m_items is a vector: enqueueing
        // a second game mid-download reallocates its storage, and a worker
        // holding &m_items[i] would be writing into freed memory. The worker
        // touches nothing but its DownloadJob; tick() copies the job's
        // progress into the item on this thread.
        DownloadJob *m_job;

        // Copied when a job starts rather than read from the caller's frame:
        // the worker outlives the tick that launched it.
        ScriptEnvironment m_scripts;

        void start_next(lancommander::GameClient &games, lancommander::LibraryClient &library);

        // Copies the running job's published progress into m_items.
        void collect_progress();
    };

} // namespace launcher

#endif // LAUNCHER_DOWNLOAD_QUEUE_H
