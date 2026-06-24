#include "settings_dialog.h"

#include <wx/filepicker.h>
#include <wx/sizer.h>
#include <wx/spinctrl.h>
#include <wx/stattext.h>
#include <wx/textctrl.h>

SettingsDialog::SettingsDialog(wxWindow* parent, Settings* settings)
    : wxDialog(parent, wxID_ANY, wxT("Preferences"),
               wxDefaultPosition, wxSize(440, 240),
               wxDEFAULT_DIALOG_STYLE),
      m_settings(settings),
      m_installDir(NULL),
      m_discoveryTimeout(NULL),
      m_alias(NULL),
      m_aliasChanged(false)
{
    wxBoxSizer* root = new wxBoxSizer(wxVERTICAL);

    wxBoxSizer* aliasRow = new wxBoxSizer(wxHORIZONTAL);
    aliasRow->Add(new wxStaticText(this, wxID_ANY,
                                   wxT("Player name (alias):")),
                  0, wxALIGN_CENTER_VERTICAL | wxRIGHT, 6);
    m_alias = new wxTextCtrl(this, wxID_ANY,
                             wxString(m_settings->alias.c_str(), wxConvUTF8));
    aliasRow->Add(m_alias, 1, wxEXPAND);
    root->Add(aliasRow, 0, wxEXPAND | wxLEFT | wxRIGHT | wxTOP, 8);

    root->Add(new wxStaticText(this, wxID_ANY,
                               wxT("Default install directory (leave blank to prompt):")),
              0, wxLEFT | wxRIGHT | wxTOP, 8);
    m_installDir = new wxDirPickerCtrl(
        this, wxID_ANY,
        wxString(m_settings->defaultInstallDir.c_str(), wxConvUTF8),
        wxT("Default install directory"),
        wxDefaultPosition, wxDefaultSize,
        wxDIRP_USE_TEXTCTRL);
    root->Add(m_installDir, 0, wxEXPAND | wxALL, 8);

    wxBoxSizer* timeoutRow = new wxBoxSizer(wxHORIZONTAL);
    timeoutRow->Add(new wxStaticText(this, wxID_ANY,
                                     wxT("Server discovery timeout (ms):")),
                    0, wxALIGN_CENTER_VERTICAL | wxRIGHT, 6);
    m_discoveryTimeout = new wxSpinCtrl(this, wxID_ANY, wxEmptyString,
                                        wxDefaultPosition, wxSize(100, -1),
                                        wxSP_ARROW_KEYS,
                                        500, 30000,
                                        m_settings->discoveryTimeoutMs);
    timeoutRow->Add(m_discoveryTimeout, 0);
    root->Add(timeoutRow, 0, wxLEFT | wxRIGHT | wxBOTTOM, 8);

    root->AddStretchSpacer(1);
    root->Add(CreateButtonSizer(wxOK | wxCANCEL), 0, wxEXPAND | wxALL, 8);
    SetSizer(root);
}

bool SettingsDialog::ApplyIfAccepted()
{
    if (ShowModal() != wxID_OK) return false;
    std::string newAlias(m_alias->GetValue().mb_str(wxConvUTF8));
    m_aliasChanged = (newAlias != m_settings->alias);
    m_settings->alias = newAlias;
    m_settings->defaultInstallDir =
        std::string(m_installDir->GetPath().mb_str());
    m_settings->discoveryTimeoutMs = m_discoveryTimeout->GetValue();
    return true;
}
