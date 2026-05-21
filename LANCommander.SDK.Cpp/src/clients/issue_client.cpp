#include "lancommander/clients/issue_client.h"
#include "../json/json_helpers.h"

#include <sstream>

namespace lancommander {

IssueClient::IssueClient(IHttpClient& http) : m_http(http) {}

Result<bool> IssueClient::open(const std::string& description, const std::string& game_id)
{
    std::string body = "{\"description\":\"" + json::escape(description)
                     + "\",\"gameId\":\"" + json::escape(game_id) + "\"}";

    HttpResponse resp = m_http.post("/api/Issue/Open", body);
    if (!resp.ok()) {
        std::ostringstream e;
        e << "OpenIssue failed (HTTP " << resp.status_code << ")";
        return Result<bool>::fail(e.str());
    }

    return Result<bool>::ok(true);
}

} // namespace lancommander
