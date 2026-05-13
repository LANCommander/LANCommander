#ifndef LANCOMMANDER_WIN9X_INSTALL_OPTIONS_DIALOG_H
#define LANCOMMANDER_WIN9X_INSTALL_OPTIONS_DIALOG_H

#include <wx/dialog.h>

#include <string>
#include <vector>

#include "game_client.h"

// Modal dialog that lets the user pick the install destination and optionally
// select addons to install alongside the base game. Mirrors the moving pieces
// of Avalonia's InstallOptionsOverlay without trying to compete on chrome.
class InstallOptionsDialog : public wxDialog
{
public:
    InstallOptionsDialog(wxWindow* parent, const wxString& gameTitle,
                         const std::vector<GameSummary>& addons,
                         const wxString& defaultDir = wxEmptyString);

    std::string GetDestDir() const;
    std::vector<GameSummary> GetSelectedAddons() const;

private:
    std::vector<GameSummary> m_addons;
    class wxDirPickerCtrl*   m_dir;
    class wxCheckListBox*    m_list;
};

#endif
