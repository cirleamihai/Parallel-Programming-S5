#include <mpi.h>
#include <iostream>
#include <vector>
#include <queue>
#include <tuple>     // for std::tuple
#include <algorithm> // for std::fill_n, etc.

using namespace std;

#define V 4   // number of vertices (for your example)
bool graphGlobal[V][V] = {
    {0, 1, 1, 1},
    {1, 0, 1, 0},
    {1, 1, 0, 1},
    {1, 0, 1, 0},
};

static const int TAG_WORK = 1;
static const int TAG_RESULT = 2;
static const int TAG_TERMINATE = 3;

// Helper function: check if it's safe to color `vertex` with color `c`
bool isSafe(int vertex, const vector<int> &coloring, int c) {
    for (int i = 0; i < V; ++i) {
        if (graphGlobal[vertex][i] && coloring[i] == c) {
            return false;
        }
    }
    return true;
}

/**
 * A struct to represent a "partial job" or state:
 *  - 'coloring': partial color assignment
 *  - 'nextVertex': the next vertex index to color
 *    (everything < nextVertex is assigned, everything >= nextVertex is not)
 */
struct WorkItem {
    int coloring[V];
    int nextVertex;
};

// Serialize WorkItem to a simple buffer so we can MPI_Send it
// This is a quick & dirty approach: we just store nextVertex followed by the coloring array.
void packWorkItem(const WorkItem &work, int *buffer) {
    buffer[0] = work.nextVertex;
    for (int i = 0; i < V; i++) {
        buffer[i + 1] = work.coloring[i];
    }
}

// Deserialize WorkItem from buffer
WorkItem unpackWorkItem(const int *buffer) {
    WorkItem w{};
    w.nextVertex = buffer[0];
    for (int i = 0; i < V; i++) {
        w.coloring[i] = buffer[i + 1];
    }
    return w;
}

// Print a solution
void printSolution(const int coloring[V]) {
    cout << "Solution found:\n";
    for (int i = 0; i < V; i++) {
        cout << "\tVertex " << i << " -> color " << coloring[i] << "\n";
    }
    cout << endl;
}

// Worker process logic
void workerCode(int rank, int numProcs, int m) {
    bool done = false;

    while (!done) {
        // Probe for any incoming message
        MPI_Status status;
        MPI_Probe(MPI_ANY_SOURCE, MPI_ANY_TAG, MPI_COMM_WORLD, &status);

        int tag = status.MPI_TAG;

        if (tag == TAG_TERMINATE) {
            // Master told us to stop
            done = true;
            // Drain the message
            MPI_Recv(nullptr, 0, MPI_INT, status.MPI_SOURCE, tag, MPI_COMM_WORLD, MPI_STATUS_IGNORE);
        }
        else if (tag == TAG_WORK) {
            // We got work to do
            // 1) figure out how large the message is
            int count;
            MPI_Get_count(&status, MPI_INT, &count);
            vector<int> buffer(count);
            // 2) actually receive the work
            MPI_Recv(buffer.data(), count, MPI_INT, status.MPI_SOURCE, tag, MPI_COMM_WORLD, MPI_STATUS_IGNORE);

            // Unpack the partial state
            WorkItem w = unpackWorkItem(buffer.data());

            // If nextVertex == V, we have a complete solution
            if (w.nextVertex == V) {
                // Found a solution
                // Send it back to master (or we could broadcast a terminate)
                MPI_Send(buffer.data(), count, MPI_INT, 0, TAG_RESULT, MPI_COMM_WORLD);
                // We can continue or just wait for a terminate signal from master
                // We'll continue the loop, but eventually the master will send TAG_TERMINATE
            }
            else {
                // Try all m color assignments for w.nextVertex
                for (int c = 1; c <= m; ++c) {
                    if (isSafe(w.nextVertex, vector<int>(begin(w.coloring), end(w.coloring)), c)) {
                        WorkItem newWork = w;
                        newWork.coloring[w.nextVertex] = c;
                        newWork.nextVertex = w.nextVertex + 1;

                        // Send partial solution back to master for distribution or direct recursion
                        const int sz = V + 1;
                        int sendBuf[sz];
                        packWorkItem(newWork, sendBuf);
                        // We'll just send it to rank 0 so it can manage the queue
                        MPI_Send(sendBuf, sz, MPI_INT, 0, TAG_WORK, MPI_COMM_WORLD);
                    }
                }
            }
        }
        else {
            // Unexpected, just drain it
            MPI_Recv(nullptr, 0, MPI_INT, status.MPI_SOURCE, tag, MPI_COMM_WORLD, &status);
        }
    }
}

// Master process logic
void masterCode(int rank, int numProcs, int m) {
    // We'll keep a queue of partial solutions
    queue<WorkItem> workQueue;

    // Initialize partial solutions. Let's start with vertex=0
    // Try each color for the first vertex
    for (int c = 1; c <= m; ++c) {
        WorkItem w{};
        fill_n(w.coloring, V, 0);
        w.coloring[0] = c;
        w.nextVertex = 1;
        workQueue.push(w);
    }

    // Use a simple round-robin to send initial jobs
    // In a real app, you'd dynamically pop from the queue and send to whichever worker is idle
    // but for brevity, we’ll do a simple approach.
    // We keep track of how many workers are actually available (numProcs-1)
    // because rank 0 is the master
    int activeWorkers = numProcs - 1;

    // If there are no workers (e.g. mpirun -np 1), trivial fallback
    if (activeWorkers < 1) {
        cout << "No workers available. Exiting." << endl;
        return;
    }

    // We’ll store any partial solutions that come back from workers to the master
    // so that the master can re-distribute them or check if they're complete solutions.
    bool solutionFound = false;
    WorkItem solutionWork{};

    while (!solutionFound) {
        // Distribute as many items in the queue as we can, one per worker
        // (or all to a single worker, whichever pattern you prefer).
        // For a more robust approach, we’d do non-blocking sends & track worker availability.
        // This is a simplified approach:
        for (int wRank = 1; wRank <= activeWorkers; ++wRank) {
            if (!workQueue.empty()) {
                WorkItem w = workQueue.front();
                workQueue.pop();
                // Send it
                int buffer[V+1];
                packWorkItem(w, buffer);
                MPI_Send(buffer, V+1, MPI_INT, wRank, TAG_WORK, MPI_COMM_WORLD);
            }
            else {
                // No more immediate partial solutions in queue
                break;
            }
        }

        // Now we wait for either:
        //  1) a worker to return a full solution (TAG_RESULT), or
        //  2) a worker to return partial solutions (TAG_WORK), or
        //  3) we realize the queue is empty and no new solutions are coming => no solution
        //
        // For simplicity, let's do a single MPI_Probe to see what's up.

        // If there's no immediate message and queue is empty, we might be done
        // But let's do a small check to see if we can get a message with MPI_Iprobe
        MPI_Status status;
        int flag = 0;
        MPI_Iprobe(MPI_ANY_SOURCE, MPI_ANY_TAG, MPI_COMM_WORLD, &flag, &status);

        if (!flag) {
            // No message right now. Are we done?
            if (workQueue.empty()) {
                // We have no partial solutions left to distribute
                // So there's no new work to be discovered
                // => either a solution is found soon or none is possible
                // Let’s do an MPI_Probe that blocks or a small sleep
                // But to keep it simpler, we could block waiting for a message:
                MPI_Probe(MPI_ANY_SOURCE, MPI_ANY_TAG, MPI_COMM_WORLD, &status);
            }
            else {
                // We still have partial solutions in queue, so just continue the outer loop
                continue;
            }
        }

        // Now we definitely have a message, receive it
        int tag = status.MPI_TAG;
        int sender = status.MPI_SOURCE;

        if (tag == TAG_RESULT) {
            // A worker found a complete solution
            int count;
            MPI_Get_count(&status, MPI_INT, &count);
            vector<int> buffer(count);
            MPI_Recv(buffer.data(), count, MPI_INT, sender, tag, MPI_COMM_WORLD, MPI_STATUS_IGNORE);

            WorkItem w = unpackWorkItem(buffer.data());
            solutionFound = true;
            solutionWork = w;
        }
        else if (tag == TAG_WORK) {
            // This means a worker is returning new partial solutions
            int count;
            MPI_Get_count(&status, MPI_INT, &count);
            vector<int> buffer(count);
            MPI_Recv(buffer.data(), count, MPI_INT, sender, tag, MPI_COMM_WORLD, MPI_STATUS_IGNORE);

            // That’s 1 partial solution in this simplified approach
            WorkItem w = unpackWorkItem(buffer.data());

            // If nextVertex == V => complete solution
            if (w.nextVertex == V) {
                // We found a solution
                solutionFound = true;
                solutionWork = w;
            } else {
                // Just push it to our local queue
                workQueue.push(w);
            }
        }
        else {
            // Possibly TAG_TERMINATE from some other reason, or unexpected
            // Let's just drain it
            MPI_Recv(nullptr, 0, MPI_INT, sender, tag, MPI_COMM_WORLD, MPI_STATUS_IGNORE);
        }

        // Check if we found a solution
        if (solutionFound) {
            // Broadcast a terminate message to all workers
            for (int wRank = 1; wRank < numProcs; ++wRank) {
                MPI_Send(nullptr, 0, MPI_INT, wRank, TAG_TERMINATE, MPI_COMM_WORLD);
            }
            // Print the solution
            printSolution(solutionWork.coloring);
            return;
        }

        // If queue is empty and we never found a solution => no solution
        // But let's not do that until we are sure no worker is in flight
        // (In a real code, we'd track how many in-flight tasks exist.)
        // For the tiny example, we can check again at the top of the loop.

        // This loop then continues until we find a solution or exhaust all possibilities.
    }

    // If we ever exit while(!solutionFound) loop without returning, that means no solution was found
    // So we broadcast terminate
    for (int wRank = 1; wRank < numProcs; ++wRank) {
        MPI_Send(nullptr, 0, MPI_INT, wRank, TAG_TERMINATE, MPI_COMM_WORLD);
    }
    cout << "No solution found.\n";
}

int main(int argc, char** argv) {
    MPI_Init(&argc, &argv);

    int rank, numProcs;
    MPI_Comm_rank(MPI_COMM_WORLD, &rank);
    MPI_Comm_size(MPI_COMM_WORLD, &numProcs);

    // number of colors
    int m = 3; // try 3 for your graph

    if (rank == 0) {
        // master
        masterCode(rank, numProcs, m);
    } else {
        // worker
        workerCode(rank, numProcs, m);
    }

    MPI_Finalize();
    return 0;
}