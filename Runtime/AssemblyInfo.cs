using System.Runtime.CompilerServices;

// Ad state is static and no public call can clear a latched-on ad, so the tests
// reset it between cases through an internal entry point rather than leaking
// state from one case into the next.
[assembly: InternalsVisibleTo("Yes2SDK.Tests")]
