#include <allegro.h>
#include <cstdio>

int main(int argc, char* argv[])
{
    (void)argc; (void)argv;

    // Log to a file next to the executable
    FILE* f = fopen("crash_log.txt", "w");
    if (!f) return 1;

    fprintf(f, "before allegro_init\n"); fflush(f);
    if (allegro_init() != 0) { fprintf(f, "allegro_init fail\n"); fclose(f); return 1; }

    install_keyboard();
    install_mouse();
    install_timer();

    set_color_depth(32);
    if (set_gfx_mode(GFX_AUTODETECT_WINDOWED, 640, 480, 0, 0) != 0) {
        fprintf(f, "gfx 32bpp fail, trying 16\n"); fflush(f);
        set_color_depth(16);
        if (set_gfx_mode(GFX_AUTODETECT_WINDOWED, 640, 480, 0, 0) != 0) {
            fprintf(f, "gfx fail: %s\n", allegro_error); fclose(f); return 1;
        }
    }
    fprintf(f, "gfx OK depth=%d\n", bitmap_color_depth(screen)); fflush(f);

    set_window_title("Allegro 4 Test");

    BITMAP* buf = create_bitmap(640, 480);
    clear_to_color(buf, makecol(0, 0, 128));
    rectfill(buf, 200, 180, 440, 300, makecol(40, 40, 80));
    fprintf(f, "drawing primitives OK\n"); fflush(f);

    // The critical test
    textout_centre_ex(buf, font, "Hello from Allegro 4!", 320, 220, makecol(255, 255, 255), -1);
    fprintf(f, "textout OK!\n"); fflush(f);

    textout_centre_ex(buf, font, "Press ESC to exit", 320, 250, makecol(180, 180, 200), -1);
    fprintf(f, "textout 2 OK!\n"); fflush(f);

    blit(buf, screen, 0, 0, 0, 0, 640, 480);
    fprintf(f, "blit OK\n"); fflush(f);

    while (!key[KEY_ESC]) {
        rest(100);
    }

    destroy_bitmap(buf);
    allegro_exit();
    fprintf(f, "clean exit\n");
    fclose(f);
    return 0;
}
END_OF_MAIN()
