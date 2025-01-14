#include <iostream>
#include <mutex>
#include <thread>

using namespace std;


#define V 4
std::mutex mtx;
bool foundSol = false;

void printSolution(int color[]);

bool isSafe(const int outgoing_vertex, bool graph[V][V], const int color[], const int c) {
    for (int i = 0; i < V; i++) {
        if (graph[outgoing_vertex][i] && c == color[i]) {
            return false;
        }
    }
    return true;
}

void graphColoringUtil(bool graph[V][V], const int m, int color[], const int outgoing_vertex) { {
        std::lock_guard lock(mtx); // Lock when accessing shared variable
        if (foundSol) {
            return;
        }
    }

    if (outgoing_vertex == V) {
        std::lock_guard lock(mtx); // Lock when accessing shared variable
        foundSol = true; // Mark solution found
        printSolution(color); // Print solution
        return;
    }

    std::vector<std::thread> threads;

    for (int i = 1; i <= m; ++i) {
        if (isSafe(outgoing_vertex, graph, color, i)) {
            threads.emplace_back([=]() mutable {
                // Capture `i` by value
                int thread_bounded_color[V]; // Create a copy of the color array
                for (int j = 0; j < V; ++j) {
                    thread_bounded_color[j] = color[j];
                }
                thread_bounded_color[outgoing_vertex] = i;
                graphColoringUtil(graph, m, thread_bounded_color, outgoing_vertex + 1);
            });
        }
    }

    // Join all threads
    for (auto &t: threads) {
        t.join(); // Wait for all spawned threads to finish
    }
}

void nGraphColoringProblem(bool graph[V][V], const int m) {
    int color[V] = {0}; // Initialize all the colors of the vertices as 0

    graphColoringUtil(graph, m, color, 0);

    if (!foundSol) {
        std::cout << "Solution does not exist" << std::endl;
    }
}

/* A utility function to print solution */
void printSolution(int color[]) {
    std::cout << "Solution Exists:\n";

    for (int i = 0; i < V; i++) {
        std::cout << "\tFor vertex " << i << " color is " << color[i] << "\n";
    }

    std::cout << "\n";
}

// Driver code
int main() {
    /* Create following graph and test
       whether it is 3 colorable
      (3)---(2)
       |   / |
       |  /  |
       | /   |
      (0)---(1)
    */
    bool graph[V][V] = {
        {0, 1, 1, 1},
        {1, 0, 1, 0},
        {1, 1, 0, 1},
        {1, 0, 1, 0},
    };

    // Number of colors
    int m = 3;

    // Function call
    nGraphColoringProblem(graph, m);
    return 0;
}