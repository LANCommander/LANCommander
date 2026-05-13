#ifndef LANCOMMANDER_WIN9X_SAVE_SYNC_WORKER_H
#define LANCOMMANDER_WIN9X_SAVE_SYNC_WORKER_H

#include <wx/thread.h>

#include <string>

#include "game_client.h"
#include "save_client.h"

struct SaveJobState
{
    enum Phase
    {
        PhaseGettingManifest,
        PhasePacking,
        PhaseUploading,
        PhaseDownloading,
        PhaseUnpacking,
        PhaseDone
    };

    volatile unsigned long received;
    volatile unsigned long total;
    volatile int  phase;
    volatile bool cancelled;
    volatile bool finished;
    bool          succeeded;
    bool          empty;     // upload only: no files matched the savepaths
    std::string   error;     // valid after finished

    SaveJobState()
        : received(0), total(0), phase(PhaseGettingManifest),
          cancelled(false), finished(false),
          succeeded(false), empty(false) {}
};

class SaveSyncWorker : public wxThread
{
public:
    enum Mode { Upload, Download };

    SaveSyncWorker(Mode mode, GameClient* games, SaveClient* saves,
                   const std::string& gameId,
                   const std::string& installDir,
                   const std::string& zipPath,
                   SaveJobState* state);

    virtual ExitCode Entry();

private:
    static bool DownloadCb(unsigned long received, unsigned long total,
                           void* userData);

    Mode             m_mode;
    GameClient*      m_games;
    SaveClient*      m_saves;
    std::string      m_gameId;
    std::string      m_installDir;
    std::string      m_zipPath;
    SaveJobState*    m_state;
};

#endif
