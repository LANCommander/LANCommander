#include "install_worker.h"
#include "archive.h"

InstallWorker::InstallWorker(GameClient* games, const std::string& gameId,
                             const std::string& zipPath, const std::string& destDir,
                             InstallJobState* state)
    : wxThread(wxTHREAD_JOINABLE),
      m_games(games), m_gameId(gameId),
      m_zipPath(zipPath), m_destDir(destDir),
      m_state(state)
{
}

bool InstallWorker::DownloadCb(unsigned long received, unsigned long total,
                               void* ud)
{
    InstallJobState* s = static_cast<InstallJobState*>(ud);
    s->received = received;
    s->total    = total;
    return !s->cancelled;
}

bool InstallWorker::ExtractCb(unsigned long idx, unsigned long count,
                              const char* name, void* ud)
{
    InstallJobState* s = static_cast<InstallJobState*>(ud);
    s->extractIdx   = idx;
    s->extractCount = count;
    if (name) s->currentName = name;
    return !s->cancelled;
}

wxThread::ExitCode InstallWorker::Entry()
{
    std::string err;

    m_state->phase = InstallJobState::PhaseDownload;
    if (!m_games->DownloadGame(m_gameId, m_zipPath, &DownloadCb, m_state, &err))
    {
        m_state->error     = err;
        m_state->succeeded = false;
        m_state->finished  = true;
        return (ExitCode)0;
    }

    m_state->phase = InstallJobState::PhaseExtract;
    if (!ExtractZip(m_zipPath, m_destDir, &ExtractCb, m_state, &err))
    {
        m_state->error     = err;
        m_state->succeeded = false;
        m_state->finished  = true;
        return (ExitCode)0;
    }

    m_state->phase     = InstallJobState::PhaseDone;
    m_state->succeeded = true;
    m_state->finished  = true;
    return (ExitCode)0;
}
