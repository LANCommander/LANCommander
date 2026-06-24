#include "save_sync_worker.h"
#include "save_sync.h"

SaveSyncWorker::SaveSyncWorker(Mode mode, GameClient* games, SaveClient* saves,
                               const std::string& gameId,
                               const std::string& installDir,
                               const std::string& zipPath,
                               SaveJobState* state)
    : wxThread(wxTHREAD_JOINABLE),
      m_mode(mode), m_games(games), m_saves(saves),
      m_gameId(gameId), m_installDir(installDir),
      m_zipPath(zipPath), m_state(state)
{
}

bool SaveSyncWorker::DownloadCb(unsigned long received, unsigned long total,
                                void* userData)
{
    SaveJobState* s = static_cast<SaveJobState*>(userData);
    s->received = received;
    s->total    = total;
    return !s->cancelled;
}

wxThread::ExitCode SaveSyncWorker::Entry()
{
    GameManifest manifest;
    m_state->phase = SaveJobState::PhaseGettingManifest;
    if (!m_games->GetManifest(m_gameId, &manifest, &m_state->error))
    {
        m_state->succeeded = false;
        m_state->finished  = true;
        return (ExitCode)0;
    }

    if (m_mode == Upload)
    {
        m_state->phase = SaveJobState::PhasePacking;
        bool empty = true;
        if (!PackSaveArchive(manifest.savePaths, m_installDir, m_zipPath,
                             &empty, &m_state->error))
        {
            m_state->succeeded = false;
            m_state->finished  = true;
            return (ExitCode)0;
        }
        if (empty)
        {
            m_state->empty     = true;
            m_state->succeeded = true;
            m_state->phase     = SaveJobState::PhaseDone;
            m_state->finished  = true;
            return (ExitCode)0;
        }

        m_state->phase = SaveJobState::PhaseUploading;
        if (!m_saves->UploadSave(m_gameId, m_zipPath, &m_state->error))
        {
            m_state->succeeded = false;
            m_state->finished  = true;
            return (ExitCode)0;
        }
    }
    else
    {
        m_state->phase = SaveJobState::PhaseDownloading;
        if (!m_saves->DownloadLatestSave(m_gameId, m_zipPath,
                                         &DownloadCb, m_state, &m_state->error))
        {
            m_state->succeeded = false;
            m_state->finished  = true;
            return (ExitCode)0;
        }

        m_state->phase = SaveJobState::PhaseUnpacking;
        if (!UnpackSaveArchive(m_zipPath, manifest.savePaths, m_installDir,
                               &m_state->error))
        {
            m_state->succeeded = false;
            m_state->finished  = true;
            return (ExitCode)0;
        }
    }

    m_state->succeeded = true;
    m_state->phase     = SaveJobState::PhaseDone;
    m_state->finished  = true;
    return (ExitCode)0;
}
