#include "app/machine_info.h"

#include <cstdio>
#include <cstdlib>
#include <cstring>

#ifdef _WIN32
// winsock2.h must come before windows.h, which otherwise drags in the Winsock
// 1.1 header and every socket type is then defined twice. LEAN_AND_MEAN keeps
// windows.h from pulling it in at all.
#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <iphlpapi.h>
#include <windows.h>
#endif

namespace launcher
{

#ifdef _WIN32
    namespace
    {
        typedef DWORD(WINAPI *GetAdaptersInfoFn)(PIP_ADAPTER_INFO, PULONG);

        // IP Helper is not on Windows 95, and it is not on a stripped 98
        // either. Linking iphlpapi.lib would make the launcher refuse to start
        // there rather than simply not knowing its MAC address, so it is
        // loaded by hand and its absence is just an empty answer.
        //
        // Resolved once: the answer does not change while the process lives,
        // and key allocation asks for it on every launch.
        GetAdaptersInfoFn resolve_get_adapters_info()
        {
            static bool tried = false;
            static GetAdaptersInfoFn fn = NULL;

            if (!tried)
            {
                tried = true;

                HMODULE module = LoadLibraryA("iphlpapi.dll");
                if (module)
                    fn = (GetAdaptersInfoFn)GetProcAddress(module, "GetAdaptersInfo");
            }

            return fn;
        }

        // The adapter table, or NULL. Caller frees with delete[].
        IP_ADAPTER_INFO *adapter_table()
        {
            GetAdaptersInfoFn get_adapters = resolve_get_adapters_info();
            if (!get_adapters)
                return NULL;

            ULONG size = 0;
            if (get_adapters(NULL, &size) != ERROR_BUFFER_OVERFLOW || size == 0)
                return NULL;

            IP_ADAPTER_INFO *adapters = (IP_ADAPTER_INFO *)new char[size];

            if (get_adapters(adapters, &size) != NO_ERROR)
            {
                delete[] (char *)adapters;
                return NULL;
            }

            return adapters;
        }

        bool is_usable(const IP_ADAPTER_INFO &adapter)
        {
            return adapter.Type != MIB_IF_TYPE_LOOPBACK;
        }
    } // namespace
#endif

    std::string PlatformMachineInfo::get_computer_name()
    {
#ifdef _WIN32
        char name[MAX_COMPUTERNAME_LENGTH + 1];
        DWORD size = sizeof(name);

        if (GetComputerNameA(name, &size))
            return std::string(name, size);

        return std::string();
#else
        // DOS has no computer name of its own. The TCP/IP stack's host name is
        // what the user configured, so it is a more useful identity than an
        // empty string.
        const char *host = getenv("HOSTNAME");
        return host ? std::string(host) : std::string();
#endif
    }

    std::string PlatformMachineInfo::get_ip_address()
    {
#ifdef _WIN32
        // Read from the adapter table rather than through Winsock: it needs no
        // WSAStartup, no ws2_32 import, and it answers for the same adapter
        // the MAC address comes from, which is what the server is matching on.
        IP_ADAPTER_INFO *adapters = adapter_table();
        if (!adapters)
            return std::string();

        std::string address;

        for (IP_ADAPTER_INFO *a = adapters; a != NULL; a = a->Next)
        {
            if (!is_usable(*a))
                continue;

            const char *ip = a->IpAddressList.IpAddress.String;

            // An adapter that is present but not configured reports 0.0.0.0.
            if (!ip || !*ip || strcmp(ip, "0.0.0.0") == 0)
                continue;

            address = ip;
            break;
        }

        delete[] (char *)adapters;

        return address;
#else
        return std::string();
#endif
    }

    std::string PlatformMachineInfo::get_mac_address()
    {
#ifdef _WIN32
        // The first non-loopback adapter with a six-byte hardware address.
        // Which adapter that is can change between boots, which is exactly why
        // the key file is authoritative over the server: asking for a fresh
        // allocation on every launch would hand the game a different key each
        // time this answer moved.
        IP_ADAPTER_INFO *adapters = adapter_table();
        if (!adapters)
            return std::string();

        std::string mac;

        for (IP_ADAPTER_INFO *a = adapters; a != NULL; a = a->Next)
        {
            if (!is_usable(*a) || a->AddressLength != 6)
                continue;

            char text[18];
            sprintf(text, "%02X:%02X:%02X:%02X:%02X:%02X",
                    a->Address[0], a->Address[1], a->Address[2],
                    a->Address[3], a->Address[4], a->Address[5]);
            mac = text;
            break;
        }

        delete[] (char *)adapters;

        return mac;
#else
        return std::string();
#endif
    }

} // namespace launcher
