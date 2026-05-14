#include "library_client.h"

#include <sstream>

bool LibraryClient::AddToLibrary(const std::string& gameId, std::string* errorOut)
{
    HttpResponse resp = m_http.PostJson(
        "/api/Library/AddToLibrary/" + gameId, std::string());
    if (resp.ok()) return true;
    if (errorOut)
    {
        std::ostringstream e;
        e << "AddToLibrary failed (HTTP " << resp.status << ")";
        *errorOut = e.str();
    }
    return false;
}

bool LibraryClient::RemoveFromLibrary(const std::string& gameId, std::string* errorOut)
{
    HttpResponse resp = m_http.PostJson(
        "/api/Library/RemoveFromLibrary/" + gameId, std::string());
    if (resp.ok()) return true;
    if (errorOut)
    {
        std::ostringstream e;
        e << "RemoveFromLibrary failed (HTTP " << resp.status << ")";
        *errorOut = e.str();
    }
    return false;
}
