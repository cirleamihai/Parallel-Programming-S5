cmake_minimum_required(VERSION 3.30)
project(Lab_8)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_FLAGS "-g -O0")

# Enable MPI
find_package(MPI REQUIRED)

message(STATUS "MPI Include Path: ${MPI_CXX_INCLUDE_PATH}")
message(STATUS "MPI Libraries: ${MPI_CXX_LIBRARIES}")

add_executable(Lab_8 main.cpp
        dsm.cpp
        dsm.h)

target_link_libraries(Lab_8 MPI::MPI_CXX)