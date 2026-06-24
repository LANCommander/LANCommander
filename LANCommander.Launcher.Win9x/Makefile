# LANCommander Win9x Launcher
#
# Build with MinGW (i686, targeting Win9x) against wxWidgets 2.8.x.
# Before building, drop the following single-file libs into vendor/:
#   vendor/cjson/cJSON.c    vendor/cjson/cJSON.h    (DaveGamble/cJSON, MIT)
#   vendor/miniz/miniz.c    vendor/miniz/miniz.h    (richgel999/miniz, MIT)
#   vendor/sqlite/sqlite3.c vendor/sqlite/sqlite3.h (SQLite amalgamation,
#                                                    public domain — grab
#                                                    the latest "sqlite-amalgamation-*.zip"
#                                                    from sqlite.org/download.html)
# Override WX_CONFIG / CXX on the command line as needed.
#
# wxWidgets must be built with JPEG and PNG support for cover art to render
# (the default `./configure` enables both). If you maintain a custom wx build,
# pass `--enable-jpeg --enable-png` (or set wxUSE_LIBJPEG=1 / wxUSE_LIBPNG=1
# in src/msw/setup.h). `wx-config --libs` already pulls in libwxjpeg / libwxpng
# from the wx tree when they're enabled, so no extra -l flags are needed here.

WX_CONFIG ?= wx-config
CXX       ?= i686-w64-mingw32-g++
CC        ?= i686-w64-mingw32-gcc

CXXFLAGS  := $(shell $(WX_CONFIG) --cxxflags) -Os -Wall \
             -Ivendor/cjson -Ivendor/miniz -Ivendor/sqlite
CFLAGS    := -Os -Wall -Ivendor/cjson -Ivendor/miniz -Ivendor/sqlite \
             -DSQLITE_OMIT_LOAD_EXTENSION -DSQLITE_THREADSAFE=1 \
             -DSQLITE_DEFAULT_MEMSTATUS=0 -DSQLITE_OMIT_DEPRECATED \
             -DSQLITE_OMIT_AUTOINIT
LDFLAGS   := $(shell $(WX_CONFIG) --libs) -lwininet -lwsock32 -mwindows \
             -static-libgcc -static-libstdc++

CXX_SRC := src/app.cpp src/http_client.cpp src/archive.cpp \
           src/auth_client.cpp src/game_client.cpp src/key_client.cpp \
           src/library_client.cpp src/media_client.cpp \
           src/profile_client.cpp src/redistributable_client.cpp \
           src/save_client.cpp src/script_client.cpp src/script_runner.cpp \
           src/installs.cpp src/launcher.cpp src/save_sync.cpp \
           src/install_worker.cpp src/settings.cpp src/discovery.cpp \
           src/media_cache.cpp src/path_expand.cpp src/logger.cpp \
           src/save_sync_worker.cpp src/catalog_db.cpp \
           src/install_options_dialog.cpp src/server_store.cpp \
           src/server_picker_dialog.cpp src/settings_dialog.cpp \
           src/screenshots_dialog.cpp
C_SRC   := vendor/cjson/cJSON.c vendor/miniz/miniz.c vendor/sqlite/sqlite3.c
OBJ     := $(CXX_SRC:.cpp=.o) $(C_SRC:.c=.o)
BIN     := lancommander.exe

all: $(BIN)

$(BIN): $(OBJ)
	$(CXX) -o $@ $^ $(LDFLAGS) -lshell32

%.o: %.cpp
	$(CXX) $(CXXFLAGS) -c -o $@ $<

%.o: %.c
	$(CC) $(CFLAGS) -c -o $@ $<

clean:
	rm -f $(OBJ) $(BIN)

.PHONY: all clean
