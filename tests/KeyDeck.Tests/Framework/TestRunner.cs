using System.Diagnostics;
using System.Reflection;

namespace KeyDeck.Tests.Framework;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestMethodAttribute : Attribute
{
    public string? Description { get; }
    public TestMethodAttribute(string? description = null) => Description = description;
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class TestClassAttribute : Attribute { }

public sealed class TestResult
{
    public required string SuiteName { get; init; }
    public required string TestName { get; init; }
    public string? Description { get; init; }
    public bool Passed { get; init; }
    public double ElapsedMilliseconds { get; init; }
    public string? ErrorMessage { get; init; }
    public string? StackTrace { get; init; }
}

public static class TestRunner
{
    public static async Task<int> RunAllTestsAsync(Assembly assembly)
    {
        var testClasses = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<TestClassAttribute>() != null)
            .OrderBy(t => t.Name)
            .ToList();

        var results = new List<TestResult>();
        var totalStopwatch = Stopwatch.StartNew();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine($" KEYDECK AUTOMATED TEST SUITE — EXHAUSTIVE QUALITY VERIFICATION");
        Console.WriteLine("================================================================================");
        Console.ResetColor();
        Console.WriteLine($"Discovered {testClasses.Count} test class(es). Commencing test execution...\n");

        foreach (var testClass in testClasses)
        {
            var suiteName = testClass.Name;
            var testMethods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<TestMethodAttribute>() != null)
                .OrderBy(m => m.Name)
                .ToList();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"▶ Suite: {suiteName} ({testMethods.Count} tests)");
            Console.ResetColor();

            object? instance = testClass.IsAbstract && testClass.IsSealed ? null : Activator.CreateInstance(testClass);

            foreach (var method in testMethods)
            {
                var attr = method.GetCustomAttribute<TestMethodAttribute>();
                var sw = Stopwatch.StartNew();
                bool passed = false;
                string? error = null;
                string? stack = null;

                try
                {
                    if (method.ReturnType == typeof(Task))
                    {
                        var task = (Task)method.Invoke(instance, null)!;
                        await task;
                    }
                    else
                    {
                        method.Invoke(instance, null);
                    }
                    sw.Stop();
                    passed = true;
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    sw.Stop();
                    passed = false;
                    error = ex.InnerException.Message;
                    stack = ex.InnerException.StackTrace;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    passed = false;
                    error = ex.Message;
                    stack = ex.StackTrace;
                }

                results.Add(new TestResult
                {
                    SuiteName = suiteName,
                    TestName = method.Name,
                    Description = attr?.Description,
                    Passed = passed,
                    ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                    ErrorMessage = error,
                    StackTrace = stack
                });

                if (passed)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("  ✔ [PASS] ");
                    Console.ResetColor();
                    Console.WriteLine($"{method.Name} ({sw.Elapsed.TotalMilliseconds:F1}ms)");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("  ✖ [FAIL] ");
                    Console.ResetColor();
                    Console.WriteLine($"{method.Name} ({sw.Elapsed.TotalMilliseconds:F1}ms)");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"     Error: {error}");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
        }

        totalStopwatch.Stop();

        int totalCount = results.Count;
        int passCount = results.Count(r => r.Passed);
        int failCount = results.Count(r => !r.Passed);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine(" TEST EXECUTION SUMMARY");
        Console.WriteLine("================================================================================");
        Console.ResetColor();
        Console.WriteLine($"Total Test Suites: {testClasses.Count}");
        Console.WriteLine($"Total Tests Run:   {totalCount}");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Passed:            {passCount}");
        Console.ResetColor();

        if (failCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed:            {failCount}");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($"Failed:            0");
        }

        Console.WriteLine($"Assertions:        {Assert.AssertionCount}");
        Console.WriteLine($"Elapsed Time:      {totalStopwatch.Elapsed.TotalMilliseconds:F1} ms");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        if (failCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nFAILED TESTS BREAKDOWN:");
            foreach (var failed in results.Where(r => !r.Passed))
            {
                Console.WriteLine($"\n[FAIL] {failed.SuiteName}.{failed.TestName}:");
                Console.WriteLine($"  {failed.ErrorMessage}");
                if (!string.IsNullOrEmpty(failed.StackTrace))
                {
                    Console.WriteLine($"  Stack Trace:\n{failed.StackTrace}");
                }
            }
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n✨ ALL TESTS PASSED SUCCESSFULLY! Program functionality validated.");
        Console.ResetColor();
        return 0;
    }
}
