#include <mpi.h>
#include <iostream>
#include <thread>

#include "dsm.h"

void on_variable_change(const std::string& var_name, int value, int rank) {
    std::cout << "Variable " << var_name << " changed to " << value << " in node rank of " << rank << std::endl;
}

int main(int argc, char** argv) {
    MPI_Init(&argc, &argv);

    int rank, size;
    MPI_Comm_rank(MPI_COMM_WORLD, &rank);
    MPI_Comm_size(MPI_COMM_WORLD, &size);

    // Create Distributed Shared Memory instance
    DSM dsm(rank, size);

    // Set callback for when a variable changes
    dsm.set_callback(on_variable_change);

    if (rank == 0) {
        // Subscribe to variables
        dsm.subscribe("var1");
        dsm.subscribe("var2");

        std::cout << "Process 0 writing to var1 and var2" << std::endl;
        dsm.write("var1", 42);  // Process 0 writes to var1
        dsm.compare_and_exchange("var2", INITIALIZED, 100);  // If var2 == 0, set it to 100
    }

    // Listen for updates from other nodes
    while (true) {
        dsm.listen_for_updates();
        std::this_thread::sleep_for(std::chrono::milliseconds(500));

        if (rank == 1) {
            dsm.compare_and_exchange("var1", 42, 1500);  // Process 1 writes to var1
        }
    }

    MPI_Finalize();
    return 0;
}