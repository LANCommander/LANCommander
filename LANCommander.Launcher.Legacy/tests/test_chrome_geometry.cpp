#include "test_main.h"

#include "ui/chrome_geometry.h"

using launcher::ui::ChromeHit;
using launcher::ui::chrome_hit_test;

// The launcher's real chrome numbers (window_chrome.cpp).
static const int CHROME_H = 32;
static const int BORDER = 6;
static const int W = 800;
static const int H = 600;
static const int DRAG_RIGHT = 690; // left of the close/minimise/user buttons

static ChromeHit hit(int x, int y)
{
    return chrome_hit_test(x, y, W, H, CHROME_H, BORDER, DRAG_RIGHT, true);
}

void test_chrome_geometry()
{
    // --- All eight resize zones ---
    CHECK(hit(0, 0) == ChromeHit::ResizeTopLeft);
    CHECK(hit(W - 1, 0) == ChromeHit::ResizeTopRight);
    CHECK(hit(0, H - 1) == ChromeHit::ResizeBottomLeft);
    CHECK(hit(W - 1, H - 1) == ChromeHit::ResizeBottomRight);
    CHECK(hit(W / 2, 0) == ChromeHit::ResizeTop);
    CHECK(hit(W / 2, H - 1) == ChromeHit::ResizeBottom);
    CHECK(hit(0, H / 2) == ChromeHit::ResizeLeft);
    CHECK(hit(W - 1, H / 2) == ChromeHit::ResizeRight);

    // --- The border is exactly `resize_border` wide ---
    // Last pixel inside the border resizes; the next one does not.
    CHECK(hit(W / 2, BORDER - 1) == ChromeHit::ResizeTop);
    CHECK(hit(W / 2, BORDER) != ChromeHit::ResizeTop);
    CHECK(hit(BORDER - 1, H / 2) == ChromeHit::ResizeLeft);
    CHECK(hit(BORDER, H / 2) != ChromeHit::ResizeLeft);
    CHECK(hit(W / 2, H - BORDER) == ChromeHit::ResizeBottom);
    CHECK(hit(W / 2, H - BORDER - 1) != ChromeHit::ResizeBottom);

    // --- Corners beat edges ---
    // At (0,0) both the top and left tests pass; the diagonal is what the
    // user means, so it must not degrade to a single-axis resize.
    CHECK(hit(BORDER - 1, BORDER - 1) == ChromeHit::ResizeTopLeft);

    // --- Edges beat the title bar ---
    // The top border overlaps the title bar. Grabbing there must resize
    // rather than drag, or the window can never be resized from the top.
    CHECK(hit(100, 0) == ChromeHit::ResizeTop);
    CHECK(hit(100, BORDER) == ChromeHit::Draggable);

    // --- Title bar ---
    CHECK(hit(100, CHROME_H - 1) == ChromeHit::Draggable);
    CHECK(hit(100, CHROME_H) == ChromeHit::Client); // below the bar

    // Right of drag_right are the window buttons: they must stay clickable
    // rather than starting a window drag.
    CHECK(hit(DRAG_RIGHT, 10) == ChromeHit::Client);
    CHECK(hit(DRAG_RIGHT - 1, 10) == ChromeHit::Draggable);

    // --- Dragging disabled (user dropdown open) ---
    // The click has to reach the UI so the menu can dismiss itself.
    CHECK(chrome_hit_test(100, 10, W, H, CHROME_H, BORDER, DRAG_RIGHT, false)
          == ChromeHit::Client);
    // ...but resizing still works while the menu is open.
    CHECK(chrome_hit_test(0, 0, W, H, CHROME_H, BORDER, DRAG_RIGHT, false)
          == ChromeHit::ResizeTopLeft);

    // --- Content ---
    CHECK(hit(W / 2, H / 2) == ChromeHit::Client);

    // --- Degenerate: window at its 640x480 minimum still behaves ---
    CHECK(chrome_hit_test(0, 0, 640, 480, CHROME_H, BORDER, DRAG_RIGHT, true)
          == ChromeHit::ResizeTopLeft);
    CHECK(chrome_hit_test(320, 240, 640, 480, CHROME_H, BORDER, DRAG_RIGHT, true)
          == ChromeHit::Client);
}
