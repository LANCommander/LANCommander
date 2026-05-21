#include "app/app.h"

// Allegro 4 requires END_OF_MAIN() after main.
#include <allegro.h>

int main(int argc, char* argv[])
{
    (void)argc;
    (void)argv;

    launcher::App app;

    if (!app.init(800, 600)) {
        allegro_message("Failed to initialize the launcher.\n%s", allegro_error);
        return 1;
    }

    int result = app.run();

    app.shutdown();

    return result;
}
END_OF_MAIN()
