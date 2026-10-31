#pragma once
#include <cstddef>
#include <cstdint>

namespace WiiUAchievements
{
    // Fixed little-endian transport header; guest buffers remain big-endian.
    // Only exchanged in-process, never persisted or sent over the network.
#pragma pack(push, 1)
    struct Packet
    {
        std::uint32_t protocol = 1;
        std::uint32_t state = 0; // stopped=0, running=1, paused=2
        std::uint64_t generation = 0;
        std::uint64_t milliseconds = 0;
        std::uint64_t titleId = 0;
        std::uint32_t titleVersion = 0;
        std::uint32_t rpxHash = 0;
        std::uint8_t beforeStage[26]{};
        std::uint8_t beforePlayers[16]{};
        std::uint8_t save[0x724]{};
        std::uint8_t afterStage[26]{};
        std::uint8_t afterPlayers[16]{};
    };
#pragma pack(pop)

    static_assert(sizeof(Packet) == 1952);
    static_assert(offsetof(Packet, beforeStage) == 40);
    static_assert(offsetof(Packet, beforePlayers) == 66);
    static_assert(offsetof(Packet, save) == 82);
    static_assert(offsetof(Packet, afterStage) == 1910);
    static_assert(offsetof(Packet, afterPlayers) == 1936);
}
