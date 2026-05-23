#include "app/download_queue.h"
#include "app/logger.h"

#include <lancommander/clients/game_client.h>
#include <lancommander/clients/library_client.h>

#include <windows.h>
#include <process.h>
#include <direct.h>
#include <cstdio>
#include <cstring>
#include <string>

#include "miniz.h"

namespace launcher
{

    // Thread context passed to the download worker.
    struct DlThreadCtx
    {
        lancommander::GameClient *games;
        lancommander::LibraryClient *library;
        DownloadItem *item;
        volatile bool *done_flag;
    };

    static bool dl_progress_cb(uint64_t received, uint64_t total, void *ud)
    {
        DlThreadCtx *ctx = (DlThreadCtx *)ud;
        ctx->item->received = (unsigned long)received;
        ctx->item->total = (unsigned long)total;
        if (total > 0)
            ctx->item->progress = (float)received / (float)total;
        return true; // continue
    }

    static unsigned __stdcall dl_thread_fn(void *param)
    {
        DlThreadCtx *ctx = (DlThreadCtx *)param;
        ctx->item->status = DownloadStatus::Downloading;
        log_info("Download started: %s (game %s)",
                 ctx->item->title.c_str(), ctx->item->game_id.c_str());
        log_info("Install dir: %s", ctx->item->install_dir.c_str());

        // Add to user's library if requested (runs in background thread
        // so it doesn't block the UI — the HTTP call can be slow on Win9x).
        if (ctx->item->add_to_library && ctx->library)
        {
            log_info("Adding game to library: %s", ctx->item->game_id.c_str());
            auto lib_result = ctx->library->add(ctx->item->game_id);
            if (!lib_result)
                log_warn("Failed to add to library: %s", lib_result.error.c_str());
        }

        // Generate temp file path.
        char temp_dir[MAX_PATH];
        char temp_file[MAX_PATH];
        GetTempPathA(MAX_PATH, temp_dir);
        GetTempFileNameA(temp_dir, "lcl", 0, temp_file);
        ctx->item->dest_path = temp_file;

        auto result = ctx->games->download(ctx->item->game_id,
                                           std::string(temp_file),
                                           [ctx](uint64_t r, uint64_t t) -> bool
                                           {
                                               return dl_progress_cb(r, t, ctx);
                                           });

        if (!result || !result.value)
        {
            ctx->item->error = result ? "Download failed" : result.error;
            ctx->item->status = DownloadStatus::Failed;
            log_error("Download failed: %s - %s",
                      ctx->item->title.c_str(), ctx->item->error.c_str());
            DeleteFileA(temp_file);
            *ctx->done_flag = true;
            return 0;
        }

        // --- Extract the archive with miniz ---
        // Use fopen + mz_zip_reader_init_cfile instead of
        // mz_zip_reader_init_file, because miniz's built-in file
        // opener uses _wfopen which doesn't exist on Win9x.
        ctx->item->status = DownloadStatus::Extracting;
        ctx->item->progress = 0.0f;

        CreateDirectoryA(ctx->item->install_dir.c_str(), NULL);

        FILE *zip_file = fopen(temp_file, "rb");
        if (!zip_file)
        {
            ctx->item->error = "Could not open archive";
            ctx->item->status = DownloadStatus::Failed;
            log_error("Extraction failed: fopen(%s) errno=%d", temp_file, errno);
            DeleteFileA(temp_file);
            *ctx->done_flag = true;
            return 0;
        }

        // Get file size for the log.
        fseek(zip_file, 0, SEEK_END);
        long zip_size = ftell(zip_file);
        fseek(zip_file, 0, SEEK_SET);
        log_info("Download complete: %s (%ld bytes)", temp_file, zip_size);

        mz_zip_archive zip;
        memset(&zip, 0, sizeof(zip));
        if (!mz_zip_reader_init_cfile(&zip, zip_file, zip_size, 0))
        {
            ctx->item->error = "Could not open archive";
            ctx->item->status = DownloadStatus::Failed;
            log_error("Extraction failed: miniz init err=%d", (int)zip.m_last_error);
            fclose(zip_file);
            DeleteFileA(temp_file);
            *ctx->done_flag = true;
            return 0;
        }

        unsigned int file_count = mz_zip_reader_get_num_files(&zip);
        log_info("Extracting %u files to %s", file_count, ctx->item->install_dir.c_str());

        bool extract_ok = true;

        // Write callback for extraction — uses plain fwrite (no _wfopen).
        struct WriteCtx { FILE *f; };

        // Build the file manifest as we extract (path | CRC32HEX).
        std::string file_manifest;

        for (unsigned int i = 0; i < file_count; ++i)
        {
            mz_zip_archive_file_stat st;
            if (!mz_zip_reader_file_stat(&zip, i, &st))
                continue;

            // Build destination path.
            std::string dest = ctx->item->install_dir;
            if (!dest.empty() && dest[dest.size() - 1] != '\\')
                dest += '\\';
            std::string name = st.m_filename;
            for (size_t c = 0; c < name.size(); ++c)
                if (name[c] == '/') name[c] = '\\';
            dest += name;

            if (mz_zip_reader_is_file_a_directory(&zip, i))
            {
                CreateDirectoryA(dest.c_str(), NULL);
            }
            else
            {
                // Ensure parent directories exist.
                for (size_t p = 0; p < dest.size(); ++p)
                {
                    if (dest[p] == '\\' && p > 2) // skip "C:\"
                    {
                        std::string parent = dest.substr(0, p);
                        CreateDirectoryA(parent.c_str(), NULL);
                    }
                }

                // Extract using fopen + callback instead of
                // mz_zip_reader_extract_to_file (which uses _wfopen).
                FILE *out = fopen(dest.c_str(), "wb");
                if (!out)
                {
                    ctx->item->error = std::string("Failed creating ") + st.m_filename;
                    log_error("Extraction failed: cannot create %s (errno=%d)",
                              dest.c_str(), errno);
                    extract_ok = false;
                    break;
                }

                mz_bool ok = mz_zip_reader_extract_to_callback(
                    &zip, i,
                    [](void *pOpaque, mz_uint64, const void *pBuf, size_t n) -> size_t {
                        return fwrite(pBuf, 1, n, (FILE *)pOpaque);
                    },
                    out, 0);

                fclose(out);

                if (!ok)
                {
                    ctx->item->error = std::string("Failed extracting ") + st.m_filename;
                    log_error("Extraction failed: %s -> %s",
                              st.m_filename, dest.c_str());
                    extract_ok = false;
                    break;
                }

                // Record file in manifest: "entry_path | CRC32HEX\n"
                char crc_hex[16];
                sprintf(crc_hex, "%08X", (unsigned int)st.m_crc32);
                file_manifest += st.m_filename;
                file_manifest += " | ";
                file_manifest += crc_hex;
                file_manifest += "\n";
            }

            ctx->item->progress = (float)(i + 1) / (float)file_count;
        }

        mz_zip_reader_end(&zip);
        fclose(zip_file);
        DeleteFileA(temp_file);

        if (extract_ok)
        {
            // Write FileList.txt to .lancommander/{GameId}/ inside install dir.
            std::string meta_dir = ctx->item->install_dir + "\\.lancommander";
            CreateDirectoryA(meta_dir.c_str(), NULL);
            meta_dir += "\\" + ctx->item->game_id;
            CreateDirectoryA(meta_dir.c_str(), NULL);

            std::string list_path = meta_dir + "\\FileList.txt";
            FILE *fl = fopen(list_path.c_str(), "w");
            if (fl)
            {
                fwrite(file_manifest.c_str(), 1, file_manifest.size(), fl);
                fclose(fl);
            }

            ctx->item->progress = 1.0f;
            ctx->item->status = DownloadStatus::Complete;
            log_info("Install complete: %s", ctx->item->title.c_str());
        }
        else
        {
            ctx->item->status = DownloadStatus::Failed;
            log_error("Install failed: %s - %s",
                      ctx->item->title.c_str(), ctx->item->error.c_str());
        }

        *ctx->done_flag = true;
        return 0;
    }

    DownloadQueue::DownloadQueue()
        : m_active_idx(-1), m_thread(NULL), m_thread_done(false)
    {
    }

    DownloadQueue::~DownloadQueue()
    {
        if (m_thread)
        {
            WaitForSingleObject((HANDLE)m_thread, INFINITE);
            CloseHandle((HANDLE)m_thread);
        }
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

    void DownloadQueue::tick(lancommander::GameClient &games, lancommander::LibraryClient &library)
    {
        // Check if the active download thread finished.
        if (m_thread && m_thread_done)
        {
            WaitForSingleObject((HANDLE)m_thread, INFINITE);
            CloseHandle((HANDLE)m_thread);
            m_thread = NULL;
            m_thread_done = false;
            m_active_idx = -1;
        }

        // Start the next queued item if nothing is active.
        if (!m_thread)
            start_next(games, library);
    }

    bool DownloadQueue::has_active() const
    {
        return m_active_idx >= 0 && m_active_idx < (int)m_items.size() &&
               (m_items[m_active_idx].status == DownloadStatus::Downloading ||
                m_items[m_active_idx].status == DownloadStatus::Extracting);
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
            }
            else
                i++;
        }
    }

    // Thread context stored on the heap — freed when tick sees thread done.
    static DlThreadCtx *s_ctx = NULL;

    void DownloadQueue::start_next(lancommander::GameClient &games, lancommander::LibraryClient &library)
    {
        for (size_t i = 0; i < m_items.size(); ++i)
        {
            if (m_items[i].status == DownloadStatus::Queued)
            {
                log_info("start_next: starting item %d '%s'", (int)i, m_items[i].title.c_str());
                m_active_idx = (int)i;
                m_thread_done = false;

                // Allocate context for the thread.
                if (s_ctx) delete s_ctx;
                s_ctx = new DlThreadCtx();
                s_ctx->games = &games;
                s_ctx->library = &library;
                s_ctx->item = &m_items[i];
                s_ctx->done_flag = &m_thread_done;

                unsigned thread_id = 0;
                m_thread = (void *)_beginthreadex(NULL, 0, dl_thread_fn, s_ctx, 0, &thread_id);
                if (!m_thread)
                {
                    log_error("Failed to create download thread (errno=%d)", errno);
                    m_items[i].status = DownloadStatus::Failed;
                    m_items[i].error = "Failed to create download thread";
                    m_active_idx = -1;
                }
                else
                {
                    log_info("Download thread created (tid=%u)", thread_id);
                }
                return;
            }
        }
    }

} // namespace launcher
