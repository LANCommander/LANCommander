#include "install_options_dialog.h"

#include <wx/checklst.h>
#include <wx/filepicker.h>
#include <wx/sizer.h>
#include <wx/stattext.h>

InstallOptionsDialog::InstallOptionsDialog(wxWindow* parent,
                                           const wxString& gameTitle,
                                           const std::vector<GameSummary>& addons,
                                           const wxString& defaultDir)
    : wxDialog(parent, wxID_ANY, wxString::Format(wxT("Install %s"), gameTitle.c_str()),
               wxDefaultPosition, wxSize(420, 360),
               wxDEFAULT_DIALOG_STYLE | wxRESIZE_BORDER),
      m_addons(addons),
      m_dir(NULL),
      m_list(NULL)
{
    (void)defaultDir; // applied below after the picker is constructed
    wxBoxSizer* root = new wxBoxSizer(wxVERTICAL);

    root->Add(new wxStaticText(this, wxID_ANY, wxT("Install directory:")),
              0, wxLEFT | wxRIGHT | wxTOP, 8);
    m_dir = new wxDirPickerCtrl(this, wxID_ANY, defaultDir,
                                wxT("Pick install directory"),
                                wxDefaultPosition, wxDefaultSize,
                                wxDIRP_USE_TEXTCTRL | wxDIRP_DIR_MUST_EXIST);
    root->Add(m_dir, 0, wxEXPAND | wxALL, 8);

    if (!m_addons.empty())
    {
        root->Add(new wxStaticText(this, wxID_ANY,
                                   wxT("Addons (DLC / expansions):")),
                  0, wxLEFT | wxRIGHT, 8);
        m_list = new wxCheckListBox(this, wxID_ANY);
        for (size_t i = 0; i < m_addons.size(); ++i)
        {
            m_list->Append(wxString(m_addons[i].title.c_str(), wxConvUTF8));
        }
        root->Add(m_list, 1, wxEXPAND | wxALL, 8);
    }
    else
    {
        root->AddStretchSpacer(1);
    }

    root->Add(CreateButtonSizer(wxOK | wxCANCEL), 0, wxEXPAND | wxALL, 8);
    SetSizer(root);
}

std::string InstallOptionsDialog::GetDestDir() const
{
    return std::string(m_dir->GetPath().mb_str());
}

std::vector<GameSummary> InstallOptionsDialog::GetSelectedAddons() const
{
    std::vector<GameSummary> out;
    if (!m_list) return out;
    for (size_t i = 0; i < m_addons.size(); ++i)
    {
        if (m_list->IsChecked((unsigned int)i))
            out.push_back(m_addons[i]);
    }
    return out;
}
