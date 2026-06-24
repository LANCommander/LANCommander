#ifndef LANCOMMANDER_HTTP_RESPONSE_H
#define LANCOMMANDER_HTTP_RESPONSE_H

#include <map>
#include <string>

namespace lancommander {

struct HttpResponse {
    int status_code = 0;
    std::string body;
    std::map<std::string, std::string> headers;

    bool ok() const { return status_code >= 200 && status_code < 300; }
};

} // namespace lancommander

#endif // LANCOMMANDER_HTTP_RESPONSE_H
