#ifndef LAUNCHER_DOWNLOAD_QUEUE_H
#define LAUNCHER_DOWNLOAD_QUEUE_H

#include <string>
#include <vector>

namespace lancommander
{
    class GameClient;
    class LibraryClient;
}

namespace launcher
{

    enum class DownloadStatus
    {
        Queued,
        Downloading,
        Extracting,
        Complete,
        Failed
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
        unsigned long received;   // bytes
        unsigned long total;      // bytes
        std::string error;

        DownloadItem()
            : add_to_library(false),
              status(DownloadStatus::Queued), progress(0.0f),
              received(0), total(0) {}
    };

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
        void tick(lancommander::GameClient &games, lancommander::LibraryClient &library);

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

        void start_next(lancommander::GameClient &games, lancommander::LibraryClient &library);
    };

} // namespace launcher

#endif // LAUNCHER_DOWNLOAD_QUEUE_H
