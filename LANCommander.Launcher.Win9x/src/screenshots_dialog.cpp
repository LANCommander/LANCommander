#include "screenshots_dialog.h"
#include "logger.h"

#include <wx/bitmap.h>
#include <wx/bmpbuttn.h>
#include <wx/image.h>
#include <wx/scrolwin.h>
#include <wx/sizer.h>
#include <wx/stattext.h>

#include <windows.h>
#include <shellapi.h>

namespace
{
    bool IsImageType(const std::string& t)
    {
        return t == "Cover" || t == "Background" || t == "Logo" ||
               t == "Icon"  || t == "Screenshot" || t == "Grid"  ||
               t == "PageImage" || t == "Avatar";
    }

    wxBitmap LoadAndScale(const std::string& path, int maxW, int maxH)
    {
        wxImage img;
        if (!img.LoadFile(wxString(path.c_str(), wxConvUTF8), wxBITMAP_TYPE_ANY))
            return wxBitmap();
        double sx = (double)maxW / (double)img.GetWidth();
        double sy = (double)maxH / (double)img.GetHeight();
        double s  = sx < sy ? sx : sy;
        int w = (int)(img.GetWidth()  * s); if (w < 1) w = 1;
        int h = (int)(img.GetHeight() * s); if (h < 1) h = 1;
        img.Rescale(w, h, wxIMAGE_QUALITY_HIGH);
        return wxBitmap(img);
    }
}

ScreenshotsDialog::ScreenshotsDialog(wxWindow* parent, MediaClient* api,
                                     MediaCache* cache,
                                     const std::string& gameId,
                                     const wxString& title)
    : wxDialog(parent, wxID_ANY,
               wxString::Format(wxT("Media \xE2\x80\x94 %s"), title.c_str()),
               wxDefaultPosition, wxSize(640, 480),
               wxDEFAULT_DIALOG_STYLE | wxRESIZE_BORDER)
{
    wxBoxSizer* root = new wxBoxSizer(wxVERTICAL);

    std::vector<MediaRef> media;
    std::string err;
    if (!api->GetMediaForGame(gameId, &media, &err))
    {
        LogError("GetGameMedia %s failed: %s", gameId.c_str(), err.c_str());
        root->Add(new wxStaticText(this, wxID_ANY,
                                   wxString(err.c_str(), wxConvUTF8)),
                  0, wxALL, 8);
        root->Add(CreateButtonSizer(wxOK), 0, wxEXPAND | wxALL, 8);
        SetSizer(root);
        return;
    }

    wxScrolledWindow* scroll = new wxScrolledWindow(this, wxID_ANY);
    scroll->SetScrollRate(0, 12);
    wxWrapSizer* grid = new wxWrapSizer(wxHORIZONTAL);

    int thumbCount = 0;
    for (size_t i = 0; i < media.size(); ++i)
    {
        const MediaRef& m = media[i];
        if (!IsImageType(m.type)) continue;

        std::string local = cache->GetThumbnail(m.id, m.crc32);
        if (local.empty()) continue;

        wxBitmap bmp = LoadAndScale(local, 200, 150);
        if (!bmp.IsOk()) continue;

        m_paths.push_back(local);
        wxBitmapButton* btn = new wxBitmapButton(
            scroll, ID_ThumbBase + thumbCount, bmp,
            wxDefaultPosition, wxSize(bmp.GetWidth() + 8, bmp.GetHeight() + 8));
        btn->SetToolTip(wxString(m.type.c_str(), wxConvUTF8));
        Connect(ID_ThumbBase + thumbCount, wxEVT_COMMAND_BUTTON_CLICKED,
                wxCommandEventHandler(ScreenshotsDialog::OnThumbClick));
        grid->Add(btn, 0, wxALL, 6);
        ++thumbCount;
    }

    if (thumbCount == 0)
    {
        root->Add(new wxStaticText(this, wxID_ANY,
                                   wxT("No screenshots or other media for this game.")),
                  0, wxALL, 16);
    }
    else
    {
        scroll->SetSizer(grid);
        scroll->FitInside();
        root->Add(scroll, 1, wxEXPAND | wxALL, 6);
    }

    root->Add(CreateButtonSizer(wxOK), 0, wxEXPAND | wxALL, 8);
    SetSizer(root);
}

void ScreenshotsDialog::OnThumbClick(wxCommandEvent& event)
{
    int idx = event.GetId() - ID_ThumbBase;
    if (idx < 0 || (size_t)idx >= m_paths.size()) return;
    // Hand off to whatever the OS uses to view images.
    ShellExecuteA(NULL, "open", m_paths[idx].c_str(), NULL, NULL, SW_SHOWNORMAL);
}
