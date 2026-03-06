using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Initio.UITests;

[CollectionDefinition("InitioApp", DisableParallelization = true)]
public sealed class InitioAppCollection
{
}
