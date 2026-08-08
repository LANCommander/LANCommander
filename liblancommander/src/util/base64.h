#ifndef LANCOMMANDER_UTIL_BASE64_H
#define LANCOMMANDER_UTIL_BASE64_H

#include <string>

namespace lancommander {
namespace base64 {

// Standard base64 (RFC 4648) with '=' padding and no line breaks, matching
// System.Convert.ToBase64String / FromBase64String so a value encoded by
// either launcher decodes in the other.
//
// The strings here hold arbitrary bytes, not text: embedded NULs and bytes
// above 0x7F round-trip intact.

std::string encode(const std::string& bytes);

// Whitespace between characters is skipped, as Convert.FromBase64String
// allows. Anything else — a stray character, bad padding, a truncated group —
// returns false rather than producing plausible-looking garbage.
bool decode(const std::string& text, std::string* out);

} // namespace base64
} // namespace lancommander

#endif // LANCOMMANDER_UTIL_BASE64_H
