cmake_minimum_required(VERSION 3.20)
project(StereoSwapApo LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

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

# MinimalApoBase (include/) is the MVP stand-in; Windows SDK supplies audioenginebaseapo.h.
if(DEFINED ENV{WindowsSdkDir})
    message(STATUS "WindowsSdkDir=$ENV{WindowsSdkDir}")
endif()

target_link_libraries(StereoSwapApo PRIVATE
    ole32
    oleaut32
    shlwapi
    shell32
)

set_target_properties(StereoSwapApo PROPERTIES
    OUTPUT_NAME "StereoSwapApo"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)
