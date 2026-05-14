#ifndef LANCOMMANDER_WIN9X_INSTALL_WORKER_H
#define LANCOMMANDER_WIN9X_INSTALL_WORKER_H

#include <wx/thread.h>

#include <string>

#include "game_client.h"

// Shared state between the install worker thread and the UI thread.
// Numeric fields are written by the worker and read by the UI; volatile is
// sufficient on Win9x/x86 (aligned word reads/writes are atomic and we never
// branch on related fields at the same time).
struct InstallJobState
{
    enum Phase { PhaseDownload, PhaseExtract, PhaseDone };

    volatile unsigned long received;
    volatile unsigned long total;
    volatile unsigned long extractIdx;
    volatile unsigned long extractCount;
    volatile int  phase;
    volatile bool cancelled;
    volatile bool finished;
    bool          succeeded;
    std::string   error;       // only valid after finished
    std::string   currentName; // updated by worker; read-on-tick

    InstallJobState()
        : received(0), total(0), extractIdx(0), extractCount(0),
          phase(PhaseDownload), cancelled(false), finished(false),
          succeeded(false) {}
};

class InstallWorker : public wxThread
{
public:
    InstallWorker(GameClient* games, const std::string& gameId,
                  const std::string& zipPath, const std::string& destDir,
                  InstallJobState* state);

    virtual ExitCode Entry();

private:
    static bool DownloadCb(unsigned long received, unsigned long total, void* ud);
    static bool ExtractCb(unsigned long idx, unsigned long count,
                          const char* name, void* ud);

    GameClient*       m_games;
    std::string       m_gameId;
    std::string       m_zipPath;
    std::string       m_destDir;
    InstallJobState*  m_state;
};

#endif
