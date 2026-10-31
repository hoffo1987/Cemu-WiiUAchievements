# Included from Cemu's src/CMakeLists.txt after the Cafe/Gui targets exist.
target_sources(CemuCafe PRIVATE
    ${CMAKE_CURRENT_LIST_DIR}/WiiUAchievementsHost.cpp
    ${CMAKE_CURRENT_LIST_DIR}/WiiUAchievementsHost.h
    ${CMAKE_CURRENT_LIST_DIR}/WiiUAchievementsPacket.h
)
if(WIN32)
    # These CLR hosting headers are in the .NET SDK, not the C++ SDK include
    # path. Visual Studio's .NET Framework 4.8 SDK component supplies both.
    set(WUA_NETFX_SDK_ROOT "" CACHE PATH "Optional .NET Framework SDK root containing Include/um and Lib/um/x64")
    # find_path/find_library results are also cache variables and may be
    # supplied explicitly with -DWUA_NETFX_INCLUDE_DIR / -DWUA_MSCOREE_LIBRARY.
    find_path(WUA_NETFX_INCLUDE_DIR metahost.h
        PATHS
        "${WUA_NETFX_SDK_ROOT}/Include/um"
        "$ENV{ProgramFiles\(x86\)}/Windows Kits/NETFXSDK/4.8.1/Include/um"
        "$ENV{ProgramFiles\(x86\)}/Windows Kits/NETFXSDK/4.8/Include/um"
        REQUIRED)
    find_library(WUA_MSCOREE_LIBRARY mscoree
        PATHS
        "${WUA_NETFX_SDK_ROOT}/Lib/um/x64"
        "$ENV{ProgramFiles\(x86\)}/Windows Kits/NETFXSDK/4.8.1/Lib/um/x64"
        "$ENV{ProgramFiles\(x86\)}/Windows Kits/NETFXSDK/4.8/Lib/um/x64"
        REQUIRED)
    target_include_directories(CemuCafe PRIVATE "${WUA_NETFX_INCLUDE_DIR}")
    target_link_libraries(CemuCafe PRIVATE "${WUA_MSCOREE_LIBRARY}")
endif()
