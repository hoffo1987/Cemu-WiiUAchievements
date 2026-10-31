#pragma once
#include <cstdint>

// Synthetic integration fixture only. These values are not game-file metadata.
namespace CafeSystem
{
    inline std::uint64_t testTitleId = 0x0005000010143600ull;
    inline std::uint16_t testTitleVersion = 0;
    inline std::uint32_t testBaseHash = 0xcdc68acd;
    inline std::uint32_t testUpdatedHash = 0xcdc68acd;
    inline std::uint64_t GetForegroundTitleId() { return testTitleId; }
    inline std::uint16_t GetForegroundTitleVersion() { return testTitleVersion; }
    inline std::uint32_t GetRPXHashBase() { return testBaseHash; }
    inline std::uint32_t GetRPXHashUpdated() { return testUpdatedHash; }
}
