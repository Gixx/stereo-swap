cmake_minimum_required(VERSION 3.20)
project(StereoSwapApo LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Static CRT — fewer loader deps inside audiodg.
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

add_library(StereoSwapApo SHARED
    src/StereoSwapApo.cpp
    src/DllMain.cpp
    src/StereoSwapApo.def
)

target_include_directories(StereoSwapApo PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_compile_definitions(StereoSwapApo PRIVATE
    UNICODE
    _UNICODE
    WIN32
    _WINDOWS
    _USRDLL
)

if(DEFINED ENV{WindowsSdkDir})
    message(STATUS "WindowsSdkDir=$ENV{WindowsSdkDir}")
endif()

target_link_libraries(StereoSwapApo PRIVATE
    ole32
    oleaut32
)

# Embedded manifests can break loading inside Protected Audio DG (audiodg).
set_target_properties(StereoSwapApo PROPERTIES
    OUTPUT_NAME "StereoSwapApo"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    LINK_FLAGS "/MANIFEST:NO"
)
