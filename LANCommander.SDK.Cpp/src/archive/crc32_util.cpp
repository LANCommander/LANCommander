#include "lancommander/archive/crc32_util.h"

#include <cstdio>

namespace lancommander {

unsigned long Crc32::file_crc32(const char* path)
{
    FILE* f = fopen(path, "rb");
    if (!f) return 0;

    Crc32 crc;
    unsigned char buf[65536];
    size_t n;
    while ((n = fread(buf, 1, sizeof(buf), f)) > 0)
        crc.update(buf, n);

    fclose(f);
    return crc.value();
}

} // namespace lancommander
