namespace BATIR;

internal static class Operational500SelfTest
{
    public static int Run()
    {
        for (var round = 1; round <= 5; round++)
        {
            var result = Operational100SelfTest.Run();
            if (result != 0)
            {
                Console.Error.WriteLine("BATIR 500-operation test stopped at round " + round + ".");
                return result;
            }
            Console.WriteLine("BATIR 500-operation test: round " + round + "/5 completed (100 operations).");
        }

        Console.WriteLine("BATIR 500-operation smoke test passed: 5 rounds × 100 operations.");
        return 0;
    }
}