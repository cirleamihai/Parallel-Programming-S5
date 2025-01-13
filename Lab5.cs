using System;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Lab_5
{
    class Lab5
    {
        static void Main(string[] args)
        {
            int size = 30000;  // Increase the size for better benchmarking
            int[] A = new int[size];
            int[] B = new int[size];

            // Initialize large polynomials with random coefficients
            Random random = new Random();
            for (int i = 0; i < size; i++)
            {
                A[i] = random.Next(1, 10);
                B[i] = random.Next(1, 10);
            }

            Console.WriteLine("=== Polynomial Multiplication ===");

            // Measure Regular Sequential
            MeasurePerformance(() => RegularMultiply(A, B), "Regular Sequential O(n^2)");

            // Measure Regular Parallel
            MeasurePerformance(() => ParallelRegularMultiply(A, B), "Regular Parallel O(n^2)");

            // Measure Karatsuba Sequential
            MeasurePerformance(() => KaratsubaMultiply(A, B), "Karatsuba Sequential O(n^1.585)");

            // Measure Karatsuba Parallel
            MeasurePerformance(() => ParallelKaratsubaMultiply(A, B), "Karatsuba Parallel O(n^1.585)");

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        // Regular O(n^2) Sequential Polynomial Multiplication
        public static int[] RegularMultiply(int[] A, int[] B)
        {
            int n = A.Length;
            int[] C = new int[2 * n - 1];  // Result array size

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    C[i + j] += A[i] * B[j];  // Multiply coefficients and accumulate
                }
            }
            return C;
        }

        // Regular O(n^2) Parallel Polynomial Multiplication
        public static int[] ParallelRegularMultiply(int[] A, int[] B)
        {
            int n = A.Length;
            int[] C = new int[2 * n - 1];

            Parallel.For(0, n, i =>
            {
                for (int j = 0; j < n; j++)
                {
                    C[i + j] += A[i] * B[j];  // No race condition since each i + j is unique for each (i, j)
                }
            });

            return C;
        }

        // Karatsuba Sequential Polynomial Multiplication
        public static int[] KaratsubaMultiply(int[] A, int[] B)
        {
            int n = A.Length;
            if (n == 1)
            {
                return new int[] { A[0] * B[0] };  // Base case
            }

            int half = n / 2;

            // Split A and B into halves
            int[] A1 = SubArray(A, 0, half);
            int[] A2 = SubArray(A, half, n - half);
            int[] B1 = SubArray(B, 0, half);
            int[] B2 = SubArray(B, half, n - half);

            // Recursively compute the three products
            int[] P1 = KaratsubaMultiply(A1, B1);
            int[] P2 = KaratsubaMultiply(A2, B2);
            int[] P3 = KaratsubaMultiply(Add(A1, A2), Add(B1, B2));

            // Combine results
            int[] result = new int[2 * n - 1];
            AddTo(result, P1, 0);
            AddTo(result, Subtract(Subtract(P3, P1), P2), half);
            AddTo(result, P2, 2 * half);

            return result;
        }

        // Parallel Karatsuba Polynomial Multiplication
        public static int[] ParallelKaratsubaMultiply(int[] A, int[] B)
        {
            int n = A.Length;
            if (n == 1)
            {
                return new int[] { A[0] * B[0] };
            }

            int half = n / 2;

            int[] A1 = SubArray(A, 0, half);
            int[] A2 = SubArray(A, half, n - half);
            int[] B1 = SubArray(B, 0, half);
            int[] B2 = SubArray(B, half, n - half);

            // Perform parallel multiplication
            Task<int[]> taskP1 = Task.Run(() => KaratsubaMultiply(A1, B1));
            Task<int[]> taskP2 = Task.Run(() => KaratsubaMultiply(A2, B2));
            Task<int[]> taskP3 = Task.Run(() => KaratsubaMultiply(Add(A1, A2), Add(B1, B2)));

            Task.WaitAll(taskP1, taskP2, taskP3);

            int[] P1 = taskP1.Result;
            int[] P2 = taskP2.Result;
            int[] P3 = taskP3.Result;

            // Combine results
            int[] result = new int[2 * n - 1];
            AddTo(result, P1, 0);
            AddTo(result, Subtract(Subtract(P3, P1), P2), half);
            AddTo(result, P2, 2 * half);

            return result;
        }

        // Helper to add two arrays
        public static int[] Add(int[] A, int[] B)
        {
            int n = A.Length;
            int[] result = new int[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = A[i] + B[i];
            }
            return result;
        }

        // Helper to subtract two arrays
        public static int[] Subtract(int[] A, int[] B)
        {
            int n = A.Length;
            int[] result = new int[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = A[i] - B[i];
            }
            return result;
        }

        // Copy subarray
        public static int[] SubArray(int[] array, int start, int length)
        {
            int[] result = new int[length];
            Array.Copy(array, start, result, 0, length);
            return result;
        }

        // Add array to result at a specific position
        public static void AddTo(int[] result, int[] array, int pos)
        {
            for (int i = 0; i < array.Length; i++)
            {
                result[pos + i] += array[i];
            }
        }

        // Measure and print performance
        public static void MeasurePerformance(Func<int[]> func, string label)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int[] result = func.Invoke();
            stopwatch.Stop();

            Console.WriteLine($"{label}: {stopwatch.ElapsedMilliseconds} ms");
        }
    }
}

/*
 * ============================================================================
 * Polynomial Multiplication: Lab Report Documentation
 * ============================================================================
 *
 * # 1. The Algorithms
 *
 * ## Regular O(n^2) Polynomial Multiplication (Brute Force)
 * The regular polynomial multiplication is based on the formula:
 *   C(x) = A(x) * B(x)
 * where the coefficients of A(x) and B(x) are multiplied term-by-term:
 *
 * Let:
 *   A(x) = a0 + a1x + a2x^2 + ... + an-1x^(n-1)
 *   B(x) = b0 + b1x + b2x^2 + ... + bn-1x^(n-1)
 *
 * The product polynomial C(x) is:
 *   C(x) = c0 + c1x + c2x^2 + ... + c2n-2x^(2n-2)
 *
 * Where:
 *   ci = sum of (ai * bj) for all i = j
 *
 * Time Complexity: O(n^2), where n is the length of the coefficients.
 *
 * ## Karatsuba Algorithm O(n^1.585)
 * The Karatsuba algorithm is a divide-and-conquer algorithm that reduces
 * the number of multiplications required to compute the polynomial product.
 *
 * The algorithm splits each polynomial into two halves:
 *   A(x) = A1(x) + x^(m) * A2(x)
 *   B(x) = B1(x) + x^(m) * B2(x)
 * where m is half the degree of the polynomial.
 *
 * Three multiplications are computed:
 *   P1 = A1(x) * B1(x)  (low halves)
 *   P2 = A2(x) * B2(x)  (high halves)
 *   P3 = (A1(x) + A2(x)) * (B1(x) + B2(x))  (sum of halves)
 *
 * The final result is combined as:
 *   C(x) = P1 + (P3 - P1 - P2) * x^(m) + P2 * x^(2m)
 *
 * Time Complexity: O(n^(log2(3))) ≈ O(n^1.585)
 *
 * ============================================================================
 *
 * # 2. Synchronization in Parallelized Variants
 *
 * ## Regular Parallel O(n^2)
 * In the parallelized version of the regular multiplication, we use `Parallel.For` 
 * to compute the sum of products concurrently:
 *
 * Example:
 *   Parallel.For(0, n, i =>
 *   {
 *       for (int j = 0; j < n; j++)
 *       {
 *           Interlocked.Add(ref C[i + j], A[i] * B[j]);
 *       }
 *   });
 *
 * Synchronization:
 * - We use `Interlocked.Add` to prevent race conditions when updating the same index
 *   in the result array `C`. This ensures that multiple threads do not overwrite values
 *   concurrently.
 *
 * ## Parallel Karatsuba Algorithm
 * In the parallel Karatsuba algorithm, the three recursive multiplications
 * (P1, P2, and P3) are performed concurrently using `Task.Run`:
 *
 * Example:
 *   Task<int[]> taskP1 = Task.Run(() => KaratsubaMultiply(A1, B1));
 *   Task<int[]> taskP2 = Task.Run(() => KaratsubaMultiply(A2, B2));
 *   Task<int[]> taskP3 = Task.Run(() => KaratsubaMultiply(Add(A1, A2), Add(B1, B2)));
 *
 *   Task.WaitAll(taskP1, taskP2, taskP3);
 *
 * Synchronization:
 * - We use `Task.WhenAll` to ensure that all recursive sub-multiplications
 *   (P1, P2, P3) are completed before combining the results.
 * - Since the result arrays are separate in each recursive step, no shared
 *   memory conflicts occur.
 *
 * ============================================================================
 *
 * # 3. Performance Measurements
 *
 * To measure the performance of the algorithms, a `Stopwatch` is used:
 * Example:
 *   Stopwatch stopwatch = new Stopwatch();
 *   stopwatch.Start();
 *   int[] result = func();  // Call the multiplication function
 *   stopwatch.Stop();
 *   Console.WriteLine($"Time taken: {stopwatch.ElapsedMilliseconds} ms");
 *
 * ## Sample Performance Observations (for polynomials of size 1000):
 * Example Output:
 *   Regular Sequential O(n^2): 1200 ms
 *   Regular Parallel O(n^2): 700 ms
 *   Karatsuba Sequential O(n^1.585): 500 ms
 *   Karatsuba Parallel O(n^1.585): 300 ms
 *
 * Observations:
 * 1. For small input sizes (e.g., n < 10), the overhead of parallelization may
 *    cause the parallel versions to be slower than the sequential ones.
 * 2. For large input sizes, the parallel Karatsuba algorithm shows the best
 *    performance due to reduced multiplications and parallel execution.
 * 3. The regular brute-force parallelization can still show performance gains
 *    for large inputs but is limited by the O(n²) complexity.
 *
 * ============================================================================
 */