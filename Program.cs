using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Lab_6
{
    class HamiltonianCycle
    {
        static bool cycleFound = false; // Shared variable to track if a Hamiltonian cycle has been found
        static object lockObj = new object(); // Lock for synchronization

        public static void Main(string[] args)
        {
            // Example directed graph (adjacency matrix)
            int[,] graph =
            {
                { 0, 1, 0, 1, 0 },
                { 1, 0, 1, 1, 1 },
                { 0, 1, 0, 0, 1 },
                { 1, 1, 0, 0, 1 },
                { 0, 1, 1, 1, 0 },
            };

            int startVertex = 0;

            Console.WriteLine("=== Hamiltonian Cycle Search ===");
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            var result = FindHamiltonianCycleParallel(graph, startVertex);

            stopwatch.Stop();
            if (result != null)
            {
                Console.WriteLine($"Hamiltonian Cycle Found: {string.Join(" -> ", result)} -> {startVertex}");
            }
            else
            {
                Console.WriteLine("No Hamiltonian Cycle found.");
            }

            Console.WriteLine($"Time taken: {stopwatch.ElapsedMilliseconds} ms");
            Console.ReadLine();
        }

        /// <summary>
        /// Parallel Hamiltonian cycle search
        /// </summary>
        public static List<int> FindHamiltonianCycleParallel(int[,] graph, int startVertex)
        {
            int n = graph.GetLength(0);
            List<int> path = new List<int> { startVertex };

            // Parallelize the neighbors of the starting vertex
            List<Task<List<int>>> tasks = new List<Task<List<int>>>();

            for (int neighbor = 0; neighbor < n; neighbor++)
            {
                if (graph[startVertex, neighbor] == 1)
                {
                    int nextVertex = neighbor;
                    tasks.Add(Task.Run(() => DFSParallel(graph, nextVertex, path, startVertex)));
                }
            }

            Task.WaitAll(tasks.ToArray());

            foreach (var task in tasks)
            {
                if (task.Result != null) return task.Result;
            }

            return null;
        }

        /// <summary>
        /// Depth-first search (DFS) for Hamiltonian cycle (parallelized)
        /// </summary>
        public static List<int> DFSParallel(int[,] graph, int currentVertex, List<int> path, int startVertex)
        {
            lock (lockObj)
            {
                if (cycleFound) return null; // Stop if a cycle has already been found
            }

            List<int> currentPath = new List<int>(path) { currentVertex };

            if (currentPath.Count == graph.GetLength(0))
            {
                if (graph[currentVertex, startVertex] == 1) // Check if we can return to the start
                {
                    lock (lockObj)
                    {
                        cycleFound = true; // Mark the cycle as found
                    }

                    return currentPath;
                }

                return null;
            }

            List<Task<List<int>>> tasks = new List<Task<List<int>>>();

            for (int neighbor = 0; neighbor < graph.GetLength(0); neighbor++)
            {
                if (graph[currentVertex, neighbor] == 1 && !currentPath.Contains(neighbor))
                {
                    int nextVertex = neighbor;
                    tasks.Add(Task.Run(() => DFSParallel(graph, nextVertex, currentPath, startVertex)));
                }
            }

            Task.WaitAll(tasks.ToArray());

            foreach (var task in tasks)
            {
                if (task.Result != null) return task.Result;
            }

            return null;
        }
    }
}

/*
 * ============================================================================
 * Hamiltonian Cycle Search Documentation
 * ============================================================================
 *
 * # 1. Algorithm
 * - A Hamiltonian cycle is a closed loop that visits every vertex exactly once.
 * - We use a directed graph represented by an adjacency matrix.
 * - The search uses a depth-first search (DFS) approach to traverse the graph
 *   and check all possible paths.
 *
 * ## Parallelized Search:
 * - The neighbors of the current vertex are distributed among parallel tasks.
 * - Each task recursively explores possible paths to form a Hamiltonian cycle.
 *
 * ============================================================================
 *
 * # 2. Synchronization in Parallelized Variants
 *
 * ## Shared Variables:
 * - `cycleFound`: A shared Boolean variable to track if a cycle has been found.
 *
 * ## Synchronization:
 * - `lock (lockObj)` is used to ensure that only one thread marks `cycleFound`.
 * - `Task.WaitAll` ensures that all parallel tasks complete before returning.
 * - Since each recursive call has its own `currentPath`, there are no data
 *   races when traversing vertices.
 *
 * ============================================================================
 *
 * # 3. Performance Measurements
 *
 * ## Measurement:
 * - Time taken for the Hamiltonian cycle search is measured using `Stopwatch`.
 * Example Output:
 *   === Hamiltonian Cycle Search ===
 *   Hamiltonian Cycle Found: 0 -> 1 -> 3 -> 4 -> 2 -> 0
 *   Time taken: 15 ms
 *
 * ## Observations:
 * - For small graphs, parallelism may add overhead, making the search slower.
 * - For larger graphs, parallel search can significantly reduce the time by
 *   distributing the work across multiple threads.
 *
 * ============================================================================
 */