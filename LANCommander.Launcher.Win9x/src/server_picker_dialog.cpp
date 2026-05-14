#include "server_picker_dialog.h"

#include <wx/button.h>
#include <wx/listbox.h>
#include <wx/sizer.h>
#include <wx/stattext.h>

wxBEGIN_EVENT_TABLE(ServerPickerDialog, wxDialog)
    EVT_BUTTON(ID_PickerConnect, ServerPickerDialog::OnConnect)
    EVT_BUTTON(ID_PickerAdd,     ServerPickerDialog::OnAdd)
    EVT_BUTTON(ID_PickerRemove,  ServerPickerDialog::OnRemove)
    EVT_LISTBOX_DCLICK(ID_PickerList, ServerPickerDialog::OnDoubleClick)
wxEND_EVENT_TABLE()

ServerPickerDialog::ServerPickerDialog(wxWindow* parent, ServerStore* store)
    : wxDialog(parent, wxID_ANY, wxT("Servers"),
               wxDefaultPosition, wxSize(420, 320),
               wxDEFAULT_DIALOG_STYLE | wxRESIZE_BORDER),
      m_store(store),
      m_list(NULL),
      m_picked(-1)
{
    wxBoxSizer* root = new wxBoxSizer(wxVERTICAL);
    root->Add(new wxStaticText(this, wxID_ANY,
                               wxT("Saved servers (double-click to connect):")),
              0, wxLEFT | wxRIGHT | wxTOP, 8);

    m_list = new wxListBox(this, ID_PickerList);
    root->Add(m_list, 1, wxEXPAND | wxALL, 8);

    wxBoxSizer* buttons = new wxBoxSizer(wxHORIZONTAL);
    buttons->Add(new wxButton(this, ID_PickerAdd,    wxT("&Add new...")), 0, wxRIGHT, 4);
    buttons->Add(new wxButton(this, ID_PickerRemove, wxT("&Remove")),     0, wxRIGHT, 4);
    buttons->AddStretchSpacer(1);
    buttons->Add(new wxButton(this, wxID_CANCEL,     wxT("&Close")),      0, wxRIGHT, 4);
    buttons->Add(new wxButton(this, ID_PickerConnect, wxT("Co&nnect")),   0);
    root->Add(buttons, 0, wxEXPAND | wxLEFT | wxRIGHT | wxBOTTOM, 8);

    SetSizer(root);
    Reload();
}

void ServerPickerDialog::Reload()
{
    m_list->Clear();
    const std::vector<ServerBookmark>& entries = m_store->Entries();
    for (size_t i = 0; i < entries.size(); ++i)
    {
        const ServerBookmark& b = entries[i];
        wxString label;
        if (!b.name.empty())
        {
            label = wxString(b.name.c_str(), wxConvUTF8);
            label += wxT(" \xE2\x80\x94 ");
        }
        label += wxString(b.url.c_str(), wxConvUTF8);
        if (!b.userName.empty())
        {
            label += wxT(" (");
            label += wxString(b.userName.c_str(), wxConvUTF8);
            label += wxT(")");
        }
        m_list->Append(label);
    }
    if (!entries.empty()) m_list->SetSelection(0);
}

void ServerPickerDialog::OnConnect(wxCommandEvent& WXUNUSED(event))
{
    int sel = m_list->GetSelection();
    if (sel == wxNOT_FOUND)
    {
        wxMessageBox(wxT("Pick a server first, or use Add new..."),
                     wxT("Servers"), wxOK | wxICON_INFORMATION, this);
        return;
    }
    m_picked = sel;
    EndModal(wxID_OK);
}

void ServerPickerDialog::OnAdd(wxCommandEvent& WXUNUSED(event))
{
    EndModal(wxID_NEW);
}

void ServerPickerDialog::OnRemove(wxCommandEvent& WXUNUSED(event))
{
    int sel = m_list->GetSelection();
    if (sel == wxNOT_FOUND) return;
    const ServerBookmark& b = m_store->Entries()[sel];
    wxString msg;
    msg.Printf(wxT("Remove %s from saved servers?"),
               wxString(b.url.c_str(), wxConvUTF8).c_str());
    if (wxMessageBox(msg, wxT("Remove"),
                     wxYES_NO | wxICON_QUESTION, this) != wxYES)
        return;
    m_store->Remove(b.url);
    m_store->Save();
    Reload();
}

void ServerPickerDialog::OnDoubleClick(wxCommandEvent& evt)
{
    OnConnect(evt);
}
