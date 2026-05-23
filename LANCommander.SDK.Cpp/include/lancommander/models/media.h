#ifndef LANCOMMANDER_MODELS_MEDIA_H
#define LANCOMMANDER_MODELS_MEDIA_H

#include <string>

namespace lancommander {

enum class MediaType {
    Icon = 0,
    Cover,
    Background,
    Avatar,
    Logo,
    Manual,
    Thumbnail,  // deprecated on server, kept for enum compat
    PageImage,
    Grid,
    Screenshot,
    Video
};

struct Media {
    std::string id;
    MediaType type = MediaType::Icon;
    std::string file_id;
    std::string crc32;
    std::string source_url;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_MEDIA_H
