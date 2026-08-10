#include "ui/chrome_geometry.h"

namespace launcher
{
    namespace ui
    {

        ChromeHit chrome_hit_test(int x, int y, int win_w, int win_h,
                                  int chrome_h, int resize_border,
                                  int drag_right, bool drag_enabled)
        {
            const bool top    = y < resize_border;
            const bool bottom = y >= win_h - resize_border;
            const bool left   = x < resize_border;
            const bool right  = x >= win_w - resize_border;

            // Corners first: at the very top-left both `top` and `left` are
            // true, and the diagonal resize is what the user means.
            if (top && left)     return ChromeHit::ResizeTopLeft;
            if (top && right)    return ChromeHit::ResizeTopRight;
            if (bottom && left)  return ChromeHit::ResizeBottomLeft;
            if (bottom && right) return ChromeHit::ResizeBottomRight;
            if (top)             return ChromeHit::ResizeTop;
            if (bottom)          return ChromeHit::ResizeBottom;
            if (left)            return ChromeHit::ResizeLeft;
            if (right)           return ChromeHit::ResizeRight;

            // Then the title bar. This is checked after the edges so the top
            // few pixels resize rather than drag.
            if (drag_enabled && y < chrome_h && x < drag_right)
                return ChromeHit::Draggable;

            return ChromeHit::Client;
        }

    } // namespace ui
} // namespace launcher
