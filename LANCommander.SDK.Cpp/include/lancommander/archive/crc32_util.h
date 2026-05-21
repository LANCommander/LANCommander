#ifndef LANCOMMANDER_ARCHIVE_CRC32_UTIL_H
#define LANCOMMANDER_ARCHIVE_CRC32_UTIL_H

#include <cstddef>

namespace lancommander {

// Minimal CRC32 implementation (IEEE 802.3 polynomial).
// No external dependencies — suitable for Win9x through modern platforms.
// For projects already linking zlib, you can use crc32() from zlib instead.
class Crc32 {
public:
    Crc32() : m_crc(0xFFFFFFFF) {}

    void update(const void* data, size_t length)
    {
        const unsigned char* p = static_cast<const unsigned char*>(data);
        for (size_t i = 0; i < length; ++i)
            m_crc = table()[(m_crc ^ p[i]) & 0xFF] ^ (m_crc >> 8);
    }

    unsigned long value() const { return m_crc ^ 0xFFFFFFFF; }

    void reset() { m_crc = 0xFFFFFFFF; }

    // Convenience: compute CRC32 of an entire file.
    // Returns 0 on I/O error (which is also a valid CRC, but rare).
    static unsigned long file_crc32(const char* path);

private:
    unsigned long m_crc;

    static const unsigned long* table()
    {
        static unsigned long t[256];
        static bool init = false;
        if (!init) {
            for (unsigned long i = 0; i < 256; ++i) {
                unsigned long c = i;
                for (int j = 0; j < 8; ++j)
                    c = (c & 1) ? (0xEDB88320UL ^ (c >> 1)) : (c >> 1);
                t[i] = c;
            }
            init = true;
        }
        return t;
    }
};

} // namespace lancommander

#endif // LANCOMMANDER_ARCHIVE_CRC32_UTIL_H
