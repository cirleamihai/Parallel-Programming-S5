#pragma once
#include <map>
#include <vector>
#include <string>
#include <functional>
#include <set>

inline int INITIALIZED = -1;

enum MessageType {
    SUBSCRIBE = 100,
    VALUE_WRITE = 101
};

class DSM {
public:
    DSM(int rank, int size);

    void subscribe(const std::string& var_name, bool broadcast = true);  // Subscribe to variable (local and propagate)
    void broadcast_subscription(const std::string& var_name);  // Send subscriptions to all other processes
    void write(const std::string& var_name, int value);
    bool compare_and_exchange(const std::string& var_name, int expected, int new_value);
    void set_callback(const std::function<void(const std::string&, int, int)>& cb);
    void listen_for_updates();

private:
    int rank;  // Current process rank
    int size;  // Total number of processes
    std::map<std::string, int> variables;  // Local variables
    std::map<std::string, std::set<int>> subscribers;  // Subscribers for each variable
    std::vector<int> other_processes;  // Other processes in the MPI_COMM_WORLD
    std::function<void(const std::string&, int, int)> callback;
};