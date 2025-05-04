using System;
using Nostr.Unity.Tests;

namespace SignatureTestProject
{
    public class TestRunner
    {
        public static void RunTest()
        {
            // Run the signature test
            string results = NostrSignatureTest.RunTest();
            Console.WriteLine(results);
        }
    }
}
