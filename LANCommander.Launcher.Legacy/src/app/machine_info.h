#ifndef LAUNCHER_APP_MACHINE_INFO_H
#define LAUNCHER_APP_MACHINE_INFO_H

#include <string>

#include <lancommander/clients/key_client.h>

// Who this machine is, for key allocation.
//
// The server allocates a key against a computer name, an IP and a MAC address,
// and which of those it keys on depends on the game's allocation method. None
// of the three is reliable enough to be the only answer -- a machine with no
// network up has no IP, a DOS box may have no MAC the launcher can see -- so
// each is best-effort and returns "" when it cannot be determined. The server
// rejects the request rather than handing out a key against a blank identity,
// which is the behaviour the recent "keys could be allocated without a valid
// MAC address" fix put in.

namespace launcher
{

    class PlatformMachineInfo : public lancommander::IMachineInfo
    {
    public:
        std::string get_computer_name();
        std::string get_ip_address();
        std::string get_mac_address();
    };

} // namespace launcher

#endif // LAUNCHER_APP_MACHINE_INFO_H
