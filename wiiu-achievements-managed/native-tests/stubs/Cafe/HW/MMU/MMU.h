#pragma once
#include <cstddef>
#include <cstdint>

namespace NativeSmokeMemory
{
    inline std::uint8_t stage[26]{};
    inline std::uint8_t players[16]{};
    inline std::uint8_t save[0x724]{};
}
inline bool memory_isAddressRangeAccessible(std::uint32_t address, std::size_t length)
{
    return (address == 0x104741e4 && length <= sizeof(NativeSmokeMemory::stage)) ||
        (address == 0x10474bdc && length <= sizeof(NativeSmokeMemory::players)) ||
        (address == 0x145b7b80 && length <= sizeof(NativeSmokeMemory::save));
}
inline void* memory_getPointerFromVirtualOffset(std::uint32_t address)
{
    if (address == 0x104741e4) return NativeSmokeMemory::stage;
    if (address == 0x10474bdc) return NativeSmokeMemory::players;
    if (address == 0x145b7b80) return NativeSmokeMemory::save;
    return nullptr;
}
