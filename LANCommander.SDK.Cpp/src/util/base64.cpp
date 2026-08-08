#include "util/base64.h"

#include <cctype>

namespace lancommander {
namespace base64 {

namespace {

const char kAlphabet[] =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

// Sextet value for a base64 character, or -1 if it is not one.
int sextet(unsigned char c)
{
    if (c >= 'A' && c <= 'Z') return c - 'A';
    if (c >= 'a' && c <= 'z') return c - 'a' + 26;
    if (c >= '0' && c <= '9') return c - '0' + 52;
    if (c == '+') return 62;
    if (c == '/') return 63;
    return -1;
}

bool is_space(unsigned char c)
{
    return c == ' ' || c == '\t' || c == '\r' || c == '\n' ||
           c == '\v' || c == '\f';
}

} // namespace

std::string encode(const std::string& bytes)
{
    std::string out;
    out.reserve(((bytes.size() + 2) / 3) * 4);

    std::size_t i = 0;

    // Whole three-byte groups become four characters with no padding.
    for (; i + 2 < bytes.size(); i += 3) {
        const unsigned long triple =
            ((unsigned long)(unsigned char)bytes[i] << 16) |
            ((unsigned long)(unsigned char)bytes[i + 1] << 8) |
            ((unsigned long)(unsigned char)bytes[i + 2]);

        out += kAlphabet[(triple >> 18) & 0x3F];
        out += kAlphabet[(triple >> 12) & 0x3F];
        out += kAlphabet[(triple >> 6) & 0x3F];
        out += kAlphabet[triple & 0x3F];
    }

    // A one- or two-byte tail is padded out to four characters with '='.
    const std::size_t remaining = bytes.size() - i;

    if (remaining == 1) {
        const unsigned long triple = (unsigned long)(unsigned char)bytes[i] << 16;

        out += kAlphabet[(triple >> 18) & 0x3F];
        out += kAlphabet[(triple >> 12) & 0x3F];
        out += '=';
        out += '=';
    } else if (remaining == 2) {
        const unsigned long triple =
            ((unsigned long)(unsigned char)bytes[i] << 16) |
            ((unsigned long)(unsigned char)bytes[i + 1] << 8);

        out += kAlphabet[(triple >> 18) & 0x3F];
        out += kAlphabet[(triple >> 12) & 0x3F];
        out += kAlphabet[(triple >> 6) & 0x3F];
        out += '=';
    }

    return out;
}

bool decode(const std::string& text, std::string* out)
{
    if (!out)
        return false;

    out->clear();
    out->reserve((text.size() / 4) * 3);

    unsigned long accumulator = 0;
    int collected = 0;      // sextets accumulated toward the current group
    int padding = 0;

    for (std::size_t i = 0; i < text.size(); ++i) {
        const unsigned char c = (unsigned char)text[i];

        if (is_space(c))
            continue;

        if (c == '=') {
            // Padding only makes sense once a group is partly filled, and at
            // most two of them.
            if (collected < 2 || ++padding > 2) {
                out->clear();
                return false;
            }
            continue;
        }

        // A character after padding means the group already ended.
        if (padding > 0) {
            out->clear();
            return false;
        }

        const int value = sextet(c);
        if (value < 0) {
            out->clear();
            return false;
        }

        accumulator = (accumulator << 6) | (unsigned long)value;

        if (++collected == 4) {
            *out += (char)((accumulator >> 16) & 0xFF);
            *out += (char)((accumulator >> 8) & 0xFF);
            *out += (char)(accumulator & 0xFF);

            accumulator = 0;
            collected = 0;
        }
    }

    if (collected == 0) {
        // A clean group boundary: padding, if any, has to have completed the
        // final group rather than dangling on its own.
        if (padding != 0) {
            out->clear();
            return false;
        }
        return true;
    }

    // A trailing partial group is valid only as the padded tail: three sextets
    // yield two bytes, two yield one. A single sextet encodes nothing.
    if (collected == 3) {
        if (padding != 1) {
            out->clear();
            return false;
        }
        accumulator <<= 6;
        *out += (char)((accumulator >> 16) & 0xFF);
        *out += (char)((accumulator >> 8) & 0xFF);
        return true;
    }

    if (collected == 2) {
        if (padding != 2) {
            out->clear();
            return false;
        }
        accumulator <<= 12;
        *out += (char)((accumulator >> 16) & 0xFF);
        return true;
    }

    out->clear();
    return false;
}

} // namespace base64
} // namespace lancommander
