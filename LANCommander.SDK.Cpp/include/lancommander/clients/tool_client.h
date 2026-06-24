#ifndef LANCOMMANDER_CLIENTS_TOOL_CLIENT_H
#define LANCOMMANDER_CLIENTS_TOOL_CLIENT_H

#include <string>
#include <vector>

#include "../http/http_client.h"
#include "../models/tool.h"
#include "../models/script.h"
#include "../types.h"

namespace lancommander {

class ToolClient {
public:
    explicit ToolClient(IHttpClient& http);

    Result<Tool> get(const std::string& tool_id);
    Result<std::vector<Script>> get_scripts(const std::string& tool_id);
    Result<bool> download(const std::string& tool_id, const std::string& dest_path,
                          DownloadProgressFn progress = nullptr);

private:
    IHttpClient& m_http;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_TOOL_CLIENT_H
