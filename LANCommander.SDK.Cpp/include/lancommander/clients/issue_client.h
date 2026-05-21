#ifndef LANCOMMANDER_CLIENTS_ISSUE_CLIENT_H
#define LANCOMMANDER_CLIENTS_ISSUE_CLIENT_H

#include <string>

#include "../http/http_client.h"
#include "../models/issue.h"
#include "../types.h"

namespace lancommander {

class IssueClient {
public:
    explicit IssueClient(IHttpClient& http);

    Result<bool> open(const std::string& description, const std::string& game_id);

private:
    IHttpClient& m_http;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_ISSUE_CLIENT_H
