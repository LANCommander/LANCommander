#ifndef LANCOMMANDER_MODELS_MULTIPLAYER_MODE_H
#define LANCOMMANDER_MODELS_MULTIPLAYER_MODE_H

#include <string>

namespace lancommander {

enum class MultiplayerType {
    Local = 0,
    LAN,
    Online
};

enum class NetworkProtocol {
    TCPIP = 0,
    IPX,
    Modem,
    Serial,
    Lobby
};

struct MultiplayerMode {
    std::string id;
    MultiplayerType type = MultiplayerType::Local;
    NetworkProtocol network_protocol = NetworkProtocol::TCPIP;
    std::string description;
    int min_players = 0;
    int max_players = 0;
    int spectators = 0;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_MULTIPLAYER_MODE_H
