#include "app.h"
#include "archive.h"
#include "launcher.h"
#include "save_sync.h"
#include "install_worker.h"
#include "save_sync_worker.h"
#include "install_options_dialog.h"
#include "server_picker_dialog.h"
#include "settings_dialog.h"
#include "screenshots_dialog.h"
#include "discovery.h"
#include "logger.h"
#include "script_runner.h"

#include <wx/choicdlg.h>
#include <wx/busyinfo.h>
#include <wx/statbmp.h>
#include <wx/image.h>
#include <wx/dcmemory.h>

#include <wx/textdlg.h>
#include <wx/dirdlg.h>
#include <wx/filename.h>
#include <wx/progdlg.h>
#include <wx/utils.h>

#include <windows.h>
#include <shellapi.h>

wxBEGIN_EVENT_TABLE(MainFrame, wxFrame)
    EVT_MENU(wxID_EXIT,  MainFrame::OnExit)
    EVT_MENU(wxID_ABOUT, MainFrame::OnAbout)
    EVT_MENU(ID_Connect,      MainFrame::OnConnect)
    EVT_MENU(ID_Disconnect,   MainFrame::OnDisconnect)
    EVT_MENU(ID_FindServers,  MainFrame::OnFindServers)
    EVT_MENU(ID_SwitchServer, MainFrame::OnSwitchServer)
    EVT_MENU(ID_ToggleOnline, MainFrame::OnToggleOnline)
    EVT_MENU(ID_Preferences,  MainFrame::OnPreferences)
    EVT_MENU(ID_Refresh,      MainFrame::OnRefresh)
    EVT_MENU(ID_ViewList,     MainFrame::OnViewList)
    EVT_MENU(ID_ViewGrid,     MainFrame::OnViewGrid)
    EVT_MENU(ID_ViewShelf,    MainFrame::OnViewShelf)
    EVT_MENU(ID_UploadSave,   MainFrame::OnUploadSave)
    EVT_MENU(ID_DownloadSave, MainFrame::OnDownloadSave)
    EVT_MENU(ID_Uninstall,    MainFrame::OnUninstall)
    EVT_MENU(ID_PlayWith,           MainFrame::OnPlayWith)
    EVT_MENU(ID_AddToLibrary,       MainFrame::OnAddToLibrary)
    EVT_MENU(ID_RemoveFromLibrary,  MainFrame::OnRemoveFromLibrary)
    EVT_MENU(ID_ChangeAlias,        MainFrame::OnChangeAlias)
    EVT_MENU(ID_ChangeKey,          MainFrame::OnChangeKey)
    EVT_LIST_ITEM_ACTIVATED(ID_GameList, MainFrame::OnGameActivated)
    EVT_LIST_ITEM_SELECTED(ID_GameList,  MainFrame::OnGameSelected)
    EVT_LIST_ITEM_DESELECTED(ID_GameList, MainFrame::OnGameSelected)
    EVT_LIST_COL_CLICK(ID_GameList, MainFrame::OnColumnClick)
    EVT_TEXT(ID_Search, MainFrame::OnSearchChanged)
    EVT_CHECKBOX(ID_FilterLibrary,   MainFrame::OnFilterChanged)
    EVT_CHECKBOX(ID_FilterInstalled, MainFrame::OnFilterChanged)
    EVT_CHOICE(ID_FilterGenre,       MainFrame::OnFilterChanged)
    EVT_BUTTON(ID_ActionPlay,       MainFrame::OnActionPlay)
    EVT_BUTTON(ID_ActionPlayWith,   MainFrame::OnActionPlayWith)
    EVT_BUTTON(ID_ActionUpdate,     MainFrame::OnActionUpdate)
    EVT_BUTTON(ID_ActionUninstall,  MainFrame::OnActionUninstall)
    EVT_BUTTON(ID_ActionSaveUp,     MainFrame::OnActionSaveUp)
    EVT_BUTTON(ID_ActionSaveDown,   MainFrame::OnActionSaveDown)
    EVT_BUTTON(ID_ActionMedia,      MainFrame::OnActionMedia)
    EVT_BUTTON(ID_ActionPrereqs,    MainFrame::OnActionPrereqs)
    EVT_LIST_ITEM_RIGHT_CLICK(ID_QueueList, MainFrame::OnQueueItemRightClick)
    EVT_MENU(ID_QueueCancelItem,    MainFrame::OnQueueCancelItem)
    EVT_MENU(ID_QueueRemoveItem,    MainFrame::OnQueueRemoveItem)
    EVT_MENU(ID_QueueShowError,     MainFrame::OnQueueShowError)
    EVT_BUTTON(ID_QueueClearFinished, MainFrame::OnQueueClearFinished)
    EVT_TIMER(ID_QueueTimer,   MainFrame::OnQueueTick)
    EVT_TIMER(ID_SessionTimer, MainFrame::OnSessionPoll)
wxEND_EVENT_TABLE()

wxIMPLEMENT_APP(LanCommanderApp);

bool LanCommanderApp::OnInit()
{
    // Picks up whichever image handlers wxWidgets was built with
    // (BMP is always on; JPEG/PNG/GIF depend on the wx build's
    // wxUSE_LIBJPEG / wxUSE_LIBPNG / wxUSE_GIF flags).
    wxInitAllImageHandlers();

    MainFrame* frame = new MainFrame();
    frame->Show(true);
    return true;
}

MainFrame::MainFrame()
    : wxFrame(NULL, wxID_ANY, wxT("LANCommander"), wxDefaultPosition, wxSize(880, 520)),
      m_auth(m_http),
      m_games_api(m_http),
      m_library_api(m_http),
      m_media_api(m_http),
      m_keys_api(m_http),
      m_profile_api(m_http),
      m_redist_api(m_http),
      m_save_api(m_http),
      m_scripts_api(m_http),
      m_media(&m_media_api),
      m_gameListHost(NULL),
      m_gameList(NULL),
      m_gameImages(NULL),
      m_viewMode(ViewList),
      m_search(NULL),
      m_filterLibrary(NULL),
      m_filterInstalled(NULL),
      m_filterGenre(NULL),
      m_cover(NULL),
      m_coverLabel(NULL),
      m_detail(NULL),
      m_actionBar(NULL),
      m_btnPlay(NULL),
      m_btnPlayWith(NULL),
      m_btnUpdate(NULL),
      m_btnSaveUp(NULL),
      m_btnSaveDown(NULL),
      m_btnUninstall(NULL),
      m_btnMedia(NULL),
      m_btnPrereqs(NULL),
      m_selectionUpdateAvailable(false),
      m_queuePanel(NULL),
      m_queueSummary(NULL),
      m_queueClearBtn(NULL),
      m_queueList(NULL),
      m_queueTimer(this, ID_QueueTimer),
      m_runningIdx(-1),
      m_currentWorker(NULL),
      m_currentState(NULL),
      m_sessionTimer(this, ID_SessionTimer),
      m_sortColumn(0),
      m_sortDescending(false),
      m_online(false)
{
    wxMenu* menuFile = new wxMenu;
    menuFile->Append(ID_Connect,      wxT("&Connect...\tCtrl-N"));
    menuFile->Append(ID_FindServers,  wxT("&Find Servers...\tCtrl-F"));
    menuFile->Append(ID_SwitchServer, wxT("&Servers...\tCtrl-S"));
    menuFile->Append(ID_Disconnect,   wxT("&Disconnect"));
    menuFile->AppendSeparator();
    menuFile->Append(ID_ToggleOnline, wxT("Go &Offline"));
    menuFile->Append(ID_Refresh,      wxT("&Refresh\tF5"));
    menuFile->AppendSeparator();
    menuFile->Append(ID_Preferences,  wxT("&Preferences...\tCtrl-,"));
    menuFile->Append(ID_ChangeAlias,  wxT("Change &Alias..."));
    menuFile->AppendSeparator();
    menuFile->Append(wxID_EXIT);

    wxMenu* menuGame = new wxMenu;
    menuGame->Append(ID_PlayWith,          wxT("&Play with..."));
    menuGame->AppendSeparator();
    menuGame->Append(ID_AddToLibrary,      wxT("&Add to Library"));
    menuGame->Append(ID_RemoveFromLibrary, wxT("&Remove from Library"));
    menuGame->AppendSeparator();
    menuGame->Append(ID_ChangeKey,         wxT("Change &Key"));
    menuGame->Append(ID_Uninstall,         wxT("&Uninstall"));

    wxMenu* menuSave = new wxMenu;
    menuSave->Append(ID_UploadSave,   wxT("&Upload Save"));
    menuSave->Append(ID_DownloadSave, wxT("&Download Save"));

    wxMenu* menuView = new wxMenu;
    menuView->AppendRadioItem(ID_ViewList,  wxT("&List\tCtrl-1"));
    menuView->AppendRadioItem(ID_ViewGrid,  wxT("&Grid\tCtrl-2"));
    menuView->AppendRadioItem(ID_ViewShelf, wxT("&Shelf\tCtrl-3"));

    wxMenu* menuHelp = new wxMenu;
    menuHelp->Append(wxID_ABOUT);

    wxMenuBar* menuBar = new wxMenuBar;
    menuBar->Append(menuFile, wxT("&File"));
    menuBar->Append(menuView, wxT("&View"));
    menuBar->Append(menuGame, wxT("&Game"));
    menuBar->Append(menuSave, wxT("&Saves"));
    menuBar->Append(menuHelp, wxT("&Help"));
    SetMenuBar(menuBar);

    CreateStatusBar();
    SetStatusText(wxT("Not connected"));

    wxPanel* panel = new wxPanel(this);
    wxBoxSizer* root = new wxBoxSizer(wxVERTICAL);

    wxBoxSizer* searchRow = new wxBoxSizer(wxHORIZONTAL);
    searchRow->Add(new wxStaticText(panel, wxID_ANY, wxT("Search:")),
                   0, wxALIGN_CENTER_VERTICAL | wxLEFT | wxRIGHT, 4);
    m_search = new wxTextCtrl(panel, ID_Search);
    searchRow->Add(m_search, 1, wxEXPAND | wxRIGHT, 4);
    root->Add(searchRow, 0, wxEXPAND | wxTOP | wxBOTTOM, 4);

    wxBoxSizer* filterRow = new wxBoxSizer(wxHORIZONTAL);
    m_filterLibrary = new wxCheckBox(panel, ID_FilterLibrary, wxT("Library only"));
    m_filterInstalled = new wxCheckBox(panel, ID_FilterInstalled, wxT("Installed only"));
    filterRow->Add(m_filterLibrary,   0, wxALIGN_CENTER_VERTICAL | wxLEFT | wxRIGHT, 4);
    filterRow->Add(m_filterInstalled, 0, wxALIGN_CENTER_VERTICAL | wxRIGHT, 8);
    filterRow->Add(new wxStaticText(panel, wxID_ANY, wxT("Genre:")),
                   0, wxALIGN_CENTER_VERTICAL | wxRIGHT, 4);
    m_filterGenre = new wxChoice(panel, ID_FilterGenre);
    m_filterGenre->Append(wxT("(All)"));
    m_filterGenre->SetSelection(0);
    filterRow->Add(m_filterGenre, 1, wxEXPAND | wxRIGHT, 4);
    root->Add(filterRow, 0, wxEXPAND | wxBOTTOM, 4);

    wxBoxSizer* body = new wxBoxSizer(wxHORIZONTAL);

    m_gameListHost = new wxPanel(panel);
    m_gameListHost->SetSizer(new wxBoxSizer(wxVERTICAL));
    body->Add(m_gameListHost, 1, wxEXPAND);

    {
        std::string dbErr;
        if (!m_catalog.Open(&dbErr))
            LogError("Catalog DB open failed: %s", dbErr.c_str());
    }
    // The actual wxListCtrl is built in BuildGameList() once settings have
    // loaded, so we respect the persisted view mode.

    wxBoxSizer* coverCol = new wxBoxSizer(wxVERTICAL);
    m_cover = new wxStaticBitmap(panel, wxID_ANY, wxNullBitmap,
                                 wxDefaultPosition, wxSize(220, 300));
    coverCol->Add(m_cover, 0, wxALL, 6);
    m_coverLabel = new wxStaticText(panel, wxID_ANY, wxEmptyString,
                                    wxDefaultPosition, wxSize(260, -1));
    coverCol->Add(m_coverLabel, 0, wxLEFT | wxRIGHT, 6);

    m_actionBar = new wxPanel(panel);
    wxBoxSizer* actBox = new wxBoxSizer(wxVERTICAL);
    wxGridSizer* actGrid = new wxGridSizer(3, 2, 4, 4);
    m_btnPlay      = new wxButton(m_actionBar, ID_ActionPlay,      wxT("Play"));
    m_btnPlayWith  = new wxButton(m_actionBar, ID_ActionPlayWith,  wxT("Play with..."));
    m_btnUpdate    = new wxButton(m_actionBar, ID_ActionUpdate,    wxT("Update"));
    m_btnSaveUp    = new wxButton(m_actionBar, ID_ActionSaveUp,    wxT("Save Up"));
    m_btnSaveDown  = new wxButton(m_actionBar, ID_ActionSaveDown,  wxT("Save Down"));
    m_btnUninstall = new wxButton(m_actionBar, ID_ActionUninstall, wxT("Uninstall"));
    actGrid->Add(m_btnPlay,      0, wxEXPAND);
    actGrid->Add(m_btnPlayWith,  0, wxEXPAND);
    actGrid->Add(m_btnUpdate,    0, wxEXPAND);
    actGrid->Add(m_btnUninstall, 0, wxEXPAND);
    actGrid->Add(m_btnSaveUp,    0, wxEXPAND);
    actGrid->Add(m_btnSaveDown,  0, wxEXPAND);
    actBox->Add(actGrid, 0, wxEXPAND);
    m_btnMedia = new wxButton(m_actionBar, ID_ActionMedia, wxT("Media..."));
    actBox->Add(m_btnMedia, 0, wxEXPAND | wxTOP, 4);
    m_btnPrereqs = new wxButton(m_actionBar, ID_ActionPrereqs,
                                wxT("Prerequisites..."));
    actBox->Add(m_btnPrereqs, 0, wxEXPAND | wxTOP, 4);
    m_actionBar->SetSizer(actBox);
    coverCol->Add(m_actionBar, 0, wxEXPAND | wxALL, 6);

    m_detail = new wxTextCtrl(panel, wxID_ANY, wxEmptyString,
                              wxDefaultPosition, wxSize(260, -1),
                              wxTE_MULTILINE | wxTE_READONLY |
                              wxTE_BESTWRAP | wxBORDER_NONE);
    coverCol->Add(m_detail, 1, wxEXPAND | wxALL, 6);
    body->Add(coverCol, 0, wxEXPAND);

    root->Add(body, 1, wxEXPAND);

    m_queuePanel = new wxPanel(panel);
    wxBoxSizer* qSizer = new wxBoxSizer(wxVERTICAL);

    wxBoxSizer* qHeader = new wxBoxSizer(wxHORIZONTAL);
    m_queueSummary  = new wxStaticText(m_queuePanel, wxID_ANY, wxEmptyString);
    m_queueClearBtn = new wxButton(m_queuePanel, ID_QueueClearFinished,
                                   wxT("Clear finished"));
    qHeader->Add(m_queueSummary,  1, wxALIGN_CENTER_VERTICAL | wxLEFT, 6);
    qHeader->Add(m_queueClearBtn, 0, wxALIGN_CENTER_VERTICAL | wxRIGHT, 6);
    qSizer->Add(qHeader, 0, wxEXPAND | wxTOP, 2);

    m_queueList = new wxListCtrl(m_queuePanel, ID_QueueList,
                                 wxDefaultPosition, wxSize(-1, 80),
                                 wxLC_REPORT | wxLC_SINGLE_SEL |
                                 wxBORDER_SUNKEN);
    m_queueList->InsertColumn(0, wxT("Game"),   wxLIST_FORMAT_LEFT, 240);
    m_queueList->InsertColumn(1, wxT("Status"), wxLIST_FORMAT_LEFT, 380);
    qSizer->Add(m_queueList, 1, wxEXPAND | wxLEFT | wxRIGHT | wxBOTTOM, 4);

    m_queuePanel->SetSizer(qSizer);
    m_queuePanel->Hide();
    root->Add(m_queuePanel, 0, wxEXPAND | wxTOP | wxBOTTOM, 2);

    panel->SetSizer(root);

    m_installs.Load();
    m_settings.Load();
    m_servers.Load();

    // Build the game list now that the persisted view mode is known.
    m_viewMode = (ViewMode)m_settings.viewMode;
    BuildGameList(m_viewMode);

    if (wxMenuBar* mb = GetMenuBar())
    {
        if (m_viewMode == ViewList)  mb->Check(ID_ViewList,  true);
        if (m_viewMode == ViewGrid)  mb->Check(ID_ViewGrid,  true);
        if (m_viewMode == ViewShelf) mb->Check(ID_ViewShelf, true);
    }

    // Seed the server store from the legacy single-server settings on first run
    // so existing users get an entry the first time they open Servers...
    if (m_servers.Entries().empty() && !m_settings.serverUrl.empty())
    {
        ServerBookmark b;
        b.url          = m_settings.serverUrl;
        b.userName     = m_settings.userName;
        b.accessToken  = m_settings.accessToken;
        b.refreshToken = m_settings.refreshToken;
        b.expiration   = m_settings.expiration;
        m_servers.Upsert(b);
        m_servers.Save();
    }

    m_filterLibrary->SetValue(m_settings.showLibraryOnly);
    m_filterInstalled->SetValue(m_settings.showInstalledOnly);

    // Always show whatever's already cached so the launcher is usable before
    // (or without) a server connection.
    ReloadFromDb();

    if (TryAutoReconnect())
    {
        SetOnline(true);
        ImportCatalog();
    }
    else
    {
        SetOnline(false);
    }
}

bool MainFrame::TryAutoReconnect()
{
    if (m_settings.serverUrl.empty() || m_settings.accessToken.empty())
        return false;

    m_serverAddress = m_settings.serverUrl;
    m_http.SetBaseUrl(m_serverAddress);
    m_http.SetBearerToken(m_settings.accessToken);

    std::string err;
    if (m_auth.Validate(&err)) return true;
    LogError("Auto-reconnect validate failed on %s: %s",
             m_serverAddress.c_str(), err.c_str());

    if (!m_settings.refreshToken.empty())
    {
        AuthTokens in;
        in.accessToken  = m_settings.accessToken;
        in.refreshToken = m_settings.refreshToken;
        in.expiration   = m_settings.expiration;

        AuthTokens fresh;
        std::string refreshErr;
        if (m_auth.Refresh(in, &fresh, &refreshErr))
        {
            m_settings.accessToken  = fresh.accessToken;
            m_settings.refreshToken = fresh.refreshToken;
            m_settings.expiration   = fresh.expiration;
            m_settings.Save();

            ServerBookmark bm;
            const ServerBookmark* existing = m_servers.Find(m_serverAddress);
            if (existing) bm = *existing;
            bm.url          = m_serverAddress;
            bm.userName     = m_settings.userName;
            bm.accessToken  = fresh.accessToken;
            bm.refreshToken = fresh.refreshToken;
            bm.expiration   = fresh.expiration;
            m_servers.Upsert(bm);
            m_servers.Save();
            return true;
        }
        LogError("Auto-reconnect refresh failed on %s: %s",
                 m_serverAddress.c_str(), refreshErr.c_str());
    }

    m_http.SetBearerToken(std::string());
    return false;
}

void MainFrame::SetOnline(bool online)
{
    m_online = online;
    UpdateConnectionState();

    // First time we see the user online, seed the local alias from the server
    // so NameChange scripts have something to pass. If the user has already
    // set an alias locally, we keep it — they may have edited it offline.
    if (online && m_settings.alias.empty())
    {
        std::string alias;
        if (m_profile_api.GetAlias(&alias, NULL) && !alias.empty())
        {
            m_settings.alias = alias;
            m_settings.Save();
        }
    }
}

void MainFrame::UpdateConnectionState()
{
    wxMenuBar* bar = GetMenuBar();
    if (bar)
    {
        wxMenuItem* toggle = bar->FindItem(ID_ToggleOnline);
        if (toggle)
            toggle->SetItemLabel(m_online ? wxT("Go &Offline") : wxT("Go &Online"));

        bool haveToken = !m_settings.accessToken.empty();
        bar->Enable(ID_Disconnect,        haveToken);
        bar->Enable(ID_Refresh,           m_online);
        bar->Enable(ID_UploadSave,        m_online);
        bar->Enable(ID_DownloadSave,      m_online);
        bar->Enable(ID_AddToLibrary,      m_online);
        bar->Enable(ID_RemoveFromLibrary, m_online);
        bar->Enable(ID_ChangeKey,         m_online);
        bar->Enable(ID_ToggleOnline,      haveToken);
    }

    wxString status;
    if (m_online)
    {
        status = wxT("Online");
        if (!m_serverAddress.empty())
        {
            status += wxT(" \xE2\x80\x93 ");
            status += wxString(m_serverAddress.c_str(), wxConvUTF8);
        }
    }
    else
    {
        status = m_settings.accessToken.empty()
            ? wxT("Offline (no server)")
            : wxT("Offline (cached catalog)");
    }
    SetStatusText(status);
}

bool MainFrame::RequireOnline(const wxString& action)
{
    if (m_online) return true;
    wxString msg = action;
    msg += wxT(" needs an online connection. Use File > Go Online or Connect first.");
    wxMessageBox(msg, wxT("Offline"), wxOK | wxICON_INFORMATION, this);
    return false;
}

std::string MainFrame::ManifestPath(const std::string& installDir)
{
    std::string out = installDir;
    if (!out.empty() && out[out.size()-1] != '\\' && out[out.size()-1] != '/')
        out += '\\';
    out += "manifest.json";
    return out;
}

bool MainFrame::SaveManifestJson(const std::string& installDir,
                                 const std::string& json)
{
    if (installDir.empty() || json.empty()) return false;
    FILE* f = fopen(ManifestPath(installDir).c_str(), "wb");
    if (!f) return false;
    size_t w = fwrite(json.data(), 1, json.size(), f);
    fclose(f);
    return w == json.size();
}

bool MainFrame::LoadManifest(const std::string& gameId,
                             const std::string& installDir,
                             GameManifest* out, std::string* errorOut)
{
    if (!installDir.empty())
    {
        FILE* f = fopen(ManifestPath(installDir).c_str(), "rb");
        if (f)
        {
            fseek(f, 0, SEEK_END);
            long sz = ftell(f);
            fseek(f, 0, SEEK_SET);
            std::string body;
            if (sz > 0)
            {
                body.resize((size_t)sz);
                size_t got = fread(&body[0], 1, (size_t)sz, f);
                if (got != (size_t)sz) body.clear();
            }
            fclose(f);
            if (!body.empty() && ParseManifestJson(body, out, NULL))
                return true;
        }
    }

    if (!m_online)
    {
        if (errorOut) *errorOut = "Manifest not cached and offline";
        return false;
    }

    std::string body;
    if (!m_games_api.FetchManifestJson(gameId, &body, errorOut)) return false;
    if (!installDir.empty())
        SaveManifestJson(installDir, body);
    return ParseManifestJson(body, out, errorOut);
}

void MainFrame::OnPreferences(wxCommandEvent& WXUNUSED(event))
{
    SettingsDialog dlg(this, &m_settings);
    if (dlg.ApplyIfAccepted())
    {
        m_settings.Save();
        if (dlg.AliasChanged() && !m_settings.alias.empty())
        {
            if (m_online)
            {
                std::string err;
                if (!m_profile_api.ChangeAlias(m_settings.alias, &err))
                    LogError("ChangeAlias failed: %s", err.c_str());
            }
            RunNameChangeForAllInstalled(m_settings.alias);
        }
    }
}

void MainFrame::OnToggleOnline(wxCommandEvent& WXUNUSED(event))
{
    if (m_online)
    {
        // Stay authenticated but stop using the network.
        SetOnline(false);
        return;
    }

    // Try to validate the cached token; fall back to refresh.
    if (m_settings.serverUrl.empty() || m_settings.accessToken.empty())
    {
        wxMessageBox(wxT("No saved server. Connect first."),
                     wxT("Go Online"), wxOK | wxICON_INFORMATION, this);
        return;
    }

    m_serverAddress = m_settings.serverUrl;
    m_http.SetBaseUrl(m_serverAddress);
    m_http.SetBearerToken(m_settings.accessToken);

    if (m_auth.Validate(NULL))
    {
        SetOnline(true);
        ImportCatalog();
        return;
    }

    AuthTokens in;
    in.accessToken  = m_settings.accessToken;
    in.refreshToken = m_settings.refreshToken;
    in.expiration   = m_settings.expiration;
    AuthTokens fresh;
    std::string err;
    if (m_auth.Refresh(in, &fresh, &err))
    {
        m_settings.accessToken  = fresh.accessToken;
        m_settings.refreshToken = fresh.refreshToken;
        m_settings.expiration   = fresh.expiration;
        m_settings.Save();
        SetOnline(true);
        ImportCatalog();
        return;
    }
    LogError("Go Online failed: %s", err.c_str());
    wxMessageBox(wxT("Could not reach the server. Stay offline?"),
                 wxT("Go Online"), wxOK | wxICON_WARNING, this);
}

MainFrame::~MainFrame()
{
    for (size_t i = 0; i < m_running.size(); ++i)
        if (m_running[i].process) CloseHandle((HANDLE)m_running[i].process);

    // Stop any in-flight install before tearing down the shared state.
    if (m_currentWorker && m_currentState)
    {
        m_currentState->cancelled = true;
        m_currentWorker->Wait();
        delete m_currentWorker;
        delete m_currentState;
        m_currentWorker = NULL;
        m_currentState  = NULL;
    }

    delete m_gameImages;
    m_gameImages = NULL;

    DiscoveryShutdown();
}

void MainFrame::OnExit(wxCommandEvent& WXUNUSED(event))
{
    Close(true);
}

void MainFrame::OnAbout(wxCommandEvent& WXUNUSED(event))
{
    wxMessageBox(wxT("LANCommander Launcher for Windows 9x"),
                 wxT("About"), wxOK | wxICON_INFORMATION);
}

void MainFrame::OnConnect(wxCommandEvent& WXUNUSED(event))
{
    wxString defServer = m_settings.serverUrl.empty()
        ? wxString(wxT("http://"))
        : wxString(m_settings.serverUrl.c_str(), wxConvUTF8);
    wxString server = wxGetTextFromUser(
        wxT("Server URL (e.g. http://lan.local:1337)"),
        wxT("Connect"), defServer, this);
    if (server.IsEmpty()) return;

    wxString defUser(m_settings.userName.c_str(), wxConvUTF8);
    wxString user = wxGetTextFromUser(wxT("Username"), wxT("Connect"),
                                      defUser, this);
    if (user.IsEmpty()) return;

    wxString pass = wxGetPasswordFromUser(wxT("Password"), wxT("Connect"),
                                          wxEmptyString, this);
    if (pass.IsEmpty()) return;

    m_serverAddress = std::string(server.mb_str());
    m_http.SetBaseUrl(m_serverAddress);

    AuthTokens tokens;
    std::string err;
    if (!m_auth.Login(std::string(user.mb_str()),
                      std::string(pass.mb_str()),
                      &tokens, &err))
    {
        LogError("Login failed for user %s on %s: %s",
                 (const char*)user.mb_str(),
                 m_serverAddress.c_str(), err.c_str());
        wxMessageBox(wxString(err.c_str(), wxConvUTF8),
                     wxT("Login failed"), wxOK | wxICON_ERROR);
        SetOnline(false);
        return;
    }

    m_settings.serverUrl    = m_serverAddress;
    m_settings.userName     = std::string(user.mb_str());
    m_settings.accessToken  = tokens.accessToken;
    m_settings.refreshToken = tokens.refreshToken;
    m_settings.expiration   = tokens.expiration;
    m_settings.Save();

    ServerBookmark bm;
    const ServerBookmark* existing = m_servers.Find(m_serverAddress);
    if (existing) bm = *existing;
    bm.url          = m_serverAddress;
    bm.userName     = m_settings.userName;
    bm.accessToken  = tokens.accessToken;
    bm.refreshToken = tokens.refreshToken;
    bm.expiration   = tokens.expiration;
    m_servers.Upsert(bm);
    m_servers.Save();

    SetOnline(true);
    ImportCatalog();
}

void MainFrame::OnFindServers(wxCommandEvent& WXUNUSED(event))
{
    std::vector<DiscoveredServer> servers;
    std::string err;

    {
        wxBusyInfo busy(wxT("Looking for servers..."), this);
        if (!DiscoverServers((unsigned int)m_settings.discoveryTimeoutMs,
                             &servers, &err))
        {
            LogError("DiscoverServers failed: %s", err.c_str());
            wxMessageBox(wxString(err.c_str(), wxConvUTF8),
                         wxT("Discovery failed"), wxOK | wxICON_ERROR);
            return;
        }
    }

    if (servers.empty())
    {
        wxMessageBox(wxT("No servers found on the local network."),
                     wxT("Find Servers"), wxOK | wxICON_INFORMATION);
        return;
    }

    wxArrayString labels;
    for (size_t i = 0; i < servers.size(); ++i)
    {
        wxString line;
        const DiscoveredServer& s = servers[i];
        wxString name(s.name.c_str(), wxConvUTF8);
        wxString addr(s.address.empty() ? s.remoteIp.c_str() : s.address.c_str(),
                      wxConvUTF8);
        if (!name.IsEmpty()) line << name << wxT(" - ");
        line << addr;
        if (!s.version.empty())
            line << wxT(" (v") << wxString(s.version.c_str(), wxConvUTF8) << wxT(")");
        labels.Add(line);
    }

    int pick = wxGetSingleChoiceIndex(wxT("Pick a server:"),
                                      wxT("Find Servers"), labels, this);
    if (pick < 0) return;

    const DiscoveredServer& chosen = servers[pick];
    std::string url = chosen.address;
    if (url.empty()) url = "http://" + chosen.remoteIp + ":1337";

    // Seed the Connect dialog with the discovered URL by pre-filling settings,
    // then invoke the same code path so the user only types user + password.
    m_settings.serverUrl = url;
    wxCommandEvent dummy;
    OnConnect(dummy);
}

void MainFrame::OnSwitchServer(wxCommandEvent& WXUNUSED(event))
{
    ServerPickerDialog dlg(this, &m_servers);
    int rc = dlg.ShowModal();

    if (rc == wxID_NEW)
    {
        // Fall through to the standard Connect flow.
        wxCommandEvent evt;
        OnConnect(evt);
        return;
    }
    if (rc != wxID_OK) return;

    int idx = dlg.PickedIndex();
    if (idx < 0 || (size_t)idx >= m_servers.Entries().size()) return;

    const ServerBookmark& b = m_servers.Entries()[idx];
    m_settings.serverUrl    = b.url;
    m_settings.userName     = b.userName;
    m_settings.accessToken  = b.accessToken;
    m_settings.refreshToken = b.refreshToken;
    m_settings.expiration   = b.expiration;
    m_settings.Save();

    m_serverAddress = b.url;
    m_http.SetBaseUrl(b.url);
    m_http.SetBearerToken(b.accessToken);

    if (b.accessToken.empty())
    {
        // Bookmark has no creds — fall back to the Connect dialog so the user
        // can authenticate.
        wxCommandEvent evt;
        OnConnect(evt);
        return;
    }

    if (TryAutoReconnect())
    {
        SetOnline(true);
        ImportCatalog();
    }
    else
    {
        // Stored creds are stale; let the user retry.
        wxMessageBox(wxT("Saved credentials are no longer valid. ")
                     wxT("Use Connect to log in again."),
                     wxT("Switch Server"), wxOK | wxICON_INFORMATION, this);
        SetOnline(false);
    }
}

void MainFrame::OnDisconnect(wxCommandEvent& WXUNUSED(event))
{
    if (!m_settings.accessToken.empty())
        m_auth.Logout();

    m_http.SetBearerToken(std::string());
    m_settings.accessToken.clear();
    m_settings.refreshToken.clear();
    m_settings.expiration.clear();
    m_settings.Save();
    ClearCover();
    ReloadFromDb();
    SetOnline(false);
}

void MainFrame::OnRefresh(wxCommandEvent& WXUNUSED(event))
{
    ImportCatalog();
}

namespace
{
    std::string Lower(const std::string& s)
    {
        std::string out = s;
        for (size_t i = 0; i < out.size(); ++i)
            if (out[i] >= 'A' && out[i] <= 'Z') out[i] = (char)(out[i] + 32);
        return out;
    }

    const std::string& SortKey(const GameSummary& g)
    {
        return g.sortTitle.empty() ? g.title : g.sortTitle;
    }
}

void MainFrame::ImportCatalog()
{
    if (!m_online)
    {
        ReloadFromDb();
        return;
    }

    std::vector<GameSummary> games;
    std::string err;
    if (!m_games_api.GetAll(&games, &err))
    {
        LogError("GetGames failed: %s", err.c_str());
        wxMessageBox(wxString(err.c_str(), wxConvUTF8),
                     wxT("Error"), wxOK | wxICON_ERROR);
        return;
    }

    std::string dbErr;
    if (!m_catalog.ReplaceCatalog(games, &dbErr))
    {
        LogError("Catalog import failed: %s", dbErr.c_str());
        // Even if persisting failed, show what we just fetched so the user
        // isn't stuck looking at stale data.
        m_games.swap(games);
        RefreshList();
        return;
    }

    ReloadFromDb();
}

void MainFrame::ReloadFromDb()
{
    if (!m_catalog.IsOpen()) return;
    std::vector<GameSummary> games;
    std::string err;
    if (!m_catalog.LoadAllGames(&games, &err))
    {
        LogError("Catalog load failed: %s", err.c_str());
        return;
    }
    m_games.swap(games);
    RefreshList();
}

void MainFrame::RefreshList()
{
    // Rebuild the genre dropdown from the current catalog, preserving the
    // user's selection if it's still present.
    {
        wxString prev = m_filterGenre->GetStringSelection();
        m_filterGenre->Freeze();
        m_filterGenre->Clear();
        m_filterGenre->Append(wxT("(All)"));

        std::set<std::string> uniq;
        for (size_t i = 0; i < m_games.size(); ++i)
        {
            const std::string& s = m_games[i].genres;
            size_t start = 0;
            while (start < s.size())
            {
                size_t end = s.find(',', start);
                if (end == std::string::npos) end = s.size();
                std::string tok = s.substr(start, end - start);
                // trim
                while (!tok.empty() && (tok[0] == ' ' || tok[0] == '\t')) tok.erase(0, 1);
                while (!tok.empty() && (tok[tok.size()-1] == ' ' ||
                                        tok[tok.size()-1] == '\t'))
                    tok.erase(tok.size() - 1);
                if (!tok.empty()) uniq.insert(tok);
                start = end + 1;
            }
        }
        for (std::set<std::string>::const_iterator it = uniq.begin();
             it != uniq.end(); ++it)
            m_filterGenre->Append(wxString(it->c_str(), wxConvUTF8));

        // Restore previous selection (prefer the persisted one on first build).
        wxString want = prev.IsEmpty()
            ? wxString(m_settings.filterGenre.c_str(), wxConvUTF8)
            : prev;
        int idx = want.IsEmpty() ? 0 : m_filterGenre->FindString(want);
        m_filterGenre->SetSelection(idx == wxNOT_FOUND ? 0 : idx);
        m_filterGenre->Thaw();
    }

    // Filter by search text + facet filters.
    std::string needle = Lower(std::string(m_search->GetValue().mb_str(wxConvUTF8)));
    bool libOnly  = m_filterLibrary->GetValue();
    bool instOnly = m_filterInstalled->GetValue();
    std::string genre;
    if (m_filterGenre->GetSelection() > 0)
        genre = std::string(m_filterGenre->GetStringSelection().mb_str(wxConvUTF8));

    std::vector<int> rows;
    rows.reserve(m_games.size());
    for (size_t i = 0; i < m_games.size(); ++i)
    {
        const GameSummary& g = m_games[i];
        if (!needle.empty() && Lower(g.title).find(needle) == std::string::npos)
            continue;
        if (libOnly && !g.inLibrary) continue;
        if (instOnly && m_installs.Get(g.id).empty()) continue;
        if (!genre.empty())
        {
            // Match as a token in the comma-joined string (case-sensitive
            // since dropdown values come from the same source).
            if (g.genres.find(genre) == std::string::npos) continue;
        }
        rows.push_back((int)i);
    }

    // Sort the surviving rows.
    int col   = m_sortColumn;
    bool desc = m_sortDescending;
    for (size_t i = 1; i < rows.size(); ++i)
    {
        int x = rows[i];
        size_t j = i;
        while (j > 0)
        {
            int a = rows[j-1];
            int b = x;
            bool aLessThanB;
            if (col == 1) // Library
            {
                bool al = m_games[a].inLibrary;
                bool bl = m_games[b].inLibrary;
                if (al == bl)
                    aLessThanB = Lower(SortKey(m_games[a])) < Lower(SortKey(m_games[b]));
                else
                    aLessThanB = al && !bl;
            }
            else if (col == 2) // Installed
            {
                bool ai = !m_installs.Get(m_games[a].id).empty();
                bool bi = !m_installs.Get(m_games[b].id).empty();
                if (ai == bi)
                    aLessThanB = Lower(SortKey(m_games[a])) < Lower(SortKey(m_games[b]));
                else
                    aLessThanB = ai && !bi;
            }
            else if (col == 3) // Year
            {
                if (m_games[a].releasedYear != m_games[b].releasedYear)
                    aLessThanB = m_games[a].releasedYear < m_games[b].releasedYear;
                else
                    aLessThanB = Lower(SortKey(m_games[a])) < Lower(SortKey(m_games[b]));
            }
            else
            {
                aLessThanB = Lower(SortKey(m_games[a])) < Lower(SortKey(m_games[b]));
            }
            bool aBeforeB = desc ? !aLessThanB : aLessThanB;
            if (aBeforeB) break;
            rows[j] = rows[j-1];
            --j;
        }
        rows[j] = x;
    }

    m_gameList->DeleteAllItems();
    // For Grid mode, drop the previously-loaded thumbnails (other than the
    // placeholder at index 0) and rebuild as items are inserted.
    if (m_viewMode == ViewGrid && m_gameImages)
    {
        while (m_gameImages->GetImageCount() > 1)
            m_gameImages->Remove(m_gameImages->GetImageCount() - 1);
    }

    for (size_t i = 0; i < rows.size(); ++i)
    {
        const GameSummary& g = m_games[rows[i]];
        wxString title(g.title.c_str(), wxConvUTF8);
        long item;

        if (m_viewMode == ViewGrid)
        {
            int imageIdx = 0; // placeholder
            if (!g.coverMediaId.empty())
            {
                std::string local = m_media.GetThumbnail(g.coverMediaId,
                                                         g.coverCrc32);
                if (!local.empty())
                {
                    wxImage img;
                    if (img.LoadFile(wxString(local.c_str(), wxConvUTF8),
                                     wxBITMAP_TYPE_ANY))
                    {
                        double sx = 96.0 / (double)img.GetWidth();
                        double sy = 144.0 / (double)img.GetHeight();
                        double s  = sx < sy ? sx : sy;
                        int w = (int)(img.GetWidth()  * s);
                        int h = (int)(img.GetHeight() * s);
                        if (w < 1) w = 1;
                        if (h < 1) h = 1;
                        img.Rescale(w, h, wxIMAGE_QUALITY_HIGH);
                        // Pad to 96x144 so wxImageList keeps a uniform cell size.
                        wxImage padded(96, 144);
                        padded.SetRGB(wxRect(0, 0, 96, 144), 32, 32, 32);
                        padded.Paste(img, (96 - w) / 2, (144 - h) / 2);
                        imageIdx = m_gameImages->Add(wxBitmap(padded));
                    }
                }
            }
            item = m_gameList->InsertItem((long)i, title, imageIdx);
        }
        else
        {
            item = m_gameList->InsertItem((long)i, title);
            if (m_viewMode == ViewList)
            {
                m_gameList->SetItem(item, 1, g.inLibrary ? wxT("Yes") : wxT(""));
                m_gameList->SetItem(item, 2,
                    m_installs.Get(g.id).empty() ? wxT("") : wxT("Yes"));
                if (g.releasedYear > 0)
                {
                    wxString y;
                    y.Printf(wxT("%d"), g.releasedYear);
                    m_gameList->SetItem(item, 3, y);
                }
            }
        }
        m_gameList->SetItemData(item, (long)rows[i]);
    }

    wxString status;
    if (m_games.empty())
        status = wxT("No games");
    else if (rows.size() == m_games.size())
        status.Printf(wxT("%lu games"), (unsigned long)m_games.size());
    else
        status.Printf(wxT("%lu / %lu games"),
                      (unsigned long)rows.size(),
                      (unsigned long)m_games.size());
    SetStatusText(status);
}

void MainFrame::BuildGameList(ViewMode mode)
{
    m_gameListHost->Freeze();

    if (m_gameList)
    {
        m_gameListHost->GetSizer()->Detach(m_gameList);
        m_gameList->Destroy();
        m_gameList = NULL;
    }
    if (m_gameImages)
    {
        delete m_gameImages;
        m_gameImages = NULL;
    }

    long style = wxLC_SINGLE_SEL;
    switch (mode)
    {
    case ViewList:  style |= wxLC_REPORT; break;
    case ViewGrid:  style |= wxLC_ICON;   break;
    case ViewShelf: style |= wxLC_LIST;   break;
    }

    m_gameList = new wxListCtrl(m_gameListHost, ID_GameList,
                                wxDefaultPosition, wxDefaultSize, style);

    if (mode == ViewList)
    {
        m_gameList->InsertColumn(0, wxT("Title"),     wxLIST_FORMAT_LEFT, 320);
        m_gameList->InsertColumn(1, wxT("Library"),   wxLIST_FORMAT_LEFT,  60);
        m_gameList->InsertColumn(2, wxT("Installed"), wxLIST_FORMAT_LEFT,  80);
        m_gameList->InsertColumn(3, wxT("Year"),      wxLIST_FORMAT_RIGHT, 60);
    }
    else if (mode == ViewGrid)
    {
        // 96x144 keeps a 2:3 box-art aspect; covers that don't match are
        // centered and padded onto a dark background so cells stay uniform.
        m_gameImages = new wxImageList(96, 144, true, 1);
        wxBitmap blank(96, 144);
        {
            wxMemoryDC dc(blank);
            dc.SetBackground(*wxLIGHT_GREY_BRUSH);
            dc.Clear();
        }
        m_gameImages->Add(blank);
        m_gameList->SetImageList(m_gameImages, wxIMAGE_LIST_NORMAL);
    }

    m_gameListHost->GetSizer()->Add(m_gameList, 1, wxEXPAND);
    m_gameListHost->Layout();
    m_gameListHost->Thaw();
}

void MainFrame::SetViewMode(ViewMode mode)
{
    if (mode == m_viewMode) return;
    m_viewMode = mode;
    m_settings.viewMode = (int)mode;
    m_settings.Save();
    BuildGameList(mode);
    RefreshList();
}

void MainFrame::OnViewList(wxCommandEvent& WXUNUSED(event))
{
    SetViewMode(ViewList);
}

void MainFrame::OnViewGrid(wxCommandEvent& WXUNUSED(event))
{
    SetViewMode(ViewGrid);
}

void MainFrame::OnViewShelf(wxCommandEvent& WXUNUSED(event))
{
    SetViewMode(ViewShelf);
}

void MainFrame::ClearCover()
{
    m_cover->SetBitmap(wxNullBitmap);
    m_coverLabel->SetLabel(wxEmptyString);
    m_detail->SetValue(wxEmptyString);
    m_btnPlay->Show();
    m_btnPlay->Disable();
    m_btnPlay->SetLabel(wxT("Play"));
    m_btnPlayWith->Hide();
    m_btnUpdate->Hide();
    m_btnSaveUp->Hide();
    m_btnSaveDown->Hide();
    m_btnUninstall->Hide();
    m_btnMedia->Hide();
    m_btnPrereqs->Hide();
    m_actionBar->Layout();
    m_selectionUpdateAvailable = false;
}

void MainFrame::ShowCover(const GameSummary& g)
{
    wxString headline(g.title.c_str(), wxConvUTF8);
    if (g.releasedYear > 0)
    {
        wxString y; y.Printf(wxT(" (%d)"), g.releasedYear);
        headline += y;
    }
    m_coverLabel->SetLabel(headline);

    wxString detail;
    std::string installedVersion = m_installs.GetVersion(g.id);
    std::string installDir       = m_installs.Get(g.id);
    if (!installDir.empty())
    {
        detail += wxT("Installed");
        if (!installedVersion.empty())
        {
            detail += wxT(": ");
            detail += wxString(installedVersion.c_str(), wxConvUTF8);
        }
        bool updateAvailable = false;
        if (m_online && !installedVersion.empty() &&
            m_games_api.CheckForUpdate(g.id, installedVersion, &updateAvailable, NULL) &&
            updateAvailable)
            detail += wxT("  [update available]");
        m_selectionUpdateAvailable = updateAvailable;
        detail += wxT("\n\n");
    }
    else
    {
        m_selectionUpdateAvailable = false;
    }

    if (!g.developers.empty())
    {
        detail += wxT("Developer: ");
        detail += wxString(g.developers.c_str(), wxConvUTF8);
        detail += wxT("\n");
    }
    if (!g.publishers.empty())
    {
        detail += wxT("Publisher: ");
        detail += wxString(g.publishers.c_str(), wxConvUTF8);
        detail += wxT("\n");
    }
    if (!g.genres.empty())
    {
        detail += wxT("Genres: ");
        detail += wxString(g.genres.c_str(), wxConvUTF8);
        detail += wxT("\n");
    }
    if (!g.description.empty())
    {
        if (!detail.IsEmpty()) detail += wxT("\n");
        detail += wxString(g.description.c_str(), wxConvUTF8);
    }
    m_detail->SetValue(detail);

    bool installed = !installDir.empty();
    m_btnPlay->Show();
    m_btnPlay->Enable(installed || m_online);
    m_btnPlay->SetLabel(installed ? wxT("Play") : wxT("Install"));
    m_btnPlayWith->Show(installed);
    m_btnUpdate->Show(installed && m_selectionUpdateAvailable && m_online);
    m_btnSaveUp->Show(installed);
    m_btnSaveDown->Show(installed);
    m_btnUninstall->Show(installed);
    m_btnMedia->Show(m_online);
    m_btnPrereqs->Show(m_online);
    m_actionBar->Layout();

    if (g.coverMediaId.empty())
    {
        m_cover->SetBitmap(wxNullBitmap);
        return;
    }

    std::string local = m_media.GetThumbnail(g.coverMediaId, g.coverCrc32);
    if (local.empty()) { m_cover->SetBitmap(wxNullBitmap); return; }

    wxImage img;
    if (!img.LoadFile(wxString(local.c_str(), wxConvUTF8), wxBITMAP_TYPE_ANY))
    {
        m_cover->SetBitmap(wxNullBitmap);
        return;
    }

    wxSize box = m_cover->GetSize();
    if (box.GetWidth() <= 0 || box.GetHeight() <= 0) box = wxSize(220, 300);

    double scale = (double)box.GetWidth()  / (double)img.GetWidth();
    double sy    = (double)box.GetHeight() / (double)img.GetHeight();
    if (sy < scale) scale = sy;
    int newW = (int)(img.GetWidth()  * scale);
    int newH = (int)(img.GetHeight() * scale);
    if (newW < 1) newW = 1;
    if (newH < 1) newH = 1;
    img.Rescale(newW, newH, wxIMAGE_QUALITY_HIGH);
    m_cover->SetBitmap(wxBitmap(img));
}

void MainFrame::OnGameSelected(wxListEvent& WXUNUSED(event))
{
    long sel = m_gameList->GetNextItem(-1, wxLIST_NEXT_ALL, wxLIST_STATE_SELECTED);
    if (sel == -1) { ClearCover(); return; }
    long gameIdx = m_gameList->GetItemData(sel);
    if (gameIdx < 0 || (size_t)gameIdx >= m_games.size())
    {
        ClearCover();
        return;
    }
    ShowCover(m_games[gameIdx]);
}

void MainFrame::OnColumnClick(wxListEvent& event)
{
    int col = event.GetColumn();
    if (col == m_sortColumn) m_sortDescending = !m_sortDescending;
    else                     { m_sortColumn = col; m_sortDescending = false; }
    RefreshList();
}

void MainFrame::OnSearchChanged(wxCommandEvent& WXUNUSED(event))
{
    RefreshList();
}

void MainFrame::OnActionPlay(wxCommandEvent& WXUNUSED(event))
{
    std::string gameId, title, installDir;
    if (!SelectedGame(&gameId, &title, &installDir)) return;
    if (installDir.empty()) InstallGame(gameId, title);
    else                    PlayGame(gameId, installDir);
}

void MainFrame::OnActionPlayWith(wxCommandEvent& evt)
{
    OnPlayWith(evt);
}

void MainFrame::OnActionUpdate(wxCommandEvent& WXUNUSED(event))
{
    std::string gameId, title, installDir;
    if (!SelectedGame(&gameId, &title, &installDir)) return;
    if (installDir.empty() || !m_online) return;
    UpdateGame(gameId, title, installDir);
}

void MainFrame::OnActionUninstall(wxCommandEvent& evt)
{
    OnUninstall(evt);
}

void MainFrame::OnActionSaveUp(wxCommandEvent& evt)
{
    OnUploadSave(evt);
}

void MainFrame::OnActionSaveDown(wxCommandEvent& evt)
{
    OnDownloadSave(evt);
}

void MainFrame::OnActionMedia(wxCommandEvent& WXUNUSED(event))
{
    if (!RequireOnline(wxT("Browsing media"))) return;
    std::string gameId, title, installDir;
    if (!SelectedGame(&gameId, &title, &installDir)) return;
    ScreenshotsDialog dlg(this, &m_media_api, &m_media, gameId,
                          wxString(title.c_str(), wxConvUTF8));
    dlg.ShowModal();
}

namespace
{
    struct PrereqDlState
    {
        wxProgressDialog* dlg;
        wxString          title;
        bool              cancelled;
    };

    bool PrereqDlCb(unsigned long received, unsigned long total, void* ud)
    {
        PrereqDlState* p = (PrereqDlState*)ud;
        int pos = 0;
        wxString msg;
        if (total > 0)
        {
            pos = (int)((double)received / (double)total * 1000.0);
            msg.Printf(wxT("Downloading %s: %lu / %lu KB"),
                       p->title.c_str(),
                       received / 1024UL, total / 1024UL);
        }
        else
        {
            msg.Printf(wxT("Downloading %s: %lu KB"),
                       p->title.c_str(), received / 1024UL);
        }
        if (!p->dlg->Update(pos, msg))
        {
            p->cancelled = true;
            return false;
        }
        return true;
    }

    // Pick a likely installer EXE inside `dir`. Preference order:
    //  1) "setup.exe" / "install.exe" (case-insensitive)
    //  2) the only .exe in the folder
    // Returns false if nothing obvious was found — caller falls back to
    // opening the folder in Explorer so the user can run it manually.
    bool FindInstaller(const std::string& dir, std::string* exePath)
    {
        std::string pattern = dir + "\\*.exe";
        WIN32_FIND_DATAA fd;
        HANDLE h = FindFirstFileA(pattern.c_str(), &fd);
        if (h == INVALID_HANDLE_VALUE) return false;
        std::string onlyExe;
        int exeCount = 0;
        std::string priority;
        do
        {
            if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) continue;
            std::string name = fd.cFileName;
            std::string lower = name;
            for (size_t i = 0; i < lower.size(); ++i)
                if (lower[i] >= 'A' && lower[i] <= 'Z') lower[i] += 32;
            ++exeCount;
            onlyExe = name;
            if (lower == "setup.exe" || lower == "install.exe")
            {
                priority = name;
                break;
            }
        } while (FindNextFileA(h, &fd));
        FindClose(h);

        if (!priority.empty())
        {
            *exePath = dir + "\\" + priority;
            return true;
        }
        if (exeCount == 1)
        {
            *exePath = dir + "\\" + onlyExe;
            return true;
        }
        return false;
    }
}

void MainFrame::OnActionPrereqs(wxCommandEvent& WXUNUSED(event))
{
    if (!RequireOnline(wxT("Installing prerequisites"))) return;
    std::string gameId, title, installDir;
    if (!SelectedGame(&gameId, &title, &installDir)) return;
    if (installDir.empty())
    {
        wxMessageBox(wxT("Install the game first; prerequisites unpack into "
                         "the game's install folder."),
                     wxT("Prerequisites"), wxOK | wxICON_INFORMATION);
        return;
    }

    std::vector<RedistributableSummary> redists;
    {
        wxBusyInfo busy(wxT("Loading prerequisites..."));
        std::string err;
        if (!m_games_api.GetRedistributables(gameId, &redists, &err))
        {
            LogError("GetRedistributables(%s) failed: %s",
                     gameId.c_str(), err.c_str());
            wxMessageBox(wxString(err.c_str(), wxConvUTF8),
                         wxT("Prerequisites"), wxOK | wxICON_ERROR);
            return;
        }
    }

    if (redists.empty())
    {
        wxMessageBox(wxT("This game has no prerequisites registered on the "
                         "server."),
                     wxT("Prerequisites"), wxOK | wxICON_INFORMATION);
        return;
    }

    wxArrayString names;
    for (size_t i = 0; i < redists.size(); ++i)
        names.Add(wxString(redists[i].name.c_str(), wxConvUTF8));
    wxArrayInt picks;
    wxGetSelectedChoices(picks,
        wxT("Select prerequisites to download and run. Each installer launches\n"
            "in turn; complete each one before the next begins."),
        wxT("Prerequisites for ") +
            wxString(title.c_str(), wxConvUTF8),
        names, this);
    if (picks.IsEmpty()) return;

    wxProgressDialog dlg(wxT("Installing prerequisites"),
                         wxT("Starting..."),
                         1000, this,
                         wxPD_APP_MODAL | wxPD_CAN_ABORT | wxPD_AUTO_HIDE);

    for (size_t pi = 0; pi < picks.GetCount(); ++pi)
    {
        const RedistributableSummary& r = redists[picks[pi]];

        std::string baseDir = installDir + "\\_lc_redist\\" + r.id;
        std::string zipPath = baseDir + ".zip";

        // Ensure parent dirs exist (_lc_redist + <id>). CreateDirectory is
        // a no-op if the dir already exists, so we don't bother checking.
        CreateDirectoryA((installDir + "\\_lc_redist").c_str(), NULL);
        CreateDirectoryA(baseDir.c_str(), NULL);

        PrereqDlState st;
        st.dlg       = &dlg;
        st.title     = wxString(r.name.c_str(), wxConvUTF8);
        st.cancelled = false;
        dlg.Update(0, wxT("Preparing ") + st.title);

        std::string err;
        if (!m_redist_api.Download(r.id, zipPath, PrereqDlCb, &st, &err))
        {
            if (st.cancelled) return;
            LogError("Redist download(%s) failed: %s", r.id.c_str(), err.c_str());
            wxMessageBox(wxString(err.c_str(), wxConvUTF8),
                         wxT("Prerequisites"), wxOK | wxICON_ERROR);
            continue;
        }

        dlg.Update(1000, wxT("Extracting ") + st.title + wxT("..."));
        std::string extractErr;
        bool ok = ExtractZip(zipPath, baseDir, NULL, NULL, &extractErr);
        ::remove(zipPath.c_str());
        if (!ok)
        {
            LogError("Redist extract(%s) failed: %s",
                     r.id.c_str(), extractErr.c_str());
            wxMessageBox(wxString(extractErr.c_str(), wxConvUTF8),
                         wxT("Prerequisites"), wxOK | wxICON_ERROR);
            continue;
        }

        // If the server ships a redist Install script, prefer that over the
        // setup.exe auto-detect heuristic — the script knows the right
        // invocation, args, and any post-install fixup steps.
        std::vector<Script> redistScripts;
        std::string redistScriptErr;
        if (m_scripts_api.GetRedistributableScripts(r.id, &redistScripts,
                                                    &redistScriptErr))
        {
            ScriptRunner::SaveAll(baseDir, r.id, redistScripts);
        }
        else
        {
            LogError("GetRedistributableScripts(%s) failed: %s",
                     r.id.c_str(), redistScriptErr.c_str());
        }
        if (ScriptRunner::Exists(baseDir, r.id, Script::TypeInstall))
        {
            std::map<std::string, std::string> vars;
            vars["InstallDirectory"] = baseDir;
            vars["GameInstallDirectory"] = installDir;
            vars["GameId"]               = gameId;
            vars["RedistributableId"]    = r.id;
            vars["RedistributableName"]  = r.name;
            vars["ServerAddress"]        = m_serverAddress;
            ScriptRunner::Run(baseDir, r.id, Script::TypeInstall, vars);
            continue;
        }

        std::string installerPath;
        if (FindInstaller(baseDir, &installerPath))
        {
            // Launch and wait so the user finishes one installer before
            // the next download starts.
            SHELLEXECUTEINFOA sei;
            ZeroMemory(&sei, sizeof(sei));
            sei.cbSize       = sizeof(sei);
            sei.fMask        = SEE_MASK_NOCLOSEPROCESS;
            sei.lpVerb       = "open";
            sei.lpFile       = installerPath.c_str();
            sei.lpDirectory  = baseDir.c_str();
            sei.nShow        = SW_SHOWNORMAL;
            if (ShellExecuteExA(&sei) && sei.hProcess)
            {
                WaitForSingleObject(sei.hProcess, INFINITE);
                CloseHandle(sei.hProcess);
            }
            else
            {
                LogError("ShellExecuteExA(%s) failed", installerPath.c_str());
            }
        }
        else
        {
            // Couldn't auto-detect — open the folder so the user can pick.
            ShellExecuteA(NULL, "open", baseDir.c_str(), NULL, NULL,
                          SW_SHOWNORMAL);
            wxMessageBox(wxT("Could not find an installer for ") + st.title +
                         wxT(". The folder has been opened — run the setup "
                             "manually, then dismiss this message."),
                         wxT("Prerequisites"), wxOK | wxICON_INFORMATION);
        }
    }

    SetStatusText(wxT("Prerequisites finished"));
}

void MainFrame::RunNameChangeForGame(const std::string& gameId,
                                     const std::string& installDir,
                                     const std::string& alias)
{
    if (alias.empty()) return;
    if (!ScriptRunner::Exists(installDir, gameId, Script::TypeNameChange))
        return;
    std::map<std::string, std::string> vars;
    vars["InstallDirectory"] = installDir;
    vars["GameId"]           = gameId;
    vars["NewName"]          = alias;
    vars["Alias"]            = alias; // SDK uses NewName; some authors expect Alias
    vars["ServerAddress"]    = m_serverAddress;
    ScriptRunner::Run(installDir, gameId, Script::TypeNameChange, vars);
}

void MainFrame::RunNameChangeForAllInstalled(const std::string& alias)
{
    if (alias.empty()) return;
    for (size_t i = 0; i < m_games.size(); ++i)
    {
        std::string dir = m_installs.Get(m_games[i].id);
        if (dir.empty()) continue;
        RunNameChangeForGame(m_games[i].id, dir, alias);
    }
}

void MainFrame::RunKeyChangeForGame(const std::string& gameId,
                                    const std::string& installDir)
{
    // Only allocate when the game actually has a KeyChange script — otherwise
    // we'd be burning server-side key inventory for no reason.
    if (!ScriptRunner::Exists(installDir, gameId, Script::TypeKeyChange))
        return;
    if (!m_online) return;
    std::string key;
    std::string err;
    if (!m_keys_api.Allocate(gameId, &key, &err) || key.empty())
    {
        LogError("Key allocate(%s) failed: %s", gameId.c_str(), err.c_str());
        return;
    }
    std::map<std::string, std::string> vars;
    vars["InstallDirectory"] = installDir;
    vars["GameId"]           = gameId;
    vars["AllocatedKey"]     = key;
    vars["Key"]              = key; // alias for script authors
    vars["ServerAddress"]    = m_serverAddress;
    ScriptRunner::Run(installDir, gameId, Script::TypeKeyChange, vars);
}

void MainFrame::OnChangeAlias(wxCommandEvent& WXUNUSED(event))
{
    wxString current(m_settings.alias.c_str(), wxConvUTF8);
    wxString next = wxGetTextFromUser(
        wxT("Player name (alias):"),
        wxT("Change Alias"), current, this);
    if (next.IsEmpty()) return;
    std::string newAlias(next.mb_str(wxConvUTF8));
    if (newAlias == m_settings.alias) return;
    m_settings.alias = newAlias;
    m_settings.Save();
    if (m_online)
    {
        std::string err;
        if (!m_profile_api.ChangeAlias(newAlias, &err))
            LogError("ChangeAlias failed: %s", err.c_str());
    }
    RunNameChangeForAllInstalled(newAlias);
    SetStatusText(wxT("Alias updated"));
}

void MainFrame::OnChangeKey(wxCommandEvent& WXUNUSED(event))
{
    if (!RequireOnline(wxT("Changing key"))) return;
    std::string gameId, title, installDir;
    if (!SelectedGame(&gameId, &title, &installDir)) return;
    if (installDir.empty())
    {
        wxMessageBox(wxT("Install the game first."),
                     wxT("Change Key"), wxOK | wxICON_INFORMATION);
        return;
    }
    if (!ScriptRunner::Exists(installDir, gameId, Script::TypeKeyChange))
    {
        wxMessageBox(wxT("This game has no ChangeKey script — there's nothing "
                         "to apply a new key to."),
                     wxT("Change Key"), wxOK | wxICON_INFORMATION);
        return;
    }
    RunKeyChangeForGame(gameId, installDir);
    SetStatusText(wxString(title.c_str(), wxConvUTF8) + wxT(": key rotated"));
}

void MainFrame::OnFilterChanged(wxCommandEvent& WXUNUSED(event))
{
    m_settings.showLibraryOnly   = m_filterLibrary->GetValue();
    m_settings.showInstalledOnly = m_filterInstalled->GetValue();
    if (m_filterGenre->GetSelection() > 0)
        m_settings.filterGenre = std::string(
            m_filterGenre->GetStringSelection().mb_str(wxConvUTF8));
    else
        m_settings.filterGenre.clear();
    m_settings.Save();
    RefreshList();
}

void MainFrame::OnGameActivated(wxListEvent& event)
{
    long gameIdx = event.GetData(); // we stuffed m_games index in SetItemData
    if (gameIdx < 0 || (size_t)gameIdx >= m_games.size()) return;
    const GameSummary& g = m_games[gameIdx];
    std::string installed = m_installs.Get(g.id);
    if (installed.empty())
    {
        InstallGame(g.id, g.title);
        return;
    }

    std::string version = m_installs.GetVersion(g.id);
    bool updateAvailable = false;
    if (m_online && !version.empty())
        m_games_api.CheckForUpdate(g.id, version, &updateAvailable, NULL);

    if (updateAvailable)
    {
        int rc = wxMessageBox(wxT("An update is available. Update now?\n")
                              wxT("Yes = update, No = play current version, Cancel = abort."),
                              wxT("Update available"),
                              wxYES_NO | wxCANCEL | wxICON_QUESTION, this);
        if (rc == wxCANCEL) return;
        if (rc == wxYES) { UpdateGame(g.id, g.title, installed); return; }
    }
    PlayGame(g.id, installed);
}

void MainFrame::UpdateGame(const std::string& gameId, const std::string& title,
                           const std::string& installDir)
{
    // Wipe the install dir contents in place so stale files from the old
    // version don't linger. The directory itself stays put.
    std::string buf = installDir;
    if (!buf.empty() && buf[buf.size()-1] != '\\' && buf[buf.size()-1] != '/')
        buf += '\\';
    buf += '*';
    buf.push_back('\0');
    buf.push_back('\0');

    SHFILEOPSTRUCTA op;
    memset(&op, 0, sizeof(op));
    op.wFunc  = FO_DELETE;
    op.pFrom  = buf.c_str();
    op.fFlags = FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT;
    SHFileOperationA(&op);

    EnqueueInstall(gameId, title, installDir);
}

void MainFrame::PlayGame(const std::string& gameId, const std::string& installDir)
{
    GameManifest manifest;
    std::string err;
    if (!LoadManifest(gameId, installDir, &manifest, &err))
    {
        LogError("LoadManifest %s failed: %s", gameId.c_str(), err.c_str());
        wxMessageBox(wxString(err.c_str(), wxConvUTF8),
                     wxT("Error"), wxOK | wxICON_ERROR);
        return;
    }

    const ManifestAction* action = PickPrimaryAction(manifest);
    if (!action)
    {
        LogError("No runnable action in manifest for game %s", gameId.c_str());
        wxMessageBox(wxT("No runnable action in manifest"),
                     wxT("Error"), wxOK | wxICON_ERROR);
        return;
    }
    LaunchPicked(gameId, installDir, manifest, *action);
}

void MainFrame::LaunchPicked(const std::string& gameId,
                             const std::string& installDir,
                             const GameManifest& manifest,
                             const ManifestAction& action)
{
    if (ScriptRunner::Exists(installDir, gameId, Script::TypeBeforeStart))
    {
        std::map<std::string, std::string> vars;
        vars["InstallDirectory"] = installDir;
        vars["GameId"]           = gameId;
        vars["GameTitle"]        = manifest.title;
        vars["GameVersion"]      = manifest.version;
        vars["ServerAddress"]    = m_serverAddress;
        ScriptRunner::Run(installDir, gameId, Script::TypeBeforeStart, vars);
    }

    void* hProcess = NULL;
    std::string err;
    if (!LaunchAction(action, installDir, m_serverAddress, &hProcess, &err))
    {
        LogError("Launch failed for game %s (action '%s', path '%s'): %s",
                 gameId.c_str(), action.name.c_str(),
                 action.path.c_str(), err.c_str());
        wxMessageBox(wxString(err.c_str(), wxConvUTF8),
                     wxT("Launch failed"), wxOK | wxICON_ERROR);
        return;
    }

    m_games_api.NotifyStarted(gameId);

    if (hProcess)
    {
        RunningGame rg;
        rg.gameId  = gameId;
        rg.process = hProcess;
        m_running.push_back(rg);
        if (!m_sessionTimer.IsRunning())
            m_sessionTimer.Start(1000);
    }

    SetStatusText(wxString(manifest.title.c_str(), wxConvUTF8) + wxT(" launched"));
}

void MainFrame::OnPlayWith(wxCommandEvent& WXUNUSED(event))
{
    std::string gameId, title, installDir;
    if (!SelectedGame(&gameId, &title, &installDir))
    {
        wxMessageBox(wxT("Select a game first."),
                     wxT("Play with..."), wxOK | wxICON_INFORMATION);
        return;
    }
    if (installDir.empty())
    {
        wxMessageBox(wxT("Game isn't installed."),
                     wxT("Play with..."), wxOK | wxICON_INFORMATION);
        return;
    }

    GameManifest manifest;
    std::string err;
    if (!LoadManifest(gameId, installDir, &manifest, &err))
    {
        LogError("LoadManifest %s failed: %s", gameId.c_str(), err.c_str());
        wxMessageBox(wxString(err.c_str(), wxConvUTF8),
                     wxT("Error"), wxOK | wxICON_ERROR);
        return;
    }
    if (manifest.actions.empty())
    {
        wxMessageBox(wxT("This game has no actions."),
                     wxT("Play with..."), wxOK | wxICON_INFORMATION);
        return;
    }

    if (manifest.actions.size() == 1)
    {
        LaunchPicked(gameId, installDir, manifest, manifest.actions[0]);
        return;
    }

    // Stable sort by sortOrder so the dialog matches the server's intended
    // ordering; selection-sort is fine for the typical handful of actions.
    std::vector<size_t> order(manifest.actions.size());
    for (size_t i = 0; i < order.size(); ++i) order[i] = i;
    for (size_t i = 1; i < order.size(); ++i)
    {
        size_t x = order[i];
        size_t j = i;
        while (j > 0 &&
               manifest.actions[order[j-1]].sortOrder > manifest.actions[x].sortOrder)
        {
            order[j] = order[j-1];
            --j;
        }
        order[j] = x;
    }

    wxArrayString labels;
    for (size_t i = 0; i < order.size(); ++i)
    {
        const ManifestAction& a = manifest.actions[order[i]];
        wxString line = a.name.empty()
            ? wxString::Format(wxT("Action %d"), (int)i + 1)
            : wxString(a.name.c_str(), wxConvUTF8);
        if (a.isPrimary) line += wxT(" (default)");
        labels.Add(line);
    }

    int pick = wxGetSingleChoiceIndex(wxT("Pick an action:"),
                                      wxT("Play with..."), labels, this);
    if (pick < 0) return;

    LaunchPicked(gameId, installDir, manifest, manifest.actions[order[pick]]);
}

bool MainFrame::SelectedGame(std::string* gameId, std::string* title,
                             std::string* installDir)
{
    long sel = m_gameList->GetNextItem(-1, wxLIST_NEXT_ALL, wxLIST_STATE_SELECTED);
    if (sel == -1) return false;
    long gameIdx = m_gameList->GetItemData(sel);
    if (gameIdx < 0 || (size_t)gameIdx >= m_games.size()) return false;
    const GameSummary& g = m_games[gameIdx];
    *gameId = g.id;
    if (title)      *title      = g.title;
    if (installDir) *installDir = m_installs.Get(g.id);
    return true;
}

static std::string MakeTempZipPath(const char* prefix)
{
    char dir[MAX_PATH];
    DWORD n = GetTempPathA(MAX_PATH, dir);
    if (n == 0) { dir[0] = '.'; dir[1] = 0; }
    char path[MAX_PATH];
    if (!GetTempFileNameA(dir, prefix, 0, path))
        return std::string();
    std::string out(path);
    // Replace .tmp suffix with .lcs so any logs read sensibly.
    if (out.size() >= 4 &&
        (out[out.size()-4] == '.' || out[out.size()-3] == '.'))
        out = out.substr(0, out.find_last_of('.')) + ".lcs";
    return out;
}

namespace
{
    wxString FormatSavePhase(int phase, unsigned long received, unsigned long total)
    {
        switch (phase)
        {
            case SaveJobState::PhaseGettingManifest: return wxT("Fetching manifest...");
            case SaveJobState::PhasePacking:         return wxT("Packing save files...");
            case SaveJobState::PhaseUploading:       return wxT("Uploading...");
            case SaveJobState::PhaseUnpacking:       return wxT("Restoring save files...");
            case SaveJobState::PhaseDone:            return wxT("Done");
            case SaveJobState::PhaseDownloading:
            {
                wxString s;
                if (total > 0)
                    s.Printf(wxT("Downloaded %lu / %lu KB"),
                             received / 1024UL, total / 1024UL);
                else
                    s.Printf(wxT("Downloaded %lu KB"), received / 1024UL);
                return s;
            }
        }
        return wxT("Working...");
    }

    int SavePhaseProgress(int phase, unsigned long received, unsigned long total)
    {
        // 0..1000 to match wxProgressDialog range used elsewhere.
        if (phase == SaveJobState::PhaseDownloading && total > 0)
            return (int)((double)received / (double)total * 1000.0);
        return 0;
    }

    // Runs a SaveSyncWorker to completion under a progress dialog. Returns
    // when the worker is done. Caller examines state.succeeded / state.error.
    void RunSaveWorker(wxWindow* parent,
                       GameClient& games, SaveClient& saves,
                       SaveSyncWorker::Mode mode,
                       const std::string& gameId,
                       const std::string& installDir,
                       const std::string& zipPath,
                       const wxString& dialogTitle,
                       SaveJobState* stateOut)
    {
        wxProgressDialog dlg(dialogTitle, wxT("Starting..."), 1000, parent,
                             wxPD_APP_MODAL | wxPD_CAN_ABORT | wxPD_AUTO_HIDE);

        SaveSyncWorker* worker = new SaveSyncWorker(mode, &games, &saves,
                                                    gameId, installDir,
                                                    zipPath, stateOut);
        if (worker->Create() != wxTHREAD_NO_ERROR ||
            worker->Run()    != wxTHREAD_NO_ERROR)
        {
            stateOut->error     = "Could not start save thread";
            stateOut->succeeded = false;
            stateOut->finished  = true;
            delete worker;
            return;
        }

        while (!stateOut->finished)
        {
            int p = stateOut->phase;
            unsigned long r = stateOut->received;
            unsigned long t = stateOut->total;
            if (!dlg.Update(SavePhaseProgress(p, r, t), FormatSavePhase(p, r, t)))
                stateOut->cancelled = true;
            wxMilliSleep(80);
        }
        worker->Wait();
        delete worker;
    }
}

void MainFrame::OnUploadSave(wxCommandEvent& WXUNUSED(event))
{
    if (!RequireOnline(wxT("Uploading a save"))) return;
    std::string gameId, title, installDir;
    if (!SelectedGame(&gameId, &title, &installDir))
    {
        wxMessageBox(wxT("Select a game first."),
                     wxT("Upload Save"), wxOK | wxICON_INFORMATION);
        return;
    }
    if (installDir.empty())
    {
        wxMessageBox(wxT("Game isn't installed."),
                     wxT("Upload Save"), wxOK | wxICON_INFORMATION);
        return;
    }

    std::string zipPath = MakeTempZipPath("lcs");
    if (zipPath.empty())
    {
        LogError("Could not allocate temp file for save upload");
        wxMessageBox(wxT("Could not allocate temp file"),
                     wxT("Error"), wxOK | wxICON_ERROR);
        return;
    }

    SaveJobState state;
    RunSaveWorker(this, m_games_api, m_save_api, SaveSyncWorker::Upload,
                  gameId, installDir, zipPath,
                  wxT("Uploading save"), &state);

    if (state.empty)
    {
        wxMessageBox(wxT("Nothing to upload."),
                     wxT("Upload Save"), wxOK | wxICON_INFORMATION);
    }
    else if (!state.succeeded)
    {
        LogError("Save upload for %s failed (phase %d): %s",
                 gameId.c_str(), state.phase, state.error.c_str());
        wxMessageBox(wxString(state.error.c_str(), wxConvUTF8),
                     wxT("Upload failed"), wxOK | wxICON_ERROR);
    }
    else
    {
        SetStatusText(wxT("Save uploaded"));
    }

    ::remove(zipPath.c_str());
}

void MainFrame::OnDownloadSave(wxCommandEvent& WXUNUSED(event))
{
    if (!RequireOnline(wxT("Downloading a save"))) return;
    std::string gameId, title, installDir;
    if (!SelectedGame(&gameId, &title, &installDir))
    {
        wxMessageBox(wxT("Select a game first."),
                     wxT("Download Save"), wxOK | wxICON_INFORMATION);
        return;
    }
    if (installDir.empty())
    {
        wxMessageBox(wxT("Game isn't installed."),
                     wxT("Download Save"), wxOK | wxICON_INFORMATION);
        return;
    }

    std::string zipPath = MakeTempZipPath("lcs");
    if (zipPath.empty())
    {
        LogError("Could not allocate temp file for save download");
        wxMessageBox(wxT("Could not allocate temp file"),
                     wxT("Error"), wxOK | wxICON_ERROR);
        return;
    }

    SaveJobState state;
    RunSaveWorker(this, m_games_api, m_save_api, SaveSyncWorker::Download,
                  gameId, installDir, zipPath,
                  wxT("Downloading save"), &state);

    if (!state.succeeded)
    {
        LogError("Save download for %s failed (phase %d): %s",
                 gameId.c_str(), state.phase, state.error.c_str());
        wxMessageBox(wxString(state.error.c_str(), wxConvUTF8),
                     wxT("Download failed"), wxOK | wxICON_ERROR);
    }
    else
    {
        SetStatusText(wxT("Save restored"));
    }

    ::remove(zipPath.c_str());
}

void MainFrame::OnAddToLibrary(wxCommandEvent& WXUNUSED(event))
{
    if (!RequireOnline(wxT("Add to library"))) return;
    std::string gameId, title, installDir;
    if (!SelectedGame(&gameId, &title, &installDir))
    {
        wxMessageBox(wxT("Select a game first."),
                     wxT("Add to Library"), wxOK | wxICON_INFORMATION);
        return;
    }
    std::string err;
    if (!m_library_api.AddToLibrary(gameId, &err))
    {
        LogError("AddToLibrary %s failed: %s", gameId.c_str(), err.c_str());
        wxMessageBox(wxString(err.c_str(), wxConvUTF8),
                     wxT("Add to Library"), wxOK | wxICON_ERROR);
        return;
    }
    for (size_t i = 0; i < m_games.size(); ++i)
        if (m_games[i].id == gameId) { m_games[i].inLibrary = true; break; }
    m_catalog.SetInLibrary(gameId, true, NULL);
    RefreshList();
    SetStatusText(wxString(title.c_str(), wxConvUTF8) + wxT(" added to library"));
}

void MainFrame::OnRemoveFromLibrary(wxCommandEvent& WXUNUSED(event))
{
    if (!RequireOnline(wxT("Remove from library"))) return;
    std::string gameId, title, installDir;
    if (!SelectedGame(&gameId, &title, &installDir))
    {
        wxMessageBox(wxT("Select a game first."),
                     wxT("Remove from Library"), wxOK | wxICON_INFORMATION);
        return;
    }
    std::string err;
    if (!m_library_api.RemoveFromLibrary(gameId, &err))
    {
        LogError("RemoveFromLibrary %s failed: %s", gameId.c_str(), err.c_str());
        wxMessageBox(wxString(err.c_str(), wxConvUTF8),
                     wxT("Remove from Library"), wxOK | wxICON_ERROR);
        return;
    }
    for (size_t i = 0; i < m_games.size(); ++i)
        if (m_games[i].id == gameId) { m_games[i].inLibrary = false; break; }
    m_catalog.SetInLibrary(gameId, false, NULL);
    RefreshList();
    SetStatusText(wxString(title.c_str(), wxConvUTF8) + wxT(" removed from library"));
}

void MainFrame::OnUninstall(wxCommandEvent& WXUNUSED(event))
{
    std::string gameId, title, installDir;
    if (!SelectedGame(&gameId, &title, &installDir))
    {
        wxMessageBox(wxT("Select a game first."),
                     wxT("Uninstall"), wxOK | wxICON_INFORMATION);
        return;
    }
    if (installDir.empty())
    {
        wxMessageBox(wxT("Game isn't installed."),
                     wxT("Uninstall"), wxOK | wxICON_INFORMATION);
        return;
    }

    wxString msg;
    msg.Printf(wxT("Delete %s and all its files at\n%s?"),
               wxString(title.c_str(), wxConvUTF8).c_str(),
               wxString(installDir.c_str(), wxConvUTF8).c_str());
    if (wxMessageBox(msg, wxT("Uninstall"),
                     wxYES_NO | wxICON_QUESTION, this) != wxYES)
        return;

    // Run the Uninstall hook while the script still exists on disk.
    if (ScriptRunner::Exists(installDir, gameId, Script::TypeUninstall))
    {
        std::map<std::string, std::string> vars;
        vars["InstallDirectory"] = installDir;
        vars["GameId"]           = gameId;
        vars["GameTitle"]        = title;
        vars["ServerAddress"]    = m_serverAddress;
        ScriptRunner::Run(installDir, gameId, Script::TypeUninstall, vars);
    }

    // SHFileOperation needs a double-null-terminated source path.
    std::string buf = installDir;
    buf.push_back('\0');
    buf.push_back('\0');

    SHFILEOPSTRUCTA op;
    memset(&op, 0, sizeof(op));
    op.wFunc  = FO_DELETE;
    op.pFrom  = buf.c_str();
    op.fFlags = FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT;

    int rc = SHFileOperationA(&op);
    if (rc != 0 || op.fAnyOperationsAborted)
    {
        wxMessageBox(wxT("Could not delete the install directory. ")
                     wxT("Removing it from the launcher anyway."),
                     wxT("Uninstall"), wxOK | wxICON_WARNING);
    }

    m_installs.Remove(gameId);
    m_installs.Save();
    RefreshList();
    SetStatusText(wxString(title.c_str(), wxConvUTF8) + wxT(" uninstalled"));
}

void MainFrame::OnSessionPoll(wxTimerEvent& WXUNUSED(event))
{
    for (size_t i = 0; i < m_running.size(); )
    {
        DWORD code = 0;
        HANDLE h = (HANDLE)m_running[i].process;
        if (GetExitCodeProcess(h, &code) && code != STILL_ACTIVE)
        {
            std::string id = m_running[i].gameId;
            CloseHandle(h);
            m_running.erase(m_running.begin() + i);
            m_games_api.NotifyStopped(id);

            std::string installDir = m_installs.Get(id);
            if (!installDir.empty() &&
                ScriptRunner::Exists(installDir, id, Script::TypeAfterStop))
            {
                std::map<std::string, std::string> vars;
                vars["InstallDirectory"] = installDir;
                vars["GameId"]           = id;
                vars["ServerAddress"]    = m_serverAddress;
                ScriptRunner::Run(installDir, id, Script::TypeAfterStop, vars);
            }
        }
        else
        {
            ++i;
        }
    }
    if (m_running.empty())
        m_sessionTimer.Stop();
}

void MainFrame::InstallGame(const std::string& gameId, const std::string& title)
{
    if (!RequireOnline(wxT("Installing"))) return;

    // Best-effort: fetch the list of addons so the user can pick which to
    // bundle. Failure is non-fatal (older servers, restricted users, etc.).
    std::vector<GameSummary> addons;
    std::string addonsErr;
    if (!m_games_api.GetAddons(gameId, &addons, &addonsErr))
        LogError("GetAddons for %s failed: %s", gameId.c_str(), addonsErr.c_str());

    InstallOptionsDialog dlg(this,
                             wxString(title.c_str(), wxConvUTF8),
                             addons,
                             wxString(m_settings.defaultInstallDir.c_str(),
                                      wxConvUTF8));
    if (dlg.ShowModal() != wxID_OK) return;

    std::string destDir = dlg.GetDestDir();
    if (destDir.empty())
    {
        wxMessageBox(wxT("Pick an install directory."),
                     wxT("Install"), wxOK | wxICON_INFORMATION, this);
        return;
    }

    EnqueueInstall(gameId, title, destDir);

    // Install picked addons into the same root. Each lands as its own queue
    // item so the user sees them progress one-by-one through the queue strip.
    std::vector<GameSummary> picks = dlg.GetSelectedAddons();
    for (size_t i = 0; i < picks.size(); ++i)
        EnqueueInstall(picks[i].id, picks[i].title, destDir);
}

int MainFrame::FindQueueItemById(const std::string& gameId) const
{
    for (size_t i = 0; i < m_queue.size(); ++i)
        if (m_queue[i].gameId == gameId)
            return (int)i;
    return -1;
}

wxString MainFrame::QueueStatusText(const InstallQueueItem& item) const
{
    switch (item.state)
    {
    case InstallQueueItem::Queued:
        return wxT("Queued");

    case InstallQueueItem::Cancelled:
        return wxT("Cancelled");

    case InstallQueueItem::Failed:
    {
        wxString s = wxT("Failed");
        if (!item.error.empty())
        {
            s += wxT(": ");
            s += wxString(item.error.c_str(), wxConvUTF8);
        }
        return s;
    }

    case InstallQueueItem::Running:
        if (!m_currentState) return wxT("Starting...");

        if (m_currentState->phase == InstallJobState::PhaseDownload)
        {
            unsigned long r = m_currentState->received;
            unsigned long t = m_currentState->total;
            wxString s;
            if (t > 0)
            {
                int pct = (int)((double)r / (double)t * 100.0);
                s.Printf(wxT("Downloading %d%% (%lu / %lu KB)"),
                         pct, r / 1024UL, t / 1024UL);
            }
            else
            {
                s.Printf(wxT("Downloading %lu KB"), r / 1024UL);
            }
            return s;
        }
        else
        {
            unsigned long i = m_currentState->extractIdx;
            unsigned long c = m_currentState->extractCount;
            wxString s;
            if (c > 0)
            {
                int pct = (int)((double)i / (double)c * 100.0);
                s.Printf(wxT("Extracting %d%% (%lu / %lu)"), pct, i, c);
            }
            else
            {
                s = wxT("Extracting...");
            }
            return s;
        }
    }
    return wxEmptyString;
}

void MainFrame::EnqueueInstall(const std::string& gameId,
                               const std::string& title,
                               const std::string& destDir)
{
    // If this game is already in the queue and finished (Failed/Cancelled),
    // re-queue it in place instead of stacking duplicate entries.
    int existing = FindQueueItemById(gameId);
    if (existing >= 0 && existing != m_runningIdx &&
        (m_queue[existing].state == InstallQueueItem::Failed ||
         m_queue[existing].state == InstallQueueItem::Cancelled))
    {
        m_queue[existing].state   = InstallQueueItem::Queued;
        m_queue[existing].error.clear();
        m_queue[existing].destDir = destDir;
    }
    else
    {
        InstallQueueItem item;
        item.gameId  = gameId;
        item.title   = title;
        item.destDir = destDir;
        item.state   = InstallQueueItem::Queued;
        m_queue.push_back(item);
    }

    if (!m_currentWorker)
        StartNextQueueItem();
    else
        UpdateQueuePanel();
}

void MainFrame::StartNextQueueItem()
{
    if (m_currentWorker) return;

    // Find the first Queued item.
    int next = -1;
    for (size_t i = 0; i < m_queue.size(); ++i)
    {
        if (m_queue[i].state == InstallQueueItem::Queued)
        {
            next = (int)i;
            break;
        }
    }
    if (next < 0)
    {
        m_runningIdx = -1;
        UpdateQueuePanel();
        return;
    }

    m_runningIdx = next;
    InstallQueueItem& item = m_queue[m_runningIdx];
    item.state = InstallQueueItem::Running;

    wxFileName zipName(wxString(item.destDir.c_str(), wxConvUTF8),
                       wxString(item.title.c_str(), wxConvUTF8) + wxT(".zip"));
    m_currentZipPath = std::string(zipName.GetFullPath().mb_str());

    m_currentState  = new InstallJobState();
    m_currentWorker = new InstallWorker(&m_games_api,
                                        item.gameId,
                                        m_currentZipPath,
                                        item.destDir,
                                        m_currentState);
    if (m_currentWorker->Create() != wxTHREAD_NO_ERROR ||
        m_currentWorker->Run()    != wxTHREAD_NO_ERROR)
    {
        LogError("Could not start install thread for %s", item.gameId.c_str());
        delete m_currentWorker; m_currentWorker = NULL;
        delete m_currentState;  m_currentState  = NULL;
        item.state = InstallQueueItem::Failed;
        item.error = "Could not start install thread";
        m_runningIdx = -1;
        StartNextQueueItem(); // skip this one
        return;
    }

    if (!m_queueTimer.IsRunning()) m_queueTimer.Start(200);
    UpdateQueuePanel();
}

void MainFrame::FinishCurrentItem(bool success)
{
    if (!m_currentWorker || !m_currentState) return;
    if (m_runningIdx < 0) return;

    InstallJobState* state  = m_currentState;
    InstallWorker*   worker = m_currentWorker;
    m_currentState  = NULL;
    m_currentWorker = NULL;

    worker->Wait();
    delete worker;

    ::remove(m_currentZipPath.c_str());

    // Take a copy of identifiers before we mutate the queue.
    InstallQueueItem& slot = m_queue[m_runningIdx];
    std::string gameId  = slot.gameId;
    std::string title   = slot.title;
    std::string destDir = slot.destDir;

    if (!success)
    {
        if (state->cancelled)
        {
            slot.state = InstallQueueItem::Cancelled;
            slot.error.clear();
        }
        else
        {
            slot.state = InstallQueueItem::Failed;
            slot.error = state->error;
            LogError("Install of game %s failed: %s",
                     gameId.c_str(), state->error.c_str());
        }
        m_runningIdx = -1;
        delete state;
        StartNextQueueItem();
        return;
    }

    // Success: drop the row, then continue. We don't keep "Done" entries —
    // the catalog refresh shows the game as installed.
    m_queue.erase(m_queue.begin() + m_runningIdx);
    m_runningIdx = -1;
    delete state;

    // Best-effort: cache manifest.json next to the install + pick up version.
    std::string manifestJson;
    std::string manifestErr;
    std::string version;
    if (m_games_api.FetchManifestJson(gameId, &manifestJson, &manifestErr))
    {
        SaveManifestJson(destDir, manifestJson);
        GameManifest manifest;
        if (ParseManifestJson(manifestJson, &manifest, NULL))
            version = manifest.version;
    }
    else
    {
        LogError("Post-install FetchManifestJson %s failed: %s",
                 gameId.c_str(), manifestErr.c_str());
    }

    if (version.empty())
        m_installs.Set(gameId, destDir);
    else
        m_installs.Set(gameId, destDir, version);
    m_installs.Save();

    // Best-effort: fetch + persist any server-side scripts, then fire the
    // Install hook. A failed Get or Run is logged but doesn't fail the install.
    std::vector<Script> scripts;
    std::string scriptErr;
    if (m_scripts_api.GetGameScripts(gameId, &scripts, &scriptErr))
    {
        if (!scripts.empty())
            ScriptRunner::SaveAll(destDir, gameId, scripts);
    }
    else
    {
        LogError("Post-install GetGameScripts(%s) failed: %s",
                 gameId.c_str(), scriptErr.c_str());
    }
    if (ScriptRunner::Exists(destDir, gameId, Script::TypeInstall))
    {
        std::map<std::string, std::string> vars;
        vars["InstallDirectory"] = destDir;
        vars["GameId"]           = gameId;
        vars["GameTitle"]        = title;
        vars["ServerAddress"]    = m_serverAddress;
        ScriptRunner::Run(destDir, gameId, Script::TypeInstall, vars);
    }

    // NameChange / KeyChange come right after Install — same ordering as the
    // SDK's post-install flow.
    if (!m_settings.alias.empty())
        RunNameChangeForGame(gameId, destDir, m_settings.alias);
    RunKeyChangeForGame(gameId, destDir);

    RefreshList();
    SetStatusText(wxString(title.c_str(), wxConvUTF8) + wxT(" installed"));

    StartNextQueueItem();
}

void MainFrame::UpdateQueuePanel()
{
    if (m_queue.empty())
    {
        m_queueTimer.Stop();
        if (m_queuePanel->IsShown())
        {
            m_queuePanel->Hide();
            m_queuePanel->GetParent()->Layout();
        }
        return;
    }

    if (!m_queuePanel->IsShown())
    {
        m_queuePanel->Show();
        m_queuePanel->GetParent()->Layout();
    }

    // Rebuild list rows from scratch — the queue is small (rarely more than
    // a handful of items) so the cost is negligible and the code stays simple.
    m_queueList->DeleteAllItems();
    int finishedCount = 0;
    int runningCount  = 0;
    int queuedCount   = 0;
    for (size_t i = 0; i < m_queue.size(); ++i)
    {
        const InstallQueueItem& it = m_queue[i];
        long row = m_queueList->InsertItem((long)i,
                       wxString(it.title.c_str(), wxConvUTF8));
        m_queueList->SetItem(row, 1, QueueStatusText(it));
        m_queueList->SetItemData(row, (long)i);

        switch (it.state)
        {
        case InstallQueueItem::Queued:    ++queuedCount;   break;
        case InstallQueueItem::Running:   ++runningCount;  break;
        case InstallQueueItem::Failed:    ++finishedCount; break;
        case InstallQueueItem::Cancelled: ++finishedCount; break;
        }
    }

    wxString summary;
    if (runningCount > 0)
    {
        summary.Printf(wxT("Installing — %d queued, %d finished"),
                       queuedCount, finishedCount);
    }
    else if (queuedCount > 0)
    {
        summary.Printf(wxT("%d queued, %d finished"),
                       queuedCount, finishedCount);
    }
    else
    {
        summary.Printf(wxT("%d finished"), finishedCount);
    }
    m_queueSummary->SetLabel(summary);
    m_queueClearBtn->Enable(finishedCount > 0);
}

void MainFrame::OnQueueTick(wxTimerEvent& WXUNUSED(event))
{
    if (!m_currentState || !m_currentWorker)
    {
        m_queueTimer.Stop();
        return;
    }
    if (m_currentState->finished)
    {
        FinishCurrentItem(m_currentState->succeeded);
        return;
    }

    // Only refresh the running row's status cell to avoid flicker from a
    // full rebuild; the rest of the queue is static between ticks.
    if (m_runningIdx >= 0)
    {
        long row = m_queueList->FindItem(-1, (long)m_runningIdx);
        if (row >= 0)
            m_queueList->SetItem(row, 1, QueueStatusText(m_queue[m_runningIdx]));
    }
}

void MainFrame::OnQueueItemRightClick(wxListEvent& event)
{
    long row = event.GetIndex();
    if (row < 0) return;
    int idx = (int)m_queueList->GetItemData(row);
    if (idx < 0 || idx >= (int)m_queue.size()) return;

    wxMenu menu;
    const InstallQueueItem& it = m_queue[idx];
    switch (it.state)
    {
    case InstallQueueItem::Running:
        menu.Append(ID_QueueCancelItem, wxT("Cancel install"));
        break;
    case InstallQueueItem::Queued:
        menu.Append(ID_QueueRemoveItem, wxT("Remove from queue"));
        break;
    case InstallQueueItem::Failed:
        menu.Append(ID_QueueShowError,  wxT("Show error..."));
        menu.Append(ID_QueueRemoveItem, wxT("Remove"));
        break;
    case InstallQueueItem::Cancelled:
        menu.Append(ID_QueueRemoveItem, wxT("Remove"));
        break;
    }
    // Stash the queue index in the list selection so the handler can find it.
    m_queueList->SetItemState(row, wxLIST_STATE_SELECTED,
                              wxLIST_STATE_SELECTED);
    PopupMenu(&menu);
}

void MainFrame::OnQueueCancelItem(wxCommandEvent& WXUNUSED(event))
{
    long row = m_queueList->GetNextItem(-1, wxLIST_NEXT_ALL,
                                        wxLIST_STATE_SELECTED);
    if (row < 0) return;
    int idx = (int)m_queueList->GetItemData(row);
    if (idx != m_runningIdx) return;
    if (m_currentState) m_currentState->cancelled = true;
}

void MainFrame::OnQueueRemoveItem(wxCommandEvent& WXUNUSED(event))
{
    long row = m_queueList->GetNextItem(-1, wxLIST_NEXT_ALL,
                                        wxLIST_STATE_SELECTED);
    if (row < 0) return;
    int idx = (int)m_queueList->GetItemData(row);
    if (idx < 0 || idx >= (int)m_queue.size()) return;
    if (idx == m_runningIdx) return; // can't remove the live one

    m_queue.erase(m_queue.begin() + idx);
    if (m_runningIdx > idx) --m_runningIdx;
    UpdateQueuePanel();
}

void MainFrame::OnQueueShowError(wxCommandEvent& WXUNUSED(event))
{
    long row = m_queueList->GetNextItem(-1, wxLIST_NEXT_ALL,
                                        wxLIST_STATE_SELECTED);
    if (row < 0) return;
    int idx = (int)m_queueList->GetItemData(row);
    if (idx < 0 || idx >= (int)m_queue.size()) return;
    const InstallQueueItem& it = m_queue[idx];
    if (it.error.empty()) return;
    wxMessageBox(wxString(it.error.c_str(), wxConvUTF8),
                 wxString(it.title.c_str(), wxConvUTF8) + wxT(" — install failed"),
                 wxOK | wxICON_ERROR);
}

void MainFrame::OnQueueClearFinished(wxCommandEvent& WXUNUSED(event))
{
    for (size_t i = m_queue.size(); i-- > 0; )
    {
        if (m_queue[i].state == InstallQueueItem::Failed ||
            m_queue[i].state == InstallQueueItem::Cancelled)
        {
            m_queue.erase(m_queue.begin() + i);
            if (m_runningIdx > (int)i) --m_runningIdx;
        }
    }
    UpdateQueuePanel();
}

