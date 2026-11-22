#include "devices.h"
#include "json_output.h"

#include <algorithm>
#include <cstdio>
#include <cstring>
#include <exception>

int main(int argc, char** argv) {
    if (argc != 2 || std::strcmp(argv[1], "--json") != 0) {
        std::fputs("Usage: BatteryProtocols --json\n", stderr);
        return 2;
    }
    try {
        auto devices = PollAllDevices();
        devices.erase(std::remove_if(devices.begin(), devices.end(), [](const auto& device) {
            return device.unsupported;
        }), devices.end());
        const auto json = DevicesToJson(devices);
        const bool written = std::fwrite(json.data(), 1, json.size(), stdout) == json.size();
        ShutdownDevices();
        return written ? 0 : 1;
    } catch (const std::exception& error) {
        std::fprintf(stderr, "%s\n", error.what());
        ShutdownDevices();
        return 1;
    }
}
