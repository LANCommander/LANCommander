#ifndef LANCOMMANDER_WIN9X_SETTINGS_DIALOG_H
#define LANCOMMANDER_WIN9X_SETTINGS_DIALOG_H

#include <wx/dialog.h>

#include "settings.h"

// Modal preferences dialog. Writes back to the supplied Settings on OK; the
// caller is responsible for persisting via Settings::Save().
class SettingsDialog : public wxDialog
{
public:
    SettingsDialog(wxWindow* parent, Settings* settings);

    // Returns true if the user accepted and `settings` was updated.
    bool ApplyIfAccepted();

private:
    Settings* m_settings;
    class wxDirPickerCtrl* m_installDir;
    class wxSpinCtrl*      m_discoveryTimeout;
    class wxTextCtrl*      m_alias;

public:
    // Set after ApplyIfAccepted() returns true: the alias the user typed.
    // Caller compares to the previous value to decide whether to push to the
    // server / re-run NameChange scripts.
    bool AliasChanged() const { return m_aliasChanged; }

private:
    bool m_aliasChanged;
};

#endif
