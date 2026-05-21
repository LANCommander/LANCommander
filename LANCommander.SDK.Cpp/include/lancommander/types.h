#ifndef LANCOMMANDER_TYPES_H
#define LANCOMMANDER_TYPES_H

#include <functional>
#include <string>
#include <utility>

namespace lancommander {

// Generic result type returned by all client methods.
template<typename T>
struct Result {
    bool success;
    T value;
    std::string error;

    explicit operator bool() const { return success; }

    static Result ok(T val) {
        return { true, std::move(val), {} };
    }

    static Result fail(std::string err) {
        return { false, T{}, std::move(err) };
    }
};

// Specialization for void-like results.
template<>
struct Result<bool> {
    bool success;
    bool value;
    std::string error;

    explicit operator bool() const { return success; }

    static Result ok(bool val = true) {
        return { true, val, {} };
    }

    static Result fail(std::string err) {
        return { false, false, std::move(err) };
    }
};

// Progress callback for downloads. Return false to abort.
using DownloadProgressFn = std::function<bool(uint64_t received, uint64_t total)>;

} // namespace lancommander

#endif // LANCOMMANDER_TYPES_H
