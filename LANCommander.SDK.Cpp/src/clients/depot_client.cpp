#include "lancommander/clients/depot_client.h"
#include "../json/json_helpers.h"

#include <sstream>

namespace lancommander {

DepotClient::DepotClient(IHttpClient& http) : m_http(http) {}

Result<DepotResults> DepotClient::get()
{
    HttpResponse resp = m_http.get("/api/Depot");
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetDepot failed (HTTP " << resp.status_code << ")";
        return Result<DepotResults>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<DepotResults>::fail("Invalid JSON response");

    DepotResults results = json::parse_depot_results(doc.root);
    return Result<DepotResults>::ok(std::move(results));
}

Result<DepotGame> DepotClient::get_game(const std::string& game_id)
{
    HttpResponse resp = m_http.get("/api/Depot/Games/" + game_id);
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetDepotGame failed (HTTP " << resp.status_code << ")";
        return Result<DepotGame>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<DepotGame>::fail("Invalid JSON response");

    DepotGame game = json::parse_depot_game(doc.root);
    if (game.id.empty()) return Result<DepotGame>::fail("No game ID in response");
    return Result<DepotGame>::ok(std::move(game));
}

} // namespace lancommander
