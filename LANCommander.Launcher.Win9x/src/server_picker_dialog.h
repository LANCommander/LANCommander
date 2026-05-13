#ifndef LANCOMMANDER_WIN9X_SERVER_PICKER_DIALOG_H
#define LANCOMMANDER_WIN9X_SERVER_PICKER_DIALOG_H

#include <wx/dialog.h>

#include "server_store.h"

// Dialog with a wxListBox of bookmarked servers + Connect / Add / Remove.
// Returns wxID_OK with PickedIndex() set when the user chose to connect, or
// wxID_NEW for "Add a new server" (caller routes to the normal Connect flow).
class ServerPickerDialog : public wxDialog
{
public:
    ServerPickerDialog(wxWindow* parent, ServerStore* store);

    int PickedIndex() const { return m_picked; }

private:
    void OnConnect(wxCommandEvent& event);
    void OnAdd(wxCommandEvent& event);
    void OnRemove(wxCommandEvent& event);
    void OnDoubleClick(wxCommandEvent& event);

    void Reload();

    ServerStore*   m_store;
    class wxListBox* m_list;
    int            m_picked;

    enum
    {
        ID_PickerList = wxID_HIGHEST + 1,
        ID_PickerConnect,
        ID_PickerAdd,
        ID_PickerRemove
    };

    wxDECLARE_EVENT_TABLE();
};

#endif
