#ifndef LANCOMMANDER_WIN9X_APP_H
#define LANCOMMANDER_WIN9X_APP_H

#include <wx/wx.h>
#include <wx/imaglist.h>
#include <wx/listctrl.h>
#include <wx/timer.h>

#include <vector>

#include "http_client.h"
#include "auth_client.h"
#include "game_client.h"
#include "library_client.h"
#include "key_client.h"
#include "media_client.h"
#include "profile_client.h"
#include "redistributable_client.h"
#include "save_client.h"
#include "script_client.h"
#include "catalog_db.h"
#include "installs.h"
#include "media_cache.h"
#include "server_store.h"
#include "settings.h"

class LanCommanderApp : public wxApp
{
public:
    virtual bool OnInit();
};

enum ViewMode { ViewList = 0, ViewGrid = 1, ViewShelf = 2 };

struct RunningGame
{
    std::string gameId;
    void* process; // HANDLE
};

struct InstallQueueItem
{
    enum State { Queued, Running, Failed, Cancelled };

    std::string gameId;
    std::string title;
    std::string destDir;
    State       state;
    std::string error; // populated when state == Failed

    InstallQueueItem() : state(Queued) {}
};

class InstallWorker;
struct InstallJobState;

class MainFrame : public wxFrame
{
public:
    MainFrame();
    virtual ~MainFrame();

private:
    void OnExit(wxCommandEvent& event);
    void OnAbout(wxCommandEvent& event);
    void OnConnect(wxCommandEvent& event);
    void OnDisconnect(wxCommandEvent& event);
    void OnFindServers(wxCommandEvent& event);
    void OnSwitchServer(wxCommandEvent& event);
    void OnToggleOnline(wxCommandEvent& event);
    void OnPreferences(wxCommandEvent& event);
    void OnRefresh(wxCommandEvent& event);
    void OnViewList(wxCommandEvent& event);
    void OnViewGrid(wxCommandEvent& event);
    void OnViewShelf(wxCommandEvent& event);
    void OnGameActivated(wxListEvent& event);
    void OnGameSelected(wxListEvent& event);
    void OnColumnClick(wxListEvent& event);
    void OnSearchChanged(wxCommandEvent& event);
    void OnFilterChanged(wxCommandEvent& event);
    void OnActionPlay(wxCommandEvent& event);
    void OnActionPlayWith(wxCommandEvent& event);
    void OnActionUpdate(wxCommandEvent& event);
    void OnActionUninstall(wxCommandEvent& event);
    void OnActionSaveUp(wxCommandEvent& event);
    void OnActionSaveDown(wxCommandEvent& event);
    void OnActionMedia(wxCommandEvent& event);
    void OnActionPrereqs(wxCommandEvent& event);
    void OnChangeAlias(wxCommandEvent& event);
    void OnChangeKey(wxCommandEvent& event);
    void OnQueueItemRightClick(wxListEvent& event);
    void OnQueueCancelItem(wxCommandEvent& event);
    void OnQueueRemoveItem(wxCommandEvent& event);
    void OnQueueShowError(wxCommandEvent& event);
    void OnQueueClearFinished(wxCommandEvent& event);
    void OnQueueTick(wxTimerEvent& event);
    void OnSessionPoll(wxTimerEvent& event);
    void OnUploadSave(wxCommandEvent& event);
    void OnDownloadSave(wxCommandEvent& event);
    void OnUninstall(wxCommandEvent& event);
    void OnPlayWith(wxCommandEvent& event);
    void OnAddToLibrary(wxCommandEvent& event);
    void OnRemoveFromLibrary(wxCommandEvent& event);

    void ImportCatalog();
    void ReloadFromDb();
    void RefreshList();
    void BuildGameList(ViewMode mode);
    void SetViewMode(ViewMode mode);
    void InstallGame(const std::string& gameId, const std::string& title);
    void RunNameChangeForAllInstalled(const std::string& alias);
    void RunNameChangeForGame(const std::string& gameId,
                              const std::string& installDir,
                              const std::string& alias);
    void RunKeyChangeForGame(const std::string& gameId,
                             const std::string& installDir);
    void EnqueueInstall(const std::string& gameId, const std::string& title,
                        const std::string& destDir);
    void UpdateGame(const std::string& gameId, const std::string& title,
                    const std::string& installDir);
    void StartNextQueueItem();
    void FinishCurrentItem(bool success);
    void UpdateQueuePanel();
    int  FindQueueItemById(const std::string& gameId) const;
    wxString QueueStatusText(const InstallQueueItem& item) const;
    void PlayGame(const std::string& gameId, const std::string& installDir);
    void LaunchPicked(const std::string& gameId, const std::string& installDir,
                      const GameManifest& manifest, const ManifestAction& action);
    bool SelectedGame(std::string* gameId, std::string* title,
                      std::string* installDir);
    void ShowCover(const GameSummary& g);
    void ClearCover();

    bool TryAutoReconnect();
    void SetOnline(bool online);
    void UpdateConnectionState();
    bool RequireOnline(const wxString& action);

    // Cached manifest helpers. `installDir` is the game's install root; the
    // manifest is cached at `<installDir>\manifest.json`.
    static std::string ManifestPath(const std::string& installDir);
    static bool SaveManifestJson(const std::string& installDir,
                                 const std::string& json);
    bool LoadManifest(const std::string& gameId, const std::string& installDir,
                      GameManifest* out, std::string* errorOut);

    HttpClient                m_http;
    AuthenticationClient      m_auth;
    GameClient                m_games_api;
    LibraryClient             m_library_api;
    MediaClient               m_media_api;
    KeyClient                 m_keys_api;
    ProfileClient             m_profile_api;
    RedistributableClient     m_redist_api;
    SaveClient                m_save_api;
    ScriptClient              m_scripts_api;
    InstallRegistry           m_installs;
    Settings                  m_settings;
    MediaCache                m_media;
    CatalogDb                 m_catalog;
    ServerStore               m_servers;
    std::string               m_serverAddress;
    wxPanel*                  m_gameListHost; // container so we can swap the list child
    wxListCtrl*               m_gameList;
    wxImageList*              m_gameImages;
    ViewMode                  m_viewMode;
    wxTextCtrl*               m_search;
    wxCheckBox*               m_filterLibrary;
    wxCheckBox*               m_filterInstalled;
    wxChoice*                 m_filterGenre;
    wxStaticBitmap*           m_cover;
    wxStaticText*             m_coverLabel;
    wxTextCtrl*               m_detail;
    wxPanel*                  m_actionBar;
    wxButton*                 m_btnPlay;
    wxButton*                 m_btnPlayWith;
    wxButton*                 m_btnUpdate;
    wxButton*                 m_btnSaveUp;
    wxButton*                 m_btnSaveDown;
    wxButton*                 m_btnUninstall;
    wxButton*                 m_btnMedia;
    wxButton*                 m_btnPrereqs;
    bool                      m_selectionUpdateAvailable;

    // Install queue + persistent list panel. All items live in m_queue; the
    // running item (if any) is at index m_runningIdx. Failed/Cancelled items
    // stay visible until the user removes them or clears finished entries.
    wxPanel*                       m_queuePanel;
    wxStaticText*                  m_queueSummary;
    wxButton*                      m_queueClearBtn;
    wxListCtrl*                    m_queueList;
    wxTimer                        m_queueTimer;
    std::vector<InstallQueueItem>  m_queue;
    int                            m_runningIdx;
    InstallWorker*                 m_currentWorker;
    InstallJobState*               m_currentState;
    std::string                    m_currentZipPath;
    wxTimer                   m_sessionTimer;
    std::vector<RunningGame>  m_running;
    std::vector<GameSummary>  m_games;       // all games from server
    int                       m_sortColumn;  // 0=title, 1=installed, 2=released
    bool                      m_sortDescending;
    bool                      m_online;

    wxDECLARE_EVENT_TABLE();
};

enum
{
    ID_GameList = 1,
    ID_Connect,
    ID_Disconnect,
    ID_FindServers,
    ID_SwitchServer,
    ID_ToggleOnline,
    ID_Preferences,
    ID_Refresh,
    ID_ViewList,
    ID_ViewGrid,
    ID_ViewShelf,
    ID_UploadSave,
    ID_DownloadSave,
    ID_Uninstall,
    ID_PlayWith,
    ID_AddToLibrary,
    ID_RemoveFromLibrary,
    ID_ChangeAlias,
    ID_ChangeKey,
    ID_Search,
    ID_FilterLibrary,
    ID_FilterInstalled,
    ID_FilterGenre,
    ID_ActionPlay,
    ID_ActionPlayWith,
    ID_ActionUpdate,
    ID_ActionUninstall,
    ID_ActionSaveUp,
    ID_ActionSaveDown,
    ID_ActionMedia,
    ID_ActionPrereqs,
    ID_QueueList,
    ID_QueueCancelItem,
    ID_QueueRemoveItem,
    ID_QueueShowError,
    ID_QueueClearFinished,
    ID_QueueTimer,
    ID_SessionTimer
};

#endif
