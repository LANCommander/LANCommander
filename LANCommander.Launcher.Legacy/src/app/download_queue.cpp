#include "app/download_queue.h"
#include "app/logger.h"
#include "app/script_host.h"
#include "app/zip_io.h"

#include <lancommander/clients/game_client.h>
#include <lancommander/clients/library_client.h>

#include "app/fs.h"
#include "app/worker.h"
#include "gfx/gfx.h"
#include <cstdio>
#include <cstring>
#include <set>
#include <string>
#include <vector>

#include "miniz.h"

namespace launcher
{

    namespace
    {
        // Window the transfer rate is averaged over, and how often the figure
        // the user sees changes. Sampling between two consecutive reads gives
        // a number that swings by an order of magnitude and is unreadable.
        const unsigned int RATE_WINDOW_MS = 500;

        // Weight the running average keeps from its previous value. 0.7 rides
        // out the stalls a Win9x box takes while it flushes its write cache
        // without lagging a genuine change in speed by more than a second or
        // two.
        const double RATE_SMOOTHING = 0.7;

        // stdio buffer used for the file being extracted. One buffer serves
        // the whole archive; see where it is allocated.
        //
        // The default is a few kilobytes, so every miniz output chunk became
        // its own WriteFile. On Win9x a file write is a thunk down to 16-bit
        // VFAT that takes the Win16Mutex on the way -- the same lock GDI
        // needs -- so the write RATE, not the byte count, is what decides how
        // much the UI stutters underneath an install.
        const size_t EXTRACT_BUFFER = 256 * 1024;
    } // namespace

    // What the worker publishes and tick() copies out.
    //
    // The worker used to hold &m_items[i] and write the item directly. Two
    // things were wrong with that: the pointer is into a std::vector, so
    // enqueueing a second game mid-download reallocated the storage and left
    // the worker writing through freed memory; and `error` is a std::string,
    // which the UI thread walks every frame while the worker reassigns it.
    //
    // Everything the two threads share now lives here behind `lock`.
    struct DownloadJob
    {
        lancommander::GameClient *games;
        lancommander::LibraryClient *library;
        volatile bool *done_flag;
        ScriptEnvironment scripts;

        // The item's inputs, copied. Read by the worker, never written by
        // either side once the job has started, so they need no lock.
        std::string game_id;
        std::string title;
        std::string install_dir;
        bool add_to_library;

        // --- Shared, guarded by `lock` ---
        void *lock;
        DownloadStatus status;
        float progress;
        uint64_t received;
        uint64_t total;
        unsigned long speed_bps;
        unsigned long eta_seconds;
        std::string error;
        std::string dest_path;

        // --- Rate estimation. Worker thread only. ---
        unsigned int rate_t0_ms;
        uint64_t rate_b0;
        double rate_ema_bps;

        DownloadJob()
            : games(NULL), library(NULL), done_flag(NULL),
              add_to_library(false), lock(NULL),
              status(DownloadStatus::Queued), progress(0.0f),
              received(0), total(0), speed_bps(0), eta_seconds(0),
              rate_t0_ms(0), rate_b0(0), rate_ema_bps(0.0)
        {
            lock = worker_mutex_create();
        }

        ~DownloadJob()
        {
            worker_mutex_destroy(lock);
        }
    };

    namespace
    {
        struct JobLock
        {
            DownloadJob *job;
            explicit JobLock(DownloadJob *j) : job(j) { worker_mutex_lock(job->lock); }
            ~JobLock() { worker_mutex_unlock(job->lock); }
        };

        void publish_status(DownloadJob *job, DownloadStatus status)
        {
            JobLock guard(job);
            job->status = status;
        }

        void publish_progress(DownloadJob *job, float progress)
        {
            JobLock guard(job);
            job->progress = progress;
        }

        // Marks the job failed with a reason. One call rather than two so a
        // status of Failed can never be visible with a stale error beside it.
        void publish_failure(DownloadJob *job, const std::string &error)
        {
            JobLock guard(job);
            job->error = error;
            job->status = DownloadStatus::Failed;
            job->speed_bps = 0;
            job->eta_seconds = 0;
        }
    } // namespace

    // The transport's timings for the job's last download. Through the game
    // client because that is the only handle the worker has on the HTTP
    // client underneath it.
    static lancommander::DownloadTiming ctx_games_timing(DownloadJob *job)
    {
        return job->games ? job->games->last_download_timing()
                          : lancommander::DownloadTiming();
    }

    static bool dl_progress_cb(uint64_t received, uint64_t total, void *ud)
    {
        DownloadJob *job = (DownloadJob *)ud;

        const unsigned int now = gfx::ticks_ms();
        const unsigned int elapsed = now - job->rate_t0_ms;

        if (elapsed >= RATE_WINDOW_MS && received >= job->rate_b0)
        {
            const double bps =
                (double)(received - job->rate_b0) * 1000.0 / (double)elapsed;

            // The first window seeds the average outright. Blending it in
            // from zero would show half the real speed for the first few
            // seconds, which is exactly when the user is looking at it.
            job->rate_ema_bps = (job->rate_ema_bps <= 0.0)
                                    ? bps
                                    : job->rate_ema_bps * RATE_SMOOTHING +
                                          bps * (1.0 - RATE_SMOOTHING);

            job->rate_t0_ms = now;
            job->rate_b0 = received;
        }

        unsigned long speed = 0;
        unsigned long eta = 0;
        if (job->rate_ema_bps > 1.0)
        {
            speed = (unsigned long)job->rate_ema_bps;
            if (total > received)
                eta = (unsigned long)((double)(total - received) / job->rate_ema_bps);
        }

        {
            JobLock guard(job);
            job->received = received;
            job->total = total;
            job->speed_bps = speed;
            job->eta_seconds = eta;
            if (total > 0)
                job->progress = (float)((double)received / (double)total);
        }

        return true; // continue
    }

    // WorkerFn (app/worker.h): a thread on Windows, inline on DOS.
    static void dl_thread_fn(void *param)
    {
        DownloadJob *job = (DownloadJob *)param;

        // Below normal, so the UI thread wins whenever the two want the same
        // thing. During an install they want the same thing constantly: on
        // Win9x the file writes below and the launcher's GDI blits both go
        // through the Win16Mutex, and whoever holds it decides whether the
        // window redraws. A download that finishes a few seconds later is a
        // better trade than a launcher that stops responding for the whole
        // of it. No-op where the platform has no thread priorities.
        worker_set_background_priority();

        publish_status(job, DownloadStatus::Downloading);
        log_info("Download started: %s (game %s)",
                 job->title.c_str(), job->game_id.c_str());
        log_info("Install dir: %s", job->install_dir.c_str());

        // Add to user's library if requested (runs in background thread
        // so it doesn't block the UI — the HTTP call can be slow on Win9x).
        if (job->add_to_library && job->library)
        {
            log_info("Adding game to library: %s", job->game_id.c_str());
            auto lib_result = job->library->add(job->game_id);
            if (!lib_result)
                log_warn("Failed to add to library: %s", lib_result.error.c_str());
        }

        // Generate temp file path.
        const std::string temp_file = fs_temp_file("lcl");
        if (temp_file.empty())
        {
            publish_failure(job, "Could not create a temporary file");
            log_error("Download failed: no temp file for %s", job->title.c_str());
            *job->done_flag = true;
            return;
        }

        {
            JobLock guard(job);
            job->dest_path = temp_file;
        }

        job->rate_t0_ms = gfx::ticks_ms();
        job->rate_b0 = 0;
        job->rate_ema_bps = 0.0;

        const unsigned int transfer_start_ms = gfx::ticks_ms();

        auto result = job->games->download(job->game_id,
                                           temp_file,
                                           [job](uint64_t r, uint64_t t) -> bool
                                           {
                                               return dl_progress_cb(r, t, job);
                                           });

        if (!result || !result.value)
        {
            const std::string reason = result ? "Download failed" : result.error;
            publish_failure(job, reason);
            log_error("Download failed: %s - %s", job->title.c_str(), reason.c_str());
            fs_remove(temp_file);
            *job->done_flag = true;
            return;
        }

        // --- Extract the archive with miniz ---
        // Through app/zip_io.h rather than miniz's own file entry points:
        // those reach the disk via _ftelli64/_fseeki64, which MinGW builds on
        // SetFilePointerEx/GetFileSizeEx -- XP-only imports that stop the
        // loader dead on Win9x. (The original reason was _wfopen, which is
        // the same story one layer up.)
        {
            JobLock guard(job);
            job->status = DownloadStatus::Extracting;
            job->progress = 0.0f;
            job->speed_bps = 0;
            job->eta_seconds = 0;
        }

        fs_mkdir(job->install_dir);

        mz_zip_archive zip;
        ZipFile zip_io;

        if (!zip_open_read(&zip, &zip_io, temp_file))
        {
            publish_failure(job, "Could not open archive");
            log_error("Extraction failed: could not read %s", temp_file.c_str());
            fs_remove(temp_file);
            *job->done_flag = true;
            return;
        }

        // Where the transfer actually went.
        //
        // The average is the number to argue with when a download is slower
        // than the link should allow -- the figure in the UI is a smoothed
        // instantaneous rate and is not it. The split underneath it is what
        // says WHICH thing was slow: reads and writes alternate on this one
        // thread, so socket and disk time partition the total between them,
        // and a slow network and a slow disk are indistinguishable from the
        // rate alone.
        {
            const unsigned int took_ms = gfx::ticks_ms() - transfer_start_ms;
            const double secs = took_ms > 0 ? took_ms / 1000.0 : 0.0;
            const double kbps = secs > 0.0 ? (double)zip_io.size / secs / 1024.0 : 0.0;

            log_info("Download complete: %s (%ld bytes in %.1fs, %.0f KB/s average)",
                     temp_file.c_str(), zip_io.size, secs, kbps);

            const lancommander::DownloadTiming t = ctx_games_timing(job);

            if (t.total_ms > 0)
            {
                // Whatever is left after the socket, the disk and the final
                // flush is time spent neither reading nor writing -- the
                // progress callback, and scheduling.
                const double socket_s = t.socket_ms / 1000.0;
                const double write_s = t.write_ms / 1000.0;
                const double flush_s = t.flush_ms / 1000.0;
                const double total_s = t.total_ms / 1000.0;
                const double other_s = total_s - socket_s - write_s;

                const double socket_kbps =
                    socket_s > 0.0 ? (double)t.bytes / socket_s / 1024.0 : 0.0;
                const double write_kbps =
                    write_s > 0.0 ? (double)t.bytes / write_s / 1024.0 : 0.0;

                log_info("  network: %.1fs (%.0f KB/s if it were the only cost)",
                         socket_s, socket_kbps);
                log_info("  disk:    %.1fs (%.0f KB/s if it were the only cost), "
                         "worst single write %lums, final flush %.1fs",
                         write_s, write_kbps, t.longest_write_ms, flush_s);
                log_info("  other:   %.1fs over %lu reads", other_s, t.reads);
            }
        }

        unsigned int file_count = mz_zip_reader_get_num_files(&zip);
        log_info("Extracting %u files to %s", file_count, job->install_dir.c_str());

        bool extract_ok = true;

        // Build the file manifest as we extract (path | CRC32HEX).
        std::string file_manifest;

        // One stdio buffer, reused for every file, rather than letting the
        // CRT allocate and free a quarter megabyte 1571 times over. Safe to
        // share because each file is fclose()d before the next is opened.
        std::vector<char> write_buffer(EXTRACT_BUFFER);

        // Directories already created, so the same parent is not re-created
        // once per file inside it.
        //
        // Every path component of every entry used to go to fs_mkdir, which
        // is a CreateDirectoryA that fails with ERROR_ALREADY_EXISTS. A 1571
        // file archive nested a few levels deep is some fifteen thousand of
        // those, all but 119 of them redundant, and each one is a filesystem
        // round trip on the machines this launcher is for.
        std::set<std::string> made_dirs;

        // Total uncompressed bytes, so the progress bar tracks work rather
        // than file count. Archives are not made of equal files -- one 800 MB
        // video and 1500 small ones would sit the bar at 99% for most of the
        // extraction, which reads as a hang.
        uint64_t total_bytes = 0;
        for (unsigned int i = 0; i < file_count; ++i)
        {
            mz_zip_archive_file_stat probe;
            if (mz_zip_reader_file_stat(&zip, i, &probe) &&
                !mz_zip_reader_is_file_a_directory(&zip, i))
                total_bytes += (uint64_t)probe.m_uncomp_size;
        }
        uint64_t done_bytes = 0;

        for (unsigned int i = 0; i < file_count; ++i)
        {
            mz_zip_archive_file_stat st;
            if (!mz_zip_reader_file_stat(&zip, i, &st))
                continue;

            // Build destination path.
            std::string dest = job->install_dir;
            if (!dest.empty() && dest[dest.size() - 1] != '\\')
                dest += '\\';
            std::string name = st.m_filename;
            for (size_t c = 0; c < name.size(); ++c)
                if (name[c] == '/') name[c] = '\\';
            dest += name;

            if (mz_zip_reader_is_file_a_directory(&zip, i))
            {
                if (made_dirs.insert(dest).second)
                    fs_mkdir(dest);
            }
            else
            {
                // Ensure parent directories exist.
                for (size_t p = 0; p < dest.size(); ++p)
                {
                    if (dest[p] == '\\' && p > 2) // skip "C:\"
                    {
                        std::string parent = dest.substr(0, p);
                        if (made_dirs.insert(parent).second)
                            fs_mkdir(parent);
                    }
                }

                // Extract using fopen + callback instead of
                // mz_zip_reader_extract_to_file (which uses _wfopen).
                FILE *out = fopen(dest.c_str(), "wb");
                if (!out)
                {
                    publish_failure(job,
                                    std::string("Failed creating ") + st.m_filename);
                    log_error("Extraction failed: cannot create %s (errno=%d)",
                              dest.c_str(), errno);
                    extract_ok = false;
                    break;
                }

                // Batch miniz's output chunks into a few large writes.
                setvbuf(out, &write_buffer[0], _IOFBF, write_buffer.size());

                mz_bool ok = mz_zip_reader_extract_to_callback(
                    &zip, i,
                    [](void *pOpaque, mz_uint64, const void *pBuf, size_t n) -> size_t {
                        return fwrite(pBuf, 1, n, (FILE *)pOpaque);
                    },
                    out, 0);

                // The return value matters: a full disk surfaces as a short
                // final fwrite that only fclose() reports, and silently
                // truncating a game file is worse than failing the install.
                if (fclose(out) != 0)
                    ok = MZ_FALSE;

                if (!ok)
                {
                    publish_failure(job,
                                    std::string("Failed extracting ") + st.m_filename);
                    log_error("Extraction failed: %s -> %s",
                              st.m_filename, dest.c_str());
                    extract_ok = false;
                    break;
                }

                done_bytes += (uint64_t)st.m_uncomp_size;

                // Record file in manifest: "entry_path | CRC32HEX\n"
                char crc_hex[16];
                sprintf(crc_hex, "%08X", (unsigned int)st.m_crc32);
                file_manifest += st.m_filename;
                file_manifest += " | ";
                file_manifest += crc_hex;
                file_manifest += "\n";
            }

            publish_progress(job,
                             total_bytes > 0
                                 ? (float)((double)done_bytes / (double)total_bytes)
                                 : (float)(i + 1) / (float)file_count);
        }

        mz_zip_reader_end(&zip);
        zip_close(&zip_io);
        fs_remove(temp_file);

        if (extract_ok)
        {
            // Write FileList.txt to .lancommander/{GameId}/ inside install dir.
            std::string meta_dir = job->install_dir + "\\.lancommander";
            fs_mkdir(meta_dir);
            meta_dir += "\\" + job->game_id;
            fs_mkdir(meta_dir);

            std::string list_path = meta_dir + "\\FileList.txt";
            FILE *fl = fopen(list_path.c_str(), "w");
            if (fl)
            {
                fwrite(file_manifest.c_str(), 1, file_manifest.size(), fl);
                fclose(fl);
            }

            publish_progress(job, 1.0f);

            // --- Scripts ---
            //
            // The archive carries the game's files; the scripts come from the
            // server separately, exactly as the Avalonia launcher fetches them.
            // Writing them under .lancommander/<id>/ is what gives Install.ps1
            // somewhere to be, and what lets either launcher run the other's
            // installs.
            if (job->scripts.host)
            {
                publish_status(job, DownloadStatus::RunningScripts);

                ScriptHost &host = *job->scripts.host;

                if (!host.write_scripts(job->game_id, job->title, job->install_dir))
                {
                    // Not fatal: the files are installed and playable. A game
                    // whose scripts could not be fetched is worse off than one
                    // with none, but it is not a failed install.
                    log_warn("Install scripts unavailable for %s; the game is "
                             "installed but nothing was run",
                             job->title.c_str());
                }

                // A game's redistributables carry scripts of their own, and a
                // RunWrapper among them owns the launch. Fetching them here is
                // what puts anything on disk for the launch path to find.
                host.write_redistributable_scripts(job->game_id, job->title,
                                                   job->install_dir);

                ScriptTarget target;
                target.game_id = job->game_id;
                target.title = job->title;
                target.install_dir = job->install_dir;
                target.server_address = job->scripts.server_address;
                target.default_install_dir = job->scripts.default_install_dir;
                // On DOS the "worker" ran inline on the drawing thread, so a
                // breakpoint here has to pump frames. worker_concurrent() is
                // the honest answer to which thread this is.
                target.on_ui_thread = !worker_concurrent();

                if (!host.run(lancommander::ScriptType::Install, target, NULL))
                {
                    // The install script is what makes some games runnable at
                    // all, so its failure is the install's failure -- and the
                    // console now has the output that says why.
                    publish_failure(job,
                                    "Install script failed - see the script console");
                    log_error("Install script failed for %s", job->title.c_str());
                    *job->done_flag = true;
                    return;
                }
            }

            publish_status(job, DownloadStatus::Complete);
            log_info("Install complete: %s", job->title.c_str());
        }
        else
        {
            // publish_failure already recorded the reason and the status.
            log_error("Install failed: %s", job->title.c_str());
        }

        *job->done_flag = true;
        return;
    }

    DownloadQueue::DownloadQueue()
        : m_active_idx(-1), m_thread(NULL), m_thread_done(false), m_job(NULL)
    {
    }

    DownloadQueue::~DownloadQueue()
    {
        if (m_thread)
        {
            worker_join(m_thread);
            m_thread = NULL;
        }

        // After the join, never before: the worker writes to it until it
        // returns.
        delete m_job;
        m_job = NULL;
    }

    void DownloadQueue::enqueue(const std::string &game_id, const std::string &title,
                                const std::string &install_dir, bool add_to_library)
    {
        DownloadItem item;
        item.game_id = game_id;
        item.title = title;
        item.install_dir = install_dir;
        item.add_to_library = add_to_library;
        item.status = DownloadStatus::Queued;
        m_items.push_back(item);
    }

    void DownloadQueue::collect_progress()
    {
        if (!m_job || m_active_idx < 0 || m_active_idx >= (int)m_items.size())
            return;

        DownloadItem &item = m_items[m_active_idx];

        JobLock guard(m_job);
        item.status = m_job->status;
        item.progress = m_job->progress;
        item.received = m_job->received;
        item.total = m_job->total;
        item.speed_bps = m_job->speed_bps;
        item.eta_seconds = m_job->eta_seconds;
        item.error = m_job->error;
        item.dest_path = m_job->dest_path;
    }

    void DownloadQueue::tick(lancommander::GameClient &games,
                             lancommander::LibraryClient &library,
                             const ScriptEnvironment &scripts)
    {
        m_scripts = scripts;

        // Check if the active download thread finished.
        if (m_thread && m_thread_done)
        {
            worker_join(m_thread);
            m_thread = NULL;
            m_thread_done = false;

            // After the join, so this picks up whatever the worker published
            // last -- including a failure recorded on its way out.
            collect_progress();

            delete m_job;
            m_job = NULL;
            m_active_idx = -1;
        }
        else
        {
            collect_progress();
        }

        // Start the next queued item if nothing is active.
        if (!m_thread)
            start_next(games, library);
    }

    bool DownloadQueue::has_active() const
    {
        return m_active_idx >= 0 && m_active_idx < (int)m_items.size() &&
               (m_items[m_active_idx].status == DownloadStatus::Downloading ||
                m_items[m_active_idx].status == DownloadStatus::Extracting ||
                m_items[m_active_idx].status == DownloadStatus::RunningScripts);
    }

    const DownloadItem *DownloadQueue::current_item() const
    {
        if (m_active_idx >= 0 && m_active_idx < (int)m_items.size())
            return &m_items[m_active_idx];
        return NULL;
    }

    const std::vector<DownloadItem> &DownloadQueue::items() const
    {
        return m_items;
    }

    int DownloadQueue::pending_count() const
    {
        int n = 0;
        for (size_t i = 0; i < m_items.size(); ++i)
            if (m_items[i].status == DownloadStatus::Queued ||
                m_items[i].status == DownloadStatus::Downloading)
                n++;
        return n;
    }

    void DownloadQueue::clear_finished()
    {
        for (size_t i = 0; i < m_items.size();)
        {
            if (m_items[i].status == DownloadStatus::Complete ||
                m_items[i].status == DownloadStatus::Failed)
            {
                if ((int)i == m_active_idx) m_active_idx = -1;
                m_items.erase(m_items.begin() + i);

                // The active item moved down one. Without this the queue goes
                // on publishing the running download's progress into whatever
                // row happens to be at the old index.
                if (m_active_idx > (int)i)
                    m_active_idx--;
            }
            else
                i++;
        }
    }

    void DownloadQueue::start_next(lancommander::GameClient &games, lancommander::LibraryClient &library)
    {
        for (size_t i = 0; i < m_items.size(); ++i)
        {
            if (m_items[i].status == DownloadStatus::Queued)
            {
                log_info("start_next: starting item %d '%s'", (int)i, m_items[i].title.c_str());
                m_active_idx = (int)i;
                m_thread_done = false;

                // The previous job's thread was joined in tick() before its
                // pointer was cleared, so there is nothing still writing to
                // whatever this replaces.
                delete m_job;
                m_job = new DownloadJob();
                m_job->games = &games;
                m_job->library = &library;
                m_job->done_flag = &m_thread_done;
                m_job->scripts = m_scripts;
                m_job->game_id = m_items[i].game_id;
                m_job->title = m_items[i].title;
                m_job->install_dir = m_items[i].install_dir;
                m_job->add_to_library = m_items[i].add_to_library;
                m_job->status = DownloadStatus::Queued;

                m_thread = worker_start(dl_thread_fn, m_job);
                if (!m_thread)
                {
                    log_error("Failed to start the download worker");
                    m_items[i].status = DownloadStatus::Failed;
                    m_items[i].error = "Failed to start the download worker";
                    delete m_job;
                    m_job = NULL;
                    m_active_idx = -1;
                }
                return;
            }
        }
    }

} // namespace launcher
