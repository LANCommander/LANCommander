#ifndef LANCOMMANDER_WIN9X_SCREENSHOTS_DIALOG_H
#define LANCOMMANDER_WIN9X_SCREENSHOTS_DIALOG_H

#include <wx/dialog.h>

#include <string>
#include <vector>

#include "media_client.h"
#include "media_cache.h"

// Modal dialog that fetches a game's full media list, caches all image-type
// thumbnails, and presents them in a wrapping grid. Clicking a thumbnail
// shells the cached file to the OS default viewer.
class ScreenshotsDialog : public wxDialog
{
public:
    ScreenshotsDialog(wxWindow* parent, MediaClient* api, MediaCache* cache,
                      const std::string& gameId, const wxString& title);

private:
    void OnThumbClick(wxCommandEvent& event);

    std::vector<std::string> m_paths; // index matches button id offset
    enum { ID_ThumbBase = wxID_HIGHEST + 1 };
};

#endif
