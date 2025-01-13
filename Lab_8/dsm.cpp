#include "dsm.h"
#include <iostream>
#include <mpi.h>

DSM::DSM(int rank, int size) : rank(rank), size(size) {
    std::cout << "DSM constructor called" << std::endl << "Rank: " << rank << std::endl << "Size: " << size <<
            std::endl;

    for (int i = 0; i < size; ++i) {
        // Initialize all the other_processes
        if (i != rank) {
            other_processes.push_back(i);
        }
    }
}

void DSM::subscribe(const std::string &var_name, bool broadcast) {
    subscribers[var_name].insert(rank); // Add this process as a subscriber
    variables[var_name] = INITIALIZED; // Initialize the variable

    // Subscribe to the variable in all other processes
    for (int process_rank: other_processes) {
        subscribers[var_name].insert(process_rank);
    }

    if (!broadcast) {
        return;
    }

    // Notify all other processes that this process is subscribing to the variable
    broadcast_subscription(var_name);
}


void DSM::broadcast_subscription(const std::string &var_name) {
    int var_name_length = var_name.length();
    const char *var_name_cstr = var_name.c_str();

    // By default, we assume that the subscribers
    // are going to be the whole rest of processes
    for (int subscriber_rank: other_processes) {
        if (subscriber_rank != rank) {
            MPI_Send(var_name_cstr, var_name_length, MPI_CHAR, subscriber_rank, SUBSCRIBE, MPI_COMM_WORLD);
        }
    }
}

void DSM::write(const std::string &var_name, int value) {
    std::cout << "Previous value: " << variables[var_name] << "Writing to variable: " << var_name << " with value: " <<
            value << std::endl;

    variables[var_name] = value; // Write locally
    int var_name_length = var_name.length();
    const char *var_name_cstr = var_name.c_str();

    // Notify all subscribers
    for (int process_rank: subscribers[var_name]) {
        if (process_rank != rank) {
            // first, we send the variable name such that the receiver can update its local copy
            MPI_Send(var_name_cstr, var_name_length, MPI_CHAR, process_rank, VALUE_WRITE, MPI_COMM_WORLD);

            // then we send the updated value
            MPI_Send(&value, 1, MPI_INT, process_rank, VALUE_WRITE, MPI_COMM_WORLD);
        }
    }
}

bool DSM::compare_and_exchange(const std::string &var_name, int expected, int new_value) {
    std::cerr << "Searching for variable: '" << var_name << "'\n";
    for (const auto &[key, value]: variables) {
        std::cerr << "Existing variable: '" << key << "'\n";
    }

    if (variables[var_name] == expected) {
        write(var_name, new_value);
        return true;
    }
    return false;
}

void DSM::set_callback(const std::function<void(const std::string &, int, int)> &cb) {
    callback = cb;
}

void DSM::listen_for_updates() {
    MPI_Status status;
    MPI_Probe(MPI_ANY_SOURCE, MPI_ANY_TAG, MPI_COMM_WORLD, &status);

    if (status.MPI_TAG == SUBSCRIBE) {
        int var_name_length;
        MPI_Get_count(&status, MPI_CHAR, &var_name_length);
        char var_name_cstr[var_name_length + 1];

        MPI_Recv(var_name_cstr, var_name_length, MPI_CHAR, status.MPI_SOURCE, SUBSCRIBE, MPI_COMM_WORLD, &status);
        var_name_cstr[var_name_length] = '\0'; // Ensure null-termination
        std::string var_name(var_name_cstr, var_name_length);

        // Add the subscriber to the list of subscribers
        subscribe(var_name, false);
    } else if (status.MPI_TAG == VALUE_WRITE) {
        int var_name_length;
        MPI_Get_count(&status, MPI_CHAR, &var_name_length);
        char var_name_cstr[var_name_length + 1];

        // First, we receive the variable name
        MPI_Recv(var_name_cstr, var_name_length, MPI_CHAR, status.MPI_SOURCE, VALUE_WRITE, MPI_COMM_WORLD, &status);
        var_name_cstr[var_name_length] = '\0'; // Ensure null-termination
        std::string var_name(var_name_cstr, var_name_length);

        // Then, we receive the value
        int value;
        MPI_Recv(&value, 1, MPI_INT, status.MPI_SOURCE, VALUE_WRITE, MPI_COMM_WORLD, &status);

        // then we update the local copy
        variables[var_name] = value;

        // finally, we call the callback
        callback(var_name, value, rank);
    } else {
        std::cout << "Unknown message type" << std::endl;
    }
}
