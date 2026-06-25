using Xunit;

// Disable parallel test execution - these tests share a server port and data directory
[assembly: CollectionBehavior(DisableTestParallelization = true)]
