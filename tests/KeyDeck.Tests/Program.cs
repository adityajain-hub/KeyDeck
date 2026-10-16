using System.Reflection;
using KeyDeck.Tests.Framework;

namespace KeyDeck.Tests;

public static class Program
{
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        return await TestRunner.RunAllTestsAsync(Assembly.GetExecutingAssembly());
    }
}
